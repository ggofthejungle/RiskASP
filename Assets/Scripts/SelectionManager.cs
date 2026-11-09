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
        _attackSelectionPhase = new AttackSelectionPhase(_gm, this, ar);
        _fortifySelectionPhase = new FortifySelectionPhase(_gm, this, ar);
        _emptySelectionPhase = new EmptySelectionPhase();
        SetPhase(_emptySelectionPhase);
        
        foreach (var territory in _tr.Territories)
            Include(territory);

        _gm.OnTurnPhaseChanged += OnTurnPhaseChanged;
    }

    // Seas and the moon are created after this object awakes, so they register themselves.
    public void Include(Territory territory)
    {
        if (territory == null || TerritoryToSelectionMap.ContainsKey(territory)) return;
        var selection = territory.GetComponent<TerritorySelection>();
        if (selection == null)
        {
            Debug.LogError($"Territory {territory.name} has no TerritorySelection component");
            return;
        }
        TerritoryToSelectionMap[territory] = selection;
        selection.OnClicked += OnTerritoryClicked;
    }

    void OnTerritoryClicked(TerritorySelection selection)
    {
        // A card / purchase / decision is waiting for a territory: give it the click instead.
        if (_targeting != null)
        {
            if (_targeting(selection.Territory)) EndTargeting();
            return;
        }
        // Otherwise map clicks belong to the turn owner; ignore them while someone else is deciding.
        if (_gm.PendingDecision != null) return;
        // Not this device's turn (online, or a bot's turn): the map is look-only.
        if (!_gm.IsLocal(_gm.CurrentPlayer)) return;
        _currentSelectionPhase.OnClicked(_gm.CurrentPlayer, selection);
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


    // ---------------- Targeting (cards, hiring, building) ----------------
    // While targeting, only territories accepted by canTarget are clickable, and each click goes to onClick,
    // which returns true when it has everything it needs. Then the normal phase selection comes back.
    private System.Func<Territory, bool> _targeting;
    private System.Func<Territory, string> _whyNot;
    public bool IsTargeting => _targeting != null;

    public void BeginTargeting(System.Func<Territory, bool> onClick, System.Func<Territory, bool> canTarget,
        System.Func<Territory, string> whyNot = null)
    {
        if (_targeting == null) _currentSelectionPhase.End(_gm.CurrentPlayer);
        _targeting = onClick;
        _whyNot = whyNot;
        DisableAllTerritories();
        foreach (var kv in TerritoryToSelectionMap)
            if (canTarget(kv.Key)) kv.Value.Enable();
    }

    // A click on a territory this card cannot use. The map stays as it is and the player gets a reason.
    public void Reject(Territory territory)
    {
        var why = _whyNot != null ? _whyNot(territory) : null;
        if (string.IsNullOrEmpty(why))
            why = "You can't use " + (territory != null ? territory.Name.Replace('_', ' ') : "that") + " for this.";
        _gm.TurnHud?.Flash(why);
    }

    public void EndTargeting()
    {
        if (_targeting == null) return;
        _targeting = null;
        _whyNot = null;
        _currentSelectionPhase.Start(_gm.CurrentPlayer);
    }

    public void DisableAllTerritories()
    {
        foreach (var territorySelection in TerritoryToSelectionMap.Values)
            territorySelection.Disable();
    }


    public void EnableTerritory(Territory territory)
    {
        if (TerritoryToSelectionMap.TryGetValue(territory, out var selection)) selection.Enable();
    }
    public void EnableTerritory(TerritorySelection territory)
    {
        territory.Enable();
    }
    public void EnableTerritories(params TerritorySelection[] territories)
    {
        foreach (var territory in territories)
            EnableTerritory(territory);
    }
    
    public void DisableTerritory(Territory territory)
    {
        if (TerritoryToSelectionMap.TryGetValue(territory, out var selection)) selection.Disable();
    }
    public void DisableTerritory(TerritorySelection territory)
    {
        territory.Disable();
    }

    public void DisableTerritories(params TerritorySelection[] territories)
    {
        foreach (var territory in territories)
            DisableTerritory(territory);
    }

    public void EnablePlayerTerritoriesWithAttackPossibility(Player player)
    {
        foreach (var territory in player.Territories)
        {
            if (territory.GetAvailableTroops() > 0 && territory.HasAnyNeighbourEnemy())
                EnableTerritory(territory);
        }
    }
    
    public void EnablePlayerTerritoriesWithAvailableTroops(Player player)
    {
        foreach (var territory in player.Territories)
        {
            if (territory.GetAvailableTroops() > 0)
                EnableTerritory(territory);
        }
    }

    public void EnablePlayerTerritories(Player player)
    {
        foreach (var territory in player.Territories)
            if (TerritoryToSelectionMap.TryGetValue(territory, out var selection)) selection.Enable();
    }

    public void EnableEnemyNeighbourTerritories(Territory territory)
    {
        foreach (var neighbour in territory.NeighbourTerritories)
        {
            if (neighbour.Owner != territory.Owner)
                if (TerritoryToSelectionMap.TryGetValue(neighbour, out var selection)) selection.Enable();
        }
    }

    public void EnablePlayerTerritoriesReachableFrom(Player player, TerritorySelection from)
    {
        
        foreach (var territory in player.Territories)
        {
            if(territory == from.Territory)
                continue;
            
            if (_tr.CanFortifyTerritory(from.Territory, territory))
                if (TerritoryToSelectionMap.TryGetValue(territory, out var selection)) selection.Enable();
        }
    }
}