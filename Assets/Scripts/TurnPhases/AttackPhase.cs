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

        // Reaction window (defender interrupt) before each dice roll: opens only when the DEFENDER holds a
        // defense card they can play right now (decision: we don't ask other players). Tests may override it.
        public Func<AttackAction, bool> ShouldOpenReactionWindow;

        // Defenders protected by Cease Fire for the rest of this turn.
        private readonly HashSet<Player> _ceaseFire = new();
        public bool IsCeaseFireAgainst(Player defender) => defender != null && _ceaseFire.Contains(defender);

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
            ShouldOpenReactionWindow = DefenderCanReact;
        }

        private bool DefenderCanReact(AttackAction attack) =>
            _gm.CommandCards != null && _gm.CommandCards.PlayableReactions(attack.Target.Owner, attack).Any();


        public void Start(Player player)
        {
            _attackResults.Clear();
            _passedForTurn.Clear();
            _ceaseFire.Clear();
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
                AskDefender(attackAction);
                return;
            }

            ResolveAttack(attackAction);
        }

        private void AskDefender(AttackAction attack)
        {
            var defender = attack.Target.Owner;
            var options = new List<string>();
            options.AddRange(_gm.CommandCards.PlayableReactions(defender, attack).Select(Cards.CommandCards.OptionFor));
            options.Add(DecisionOptions.Pass);
            options.Add(DecisionOptions.PassForRestOfTurn);
            _gm.OpenDecision(new PendingDecision(_gm.GetPlayerId(defender), DecisionKind.DefenseReaction, options),
                OnDefenderAnswered);
        }

        private void OnDefenderAnswered(DecisionResponseAction response)
        {
            var attack = _pendingAttack;
            _pendingAttack = null;
            if (attack == null) return;

            var defender = response.Player;
            var card = _gm.CommandCards.CardFromOption(defender, response.Option);
            if (card != null)
            {
                var cancelled = _gm.CommandCards.PlayReaction(defender, card, attack);
                if (cancelled)
                {
                    _ceaseFire.Add(defender);
                    Debug.Log($"Cease Fire: {attack.Player.Name} can't attack {defender.Name} for the rest of this turn.");
                    SetState(AttackState.Attacking);
                    OnAttackTurn?.Invoke();
                    return;
                }
                // More than one card per invasion is allowed: ask again while the defender can still react.
                if (attack.Target.Owner == defender && attack.Target.Troops > 0
                    && _gm.CommandCards.PlayableReactions(defender, attack).Any())
                {
                    _pendingAttack = attack;
                    AskDefender(attack);
                    return;
                }
            }
            else if (response.Option == DecisionOptions.PassForRestOfTurn)
                _passedForTurn.Add(defender);

            SetState(AttackState.Attacking);

            // Cards may have changed things (Death Trap): re-check the attack before rolling.
            var troops = Math.Min(attack.Troops, attack.Origin.GetAvailableTroops());
            if (troops + attack.Commanders.Count < 1 || attack.Origin.GetAvailableTroops() < 1 || attack.Origin.Owner != attack.Player)
            {
                Debug.Log($"AttackPhase: {attack.Player.Name}'s invasion of {attack.Target.Name} can't go ahead any more.");
                OnAttackTurn?.Invoke();
                return;
            }
            if (troops != attack.Troops)
                attack = new AttackAction(attack.Player, attack.Turn, attack.AttackTurn, attack.Origin, attack.Target, troops, attack.Commanders);
            ResolveAttack(attack);
        }

        // Rolls the dice for a declared attack and applies the result.
        private void ResolveAttack(AttackAction attackAction)
        {
            // Evacuated (empty) territory: no battle, the attacker simply takes it.
            var attackResult = attackAction.Target.Troops == 0
                ? new AttackResult(attackAction, 0, 0, attackAction.Origin.Troops, 0, 0, new int[0], new int[0])
                : _bs.SimulateAttack(attackAction);

            attackResult.Origin.RemoveTroops(attackResult.AttackerLosses - attackResult.AttackerCommanderLosses);
            attackResult.Target.RemoveTroops(attackResult.DefenderLosses);
            // Attacking commanders die only if no attacking MOD was left to take the hit (e.g. a commander alone).
            foreach (var type in attackAction.Commanders.Reverse().Take(attackResult.AttackerCommanderLosses))
            {
                var dead = attackResult.Origin.CommandersList.FirstOrDefault(c => c.CommanderType == type);
                if (dead == null) continue;
                attackResult.Origin.RemoveCommander(dead);
                _gm.ReportCommanderKilled(attackAction.Player, type, attackResult.Origin, $"attacking {attackResult.Target.Name}");
            }
            _attackResults.Add(attackResult);
            _attackTurn++;

            
            if (attackResult.HasAttackerWonTerritory())
            {
                Purchases.OnTerritoryConquered(attackResult.Target, attackAction.Player);
                attackResult.Target.SetOwner(attackAction.Player, 0);
                // Commanders that attacked move in with the army (2210 FAQ).
                FortifyAction.MoveCommanders(attackResult.Origin, attackResult.Target, attackAction.Commanders);
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
            FortifyAction.MoveCommanders(attackReinforceAction.From, attackReinforceAction.To, attackReinforceAction.MovingCommanders);
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