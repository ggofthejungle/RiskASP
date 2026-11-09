using System;
using System.Linq;
using Actions;
using Map;
using player;
using Unity.VisualScripting;
using UnityEngine;

namespace TurnPhases.Selection
{
    public class AttackSelectionPhase : ISelectionPhase
    {
        private readonly GameManager _gm;
        private readonly SelectionManager _sm;
        private readonly ActionReader _ar;

        private AttackPhase _attackPhase => _gm.AttackPhase;
        public AttackState State => _attackPhase.State;

        // Read-only view of the current selection, for the HUD instructions and the arrow.
        public Territory From => _from != null ? _from.Territory : null;
        public Territory To => _to != null ? _to.Territory : null;

        private TerritorySelection _from;
        private TerritorySelection _to;

        private int _reinforcementsToMove;

        public AttackSelectionPhase(GameManager gm, SelectionManager sm, ActionReader ar)
        {
            _gm = gm;
            _sm = sm;
            _ar = ar;
            _attackPhase.OnAttackTurn += OnAttackTurn;
        }

        private void OnAttackTurn()
        {
            if (State == AttackState.Attacking)
            {
                UnselectAll();
                EnableTerritoriesToAttackFrom(_gm.CurrentPlayer);
            }
            else if (State == AttackState.Fortifying)
            {
                if (_from == null)
                {
                    var from = _attackPhase.LastAttackResult.Origin;
                    _from = _sm.TerritoryToSelectionMap[from];
                }
                if (_to == null)
                {
                    var to = _attackPhase.LastAttackResult.Target;
                    _to = _sm.TerritoryToSelectionMap[to];
                }

                EnableTerritoriesToConfirmMove();
            }
        }


        public void Start(Player player)
        {
            UnselectAll();
            _reinforcementsToMove = 0;
            EnableTerritoriesToAttackFrom(player);
        }

        public void OnClicked(Player player, TerritorySelection selection)
        {
            if (selection == null) throw new ArgumentNullException(selection.Territory.Name);

            if (!selection.IsSelected) OnSelected(player, selection);
            else OnUnselected(player, selection);
        }

        private void OnSelected(Player player, TerritorySelection selection)
        {
            if (State == AttackState.Fortifying)
                return;

            if (_from == null)
            {
                var locked = Expansions.Purchases.WhyCantEnterBattle(player, selection.Territory, selection.Territory);
                if (locked != null)
                {
                    _gm.TurnHud?.Flash(locked);
                    return;
                }
                if (selection.IsDisabled || selection.Territory.Owner != player) return;
                _from = selection;
                _from.Select();
                EnableTerritoriesToAttack(player);
            }
            else if (_to == null)
            {
                if (_from == selection)
                    throw new Exception("Cannot attack from a territory to itself");
                var locked = Expansions.Purchases.WhyCantEnterBattle(player, _from.Territory, selection.Territory);
                if (locked != null)
                {
                    _gm.TurnHud?.Flash(locked);
                    return;
                }
                if (_from.Territory.Kind == TerrainKind.Moon && selection.Territory.Kind == TerrainKind.Land
                    && (_gm.CommandCards == null || !_gm.CommandCards.AllowsSurfaceAttack(player, _from.Territory, selection.Territory)))
                {
                    _gm.TurnHud?.Flash("The Moon can attack Earth only with Invade Earth.");
                    return;
                }
                if (selection.IsDisabled || selection.Territory.Owner == player) return;
                _to = selection;
                _to.Select();

                CreateAttackAction(player);
            }
        }

        private void CreateAttackAction(Player player)
        {
            var from = _from.Territory;
            var to = _to.Territory;
            // Commanders here: ask who joins the attack (and how many units) before rolling.
            if (from.CommandersList.Count > 0 && _gm.CommandHud != null && _gm.IsLocal(player))
            {
                _gm.CommandHud.AskAttack(player, from, to,
                    (troops, commanders) => _ar.AddAction(new AttackAction(player, _gm.Turn, _attackPhase.AttackTurn, from, to, troops, commanders)),
                    CancelTarget);
                return;
            }
            var t = Math.Min(from.GetAvailableTroops(), 3);
            _ar.AddAction(new AttackAction(player, _gm.Turn, _attackPhase.AttackTurn, from, to, t, Array.Empty<Expansions.CommanderType>()));
        }

        // Back to "choose a target" (keeps the attacking territory selected).
        public void CancelTarget()
        {
            if (_to != null) _to.Unselect();
            _to = null;
            if (_from != null) EnableTerritoriesToAttack(_gm.CurrentPlayer);
        }

        public void OnUnselected(Player player, TerritorySelection selection)
        {
            if (State == AttackState.Attacking)
                OnUnselectedAttacking(player, selection);
            else if (State == AttackState.Fortifying)
                OnUnselectedMoving(player, selection);
        }


        private void OnUnselectedAttacking(Player player, TerritorySelection selection)
        {
            if (_from == selection)
            {
                UnselectAll();
                EnableTerritoriesToAttackFrom(player);
            }
            else if (_to == selection)
            {
                _to.Unselect();
                _to = null;
                EnableTerritoriesToAttack(player);
            }
            else Debug.LogError($"Territory {selection.Territory.Name} is not from or to");
        }

        private void OnUnselectedMoving(Player player, TerritorySelection selection)
        {
            if (_from == selection)
            {
                SetTroopsToMove(_attackPhase.LastAttackResult.GetMinTroopsToMoveAfterWin());
                CreateMoveAction(player);
            }
            else if (_to == selection)
            {
                SetTroopsToMove(_from.Territory.GetAvailableTroops());
                CreateMoveAction(player);
            }
            else Debug.LogError($"Territory {selection.Territory.Name} is not from or to");
        }

        private void CreateMoveAction(Player player)
        {
            var attackAction = _attackPhase.LastAttackResult.AttackAction;
            var attackTurn = _attackPhase.AttackTurn;
            var action = new AttackReinforceAction(player, _gm.Turn, attackTurn, attackAction, _reinforcementsToMove);
            _ar.AddAction(action);
        }

        public void End(Player player)
        {
            UnselectAll();
        }


        private void EnableTerritoriesToAttackFrom(Player player)
        {
            _sm.DisableAllTerritories();
            _sm.EnablePlayerTerritoriesWithAttackPossibility(player);
            if (_gm.CommandCards == null) return;
            foreach (var t in player.Territories)
                if (t.GetAvailableTroops() > 0 && _gm.CommandCards.SurfaceLands(player).Any(land => _gm.CommandCards.AllowsSurfaceAttack(player, t, land)))
                    _sm.EnableTerritory(t);
        }

        private void EnableTerritoriesToAttack(Player player)
        {
            _sm.DisableAllTerritories();
            _sm.EnableEnemyNeighbourTerritories(_from.Territory);
            foreach (var n in _from.Territory.NeighbourTerritories)
            {
                if (n.Owner != player && _gm.AttackPhase.IsCeaseFireAgainst(n.Owner))
                    _sm.TerritoryToSelectionMap[n].Disable();
                if (_from.Territory.Kind == TerrainKind.Moon && n.Kind == TerrainKind.Land
                    && (_gm.CommandCards == null || !_gm.CommandCards.AllowsSurfaceAttack(player, _from.Territory, n)))
                    _sm.TerritoryToSelectionMap[n].Disable();
            }
            _sm.EnableTerritory(_from);
            if (_gm.CommandCards == null) return;
            foreach (var land in _gm.CommandCards.SurfaceLands(player))
                if (_gm.CommandCards.AllowsSurfaceAttack(player, _from.Territory, land) && !_gm.AttackPhase.IsCeaseFireAgainst(land.Owner))
                    _sm.EnableTerritory(land);
        }


        private void EnableTerritoriesToConfirmMove()
        {
            _sm.DisableAllTerritories();
            _sm.EnableTerritories(_from, _to);
        }

        private void UnselectAll()
        {
            if (_from) _from.Unselect();
            if (_to) _to.Unselect();
            _from = null;
            _to = null;
        }

        public void SetTroopsToMove(int troops)
        {
            if (State == AttackState.Fortifying)
                _reinforcementsToMove = troops;
            else
                Debug.LogError("Cannot set troops to move when not in moving state");
        }
    }
}