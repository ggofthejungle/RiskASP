using System.Collections.Generic;
using Map;
using player;
using TurnPhases;
using TurnPhases.Selection;
using UnityEngine;

public class SelectionManager : MonoBehaviour
{
    public Dictionary<Territory,TerritorySelection> TerritoryToSelectionMap { get; } = new();

    private GameManager _gm;
    private TerritoryRepository _tr;

    private IPhase _currentPhase => _gm.CurrentPhase;

    public static SelectionManager Instance { get; private set; }

    public ISelectionPhase CurrentSelectionPhase => _currentSelectionPhase;
    private ISelectionPhase _currentSelectionPhase;

    private ReinforceSelectionPhase _reinforceSelectionPhase;
    private AttackSelectionPhase _attackSelectionPhase;
    private FortifySelectionPhase _fortifySelectionPhase;
    private EmptySelectionPhase _emptySelectionPhase;


    private void Awake()
    {
        if (Instance != null && Instance != this)
            Destroy(gameObject);
        else
            Instance = this;

        _tr = TerritoryRepository.Instance;
        _gm = GameManager.Instance;

        Setup();
    }

    private void Setup()
    {
        var ar = ActionReader.Instance; 
        _reinforceSelectionPhase = new ReinforceSelectionPhase(_gm, this, ar);
        _attackSelectionPhase = new AttackSelectionPhase();
        _fortifySelectionPhase = new FortifySelectionPhase();
        _emptySelectionPhase = new EmptySelectionPhase();
        SetPhase(_emptySelectionPhase);
        
        foreach (var territory in _tr.Territories.Values)
        {
            var territorySelection = territory.GetComponent<TerritorySelection>();
            if (territorySelection)
                TerritoryToSelectionMap[territory] = territorySelection;
            else
                Debug.LogError($"Territory {territory.name} has no TerritorySelection component");
        }

        SubscribeToCallbacks();
    }

    private void SubscribeToCallbacks()
    {
        foreach (var territorySelection in TerritoryToSelectionMap.Values)
        {
            territorySelection.OnSelected += (selection) =>
            {
                _currentSelectionPhase.OnSelected(_gm.CurrentPlayer, selection);
            };
            
            territorySelection.OnUnselected += (selection) =>
            {
                _currentSelectionPhase.OnUnselected(_gm.CurrentPlayer, selection);
            };
        }

        _gm.OnTurnPhaseChanged += OnTurnPhaseChanged;
    }


    private void OnTurnPhaseChanged(IPhase oldPhase, IPhase newPhase)
    {
        _currentSelectionPhase.End(_gm.CurrentPlayer);
        ISelectionPhase nextPhase = newPhase switch
        {
            ReinforcePhase => _reinforceSelectionPhase,
            AttackPhase => _attackSelectionPhase,
            FortifyPhase => _fortifySelectionPhase,
            _ => _emptySelectionPhase
        };
        SetPhase(nextPhase);
    }


    private void SetPhase(ISelectionPhase phase)
    {
        _currentSelectionPhase = phase;
        _currentSelectionPhase.Start(_gm.CurrentPlayer);
    }


    public void DisableAllTerritories()
    {
        foreach (var territorySelection in TerritoryToSelectionMap.Values)
            territorySelection.Disable();
    }

    public void EnablePlayerTerritoriesWithAvailableTroops(Player player)
    {
        foreach (var territory in player.Territories)
        {
            if (territory.GetAvailableTroops() > 0)
                TerritoryToSelectionMap[territory].Enable();
        }
    }

    public void EnablePlayerTerritories(Player player)
    {
        foreach (var territory in player.Territories)
            TerritoryToSelectionMap[territory].Enable();
    }
}