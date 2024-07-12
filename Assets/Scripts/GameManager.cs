using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Actions;
using Cards;
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

    public Player CurrentPlayer => _currentPlayer;
    private Player _currentPlayer;

    public GameState GameState => _gameState;
    private GameState _gameState = GameState.Setup;

    public int Turn => _turn;
    private int _turn;
    public int Year => _year;
    private int _year = 1;
    
    public IPhase CurrentPhase => _currentPhase;
    private IPhase _currentPhase;

    public AnnualBidding AnnualBidding { get; private set; }
    public ReinforcePhase ReinforcePhase { get; private set; }
    public AttackPhase AttackPhase { get; private set; }
    public FortifyPhase FortifyPhase { get; private set; }
    public EmptyPhase EmptyPhase { get; private set; }


    [SerializeField] private int _startingCardsPerPlayer = 4;
    [SerializeField] private int _maxCardsPerPlayer = 5;
    

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
        AnnualBidding = new AnnualBidding(this);
        CreatePlayers();

        SubscribeToEvents();
    }

    private void SubscribeToEvents()
    {
        OnPlayerTurnEnded += TryDrawCardOnTurnEnd;
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


    private void Start()
    {
        OnPlayerTurnChanged += LogPlayerTurnChanged; // subscribe to the OnPlayerTurnChanged event. 

        SetupGame();
        
        //regular game calls NextTurn(); but we only want to call it when we're not bidding
        //_currentPlayer = _playerQueue.Peek(); //pick the first player in the queue to start the bidding - need to comment this because t was causing the HandlePlayerBidEnergy() method to be called immediately at the start of the game, without waiting for the player to enter their bid.
        //_turn++; //was used in the classic game.
        
        //listen for the player's bid:
        GameObject bidEnergyButton = GameObject.Find("BidEnergyButton");
        bidEnergyButton.GetComponent<Button>().onClick.AddListener(() => HandlePlayerBidEnergy());
        Debug.Log("Start(). Before NextYear():");
        NextYear();
    }
    private void LogPlayerTurnChanged(Player oldPlayer, Player newPlayer)
    {
        if (oldPlayer != null && newPlayer != null && oldPlayer != newPlayer)
            Debug.Log($"OnPlayerTurnChanged event triggered. Old player: {oldPlayer.Name}, New player: {newPlayer.Name}");
    }
    private void NextYear()
    {
        Debug.Log("Next year: " + _year);
        if (_year > 5)
        {
            GameOver();
            return;
        }

        //Start the bidding phase - NO - this is now called from Start, as a response to the user clicking!
        //HandlePlayerBidEnergy();
        NextTurn();
    }
    private void SetupGame()
    {
        _tr.RandomlyAssignTerritories(Players);
        DistributeTroops();
        AssignEnergy();
        EnqueuePlayers();
        //this order can be random to start bidding. after bidding, the order will be set on a turn-by-turn basis
        //call it once so that HandlePlayerBidEnergy can continue
        //DrawStartingCards();
    }

    private void DrawStartingCards()
    {
        foreach (var player in Players)
        {
            for (int i = 0; i < _startingCardsPerPlayer; i++)
            {
                player.AddCard(_cr.DrawRandomCard());
            }
        }
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
            Players.AddRange(FindObjectsByType<Player>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));

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

    private void TryDrawCardOnTurnEnd(Player player)
    {
        if (AttackPhase.ConqueredTerritoriesCount == 0
            || (_maxCardsPerPlayer >= 0 && player.Cards.Count >= _maxCardsPerPlayer))
            return;

        var card = _cr.DrawRandomCard();
        player.AddCard(card);
    }

    public void HandlePlayerAction(PlayerAction action)
    {
        if (!action.IsValid())
        {
            Debug.LogWarning("Invalid action" + action);
            return;
        }
        _currentPhase.OnAction(_currentPlayer, action);
    }
    
    public void HandlePlayerBidEnergy()
    {
        Debug.Log("HandlePlayerBidEnergy() called.");
        GameObject bidEnergyInput = GameObject.Find("BidEnergyInput");
        //_currentPlayer._energyBid = Convert.ToInt32(bidEnergyInput.GetComponent<TMP_InputField>().text);
        string bidEnergyText = bidEnergyInput.GetComponent<TMP_InputField>().text;
        if (int.TryParse(bidEnergyText, out int bidEnergy))
        {
            _currentPlayer._energyBid = bidEnergy;
            //loop through all players and check if they have bid:
            bool allPlayersBid = true;
            foreach (Player player in _playerQueue)
            {
                if (player._energyBid == -1)
                {
                    allPlayersBid = false;
                    break;
                }
            }
            
            //if all players have bid, save the new turn order and go to the next year.
            if (allPlayersBid)
            {
                //order the players by player._energyBid:   
                List<Player> playersList = new List<Player>(_playerQueue);
                playersList.Sort((x, y) =>
                {
                    // Compare by energy bid
                    int result = y._energyBid.CompareTo(x._energyBid); // Reverse order (higher bids first)

                    // If bids are the same, shuffle randomly
                    if (result == 0)
                    {
                        result = new Random().Next(-1, 2); // -1, 0, or 1 randomly
                    }

                    return result;
                });

                _playerQueue.Clear();

                //Re-enqueue the players in the order of their bids by looking at player._energyBid: #2#
                foreach (var player in playersList)
                {
                    _playerQueue.Enqueue(player);
                    player._energy = player._energy - player._energyBid;
                    //need to verify next round if the old bid energy - should be overwritten by new bid
                }
                
                foreach (var player in _playerQueue)
                {
                    Debug.Log($"Name: {player.Name}, Ordered Energy Bid: {player._energyBid}");
                }
                
                UIGameInfo.CloseInstructionWindow();
                GameObject bidEnergyButton = GameObject.Find("BidEnergyButton");
                bidEnergyButton.SetActive(false);
                bidEnergyInput.SetActive(false);
                //SetGameState(GamePhase.Playing);
                //debug log the order of the players in a single line:
                string playerOrder = "";
                foreach (var player in _playerQueue)
                {
                    playerOrder += player.Name + " ";
                }
                Debug.Log("HandlePlayerBidEnergy: All players have bid. New turn order set. Player order: " + playerOrder + " Calling NextTurn next.");
                NextTurn(); //this needs to picks up the first player in the NEW queue
            }
            else //there are still players left to bid
            {
                Debug.Log("HandlePlayerBidEnergy: we're still waiting for other players to bid. Changing turn orders");
                var oldPlayer = _currentPlayer;
                OnPlayerTurnChanged?.Invoke(oldPlayer, _currentPlayer);
                NextTurnAnnualBidding();
            }
        }
        else
        {
            Debug.Log("Invalid bid. Please enter a valid number.");
            UIGameInfo.ChangeInstructionWindowMessage("Invalid bid. Please enter a valid number.");
        }
        
        /*
        if (_currentPlayer._energyBid < 0 || _currentPlayer._energyBid > _currentPlayer.GetEnergy())
        {
            GameObject ExtraInfo1 = GameObject.Find("ExtraInfo1");
            Debug.Log("Invalid bid. Please bid between 0 and " + _currentPlayer.GetEnergy());
            UIGameInfo.ChangeInstructionWindowMessage("Invalid bid. Please bid between 0 and " + _currentPlayer.GetEnergy());
            return;
        }
        
        Debug.Log("Player " + _currentPlayer.Name + " bid " + _currentPlayer._energyBid + " energy.");
        UIGameInfo.ChangeInstructionWindowMessage("Player " + _currentPlayer.Name + " bid " + _currentPlayer._energyBid + " energy.");
        bidEnergyInput.GetComponent<TMP_InputField>().text = "";
        */
        
        
    }

    //Regular turn. Make sure it doesn't get called when it's bidding time. 
    private void NextTurn()
    {
        Debug.Log("NextTurn() begins. turn= " + _turn);
        var oldPlayer = _currentPlayer;
        if (oldPlayer != null)
            OnPlayerTurnEnded?.Invoke(oldPlayer);

        do
        {
            _currentPlayer = _playerQueue.Dequeue();
        } while (_currentPlayer.IsDead());

        _playerQueue.Enqueue(_currentPlayer);
        
        /*if (_turn >= NPlayers) //end of the year if all players have had a turn
        {
            _turn = 0;
            _year++;
            Debug.Log("NextTurn() end: End of the year. Resetting _turn to zero and incrementing _year = " + _year);
            NextYear();
        }
        else //next player's turn. same year
        {
            _turn++;
            Debug.Log("NextTurn() end: Same year. incrementing _turn = " + _turn);
            SetTurnPhase(ReinforcePhase);
        }*/
        
        _turn++;
        if (_turn >= NPlayers)
        {
            _turn = 0;
            NextYear();
        }
        Debug.Log("NextTurn increased turn: " + _turn);
        OnPlayerTurnChanged?.Invoke(oldPlayer, _currentPlayer);
        SetTurnPhase(ReinforcePhase);
    }
    
    //Bidding turn
    private void NextTurnAnnualBidding()
    {
        //need to wait for player input of the bid; can't just go to the next turn already.
        Debug.Log("NextTurnAnnualBidding(): before dequeue:" + _currentPlayer.Name);
        var oldPlayer = _currentPlayer;
        if (oldPlayer != null)
            OnPlayerTurnEnded?.Invoke(oldPlayer);

        //_playerQueue.Enqueue(_currentPlayer); //doesn't matter what order we're bidding, as long as all players bid.
        // Dequeue the next player and set them as the current player
        _currentPlayer = _playerQueue.Dequeue();
        _playerQueue.Enqueue(_currentPlayer);
        
        //_turn++;
        Debug.Log("NextTurnAnnualBidding after dequeue: " + _currentPlayer.Name);
        //SetTurnPhase(ReinforcePhase);
        OnPlayerTurnChanged?.Invoke(oldPlayer, _currentPlayer);
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