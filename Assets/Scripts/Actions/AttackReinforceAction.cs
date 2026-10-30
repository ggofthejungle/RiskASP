using System;
using Map;
using player;
using TurnPhases;

namespace Actions
{
    public class AttackReinforceAction : AttackPhaseAction
    {
        private AttackAction AttackAction { get; }
        public int ReinforcingTroops { get; }
        public Territory From => AttackAction.Origin;
        public Territory To => AttackAction.Target;
        public int AttackingTroops => AttackAction.Troops;

        // Commanders that stayed in the attacking territory and now move in too (attacking commanders already moved).
        public System.Collections.Generic.IReadOnlyList<Expansions.CommanderType> MovingCommanders { get; }

        public AttackReinforceAction(Player player, int turn, int attackTurn, AttackAction attackAction,
            int reinforcingTroops, System.Collections.Generic.IEnumerable<Expansions.CommanderType> movingCommanders = null)
            : base(player, turn, attackTurn)
        {
            AttackAction = attackAction;
            ReinforcingTroops = reinforcingTroops;
            MovingCommanders = System.Linq.Enumerable.ToList(movingCommanders ?? System.Array.Empty<Expansions.CommanderType>());
        }

        public static int MinToMove(AttackAction attack) =>
            Math.Min(Math.Max(1, Math.Min(attack.Troops, 3)), attack.Origin.GetAvailableTroops());

        public override bool IsValid()
        {
            var gm = GameManager.Instance;
            if(gm.AttackPhase.State != AttackState.Fortifying)
            {
                LogError("AttackPhase is not in Fortifying state");
                return false;
            }
            
            if (Player != AttackAction.Player)
            {
                LogError(
                    $"Player ({Player.Name}) is not the same as the player in the attack action ({AttackAction.Player.Name})");
                return false;
            }

            if (ReinforcingTroops > From.GetAvailableTroops())
            {
                LogError(
                    $"Reinforcing troops ({ReinforcingTroops}) is greater than available troops ({From.GetAvailableTroops()})");
                return false;
            }

            var minReinforcing = MinToMove(AttackAction);
            foreach (var type in MovingCommanders)
                if (!System.Linq.Enumerable.Any(From.CommandersList, c => c.CommanderType == type))
                {
                    LogWarning($"There is no {type} in {From.Name} to move");
                    return false;
                }
            if (ReinforcingTroops < minReinforcing)
            {
                LogError($"Reinforcing troops ({ReinforcingTroops}) is less than the minimum ({minReinforcing})");
                return false;
            }

            return base.IsValid();
        }
    }
}