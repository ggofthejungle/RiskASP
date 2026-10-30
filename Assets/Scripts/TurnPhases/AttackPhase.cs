using System;
using System.Collections.Generic;
using System.Linq;
using Actions;
using Decisions;
using Expansions;
using JetBrains.Annotations;
using Map;
using player;
using UnityEngine;

namespace TurnPhases
{
    public class AttackPhase : IPhase
    {
        public string Name => "Attack";

        private readonly GameManager _gm;
        private readonly BattleSimulator _bs;


        public int ConqueredTerritoriesCount { get; private set; }

        public AttackState State => _state;
        private AttackState _state = AttackState.Attacking;
       
        public int AttackTurn => _attackTurn;
        private int _attackTurn;


        private List<AttackResult> _attackResults = new ();
        public AttackResult LastAttackResult => _attackResults.Last();
        public IEnumerable<AttackResult> AttackResults => _attackResults;

        // Reaction window (defender interrupt) before each dice roll.
        // Step 1: the hook exists but nothing opens it yet (no defense cards). Step 2 will make it
        // "defender holds a playable defense card". Tests can set it to force the window open.
        public Func<AttackAction, bool> ShouldOpenReactionWindow = _ => false;

        // Attack declared, waiting for the defender's answer before rolling.
        public AttackAction PendingAttack => _pendingAttack;
        private AttackAction _pendingAttack;

        // Defenders who chose "Pass for rest of turn" this turn.
        private readonly HashSet<Player> _passedForTurn = new();
        public bool HasPassedForTurn(Player defender) => _passedForTurn.Contains(defender);

        public Action<AttackResult> OnAttacked;
        public Action<AttackReinforceAction> OnReinforced;
        public Action OnAttackStateChanged;
        public Action OnAttackTurn;

        public AttackPhase(GameManager gm, BattleSimulator bs)
        {
            _gm = gm;
            _bs = bs;
        }


        public void Start(Player player)
        {
            _attackResults.Clear();
            _passedForTurn.Clear();
            _pendingAttack = null;
            ConqueredTerritoriesCount = 0;
            _attackTurn = 1;
            SetState(AttackState.Attacking);
            //_dcca.EnergyCrisis(player);
            OnAttackTurn?.Invoke();
        }

        // ReSharper disable Unity.PerformanceAnalysis
        public void OnAction(Player player, PlayerAction action)
        {
            if (_state == AttackState.AwaitingDefender)
            {
                Debug.LogWarning($"AttackPhase: waiting for the defender, ignoring {action.GetType().Name}");
                return;
            }

            if (action is AttackAction attackAction)
                HandleAttackAction(attackAction);
            else if (action is AttackReinforceAction attackReinforceAction)
                HandleAttackReinforceAction(attackReinforceAction);
            else if (action is EndPhaseAction)
            {
                if (_state == AttackState.Attacking)
                    _gm.NextTurnPhase();
                else
                    Debug.LogWarning($"AttackPhase: Cannot end Attack phase while in state {_state}");
            }
            else
                Debug.LogError($"AttackPhase: Received action of type {action.GetType().Name}");
        }

        // ReSharper disable Unity.PerformanceAnalysis
        private void HandleAttackAction(AttackAction attackAction)
        {
            if (_state != AttackState.Attacking)
            {
                Debug.LogWarning($"AttackPhase: Cannot attack while in state {_state}");
                return;
            }

            // 1) Declare. 2) Reaction window for the defender (if it applies). 3) Roll.
            var defender = attackAction.Target.Owner;
            if (defender != null && !_passedForTurn.Contains(defender) && ShouldOpenReactionWindow(attackAction))
            {
                _pendingAttack = attackAction;
                SetState(AttackState.AwaitingDefender);
                _gm.OpenDecision(
                    new PendingDecision(_gm.GetPlayerId(defender), DecisionKind.DefenseReaction,
                        new[] { DecisionOptions.Pass, DecisionOptions.PassForRestOfTurn }),
                    OnDefenderAnswered);
                return;
            }

            ResolveAttack(attackAction);
        }

        private void OnDefenderAnswered(DecisionResponseAction response)
        {
            var attack = _pendingAttack;
            _pendingAttack = null;
            if (attack == null) return;

            if (response.Option == DecisionOptions.PassForRestOfTurn)
                _passedForTurn.Add(response.Player);
            // (Defense cards will be handled here in later steps.)

            SetState(AttackState.Attacking);
            ResolveAttack(attack);
        }

        // Rolls the dice for a declared attack and applies the result.
        private void ResolveAttack(AttackAction attackAction)
        {
            var attackResult = _bs.SimulateAttack(attackAction);

            attackResult.Origin.RemoveTroops(attackResult.AttackerLosses);
            attackResult.Target.RemoveTroops(attackResult.DefenderLosses);
            _attackResults.Add(attackResult);
            _attackTurn++;

            
            if (attackResult.HasAttackerWonTerritory())
            {
                attackResult.Target.SetOwner(attackAction.Player, 0);
                ConqueredTerritoriesCount++;
                SetState(AttackState.Fortifying);
            }
            
           
            // Debug.Log($"AttackPhase: Attacked {attackResult.Target.Name} from {attackResult.Origin.Name}, " +
            //           $"result: {attackResult.AttackerLosses} losses for attacker, " +
            //           $"{attackResult.DefenderLosses} losses for defender");
            

            OnAttacked?.Invoke(attackResult);
            OnAttackTurn?.Invoke();
        }

        private void HandleAttackReinforceAction(AttackReinforceAction attackReinforceAction)
        {
            if (_state != AttackState.Fortifying)
                Debug.LogWarning($"AttackPhase:  Player {attackReinforceAction.Player.Name} Cannot reinforce while in state {_state}");

            var troops = attackReinforceAction.ReinforcingTroops;
            attackReinforceAction.From.RemoveTroops(troops);
            attackReinforceAction.To.AddTroops(troops);
            _attackTurn++;
            
            
            SetState(AttackState.Attacking);

            //add player name to this log:
            
            Debug.Log(
                $"AttackPhase: Player {attackReinforceAction.Player.Name} reinforced {troops} troops from {attackReinforceAction.From.Name} to {attackReinforceAction.To.Name}");
            
            OnReinforced?.Invoke(attackReinforceAction);
            OnAttackTurn?.Invoke();
        }

        public void End(Player player)
        {
            _pendingAttack = null;
        }
        
        private void SetState(AttackState state)
        {
            _state = state;
            OnAttackStateChanged?.Invoke();
        }
        
    }

    public enum AttackState
    {
        Attacking,
        Fortifying,
        AwaitingDefender, // attack declared; defender's reaction window is open
    }
}