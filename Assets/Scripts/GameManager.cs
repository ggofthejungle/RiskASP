using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Actions;
using Cards;
using Decisions;
using Expansions;
using Extensions;
using Map;
using player;
using TurnPhases;
using UnityEngine;
using TMPro;
using UI;
using UnityEngine.UI;
using Random = System.Random;

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
        AnnualBidding = new AnnualBidding(this);
        CreatePlayers();

        SubscribeToEvents();
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
    private readonly Random _rng = new Random();

    private void Start()
    {
        OnPlayerTurnChanged += LogPlayerTurnChanged;
        SetupGame();
        _bidButton = GameObject.Find("BidEnergyButton");
        _bidInputGO = GameObject.Find("BidEnergyInput");
        if (_bidButton != null)
            _bidButton.GetComponent<Button>().onClick.AddListener(HandlePlayerBidEnergy);
        CreateHud();
        StartYear();
    }

    // Turn banner + instructions + turn splash, and the attack/move arrow on the map.
    public UI.TurnHud TurnHud { get; private set; }
    public Map.SelectionArrow SelectionArrow { get; private set; }

    private void CreateHud()
    {
        var canvas = UIGameInfo != null ? UIGameInfo.GetComponentInParent<Canvas>() : null;
        if (canvas != null) canvas = canvas.rootCanvas;
        if (canvas != null)
            TurnHud = UI.TurnHud.Create(this, canvas);
        SelectionArrow = Map.SelectionArrow.Create(this);
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
        if (_currentPhase != EmptyPhase)
            SetTurnPhase(EmptyPhase);
        SetBidUIActive(true);

        var oldPlayer = _currentPlayer;
        _currentPlayer = _playerQueue.First(p => !p.IsDead());
        OnPlayerTurnChanged?.Invoke(oldPlayer, _currentPlayer);
        PromptCurrentBidder();
    }

    private void PromptCurrentBidder()
    {
        if (_currentPlayer.IsBot())
        {
            SubmitBid(0); // simple bot bid for now
            return;
        }
        ShowMessage($"Year {_year}: {_currentPlayer.Name}, bid energy for turn order (0-{_currentPlayer.GetEnergy()}).");
    }

    // Called by the Bid button (reads the input field).
    public void HandlePlayerBidEnergy()
    {
        var input = _bidInputGO != null ? _bidInputGO.GetComponent<TMP_InputField>() : BidEnergyInput;
        var text = input != null ? input.text : "";
        if (!int.TryParse(text, out var bid))
        {
            ShowMessage("Invalid bid. Please enter a whole number.");
            return;
        }
        if (SubmitBid(bid) && input != null)
            input.text = "";
    }

    // Submits a bid for the current bidder. Returns false (and changes nothing) if the bid is invalid.
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

        _currentPlayer._energyBid = bid;
        Debug.Log($"Year {_year}: {_currentPlayer.Name} bid {bid}");

        var next = _playerQueue.FirstOrDefault(p => !p.IsDead() && p._energyBid < 0);
        if (next == null)
        {
            FinishBidding();
            return true;
        }

        var oldPlayer = _currentPlayer;
        _currentPlayer = next;
        OnPlayerTurnChanged?.Invoke(oldPlayer, _currentPlayer);
        PromptCurrentBidder();
        return true;
    }

    private void FinishBidding()
    {
        var bidders = _playerQueue.Where(p => !p.IsDead()).ToList();
        var dead = _playerQueue.Where(p => p.IsDead()).ToList();
        var tieBreak = bidders.ToDictionary(p => p, _ => _rng.Next());
        var ordered = bidders.OrderByDescending(p => p._energyBid).ThenBy(p => tieBreak[p]).ToList();

        foreach (var player in ordered)
            player.SetEnergy(player.GetEnergy() - player._energyBid);

        Debug.Log($"Bidding done (year {_year}). Bid order: " +
                  string.Join(", ", ordered.Select(p => $"{p.Name} ({p._energyBid})")));

        _isBidding = false;
        SetBidUIActive(false);
        if (UIGameInfo != null)
            UIGameInfo.CloseInstructionWindow();

        // 2210: in bid order (highest first), each player CHOOSES their turn position for this year.
        _isChoosingOrder = true;
        _choosers = new Queue<Player>(ordered);
        _chosenPositions = new Player[ordered.Count];
        _deadAfterChoosing = dead;
        PromptNextChooser();
    }

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

        var chooser = _choosers.Dequeue();
        _chosenPositions[position - 1] = chooser;
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
        if (_bidButton != null) _bidButton.SetActive(active);
        if (_bidInputGO != null) _bidInputGO.SetActive(active);
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
        _tr.RandomlyAssignTerritories(Players);
        DistributeTroops();
        AssignEnergy();
        EnqueuePlayers();
        //this order can be random to start bidding. after bidding, the order will be set on a turn-by-turn basis
        //call it once so that HandlePlayerBidEnergy can continue
        // Test setup from main (commanders + space station on first territory)
        _tr.Territories.First().AddCommander(new Commander(CommanderType.LandCommander));
        _tr.Territories.First().AddCommander(new Commander(CommanderType.Diplomat));
        _tr.Territories.First().AddCommander(new Commander(CommanderType.NuclearCommander));
        _tr.Territories.First().AddSpaceStation();
    }


    private void AssignEnergy()
    {
        foreach (var player in Players)
        {
            player.SetEnergy(3);
        }
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

    private void DistributeTroops()
    {
        int[] troopsPerNumberOfPlayer = { -1, -1, 40, 35, 30, 25, 20 };

        foreach (var player in Players)
        {
            int troopsPerPlayer = troopsPerNumberOfPlayer[Players.Count];
            player.ClearTroops();
            troopsPerPlayer = player.DistributeNTroopsPerTerritory(1, troopsPerPlayer);
            player.RandomlyDistributeTroops(troopsPerPlayer);
        }
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

        // Command cards (2210): handled here, not by the phase, because they can happen in several phases.
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
        _onDecisionAnswered = onAnswered;
        Debug.Log($"Decision opened: {decision} ({InputPlayer.Name})");
        OnPendingDecisionChanged?.Invoke(decision);
    }

    private void CloseDecision()
    {
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

    private void GameOver()
    {
        SetGameState(GameState.Over);
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