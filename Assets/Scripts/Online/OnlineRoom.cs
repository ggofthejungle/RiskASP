using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

// A private room with a join code (Unity Gaming Services sessions, project "InvadeEarth").
// 2 to 5 players. Each player carries their display name and PlayFab id.
// The host starts the match by writing a seed into the room; every member sees it.
public static class OnlineRoom
{
    public const int MinPlayers = 2;
    public const int MaxPlayers = 5;

    const string NameKey = "name";
    const string PlayFabKey = "pf";
    const string ReadyKey = "ready";
    const string SeedKey = "seed";
    const string SeatsKey = "seats";
    const string BotsKey = "bots";
    const string NamePref = "ie.name";

    // Bot seats in the seat list are "bot:1", "bot:2", ... (not UGS players; the host's game plays them).
    public const string BotSeatPrefix = "bot:";
    public static bool IsBotSeat(string seatId) => seatId != null && seatId.StartsWith(BotSeatPrefix);

    public static int Bots => Session != null && Session.Properties.TryGetValue(BotsKey, out var b)
                              && int.TryParse(b.Value, out var n) ? n : 0;

    // The host's match rules (MatchOptions.Encode), or null before the host has set them.
    const string RulesKey = "rules";
    public static string Rules => Session != null && Session.Properties.TryGetValue(RulesKey, out var r) ? r.Value : null;

    public static async Task SetRulesAsync(string rules)
    {
        if (!IsHost) return;
        var host = Session.AsHost();
        host.SetProperty(RulesKey, new SessionProperty(rules ?? "", VisibilityPropertyOptions.Member));
        await host.SavePropertiesAsync();
        OnChanged();
    }

    public static async Task SetBotsAsync(int count)
    {
        if (!IsHost) return;
        count = Mathf.Clamp(count, 0, MaxPlayers - Members.Count);
        var host = Session.AsHost();
        host.SetProperty(BotsKey, new SessionProperty(count.ToString(), VisibilityPropertyOptions.Member));
        await host.SavePropertiesAsync();
        OnChanged();
    }

    // Host: take a player out of the room (before the match starts).
    public static async Task KickAsync(string playerId)
    {
        if (!IsHost || string.IsNullOrEmpty(playerId) || playerId == MyId) return;
        await Session.AsHost().RemovePlayerAsync(playerId);
        OnChanged();
    }

    public static ISession Session { get; private set; }
    public static bool InRoom => Session != null;
    public static bool IsHost => Session != null && Session.IsHost;
    public static string Code => Session?.Code;
    // null until signed in to Unity Gaming Services (offline games never are).
    public static string MyId => UnityServices.State == ServicesInitializationState.Initialized
        ? AuthenticationService.Instance.PlayerId
        : null;

    // Fires whenever the player list, a player's ready flag, or the room's start seed changes.
    public static event Action Changed;
    // Fires once on every member when the host starts the match.
    public static event Action<int> MatchStarted;

    public static string DisplayName
    {
        get
        {
            var name = PlayerPrefs.GetString(NamePrefKey, "");
            if (name.Length > 0) return name;
            name = "Commander " + UnityEngine.Random.Range(1000, 10000);
            PlayerPrefs.SetString(NamePrefKey, name);
            return name;
        }
        set => PlayerPrefs.SetString(NamePrefKey, string.IsNullOrWhiteSpace(value) ? "" : value.Trim());
    }

    static string NamePrefKey => LocalProfile is { } p ? NamePref + "." + p : NamePref;

    public sealed class Member
    {
        public string Id;
        public string Name;
        public string PlayFabId;
        public bool Ready;
        public bool Host;
    }

    public static IReadOnlyList<Member> Members => Session == null
        ? Array.Empty<Member>()
        : Session.Players.Select(p => new Member
        {
            Id = p.Id,
            Name = Prop(p.Properties, NameKey) ?? "Player",
            PlayFabId = Prop(p.Properties, PlayFabKey),
            Ready = Prop(p.Properties, ReadyKey) == "1",
            Host = p.Id == Session.Host,
        }).ToList();

    static string Prop(IReadOnlyDictionary<string, PlayerProperty> props, string key) =>
        props != null && props.TryGetValue(key, out var p) ? p.Value : null;

    // Several copies of the game on one PC (Multiplayer Play Mode virtual players, or builds started with
    // "-ieprofile <name>") would share one anonymous UGS account. Each copy gets its own profile instead.
    // null = the normal, single copy.
    public static string LocalProfile
    {
        get
        {
            var args = Environment.GetCommandLineArgs();
            var i = Array.IndexOf(args, "-ieprofile");
            if (i >= 0 && i + 1 < args.Length) return Clean(args[i + 1]);
            var path = Application.dataPath.Replace('\\', '/');
            var vp = path.IndexOf("/Library/VP/", StringComparison.OrdinalIgnoreCase);
            if (vp < 0) return null;
            var rest = path.Substring(vp + "/Library/VP/".Length);
            var slash = rest.IndexOf('/');
            return Clean("vp_" + (slash < 0 ? rest : rest.Substring(0, slash)));
        }
    }

    static string Clean(string s)
    {
        var chars = s.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').Take(30).ToArray();
        return chars.Length == 0 ? null : new string(chars);
    }

    public static async Task SignInAsync()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
        {
            var options = new InitializationOptions();
            if (LocalProfile is { } profile) options.SetProfile(profile);
            await UnityServices.InitializeAsync(options);
        }
        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    public static async Task HostAsync(int maxPlayers = MaxPlayers)
    {
        await SignInAsync();
        await LeaveAsync();
        OnlineNet.EnsureManager();
        var options = new SessionOptions
        {
            MaxPlayers = Mathf.Clamp(maxPlayers, MinPlayers, MaxPlayers),
            IsPrivate = true,
            PlayerProperties = MyProperties(),
        }.WithRelayNetwork().WithHostMigration(new LockstepMigrationData());
        Attach(await MultiplayerService.Instance.CreateSessionAsync(options));
        OnlineNet.Listen();
        await SetRulesAsync(UI.MatchSession.Options.Encode());
    }

    public static async Task JoinAsync(string code)
    {
        await SignInAsync();
        await LeaveAsync();
        OnlineNet.EnsureManager();
        var options = new JoinSessionOptions { PlayerProperties = MyProperties() }
            .WithHostMigration(new LockstepMigrationData());
        Attach(await MultiplayerService.Instance.JoinSessionByCodeAsync(code.Trim().ToUpperInvariant(), options));
        OnlineNet.Listen();
    }

    // Seat order for the match (UGS player ids), written by the host together with the seed.
    public static string[] StartedSeats { get; private set; } = Array.Empty<string>();

    public static async Task SetReadyAsync(bool ready)
    {
        if (Session == null) return;
        Session.CurrentPlayer.SetProperty(ReadyKey, new PlayerProperty(ready ? "1" : "0", VisibilityPropertyOptions.Member));
        await Session.SaveCurrentPlayerDataAsync();
    }

    public static string WhyCantStart()
    {
        if (!IsHost) return "Only the host can start.";
        var members = Members;
        if (members.Count + Bots < MinPlayers) return "Waiting for at least one more player (or add a bot).";
        if (members.Count + Bots > MaxPlayers) return $"At most {MaxPlayers} players: remove a bot.";
        if (members.Any(m => !m.Host && !m.Ready)) return "Waiting for everyone to be ready.";
        var unreachable = members.FirstOrDefault(m => !OnlineNet.HasPlayer(m.Id));
        if (unreachable != null) return $"Waiting for {unreachable.Name}'s connection.";
        return null;
    }

    public static async Task StartMatchAsync()
    {
        var why = WhyCantStart();
        if (why != null) throw new InvalidOperationException(why);
        // Not locked: a dropped player rejoins with the same code. Anyone not in the seat list is ignored.
        var host = Session.AsHost();
        var seed = UnityEngine.Random.Range(1, int.MaxValue);
        var seats = string.Join(",", Members.Select(m => m.Id)
            .Concat(Enumerable.Range(1, Bots).Select(i => BotSeatPrefix + i)));
        host.SetProperty(SeatsKey, new SessionProperty(seats, VisibilityPropertyOptions.Member));
        host.SetProperty(SeedKey, new SessionProperty(seed.ToString(), VisibilityPropertyOptions.Member));
        host.SetProperty(RulesKey, new SessionProperty(UI.MatchSession.Options.Encode(), VisibilityPropertyOptions.Member));
        await host.SavePropertiesAsync();
        OnChanged();
    }

    public static string NameOf(string playerId) =>
        IsBotSeat(playerId)
            ? "Bot " + playerId.Substring(BotSeatPrefix.Length)
            : Members.FirstOrDefault(m => m.Id == playerId)?.Name ?? "Player";

    public static async Task LeaveAsync()
    {
        var session = Session;
        if (session == null) return;
        Detach();
        try { await session.LeaveAsync(); }
        catch (Exception e) { Debug.LogWarning("Leaving the room failed: " + e.Message); }
    }

    static Dictionary<string, PlayerProperty> MyProperties() => new()
    {
        { NameKey, new PlayerProperty(DisplayName, VisibilityPropertyOptions.Member) },
        { PlayFabKey, new PlayerProperty(PlayFabLogin.PlayFabId ?? "", VisibilityPropertyOptions.Member) },
        { ReadyKey, new PlayerProperty("0", VisibilityPropertyOptions.Member) },
    };

    static int _startedSeed;

    // The last room this device was in, for "Rejoin".
    public static string LastCode
    {
        get => PlayerPrefs.GetString(LastCodePref, "");
        private set => PlayerPrefs.SetString(LastCodePref, value ?? "");
    }
    const string LastCodePref = "ie.room.last";

    static OnlineRoom()
    {
        OnlineNet.PlayerJoined += _ => Changed?.Invoke();
        OnlineNet.PlayerLeft += _ => Changed?.Invoke();
    }

    static void Attach(ISession session)
    {
        Session = session;
        if (!string.IsNullOrEmpty(session.Code)) LastCode = session.Code;
        _startedSeed = 0;
        session.Changed += OnChanged;
        session.RemovedFromSession += Detach;
        session.Deleted += Detach;
        session.SessionHostChanged += OnHostChanged;
        session.SessionMigrated += OnMigrated;
        if (session.Network != null) session.Network.MigrationFailed += OnMigrationFailed;
        OnChanged();
    }

    static void Detach()
    {
        if (Session != null)
        {
            Session.Changed -= OnChanged;
            Session.RemovedFromSession -= Detach;
            Session.Deleted -= Detach;
            Session.SessionHostChanged -= OnHostChanged;
            Session.SessionMigrated -= OnMigrated;
            if (Session.Network != null) Session.Network.MigrationFailed -= OnMigrationFailed;
        }
        Session = null;
        Changed?.Invoke();
    }

    // Host migration: when the host leaves, the room picks a new host and moves the Relay network to them.
    // Every device already has the whole game and its action log, so the new host just carries on.
    public static string HostId => Session?.Host;
    public static event Action<string> HostChanged;
    public static event Action Migrated;
    public static event Action<string> MigrationFailed;

    static void OnHostChanged(string newHostId)
    {
        Debug.Log($"Online: the room's host is now {NameOf(newHostId)}.");
        HostChanged?.Invoke(newHostId);
        Changed?.Invoke();
    }

    static void OnMigrated()
    {
        Debug.Log("Online: the network moved to the new host.");
        Migrated?.Invoke();
    }

    static void OnMigrationFailed(SessionError error)
    {
        Debug.LogWarning("Online: moving the game to a new host failed: " + error);
        MigrationFailed?.Invoke(error.ToString());
    }

    static void OnChanged()
    {
        Changed?.Invoke();
        if (Session == null || _startedSeed != 0) return;
        if (Session.Properties.TryGetValue(SeedKey, out var seed) && int.TryParse(seed.Value, out var value) && value != 0
            && Session.Properties.TryGetValue(SeatsKey, out var seats) && !string.IsNullOrEmpty(seats.Value))
        {
            _startedSeed = value;
            StartedSeats = seats.Value.Split(',');
            MatchStarted?.Invoke(value);
        }
    }
}
