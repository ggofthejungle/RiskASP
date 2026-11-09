using System;
using InvadeEarth.Rules;
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

        // Contested capture: move in at least as many units as the dice you rolled. The commanders who
        // rolled a d8 have already moved. An empty territory has no roll, so one unit is enough.
        public static int MinToMove(AttackAction attack, bool contested = true)
        {
            var forced = 0;
            foreach (var type in attack.Commanders)
                if (BattleSimulator.AttacksWithD8(type, attack.Origin, attack.Target)) forced++;
            var quota = contested ? attack.Dice : 1;
            var still = System.Math.Max(0, quota - forced);
            var room = attack.Origin.GetAvailableTroops();
            return System.Math.Min(still, System.Math.Max(0, room));
        }

        // MODs that can leave while `movingCommanders` also leave. One unit stays. A commander counts.
        public static int MaxMods(Territory from, int movingCommanders)
        {
            if (from == null) return 0;
            var commanders = from.CommandersList != null ? from.CommandersList.Count : 0;
            var spare = from.Troops + commanders - 1 - System.Math.Max(0, movingCommanders);
            if (spare < 0) spare = 0;
            return System.Math.Min(from.Troops, spare);
        }

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

            var leaving = ReinforcingTroops + MovingCommanders.Count;
            var stillThere = From.Troops + From.CommandersList.Count;
            if (ReinforcingTroops > From.Troops || leaving > stillThere - 1)
            {
                Reject($"Leave 1 unit in {From.Name.Replace('_', ' ')}. A commander counts. You can move {System.Math.Max(0, stillThere - 1)} of {stillThere}.");
                return false;
            }

            var last = gm.AttackPhase.LastAttackResult;
            var minUnits = MinToMove(AttackAction, last != null && last.DefendingTroops > 0);
            foreach (var type in MovingCommanders)
                if (!System.Linq.Enumerable.Any(From.CommandersList, c => c.CommanderType == type))
                {
                    LogWarning($"There is no {type} in {From.Name} to move");
                    return false;
                }
                else if (!Expansions.Purchases.CanStand(type, To))
                {
                    Reject($"A {type} can't enter {To.Name.Replace('_', ' ')}.");
                    return false;
                }
            if (ReinforcingTroops + MovingCommanders.Count < minUnits)
            {
                Reject($"Move at least {minUnits} into {To.Name.Replace('_', ' ')}. MODs and commanders both count.");
                return false;
            }

            return base.IsValid();
        }
    }
}