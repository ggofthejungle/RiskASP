using System;
using Map;
using player;
using UnityEngine;

namespace Actions
{
    public class FortifyAction : PlayerAction
    {
        public Territory From { get; }
        public Territory To { get; }
        public int MovedTroops { get; }

        // Commanders moving along with the units (optional).
        public System.Collections.Generic.IReadOnlyList<Expansions.CommanderType> MovingCommanders { get; }

        public FortifyAction(Player player, int turn, Territory from, Territory to, int movedTroops,
            System.Collections.Generic.IEnumerable<Expansions.CommanderType> movingCommanders = null) : base(player, turn)
        {
            From = from;
            To = to;
            MovedTroops = movedTroops;
            MovingCommanders = System.Linq.Enumerable.ToList(movingCommanders ?? System.Array.Empty<Expansions.CommanderType>());
        }

        // Moves the listed commanders (one of each listed type) between two territories.
        public static void MoveCommanders(Territory from, Territory to, System.Collections.Generic.IEnumerable<Expansions.CommanderType> types)
        {
            foreach (var type in types)
            {
                var commander = System.Linq.Enumerable.FirstOrDefault(from.CommandersList, c => c.CommanderType == type);
                if (commander == null) continue;
                from.RemoveCommander(commander);
                to.AddCommander(commander);
            }
        }

        public override bool IsValid()
        {
            var gm = GameManager.Instance;
            if (gm.CurrentPhase != gm.FortifyPhase)
            {
                LogError($"Current phase ({gm.CurrentPhase.Name}) is not FortifyPhase ({gm.FortifyPhase.Name})");
                return false;
            }

            if (From.Owner != Player)
            {
                LogError(
                    $"From territory ({From.Name} owned by {From.Owner.Name}) is not owned by the player ({Player.Name})");
                return false;
            }

            if (To.Owner != Player)
            {
                LogError(
                    $"To territory ({To.Name} owned by {To.Owner.Name}) is not owned by the player ({Player.Name})");
                return false;
            }

            if (MovedTroops > From.GetAvailableTroops())
            {
                LogError(
                    $".Moved troops ({MovedTroops}) is greater than available troops ({From.GetAvailableTroops()})");
                return false;
            }

            if (!TerritoryRepository.Instance.CanFortifyTerritory(From, To))
            {
                LogError($"From territory ({From.Name}) cannot reach to territory ({To.Name})");
                return false;
            }

            foreach (var type in MovingCommanders)
                if (!System.Linq.Enumerable.Any(From.CommandersList, c => c.CommanderType == type))
                {
                    LogError($"There is no {type} in {From.Name} to move");
                    return false;
                }

            if (MovedTroops <= 0)
            {
                LogError($"Moved troops ({MovedTroops}) is less than or equal to 0");
                return false;
            }

            return base.IsValid();
        }
    }
}