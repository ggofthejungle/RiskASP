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

        public FortifyAction(Player player, int turn, Territory from, Territory to, int movedTroops) : base(player, turn)
        {
            From = from;
            To = to;
            MovedTroops = movedTroops;
         
            if (!IsValid())
                throw new ArgumentException("FortifyAction is not valid");
        }

        private bool IsValid()
        {
            var gm = GameManager.Instance;
            if (Player != gm.CurrentPlayer)
            {
                LogError($"Player ({Player.Name}) is not the current player ({gm.CurrentPlayer.Name})");
                return false;
            }
            if (From.Owner != Player)
            {
                LogError($"From territory ({From.Name} owned by {From.Owner.Name}) is not owned by the player ({Player.Name})");
                return false;
            }
            if (To.Owner != Player)
            {
                LogError($"To territory ({To.Name} owned by {To.Owner.Name}) is not owned by the player ({Player.Name})");
                return false;
            }
            if (MovedTroops > From.GetAvailableTroops())
            {
                LogError($".Moved troops ({MovedTroops}) is greater than available troops ({From.GetAvailableTroops()})");
                return false;
            }
            if (!TerritoryRepository.Instance.CanReachTerritory(From, To))
            {
                LogError($"From territory ({From.Name}) cannot reach to territory ({To.Name})");
                return false;
            }
            if (MovedTroops <= 0)
            {
                LogError($"Moved troops ({MovedTroops}) is less than or equal to 0");
                return false;
            }
            
            return true;
        }

        private static void LogError(string message)
        {
            Debug.LogError($"FortifyAction: {message}");
        }
    }
}