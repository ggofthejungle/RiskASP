using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Actions;
using Cards;
using Extensions;
using Map;
using player;
using TurnPhases;
using UnityEngine;
using TMPro;
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

    public Player CurrentPlayer => _currentPlayer;
    private Player _currentPlayer;

    public GamePhase GamePhase => _gamePhase;
    private GamePhase _gamePhase = GamePhase.Setup;

    public int Turn => _turn;
    private int _turn;

    public IPhase CurrentPhase => _currentPhase;
    private IPhase _currentPhase;

    public BiddingPhase BiddingPhase { get; private set; }
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

    public Action<GamePhase> OnGamePhaseChanged;


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("There is more than one GameManager in the scene");
            Destroy(gameObject);
        }
        else
            Instance = this;

        SetGamePhase(GamePhase.Setup);

        _tr = TerritoryRepository.Instance;
        _cr = CardRepository.Instance;
        _bs = BattleSimulator.Instance;
        SetupPhases();
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
        SetupGame();
        SetGamePhase(GamePhase.Bidding);
        //bidEnergyInput.SetActive(true);
        //bidEnergyButton.SetActive(true);
        
        //regular game calls NextTurn(); but we only want some of the functionality
        _currentPlayer = _playerQueue.Dequeue(); //pick the first player in the queue to start the bidding
        //_playerQueue.Enqueue(_currentPlayer);
        //_turn++; //is this needed? 
        
        //listen for the player's bid:
        GameObject bidEnergyButton = GameObject.Find("BidEnergyButton");
        bidEnergyButton.GetComponent<Button>().onClick.AddListener(() => HandlePlayerBidEnergy());
    }

    private void SetupGame()
    {
        _tr.RandomlyAssignTerritories(Players);
        DistributeTroops();
        AssignEnergy();
        EnqueuePlayers();//this order can be random to start bidding. after bidding, the order will be set for this turn only
        //Next turn, the order will be set again by bidding
                         //call it once so that HandlePlayerBidEnergy can continue
        //DrawStartingCards();
        
        //I think we are done with Setup.
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
            if (player.Name == "")
            {
                if (player.Color == null || player.Color.name == "UNDEFINED")
                    playerCreator.SetUpPlayerFromRandomColor(player);
                else
                    playerCreator.SetUpPlayerFromColor(player, player.Color);
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
            global::TurnPhases.ReinforcePhase => AttackPhase,
            global::TurnPhases.AttackPhase => FortifyPhase,
            global::TurnPhases.FortifyPhase => EmptyPhase,
            global::TurnPhases.EmptyPhase => EmptyPhase,
            _ => throw new ArgumentOutOfRangeException()
        };

        SetTurnPhase(nextTurnPhase);


        if (_currentPhase == EmptyPhase)
        {
            NextTurn();
        }
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
        
        GameObject bidEnergyInput = GameObject.Find("BidEnergyInput");
        _currentPlayer._energyBid = Convert.ToInt32(bidEnergyInput.GetComponent<TMP_InputField>().text);
        
        //Later, instead of debug show that to the player in the UI under ExtraInfo:
        //BidEnergyOutput.text = "Player " + _currentPlayer.Name + " bid " + _currentPlayer._energyBid + " energy.";
        
        //UpdatePlayerText
        //_extraInfo[1].gameObject.SetActive(true);
        //_extraInfoTexts[1].text = $"Losses: Atk: {attackResult.AttackerLosses}, Def: {attackResult.DefenderLosses}";
        
        if (_currentPlayer._energyBid < 0 || _currentPlayer._energyBid > _currentPlayer.GetEnergy())
        {
            GameObject ExtraInfo1 = GameObject.Find("ExtraInfo1");
            //ExtraInfo1.text = 
            Debug.Log("Invalid bid. Please bid between 0 and " + _currentPlayer.GetEnergy());
            return;
        }
        
        Debug.Log("Player " + _currentPlayer.Name + " bid " + _currentPlayer._energyBid + " energy.");
        bidEnergyInput.GetComponent<TMP_InputField>().text = "";
        _turn++; // doesn't do anything
        //bidEnergyInput.text = "";
        
        //bidEnergyInput.SetActive(false);
        
        //after all players have bid, go to the next phase.
        if (_currentPlayer == _playerQueue.Peek())
        {
            //Determine the player with the highest bid looking at their energyBid:
            /*
            Player playerWithHighestBid = _playerQueue.Peek();
            foreach (Player player in _playerQueue)
            {
                if (player._energyBid > playerWithHighestBid._energyBid)
                {
                    playerWithHighestBid = player;
                }
            }

            //Set the player with the highest bid as the current player:
            _currentPlayer = playerWithHighestBid;
            //set the next player in the queue as the current player:
            _playerQueue.Enqueue(_playerQueue.Dequeue());*/
            
            SetGamePhase(GamePhase.Playing);
            NextTurn(); //nextTurn calls NextTurnPhase (reinforce), we don't want this until all players finished the bidding phase
        }
    }

    private void NextTurn()
    {
        var oldPlayer = _currentPlayer;
        if (oldPlayer != null)
            OnPlayerTurnEnded?.Invoke(oldPlayer);

        do
        {
            _currentPlayer = _playerQueue.Dequeue();
        } while (_currentPlayer.IsDead());

        _playerQueue.Enqueue(_currentPlayer);

        _turn++;
        SetTurnPhase(ReinforcePhase);
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

    private void SetGamePhase(GamePhase gamePhase)
    {
        _gamePhase = gamePhase;
        OnGamePhaseChanged?.Invoke(gamePhase);
    }

    private void GameOver()
    {
        SetGamePhase(GamePhase.Over);
    }

    public bool IsCurrentPlayer(Player player)
    {
        return _currentPlayer == player;
    }
}

public enum GamePhase
{
    Setup,
    Bidding,
    Playing,
    Over
}