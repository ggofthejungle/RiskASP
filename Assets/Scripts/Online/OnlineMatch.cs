using System;
using System.Collections.Generic;
using System.Linq;
using Actions;
using player;
using UnityEngine;

// The game GameManager sees on every device. The game is not sent over the network as state.
public interface IActionRelay
{
    // An action this device wants to make. Returns false if it could not even be sent.
    bool Send(PlayerAction action);
    // Only the host runs the reaction-window clock; clients just show it.
    bool DecidesTimeouts { get; }
    void Bind(GameManager gm);
    void Unbind();
}

// Host-authoritative lockstep. Every device loads the same map with the same seed and player list.
// Devices send their actions to the host; the host applies each one and, if it is accepted, sends it to
// every client with a sequence number. Clients apply the same actions in the same order, so the same
// GameRandom calls produce the same dice everywhere.
// Joining late or rejoining is the same thing: load the scene, ask the host for its log, replay it.
public sealed class OnlineMatch : IActionRelay, ISecretSource
{
    [Serializable]
    sealed class Envelope
    {
        public int Seq = -1;
        public string Kind = "";
        public ActionData Action;
        public int Seat = -1;
        public bool Connected;
        public string Error = "";
        // Board hash on the host after this entry; 0 = not checked.
        public int Hash;
        public string[] Batch;
        // Every random number the host drew while applying this entry (dice, draws, tie-breaks).
        public int[] Rolls;
        // The host's answers to things that depend on hidden hands (Secrets.Public), in order.
        public string[] Facts;
        // The same, but only for the device this copy goes to (Secrets.Private: the card it drew).
        public string[] Private;
    }

    const string KindAction = "act";
    const string KindSeat = "seat";
    const string KindRejected = "no";
    const string KindSyncRequest = "sync";
    const string KindBatch = "batch";
    // Host to a client whose log is longer than the host's (after a host migration): reload and replay.
    const string KindAhead = "ahead";
    // Host to one client: the cards that client's player holds (Batch = card ids) after entry Seq.
    const string KindHand = "hand";
    const int BatchSize = 150;

    static OnlineMatch _pending;

    public readonly int Seed;
    public readonly string[] SeatPlayerIds;
    public readonly int MySeat;
    // Changes when the host leaves and the room moves the game to this device.
    public bool IsHost { get; private set; }
    string _hostPlayerId;

    GameManager _gm;
    int _nextSeq;
    readonly List<Envelope> _log = new();
    // Private facts per entry, by seat. A client only has its own; the host has everyone's.
    readonly List<Dictionary<int, string[]>> _privateLog = new();
    readonly SortedDictionary<int, Envelope> _waiting = new();
    readonly Queue<Envelope> _early = new();
    bool _outOfSync;

    public bool DecidesTimeouts => IsHost;
    public int Applied => _nextSeq;
    public bool OutOfSync => _outOfSync;

    OnlineMatch(int seed, string[] seats, int mySeat, bool isHost)
    {
        Seed = seed;
        SeatPlayerIds = seats;
        MySeat = mySeat;
        IsHost = isHost;
        _hostPlayerId = OnlineRoom.HostId;
        OnlineNet.Received += OnReceived;
        OnlineNet.PlayerLeft += OnPlayerLeft;
        OnlineNet.PlayerJoined += OnPlayerJoined;
        OnlineNet.HostLost += OnHostLost;
        OnlineNet.ConnectedToHost += OnConnectedToHost;
        OnlineRoom.HostChanged += OnRoomHostChanged;
        OnlineRoom.MigrationFailed += OnMigrationFailed;
    }

    // Called by the menu just before the match scene loads. Listens straight away, because the host can
    // act before this device has finished loading; those messages wait until Bind.
    public static void Arm(int seed, string[] seats, int mySeat, bool isHost)
    {
        _pending?.Unbind();
        _pending = new OnlineMatch(seed, seats, mySeat, isHost);
        GameRandom.UseSeed(seed);
    }

    public static bool TryTake(out OnlineMatch match)
    {
        match = _pending;
        _pending = null;
        return match != null;
    }

    public void Bind(GameManager gm)
    {
        _gm = gm;
        gm.gameObject.AddComponent<OnlineMatchDriver>().Match = this;
        while (_early.Count > 0) Queue(_early.Dequeue());
        Drain();
        // Anything the host did before this device was listening (or while it was away).
        if (!IsHost) SendSyncRequest();
    }

    // Also says which cards this device's player holds, for a host that took over and can't see them.
    void SendSyncRequest()
    {
        var me = _gm?.GetPlayerById(MySeat);
        var hand = me == null ? Array.Empty<string>() : me.Cards.Where(c => !c.IsHidden).Select(c => c.Id.ToString()).ToArray();
        OnlineNet.SendToHost(JsonUtility.ToJson(new Envelope { Kind = KindSyncRequest, Seq = _nextSeq, Batch = hand }));
    }

    public void Unbind()
    {
        OnlineNet.Received -= OnReceived;
        OnlineNet.PlayerLeft -= OnPlayerLeft;
        OnlineNet.PlayerJoined -= OnPlayerJoined;
        OnlineNet.HostLost -= OnHostLost;
        OnlineNet.ConnectedToHost -= OnConnectedToHost;
        OnlineRoom.HostChanged -= OnRoomHostChanged;
        OnlineRoom.MigrationFailed -= OnMigrationFailed;
        if (GameRandom.Current == _secret || GameRandom.Current == _replay) GameRandom.UseUnseeded();
        if (Secrets.Source == this) Secrets.Source = null;
        _gm = null;
    }

    // ---------------- Secret randomness ----------------
    // The shared seed only sets up the match (map deal, first scene state). From the first action on,
    // the host draws from a stream only it knows and sends the numbers with each entry, so no device can
    // work out future dice or draws from the seed.

    RecordingRandom _secret;
    ReplayRandom _replay;
    IRandom _drawRandom;

    void EnsureStream()
    {
        Secrets.Source = this;
        if (IsHost)
        {
            if (_secret != null) return;
            _secret = new RecordingRandom(new SeededRandom(SecretSeed()));
            _drawRandom = new SeededRandom(SecretSeed());
            _replay = null;
            GameRandom.Use(_secret);
        }
        else if (_replay == null)
        {
            _replay = new ReplayRandom();
            GameRandom.Use(_replay);
        }
    }

    // ---------------- Hidden hands (ISecretSource) ----------------

    readonly List<string> _facts = new();
    readonly Dictionary<int, List<string>> _privateFacts = new();
    Queue<string> _replayFacts;
    Queue<string> _replayPrivate;

    public bool Decides => IsHost;
    public IRandom DrawRandom => _drawRandom ??= new SeededRandom(SecretSeed());

    public bool Knows(Player p) => IsHost || (_gm != null && p != null && _gm.GetPlayerId(p) == MySeat);

    public void BeforeDeciding()
    {
        if (!IsHost || _gm == null || _gm.CommandCards == null) return;
        if (_gm.Players.Any(p => p.Cards.Any(c => c.IsHidden)))
            _gm.CommandCards.ResolveFaceDownCards(DrawRandom);
    }

    public void PutPublic(string value) => _facts.Add(value ?? "");

    public void PutPrivate(Player owner, string value)
    {
        var seat = _gm != null ? _gm.GetPlayerId(owner) : -1;
        if (seat < 0) return;
        if (!_privateFacts.TryGetValue(seat, out var list)) _privateFacts[seat] = list = new List<string>();
        list.Add(value ?? "");
    }

    public string TakePublic() => _replayFacts != null && _replayFacts.Count > 0 ? _replayFacts.Dequeue() : null;

    public string TakePrivate() => _replayPrivate != null && _replayPrivate.Count > 0 ? _replayPrivate.Dequeue() : null;

    Dictionary<int, string[]> TakePrivateFacts()
    {
        var bySeat = _privateFacts.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray());
        _privateFacts.Clear();
        return bySeat;
    }

    int SeatOfClient(ulong client) => Array.IndexOf(SeatPlayerIds, OnlineNet.PlayerOfClient(client));

    // One entry as the device in `seat` may see it.
    string JsonFor(int seq, int seat)
    {
        var e = _log[seq];
        e.Private = seat >= 0 && _privateLog[seq].TryGetValue(seat, out var mine) ? mine : null;
        var json = JsonUtility.ToJson(e);
        e.Private = null;
        return json;
    }

    // Host: a client that reconnected says which cards it holds; this device may only have face-down
    // cards for them (it took over from the old host). Then it gets its hand as this device sees it.
    void ApplyHandClaim(int seat, string[] ids)
    {
        var p = _gm.GetPlayerById(seat);
        if (p == null || _gm.CommandCards == null) return;
        if (ids != null && p.Cards.Any(c => c.IsHidden))
            foreach (var text in ids)
                if (int.TryParse(text, out var id))
                    _gm.CommandCards.Reveal(p, Cards.CardRepository.Instance.CardById(id));
        _gm.CommandCards.ResolveFaceDownCards(DrawRandom, p);
    }

    void SendHand(ulong client, int seat)
    {
        var p = _gm.GetPlayerById(seat);
        if (p == null) return;
        var ids = p.Cards.Where(c => !c.IsHidden).Select(c => c.Id.ToString()).ToArray();
        OnlineNet.SendToClient(client, JsonUtility.ToJson(new Envelope { Kind = KindHand, Seq = _log.Count, Batch = ids }));
    }

    Envelope _pendingHand;

    void ApplyOwnHand(Envelope e)
    {
        var me = _gm?.GetPlayerById(MySeat);
        if (me == null || _gm.CommandCards == null) return;
        var repo = Cards.CardRepository.Instance;
        var cards = (e.Batch ?? Array.Empty<string>())
            .Select(t => int.TryParse(t, out var id) ? repo.CardById(id) : null)
            .Where(c => c != null);
        _gm.CommandCards.SetOwnHand(me, cards);
    }

    static int SecretSeed()
    {
        var bytes = new byte[4];
        using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create()) rng.GetBytes(bytes);
        return BitConverter.ToInt32(bytes, 0);
    }

    void BeginHostEntry()
    {
        EnsureStream();
        _secret.Begin();
        _facts.Clear();
        _privateFacts.Clear();
    }

    // ---------------- Host migration ----------------

    bool _hostGone;

    void OnRoomHostChanged(string newHostId)
    {
        if (string.IsNullOrEmpty(newHostId) || newHostId == _hostPlayerId) return;
        var oldHost = _hostPlayerId;
        _hostPlayerId = newHostId;
        _hostGone = false;
        if (newHostId != OnlineRoom.MyId || IsHost) return;

        IsHost = true;
        // Entries after a gap were never going to arrive; the log so far is the game.
        _waiting.Clear();
        _replay = null;
        EnsureStream();
        Debug.Log($"Online: this device is now the host (after entry {_nextSeq}).");
        if (_gm == null) return;
        Flash("The host left. Your device is running the match now.", 6f);
        SetSeatConnected(oldHost, false);
        _gm.ResumeBots();
    }

    void OnConnectedToHost()
    {
        if (IsHost || _gm == null) return;
        if (_hostGone) Flash("Reconnected to the new host.", 4f);
        _hostGone = false;
        SendSyncRequest();
    }

    void OnMigrationFailed(string error)
    {
        var code = OnlineRoom.LastCode;
        Flash(string.IsNullOrEmpty(code)
            ? "Couldn't move the match to a new host."
            : $"Couldn't move the match to a new host. Rejoin room {code} from Multiplayer if it's still open.", 30f);
    }

    public bool Send(PlayerAction action)
    {
        if (_gm == null) return false;
        if (_outOfSync)
        {
            Flash("Out of sync with the host. Leave and rejoin the room.", 5f);
            return false;
        }
        ActionData data;
        try { data = ActionCodec.ToData(action, _gm); }
        catch (Exception e)
        {
            Debug.LogWarning("Online: can't send " + action.GetType().Name + ": " + e.Message);
            return false;
        }

        if (IsHost) return Authorize(data, out _);
        if (action.Player != null && action.Player.IsBot()) return false;
        OnlineNet.SendToHost(JsonUtility.ToJson(new Envelope { Kind = KindAction, Action = data }));
        return true;
    }

    // Host: validate by applying; only accepted actions get a sequence number and go out.
    bool Authorize(ActionData data, out string error)
    {
        error = null;
        if (data.PlayerId < 0 || data.PlayerId >= SeatPlayerIds.Length)
        {
            error = "Unknown seat.";
            return false;
        }
        PlayerAction action;
        try { action = ActionCodec.FromData(data, _gm); }
        catch (Exception e)
        {
            error = e.Message;
            return false;
        }
        bool accepted;
        BeginHostEntry();
        try { accepted = _gm.ApplyAgreed(action); }
        catch (Exception e)
        {
            Debug.LogException(e);
            error = "The host hit an error applying that action.";
            return false;
        }
        if (!accepted)
        {
            error = "Not allowed right now.";
            return false;
        }
        Broadcast(new Envelope { Kind = KindAction, Action = data });
        return true;
    }

    void Broadcast(Envelope e)
    {
        e.Seq = _nextSeq++;
        LockstepMigrationData.AppliedEntries = _nextSeq;
        e.Rolls = _secret?.Take();
        e.Facts = _facts.ToArray();
        _facts.Clear();
        e.Hash = BoardHash.Of(_gm);
        _log.Add(e);
        _privateLog.Add(TakePrivateFacts());
        // Each device gets its own copy: the private facts are only for the seat they belong to.
        foreach (var client in OnlineNet.ClientIds)
        {
            var seat = SeatOfClient(client);
            if (seat < 0) continue; // not said hello yet; its sync request will fetch this entry
            OnlineNet.SendToClient(client, JsonFor(e.Seq, seat));
        }
    }

    void OnReceived(ulong sender, string channel, string text)
    {
        Envelope e;
        try { e = JsonUtility.FromJson<Envelope>(text); }
        catch (Exception ex)
        {
            Debug.LogWarning("Online: bad message: " + ex.Message);
            return;
        }
        if (e == null) return;

        if (IsHost && channel == OnlineNet.ToHost)
            OnHostReceived(sender, e);
        else if (!IsHost && channel == OnlineNet.ToClients)
            OnClientReceived(e);
    }

    void OnHostReceived(ulong sender, Envelope e)
    {
        if (_gm == null) return;
        var seat = Array.IndexOf(SeatPlayerIds, OnlineNet.PlayerOfClient(sender));
        if (e.Kind == KindSyncRequest)
        {
            if (e.Seq > _log.Count)
            {
                OnlineNet.SendToClient(sender, JsonUtility.ToJson(new Envelope { Kind = KindAhead, Seq = _log.Count }));
                return;
            }
            if (seat >= 0) ApplyHandClaim(seat, e.Batch);
            SendLog(sender, seat, Math.Max(0, e.Seq));
            if (seat >= 0) SendHand(sender, seat);
            return;
        }
        if (e.Kind != KindAction || e.Action == null) return;
        if (seat < 0 || seat != e.Action.PlayerId)
        {
            Reject(sender, "That isn't your seat.");
            return;
        }
        if (!Authorize(e.Action, out var error))
            Reject(sender, error);
    }

    void SendLog(ulong client, int seat, int from)
    {
        var items = new List<string>();
        for (var i = from; i < _log.Count; i++)
        {
            items.Add(JsonFor(i, seat));
            if (items.Count == BatchSize || i == _log.Count - 1)
            {
                OnlineNet.SendToClient(client, JsonUtility.ToJson(new Envelope { Kind = KindBatch, Batch = items.ToArray() }));
                items.Clear();
            }
        }
    }

    void OnClientReceived(Envelope e)
    {
        if (e.Kind == KindRejected)
        {
            Debug.LogWarning("Online: the host rejected an action: " + e.Error);
            Flash(e.Error, 3f);
            return;
        }
        if (e.Kind == KindHand)
        {
            _pendingHand = e;
            Drain();
            return;
        }
        if (e.Kind == KindAhead)
        {
            Debug.LogWarning($"Online: this device has {_nextSeq} entries but the new host has {e.Seq}. Replaying the host's log.");
            Resync();
            return;
        }
        if (e.Kind == KindBatch)
        {
            foreach (var json in e.Batch ?? Array.Empty<string>())
            {
                var item = JsonUtility.FromJson<Envelope>(json);
                if (item != null) Receive(item);
            }
        }
        else Receive(e);
        Drain();
    }

    void Receive(Envelope e)
    {
        if (_gm == null) _early.Enqueue(e);
        else Queue(e);
    }

    void Queue(Envelope e)
    {
        if (e.Seq >= _nextSeq) _waiting[e.Seq] = e;
    }

    // Applies entries strictly in sequence order; a gap waits for the sync reply to fill it.
    void Drain()
    {
        if (_gm == null) return;
        while (_waiting.TryGetValue(_nextSeq, out var e))
        {
            _waiting.Remove(_nextSeq);
            _nextSeq++;
            LockstepMigrationData.AppliedEntries = _nextSeq;
            var mine = e.Private;
            e.Private = null;
            _log.Add(e);
            _privateLog.Add(mine != null ? new Dictionary<int, string[]> { [MySeat] = mine } : new Dictionary<int, string[]>());
            Apply(e, mine);
            if (e.Hash != 0 && !_outOfSync && BoardHash.Of(_gm) != e.Hash)
            {
                _outOfSync = true;
                Debug.LogError($"Online: board differs from the host after entry {e.Seq} ({e.Kind} {e.Action?.Type}).");
                if (_resyncs < MaxResyncs) Resync();
                else Flash("Out of sync with the host. Leave and rejoin the room.", 10f);
                return;
            }
        }
        // The host's view of this device's hand, once this device has caught up to the same entry.
        if (_pendingHand != null && _pendingHand.Seq <= _nextSeq)
        {
            if (_pendingHand.Seq == _nextSeq) ApplyOwnHand(_pendingHand);
            _pendingHand = null;
        }
    }

    // Reload the match scene from the same seed and replay the host's whole log.
    static int _resyncs;
    const int MaxResyncs = 1;

    void Resync()
    {
        _resyncs++;
        Flash("Out of sync with the host. Reloading the match…", 5f);
        Unbind();
        Arm(Seed, SeatPlayerIds, MySeat, IsHost);
        global::UI.MatchSession.Select(global::UI.MatchKind.Online);
        global::UI.MatchSession.ArmForNextLoad();
        UnityEngine.SceneManagement.SceneManager.LoadScene("Main");
    }

    void Apply(Envelope e, string[] privateFacts)
    {
        EnsureStream();
        _replay.Begin(e.Rolls);
        _replayFacts = new Queue<string>(e.Facts ?? Array.Empty<string>());
        _replayPrivate = new Queue<string>(privateFacts ?? Array.Empty<string>());
        try { ApplyEntry(e); }
        finally
        {
            var left = _replay.End();
            if (left != 0) Debug.LogError($"Online: entry {e.Seq} left {left} of the host's random numbers unused.");
            if (_replayFacts.Count != 0) Debug.LogError($"Online: entry {e.Seq} left {_replayFacts.Count} of the host's answers unused.");
            _replayFacts = null;
            _replayPrivate = null;
        }
        if (_replay.Overran) Debug.LogError($"Online: entry {e.Seq} needed more random numbers than the host drew.");
    }

    void ApplyEntry(Envelope e)
    {
        switch (e.Kind)
        {
            case KindAction:
                PlayerAction action;
                try { action = ActionCodec.FromData(e.Action, _gm); }
                catch (Exception ex)
                {
                    Debug.LogError($"Online: can't read action {e.Seq}: {ex.Message}.");
                    return;
                }
                if (!_gm.ApplyAgreed(action))
                    Debug.LogError($"Online: action {e.Seq} ({e.Action.Type}) was accepted by the host but not here.");
                break;
            case KindSeat:
                _gm.ApplyAgreedConnection(_gm.GetPlayerById(e.Seat), e.Connected);
                break;
        }
    }

    static void Reject(ulong sender, string error) =>
        OnlineNet.SendToClient(sender, JsonUtility.ToJson(new Envelope { Kind = KindRejected, Error = error ?? "" }));

    void OnPlayerLeft(string playerId) => SetSeatConnected(playerId, false);

    void OnPlayerJoined(string playerId) => SetSeatConnected(playerId, true);

    void SetSeatConnected(string playerId, bool connected)
    {
        if (!IsHost || _gm == null) return;
        var seat = Array.IndexOf(SeatPlayerIds, playerId);
        var player = _gm.GetPlayerById(seat);
        if (player == null || _gm.IsConnected(player) == connected) return;
        BeginHostEntry();
        _gm.ApplyAgreedConnection(player, connected);
        Broadcast(new Envelope { Kind = KindSeat, Seat = seat, Connected = connected });
        Flash(connected ? $"{player.Name} is back." : $"{player.Name} left. Their turns are skipped and the AI answers for them.", 5f);
    }

    void OnHostLost()
    {
        Debug.LogWarning("Online: lost the connection to the host.");
        if (IsHost) return;
        _hostGone = true;
        // The room hands the game to another player; OnConnectedToHost or OnMigrationFailed follows.
        Flash("Lost the host. Moving the match to another player…", 30f);
    }

    void Flash(string message, float seconds)
    {
        if (_gm != null && _gm.TurnHud != null) _gm.TurnHud.Flash(message, seconds);
    }

    // Host, every frame: a dropped player's turn can't wait for them. Bid 0, take the first free seat,
    // and end each phase. Their open questions are already answered by the AI.
    internal void HostTick()
    {
        if (!IsHost || _gm == null || _gm.GameState == GameState.Over || _gm.PendingDecision != null) return;
        var who = _gm.InputPlayer;
        if (who == null || who.IsBot() || _gm.IsConnected(who)) return;
        if (Time.unscaledTime < _nextAbsentMove) return;
        _nextAbsentMove = Time.unscaledTime + 1f;

        if (_gm.IsBidding) _gm.SubmitBid(0);
        else if (_gm.IsChoosingOrder && _gm.AvailableTurnPositions.Count > 0) _gm.ChooseTurnPosition(_gm.AvailableTurnPositions[0]);
        else if (_gm.GameState == GameState.Playing && _gm.CurrentPlayer == who && _gm.CurrentPhase != _gm.EmptyPhase)
            _gm.HandlePlayerAction(new EndPhaseAction(who, _gm.Turn));
    }

    float _nextAbsentMove;
}

public sealed class OnlineMatchDriver : MonoBehaviour
{
    internal OnlineMatch Match;

    void Update() => Match?.HostTick();
}

// FNV-1a over the full match snapshot plus the draw deck's order. Same actions in the same order must
// give the same number.
public static class BoardHash
{
    public static int Of(GameManager gm)
    {
        unchecked
        {
            // Public state only: devices hold face-down cards for other players, so hands count by size.
            var snapshot = MatchSnapshots.Capture(gm);
            foreach (var p in snapshot.Players)
            {
                p.CardIds = Array.Empty<int>();
                p.ScoutedTerritories = Array.Empty<string>();
            }
            var h = Mix((int)2166136261, MatchSnapshots.ToJson(snapshot));
            var repo = Cards.CardRepository.Instance;
            if (repo != null)
                foreach (var deck in Cards.DeckLists.DecksInPlay)
                    h = (h ^ repo.DeckCount(deck)) * 16777619;
            return h == 0 ? 1 : h;
        }
    }

    static int Mix(int h, string s)
    {
        unchecked
        {
            foreach (var c in s)
                h = (h ^ c) * 16777619;
            return h;
        }
    }
}
