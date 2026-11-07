using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Actions;
using Cards;
using Decisions;
using Expansions;
using Extensions;
using InvadeEarth.Rules;
using Map;
using player;
using TurnPhases;
using UnityEngine;
using TMPro;
using UI;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    private TerritoryRepository _tr;
    private CardRepository _cr;
    private BattleSimulator _bs;

    public int NPlayers => _nPlayers;
    [SerializeField, Range(2, 6)] private int _nPlayers = 2;

    [SerializeField] private StartingPlayersConfiguration _startingPlayersConfiguration;

    public List<Player> Players;
    
    //public TextMeshProUGUI BidEnergyOutput;
    public TMP_InputField BidEnergyInput;
    //private int[] _energyBids;

    private Queue<Player> _playerQueue = new();
    public List<Player> GetPlayersInTurnOrder() => _playerQueue.ToList();

    // This year's seats, 1st to last (the queue rotates as turns are taken; this doesn't).
    // Before any seats are chosen, or while some are still free, it is the player list.
    public IReadOnlyList<Player> SeatOrder =>
        _chosenPositions.Length > 0 && _chosenPositions.All(p => p != null)
            ? _chosenPositions.Concat(Players.Where(p => !_chosenPositions.Contains(p))).ToList()
            : Players;

    // Turn owner: whose turn it is.
    public Player CurrentPlayer => _currentPlayer;
    private Player _currentPlayer;

    // Input owner: who the game is waiting on right now. Same as the turn owner unless a
    // PendingDecision is open (e.g. a defender's reaction window), then it's that player.
    public Player InputPlayer => _pendingDecision != null ? GetPlayerById(_pendingDecision.PlayerId) : _currentPlayer;
    public bool IsInputPlayer(Player player) => player != null && InputPlayer == player;

    public PendingDecision PendingDecision => _pendingDecision;
    private PendingDecision _pendingDecision;
    private Action<DecisionResponseAction> _onDecisionAnswered;
    public Action<PendingDecision> OnPendingDecisionChanged; // null = decision closed

    // Stable player ids (index in Players) so actions/decisions can be serialized.
    public int GetPlayerId(Player player) => Players.IndexOf(player);
    public Player GetPlayerById(int id) => id >= 0 && id < Players.Count ? Players[id] : null;

    public GameState GameState => _gameState;
    private GameState _gameState = GameState.Setup;

    public int Turn => _turn;
    private int _turn;
    public int Year => _year;
    private int _year = 1;
    
    public IPhase CurrentPhase => _currentPhase;
    private IPhase _currentPhase;

    public AnnualBidding AnnualBidding { get; private set; }
    public CommandCards CommandCards { get; private set; }
    public Purchases Purchases { get; private set; }

    // 2210 "command step": the current player's turn, from Reinforce until their first invasion.
    // Buying/playing "before first invasion" cards, hiring commanders and building stations happen here.
    public bool IsCommandStep(Player p)
    {
        if (_isBidding || _isChoosingOrder || _gameState != GameState.Playing || _currentPlayer != p || p == null)
            return false;
        return _currentPhase switch
        {
            global::TurnPhases.ReinforcePhase => true,
            global::TurnPhases.AttackPhase ap => !ap.AttackResults.Any() && ap.PendingAttack == null,
            _ => false
        };
    }
    public ReinforcePhase ReinforcePhase { get; private set; }
    public AttackPhase AttackPhase { get; private set; }
    public FortifyPhase FortifyPhase { get; private set; }
    public EmptyPhase EmptyPhase { get; private set; }


    

    public Action<IPhase> OnPhaseStarted;
    public Action<IPhase> OnPhaseEnded;
    public Action<IPhase, IPhase> OnTurnPhaseChanged;
    public Action<Player, Player> OnPlayerTurnChanged;
    public Action<Player> OnPlayerTurnEnded;

    public Action<GameState> OnGameStateChanged;

    public UIGameInfo UIGameInfo;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("There is more than one GameManager in the scene");
            Destroy(gameObject);
        }
        else
            Instance = this;

        SetGameState(GameState.Setup);

        _tr = TerritoryRepository.Instance;
        _cr = CardRepository.Instance;
        _bs = BattleSimulator.Instance;
        SetupPhases();
        CommandCards = new CommandCards(this, _cr);
        Purchases = new Purchases(this);
        AnnualBidding = new AnnualBidding(this);
        CreatePlayers();
        ApplyMatchSession();

        SubscribeToEvents();
    }

    // Match rules from the menu (online: the host's). A scene loaded without the menu keeps the plain setup.
    public MatchOptions Options { get; private set; } = MatchOptions.Plain;

    // One human on this device is the local player (single player vs bots).
    // Two or more humans share the device, so the pass-the-device covers come on.
    void ApplyMatchSession()
    {
        if (!MatchSession.TryConsume(out var kind)) return;
        Options = MatchSession.Options;
        if (kind == MatchKind.Online)
        {
            if (!OnlineMatch.TryTake(out var match)) return;
            Relay = match;
            LocalPlayer = GetPlayerById(match.MySeat);
            PassTheDeviceScreens = false;
            ReactionTimeoutSeconds = (float)DecisionClock.OnlineReactionSeconds;
            return;
        }
        if (kind != MatchKind.SinglePlayer && kind != MatchKind.HotSeat) return;
        var humans = Players.Where(p => p != null && !p.IsBot()).ToList();
        PassTheDeviceScreens = humans.Count > 1;
        if (humans.Count == 1)
            LocalPlayer = humans[0];
    }

    private void SubscribeToEvents()
    {
        foreach (var player in Players)
        {
            player.OnEliminated += tuple => OnPlayerEliminated(tuple.eliminatedBy, tuple.eliminated);
        }
    }

    private void OnPlayerEliminated(Player eliminatedBy, Player eliminated)
    {
        var cards = new List<Card>(eliminated.Cards);
        eliminatedBy.AddCards(cards);
        eliminated.RemoveCards(cards);
        // Online: the winner's device now sees the cards it took.
        var ids = Secrets.Private(eliminatedBy, () => string.Join(",", cards.Select(c => c.Id)));
        if (!Secrets.Decides && !string.IsNullOrEmpty(ids))
            foreach (var id in ids.Split(','))
                CommandCards.Reveal(eliminatedBy, _cr.CardById(int.Parse(id)));

        if (Players.Count(player => player.IsAlive()) == 1)
            GameOver();
    }

    //Turn phases
    private void SetupPhases()
    {
        ReinforcePhase = new ReinforcePhase(this, _cr);
        AttackPhase = new AttackPhase(this, _bs);
        FortifyPhase = new FortifyPhase(this);
        EmptyPhase = new EmptyPhase();
        SetTurnPhase(EmptyPhase);
    }


    // ---------------- Year / bidding / turn flow ----------------
    // Each year: every alive player bids energy (0..their energy) -> turn order by highest bid
    // (ties broken randomly but consistently) -> each alive player takes exactly one turn -> next year.
    // The game ends after MaxYears.
    public const int MaxYears = 5;
    public bool IsBidding => _isBidding;
    private bool _isBidding;
    public int TurnsTakenThisYear => _turnsTakenThisYear;
    private int _turnsTakenThisYear;
    private GameObject _bidButton;
    private GameObject _bidInputGO;

    // Online: actions made on this device go to the host instead of being applied here. The host sends
    // the accepted ones back to everyone in one order, and they are applied with ApplyAgreed.
    // null = local game.
    public IActionRelay Relay { get; private set; }
    private int _agreedDepth;

    // The solver bots take their turns on one device only (the host online); the others replay the moves.
    // Bids, seat choices and AI answers to decisions are not solver moves: they run everywhere, inside ApplyAgreed.
    public bool RunsBotAI => Relay == null || Relay.DecidesTimeouts;

    public void ResumeBots()
    {
        foreach (var bot in FindObjectsByType<player.BotPlayer>(FindObjectsInactive.Exclude))
            bot.Resume();
    }

    // Applies an action every device has agreed on. Anything it sets off (AI answers, the last seat
    // being auto-taken, bots) happens inside the same call, so it happens identically everywhere.
    public bool ApplyAgreed(PlayerAction action)
    {
        _agreedDepth++;
        try { return HandlePlayerAction(action); }
        finally { _agreedDepth--; }
    }

    public void ApplyAgreedConnection(Player p, bool connected)
    {
        _agreedDepth++;
        try { SetConnected(p, connected); }
        finally { _agreedDepth--; }
    }

    private void OnDestroy()
    {
        Relay?.Unbind();
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        _agreedDepth++;
        try { StartMatch(); }
        finally { _agreedDepth--; }
        Relay?.Bind(this);
    }

    private void StartMatch()
    {
        OnPlayerTurnChanged += LogPlayerTurnChanged;
        SetupGame();
        _bidButton = GameObject.Find("BidEnergyButton");
        _bidInputGO = GameObject.Find("BidEnergyInput");
        if (_bidButton != null)
            _bidButton.GetComponent<Button>().onClick.AddListener(HandlePlayerBidEnergy);
        SetUpBidBox();
        CreateHud();
        if (Options.Placement == TerritorySetup.TakeTurns)
        {
            StartDraft();
            return;
        }
        BeginFirstYear();
    }

    // 2210 year order: bid → choose seats → (year 1: place starting commanders/station) → turns.
    private void BeginFirstYear()
    {
        if (AutoPlaceStartingUnits)
        {
            foreach (var player in Players)
                Purchases.PlaceStartingUnits(player);
            _startingUnitsPlaced = true;
        }
        StartYear();
    }

    // ---------------- Take-turns setup (match option, 2210 rules) ----------------
    // In a random order, each player claims one empty territory with 1 MOD, round after round, until the
    // map is full. Then, in the same order, everyone places the rest of their starting army 3 MODs at a time.
    public const int DraftBatch = 3;
    public bool IsDrafting { get; private set; }
    // MODs the current player may still place in this batch of 3.
    public int DraftBatchLeft { get; private set; }
    public int DraftUnitsLeft(Player p) => _draftLeft != null && _draftLeft.TryGetValue(p, out var n) ? n : 0;
    private List<Player> _draftOrder;
    private Dictionary<Player, int> _draftLeft;
    private int _draftTurn;
    private bool _draftStepping, _draftAgain;

    private void StartDraft()
    {
        IsDrafting = true;
        var order = Enumerable.Range(0, Players.Count).ToArray();
        OpeningDeal.Shuffle(order, GameRandom.Current);
        _draftOrder = order.Select(i => Players[i]).ToList();
        var army = OpeningDeal.TroopsForPlayerCount(Players.Count);
        _draftLeft = Players.ToDictionary(p => p, _ => army);
        _draftTurn = 0;
        DraftBatchLeft = 0;
        Debug.Log("Take-turns setup, order: " + string.Join(", ", _draftOrder.Select(p => p.Name)));
        NextDraftStep();
    }

    // Bots answer at once, inside OpenDecision; looping here instead of recursing keeps the stack flat
    // through a whole all-bot setup.
    private void NextDraftStep()
    {
        if (_draftStepping) { _draftAgain = true; return; }
        _draftStepping = true;
        try
        {
            do { _draftAgain = false; DraftStep(); } while (_draftAgain);
        }
        finally { _draftStepping = false; }
    }

    private void DraftStep()
    {
        var empty = _tr.Territories.Where(t => t.Owner == null).ToList();
        if (empty.Count > 0)
        {
            var claimer = _draftOrder[_draftTurn % _draftOrder.Count];
            _draftTurn++;
            SetDraftPlayer(claimer);
            OpenDecision(new PendingDecision(GetPlayerId(claimer), DecisionKind.ClaimTerritory,
                empty.Select(t => t.Name).ToList()), OnClaimed);
            return;
        }

        if (DraftBatchLeft <= 0 || DraftUnitsLeft(_currentPlayer) <= 0)
        {
            var next = NextDraftPlacer();
            if (next == null)
            {
                FinishDraft();
                return;
            }
            SetDraftPlayer(next);
            DraftBatchLeft = Math.Min(DraftBatch, DraftUnitsLeft(next));
        }
        OpenDecision(new PendingDecision(GetPlayerId(_currentPlayer), DecisionKind.PlaceStartingTroops,
            _currentPlayer.Territories.Select(t => t.Name).ToList(), DraftBatchLeft.ToString()), OnDraftPlaced);
    }

    private Player NextDraftPlacer()
    {
        for (var i = 0; i < _draftOrder.Count; i++)
        {
            var p = _draftOrder[_draftTurn % _draftOrder.Count];
            _draftTurn++;
            if (DraftUnitsLeft(p) > 0 && p.Territories.Any()) return p;
        }
        return null;
    }

    private void SetDraftPlayer(Player p)
    {
        if (_currentPlayer == p) return;
        var old = _currentPlayer;
        _currentPlayer = p;
        OnPlayerTurnChanged?.Invoke(old, _currentPlayer);
    }

    private void OnClaimed(DecisionResponseAction response)
    {
        var territory = _tr.FromName(response.Option);
        territory.SetOwner(response.Player, 1);
        _draftLeft[response.Player]--;
        NextDraftStep();
    }

    private void OnDraftPlaced(DecisionResponseAction response)
    {
        _tr.FromName(response.Option).AddTroops(1);
        _draftLeft[response.Player]--;
        DraftBatchLeft--;
        NextDraftStep();
    }

    private void FinishDraft()
    {
        IsDrafting = false;
        DraftBatchLeft = 0;
        Debug.Log("Take-turns setup done.");
        BeginFirstYear();
    }

    // Turn banner + instructions + turn splash, and the attack/move arrow on the map.
    public UI.TurnHud TurnHud { get; private set; }
    public UI.CommandHud CommandHud { get; private set; }
    public UI.Scoreboard Scoreboard { get; private set; }
    public UI.PauseMenu PauseMenu { get; private set; }

    // A commander died (killed attacking, destroyed on conquest, Assassin Bomb...). Owner, type, where, how.
    public Action<Player, CommanderType, Territory, string> OnCommanderKilled;
    public void ReportCommanderKilled(Player owner, CommanderType type, Territory where, string how)
    {
        Debug.Log($"{owner?.Name}'s {type} was killed in {where?.Name} ({how}).");
        OnCommanderKilled?.Invoke(owner, type, where, how);
    }

    // Who is sitting at THIS device. Online, set it to this client's player: the UI then only shows that
    // player's hand and prompts. null = every human plays on this device (local testing / hot-seat).
    public Player LocalPlayer { get; set; }
    public bool IsLocal(Player p) => p != null && !p.IsBot() && (LocalPlayer == null || LocalPlayer == p);

    // Hot-seat only (testing): cover screens "Pass the device to Bob" around a defender's decision.
    // Off by default because the real game is online, where each player has their own device.
    [SerializeField] private bool _passTheDeviceScreens = false;
    public bool PassTheDeviceScreens { get => _passTheDeviceScreens; set => _passTheDeviceScreens = value; }

    // Seconds a human defender gets in the reaction window before it auto-passes. 0 = no limit (local play).
    // Online matches set DecisionClock.OnlineReactionSeconds.
    public float ReactionTimeoutSeconds { get; set; }
    private readonly DecisionClock _decisionClock = new();
    public double DecisionSecondsLeft => _decisionClock.SecondsLeft(Time.realtimeSinceStartupAsDouble);

    // A player whose device dropped. Their questions are answered by the AI straight away.
    private readonly HashSet<int> _disconnected = new();
    public bool IsConnected(Player p) => p != null && !_disconnected.Contains(GetPlayerId(p));
    public void SetConnected(Player p, bool connected)
    {
        var id = GetPlayerId(p);
        if (id < 0) return;
        if (connected) _disconnected.Remove(id);
        else _disconnected.Add(id);
        if (!connected && _pendingDecision != null && _pendingDecision.PlayerId == id)
            AnswerForAbsentPlayer();
    }

    private void Update()
    {
        if (Relay != null && !Relay.DecidesTimeouts) return;
        if (_pendingDecision != null && _decisionClock.Expired(Time.realtimeSinceStartupAsDouble))
            TimeOutDecision();
    }

    // Reaction window ran out: Pass. Asking the AI instead would play cards the player chose not to.
    private void TimeOutDecision()
    {
        var d = _pendingDecision;
        _decisionClock.Stop();
        var who = GetPlayerById(d.PlayerId);
        Debug.Log($"{who?.Name} ran out of time on {d.Kind}.");
        if (d.Kind is DecisionKind.DefenseReaction or DecisionKind.ArmageddonCard && d.HasOption(DecisionOptions.Pass))
            HandlePlayerAction(new DecisionResponseAction(who, _turn, DecisionOptions.Pass));
        else
            AnswerForAbsentPlayer();
    }

    private void AnswerForAbsentPlayer()
    {
        var d = _pendingDecision;
        if (d == null) return;
        var who = GetPlayerById(d.PlayerId);
        HandlePlayerAction(new DecisionResponseAction(who, _turn, Secrets.Public(() => DefenderAI.Choose(this, d))));
    }
    public Map.SelectionArrow SelectionArrow { get; private set; }
    public Map.MapBadges MapBadges { get; private set; }

    private void CreateHud()
    {
        var canvas = UIGameInfo != null ? UIGameInfo.GetComponentInParent<Canvas>() : null;
        if (canvas != null) canvas = canvas.rootCanvas;
        if (canvas != null)
        {
            TurnHud = UI.TurnHud.Create(this, canvas);
            CommandHud = UI.CommandHud.Create(this, canvas);
            Scoreboard = UI.Scoreboard.Create(this, canvas);
            PauseMenu = UI.PauseMenu.Create(this, canvas);
        }
        SelectionArrow = Map.SelectionArrow.Create(this);
        MapBadges = Map.MapBadges.Create(this, canvas);
    }

    private void StartYear()
    {
        Debug.Log($"StartYear(): year {_year}");
        if (_year > MaxYears)
        {
            GameOver();
            return;
        }

        _isBidding = true;
        foreach (var player in Players)
            player._energyBid = -1;
        _yearTable = YearOrder.Begin(_playerQueue.Select(p => new YearSeat
        {
            PlayerId = GetPlayerId(p),
            Alive = !p.IsDead(),
            Energy = p.GetEnergy(),
        }).ToArray());
        if (_currentPhase != EmptyPhase)
            SetTurnPhase(EmptyPhase);
        SetBidUIActive(true);

        var oldPlayer = _currentPlayer;
        _currentPlayer = GetPlayerById(_yearTable.ActorId);
        OnPlayerTurnChanged?.Invoke(oldPlayer, _currentPlayer);
        PromptCurrentBidder();
    }

    // Old typed bid box in the scene is retired; TurnHud shows 0..energy buttons instead.
    private void SetUpBidBox()
    {
        if (_bidButton != null) _bidButton.SetActive(false);
        if (_bidInputGO != null) _bidInputGO.SetActive(false);
        if (BidEnergyInput != null) BidEnergyInput.gameObject.SetActive(false);
    }

    private void PromptCurrentBidder()
    {
        if (_currentPlayer.IsBot())
        {
            SubmitBid(BotBidding.Choose(_currentPlayer.GetEnergy(), _year, MaxYears, GameRandom.Current));
            return;
        }
        ShowMessage($"Year {_year}: {_currentPlayer.Name}, pick a bid (0-{_currentPlayer.GetEnergy()}).");
    }

    // Kept for any leftover scene Bid button wiring; prefers the number-button path.
    public void HandlePlayerBidEnergy()
    {
        ShowMessage($"Pick a bid from 0 to {_currentPlayer?.GetEnergy() ?? 0}.");
    }

    // Submits a bid for the current bidder. Returns false (and changes nothing) if the bid is invalid.
    // Valid bids go through HandlePlayerAction so the same path can come from the network.
    public bool SubmitBid(int bid)
    {
        if (!_isBidding || _currentPlayer == null)
        {
            Debug.Log("SubmitBid ignored: not in bidding.");
            return false;
        }

        var energy = _currentPlayer.GetEnergy();
        if (bid < 0 || bid > energy)
        {
            ShowMessage($"Invalid bid. {_currentPlayer.Name} can bid 0 to {energy} energy.");
            return false;
        }

        return HandlePlayerAction(new BidAction(_currentPlayer, _turn, bid));
    }

    // The year's bid table. Rules live in YearOrder; this is the scene copy.
    YearTable _yearTable;

    bool ApplyBid(int bid)
    {
        var bidder = _currentPlayer;
        if (_yearTable == null || !YearOrder.TryBid(_yearTable, GetPlayerId(bidder), bid, GameRandom.Current, out var next))
        {
            Debug.LogWarning("Bid rejected by the year rules.");
            return false;
        }

        _yearTable = next;
        WriteSeats(next);
        Debug.Log($"Year {_year}: {bidder.Name} bid {bid}");
        if (next.Choosing)
        {
            OpenChoosing(next);
            return true;
        }

        var oldPlayer = _currentPlayer;
        _currentPlayer = GetPlayerById(next.ActorId);
        OnPlayerTurnChanged?.Invoke(oldPlayer, _currentPlayer);
        PromptCurrentBidder();
        return true;
    }

    void WriteSeats(YearTable table)
    {
        foreach (var seat in table.Seats)
        {
            var player = GetPlayerById(seat.PlayerId);
            player._energyBid = seat.Bid;
            player.SetEnergy(seat.Energy);
        }
    }

    void OpenChoosing(YearTable next)
    {
        Debug.Log($"Bidding done (year {_year}). Bid order: " +
                  string.Join(", ", next.ChooserIds.Select(id => $"{GetPlayerById(id).Name} ({next.Seats.First(s => s.PlayerId == id).Bid})")));

        _isBidding = false;
        SetBidUIActive(false);
        if (UIGameInfo != null)
            UIGameInfo.CloseInstructionWindow();

        // 2210: in bid order (highest first), each player CHOOSES their turn position for this year.
        _isChoosingOrder = true;
        SyncChoosing(next);
        PromptNextChooser();
    }

    void SyncChoosing(YearTable next)
    {
        _chosenPositions = next.SeatOwner.Select(id => id < 0 ? null : GetPlayerById(id)).ToArray();
        _choosers = new Queue<Player>(next.ChooserIds.Skip(next.NextChooser).Select(GetPlayerById));
        _deadAfterChoosing = next.DeadIds.Select(GetPlayerById).ToList();
    }

    // ---------------- Placing starting units (after year-1 bid + turn order) ----------------
    // In the chosen turn order, each player picks a territory for their Diplomat, then Land Commander, then
    // Space Station. Then the first reinforce turn begins.
    public static bool AutoPlaceStartingUnits = false; // tests / quick start: place them automatically instead
    public static readonly string[] StartingUnits = { "Diplomat", "LandCommander", "SpaceStation" };
    public bool IsPlacingStartingUnits => _isPlacing;
    private bool _isPlacing;
    private bool _startingUnitsPlaced;
    private readonly Queue<(Player player, string unit, int index)> _placements = new();
    public int PlacementIndex { get; private set; } // 1..3 for the current player

    private void StartPlacement()
    {
        _isPlacing = true;
        // Place in the year's turn order (already in _playerQueue after choosing).
        foreach (var player in _playerQueue.Where(p => !p.IsDead()))
            for (int i = 0; i < StartingUnits.Length; i++)
                _placements.Enqueue((player, StartingUnits[i], i + 1));
        NextPlacement();
    }

    private void NextPlacement()
    {
        if (_placements.Count == 0)
        {
            _isPlacing = false;
            _startingUnitsPlaced = true;
            Debug.Log("Starting units placed.");
            StartNextPlayerTurn();
            return;
        }

        var (player, unit, index) = _placements.Dequeue();
        PlacementIndex = index;
        if (_currentPlayer != player)
        {
            var old = _currentPlayer;
            _currentPlayer = player;
            OnPlayerTurnChanged?.Invoke(old, _currentPlayer);
        }
        var options = player.Territories
            .Where(t => unit != "SpaceStation" || !t.containsSpaceStation)
            .Select(t => t.Name).ToList();
        OpenDecision(new PendingDecision(GetPlayerId(player), DecisionKind.PlaceStartingUnit, options, unit), OnPlaced);
    }

    private void OnPlaced(DecisionResponseAction response)
    {
        var territory = _tr.FromName(response.Option);
        var unit = PendingSubjectOf(response);
        if (unit == "SpaceStation") territory.AddSpaceStation();
        else territory.AddCommander(new Commander((CommanderType)Enum.Parse(typeof(CommanderType), unit)));
        Debug.Log($"{response.Player.Name} placed their {unit} in {territory.Name}.");
        NextPlacement();
    }

    private string _lastSubject;
    private string PendingSubjectOf(DecisionResponseAction _) => _lastSubject;

    // ---------------- Choosing turn order (after bidding) ----------------
    public bool IsChoosingOrder => _isChoosingOrder;
    private bool _isChoosingOrder;
    private Queue<Player> _choosers = new();
    private Player[] _chosenPositions = Array.Empty<Player>();
    private List<Player> _deadAfterChoosing = new();

    // 1-based positions still free this year.
    public List<int> AvailableTurnPositions =>
        Enumerable.Range(0, _chosenPositions.Length).Where(i => _chosenPositions[i] == null).Select(i => i + 1).ToList();

    private void PromptNextChooser()
    {
        if (_choosers.Count == 0)
        {
            FinishChoosingOrder();
            return;
        }

        var chooser = _choosers.Peek();
        var free = AvailableTurnPositions;
        var oldPlayer = _currentPlayer;
        _currentPlayer = chooser;
        OnPlayerTurnChanged?.Invoke(oldPlayer, _currentPlayer);

        // The last player gets whatever is left; bots take the earliest free position.
        if (free.Count == 1 || chooser.IsBot())
        {
            ChooseTurnPosition(free[0]);
            return;
        }
        ShowMessage($"Year {_year}: {chooser.Name}, choose when you play this year ({string.Join(", ", free.Select(Ordinal))}).");
    }

    // The current chooser takes a 1-based turn position. Returns false (nothing changes) if it isn't free.
    public bool ChooseTurnPosition(int position)
    {
        if (!_isChoosingOrder || _choosers.Count == 0)
        {
            Debug.Log("ChooseTurnPosition ignored: not choosing turn order.");
            return false;
        }
        if (position < 1 || position > _chosenPositions.Length || _chosenPositions[position - 1] != null)
        {
            ShowMessage($"Position {position} isn't free. Free: {string.Join(", ", AvailableTurnPositions.Select(Ordinal))}.");
            return false;
        }

        return HandlePlayerAction(new ChooseTurnPositionAction(_currentPlayer, _turn, position));
    }

    bool ApplyTurnPosition(int position)
    {
        if (_yearTable == null || _choosers.Count == 0)
            return false;
        var chooser = _choosers.Peek();
        if (!YearOrder.TryChoose(_yearTable, GetPlayerId(chooser), position, out var next))
        {
            Debug.LogWarning("Turn position rejected by the year rules.");
            return false;
        }

        _yearTable = next;
        SyncChoosing(next);
        Debug.Log($"Year {_year}: {chooser.Name} plays {Ordinal(position)}");
        PromptNextChooser();
        return true;
    }

    private void FinishChoosingOrder()
    {
        _playerQueue.Clear();
        foreach (var player in _chosenPositions)
            _playerQueue.Enqueue(player);
        foreach (var player in _deadAfterChoosing)
            _playerQueue.Enqueue(player);
        Debug.Log($"Turn order (year {_year}): " + string.Join(", ", _chosenPositions.Select(p => p.Name)));

        _isChoosingOrder = false;
        if (UIGameInfo != null)
            UIGameInfo.CloseInstructionWindow();
        if (_gameState == GameState.Setup)
            SetGameState(GameState.Playing);

        _turnsTakenThisYear = 0;
        // Year 1 only: after seats are chosen, place starting commanders/station in that order.
        if (!_startingUnitsPlaced && !AutoPlaceStartingUnits)
        {
            StartPlacement();
            return;
        }
        StartNextPlayerTurn();
    }

    public static string Ordinal(int n) => n switch { 1 => "1st", 2 => "2nd", 3 => "3rd", _ => $"{n}th" };

    private void StartNextPlayerTurn()
    {
        var oldPlayer = _currentPlayer;
        do
        {
            _currentPlayer = _playerQueue.Dequeue();
            _playerQueue.Enqueue(_currentPlayer);
        } while (_currentPlayer.IsDead());

        _turn++;
        // Frequency Jam only lasts for the jamming player's turn.
        foreach (var player in Players)
            player.canPlayCommandCards = true;
        CommandCards.OnTurnStarted();
        Debug.Log($"Year {_year}, turn {_turnsTakenThisYear + 1}: {_currentPlayer.Name}");
        OnPlayerTurnChanged?.Invoke(oldPlayer, _currentPlayer);
        SetTurnPhase(ReinforcePhase);
    }

    // Called when the current player's turn is over (Fortify ended).
    private void NextTurn()
    {
        if (_isBidding || _isChoosingOrder || _gameState == GameState.Over)
            return;

        if (_currentPlayer != null)
            OnPlayerTurnEnded?.Invoke(_currentPlayer);

        _turnsTakenThisYear++;
        if (_turnsTakenThisYear >= Players.Count(p => !p.IsDead()))
        {
            _year++;
            StartYear();
            return;
        }
        StartNextPlayerTurn();
    }

    private void SetBidUIActive(bool active)
    {
        // Typed bid UI stays hidden; TurnHud's bid buttons show/hide from IsBidding.
        if (_bidButton != null) _bidButton.SetActive(false);
        if (_bidInputGO != null) _bidInputGO.SetActive(false);
        if (BidEnergyInput != null) BidEnergyInput.gameObject.SetActive(false);
    }

    private void ShowMessage(string message)
    {
        Debug.Log(message);
        if (UIGameInfo != null)
            UIGameInfo.ChangeInstructionWindowMessage(message);
    }
    private void LogPlayerTurnChanged(Player oldPlayer, Player newPlayer)
    {
        if (oldPlayer != null && newPlayer != null && oldPlayer != newPlayer)
            Debug.Log($"OnPlayerTurnChanged event triggered. Old player: {oldPlayer.Name}, New player: {newPlayer.Name}");
    }
    private void SetupGame()
    {
        DevastateTerritories(Options.Devastated);
        if (Options.Placement == TerritorySetup.TakeTurns)
        {
            AssignEnergy();
            EnqueuePlayers();
            return; // territories are claimed in StartDraft
        }
        var deal = OpeningDeal.Deal(_tr.Territories.Count, Players.Count, GameRandom.Current);
        for (var i = 0; i < deal.OwnerOf.Length; i++)
        {
            var territory = _tr.Territories[i];
            territory.SetOwner(Players[deal.OwnerOf[i]], deal.Troops[i]);
        }
        AssignEnergy();
        EnqueuePlayers();
    }


    // Random territories become wasteland before anything is dealt. A pick that would cut a territory off
    // from every neighbour is skipped.
    private void DevastateTerritories(int count)
    {
        if (count <= 0) return;
        var order = Enumerable.Range(0, _tr.Territories.Count).ToArray();
        OpeningDeal.Shuffle(order, GameRandom.Current);
        var candidates = order.Select(i => _tr.Territories[i]).ToList();
        var done = 0;
        foreach (var t in candidates)
        {
            if (done >= count) break;
            if (t.NeighbourTerritories.Any(n => n.NeighbourTerritories.Count <= 1)) continue;
            _tr.Devastate(t);
            done++;
            Debug.Log($"{t.Name} is devastated.");
        }
    }

    private void AssignEnergy()
    {
        foreach (var player in Players)
            player.SetEnergy(IncomeRules.StartingEnergy);
    }

    private void CreatePlayers()
    {
        if (Players.Count == 0)
            Players.AddRange(FindObjectsByType<Player>(FindObjectsInactive.Exclude));

        var playerCreator = PlayerCreator.Instance;
        
        if (_startingPlayersConfiguration != null)
            foreach (var playerCreationConfiguration in _startingPlayersConfiguration.PlayersConfiguration)
                Players.Add(playerCreator.CreatePlayerFromConfiguration(playerCreationConfiguration));

        for (var i = Players.Count; i < NPlayers; i++)
            Players.Add(playerCreator.CreateBotPlayer());

        foreach (var player in Players)
        {
            if (player.Name == "")
            {
                if (player.Color == null || player.Color.name == "UNDEFINED")
                    playerCreator.SetUpPlayerFromRandomColor(player);
                else
                    playerCreator.SetUpPlayerFromColor(player, player.Color);
            }
            player._energyBid = -1; //This needs to be changed to a .setEnergyBid() method later
        }
    }

    private void EnqueuePlayers() //only called once before bidding
    {
        _playerQueue = new Queue<Player>();

        var playerOrder = Enumerable.Range(0, Players.Count).ToList();
        playerOrder.Shuffle();
        foreach (var i in playerOrder)
            _playerQueue.Enqueue(Players[i]);
    }

    public void NextTurnPhase()
    {
        EndTurnPhase();
        IPhase nextTurnPhase = _currentPhase switch
        {
            //global::TurnPhases.AnnualBidding => ReinforcePhase,//we can't have this here because we only bid at the beginning of a year.
            global::TurnPhases.ReinforcePhase => AttackPhase,
            global::TurnPhases.AttackPhase => FortifyPhase,
            global::TurnPhases.FortifyPhase => EmptyPhase,
            global::TurnPhases.EmptyPhase => EmptyPhase,
            _ => throw new ArgumentOutOfRangeException()
        };
        SetTurnPhase(nextTurnPhase);
        if (_currentPhase == EmptyPhase) 
            NextTurn();
    }


    // Single entry point for every player action (UI, bots, tests, later the network).
    // Returns false if the action was rejected (nothing changes in that case).
    public bool HandlePlayerAction(PlayerAction action)
    {
        if (action == null)
            return false;

        // While a decision is open, the only thing allowed is an answer to it.
        if (_pendingDecision != null && action is not DecisionResponseAction)
        {
            Debug.LogWarning($"Action {action.GetType().Name} from {action.Player?.Name} rejected: " +
                             $"waiting for {InputPlayer?.Name} to answer {_pendingDecision.Kind}");
            return false;
        }

        if (!action.IsValid())
        {
            Debug.LogWarning("Invalid action " + action);
            return false;
        }

        if (Relay != null && _agreedDepth == 0)
            return Relay.Send(action);

        if (action is BidAction bid)
            return ApplyBid(bid.EnergyThePlayerBid);
        if (action is ChooseTurnPositionAction seat)
            return ApplyTurnPosition(seat.Position);

        // Command cards (2210): handled here, not by the phase, because they can happen in several phases.
        if (action is HireCommanderAction hire)
        {
            Purchases.Hire(hire.Player, hire.Type, hire.Territory);
            return true;
        }
        if (action is BuildSpaceStationAction build)
        {
            Purchases.Build(build.Player, build.Territory);
            return true;
        }
        if (action is BuyCommandCardAction buy)
        {
            CommandCards.Buy(buy.Player, buy.Deck);
            return true;
        }
        if (action is PlayCommandCardAction play)
        {
            CommandCards.Play(play.Player, play.Card, play.Targets);
            return true;
        }

        if (action is DecisionResponseAction response)
        {
            var callback = _onDecisionAnswered;
            CloseDecision();
            callback?.Invoke(response);
            return true;
        }

        _currentPhase.OnAction(_currentPlayer, action);
        return true;
    }

    // Opens a decision for a (possibly non-turn-owner) player. `onAnswered` runs once,
    // after the decision is closed, with the validated answer.
    public void OpenDecision(PendingDecision decision, Action<DecisionResponseAction> onAnswered)
    {
        if (decision == null) throw new ArgumentNullException(nameof(decision));
        if (_pendingDecision != null)
            throw new InvalidOperationException($"A decision is already pending: {_pendingDecision}");
        if (GetPlayerById(decision.PlayerId) == null)
            throw new ArgumentException($"Unknown player id {decision.PlayerId}");

        _pendingDecision = decision;
        _lastSubject = decision.Subject;
        _onDecisionAnswered = onAnswered;
        Debug.Log($"Decision opened: {decision} ({InputPlayer.Name})");
        OnPendingDecisionChanged?.Invoke(decision);

        // AI defenders, and players whose device dropped, decide instantly.
        var asked = InputPlayer;
        if (asked != null && (asked.IsBot() || !IsConnected(asked)))
        {
            // The AI may look at the player's hand, so online the host decides and the others use its answer.
            HandlePlayerAction(new DecisionResponseAction(asked, _turn, Secrets.Public(() => DefenderAI.Choose(this, decision))));
            return;
        }
        if (decision.Kind is DecisionKind.DefenseReaction or DecisionKind.EvacuationDestination
            or DecisionKind.ArmageddonCard or DecisionKind.ArmageddonTarget)
            _decisionClock.Start(Time.realtimeSinceStartupAsDouble, ReactionTimeoutSeconds);
    }

    private void CloseDecision()
    {
        _decisionClock.Stop();
        if (_pendingDecision == null) return;
        _pendingDecision = null;
        _onDecisionAnswered = null;
        OnPendingDecisionChanged?.Invoke(null);
    }
    

    

    private void StartTurnPhase()
    {
        _currentPhase.Start(_currentPlayer);
        OnPhaseStarted?.Invoke(_currentPhase);
    }

    private void EndTurnPhase()
    {
        _currentPhase.End(_currentPlayer);
        OnPhaseEnded?.Invoke(_currentPhase);
    }

    private void SetTurnPhase(IPhase phase)
    {
        CloseDecision();
        var oldPhase = _currentPhase;
        _currentPhase = phase;
        StartTurnPhase();
        OnTurnPhaseChanged?.Invoke(oldPhase, _currentPhase);
    }

    private void SetGameState(GameState gameState)
    {
        _gameState = gameState;
        OnGameStateChanged?.Invoke(gameState);
    }

    // Final ranking (2210 scoring), set when the game ends.
    public List<Player> FinalRanking { get; private set; } = new();
    public Player Winner => FinalRanking.FirstOrDefault();

    private void GameOver()
    {
        if (_gameState == GameState.Over) return;
        RevealAllHands();
        FinalRanking = Scoring.Ranking(Players);
        Debug.Log("Game over. " + string.Join(", ", FinalRanking.Select(p => $"{p.Name}: {Scoring.ScoreOf(p)}")));
        SetGameState(GameState.Over);
    }

    // Colony Influence cards count in the final score, so at the end every device sees every hand.
    private void RevealAllHands()
    {
        foreach (var p in Players)
        {
            var hand = p;
            var ids = Secrets.Public(() => string.Join(",", hand.Cards.Select(c => c.Id)));
            if (Secrets.Decides || string.IsNullOrEmpty(ids)) continue;
            foreach (var id in ids.Split(','))
                CommandCards.Reveal(p, _cr.CardById(int.Parse(id)));
        }
    }

    public bool IsCurrentPlayer(Player player)
    {
        return _currentPlayer == player;
    }
}

public enum GameState
{
    Setup,
    Playing,
    Over
}