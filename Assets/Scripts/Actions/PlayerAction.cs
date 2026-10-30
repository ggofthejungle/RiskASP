using player;

namespace Actions
{
    public abstract class PlayerAction
    {
        public Player Player { get; }
        public int Turn { get; }

        public PlayerAction(Player player, int turn)
        {
            Player = player;
            Turn = turn;
        }


        public virtual bool IsValid()
        {
            var gm  = GameManager.Instance;
            // The input owner is the turn owner, unless a PendingDecision is asking someone else.
            var inputPlayer = gm.InputPlayer;
            if(Player != inputPlayer)
            {
                LogError($"Player ({Player?.Name}) is not the input player ({inputPlayer?.Name})");
                return false;
            }
            
            if (gm.Turn != Turn)
            {
                LogError($"Turn ({Turn}) is not the current turn ({gm.Turn})");
                return false;
            }
            
            return true;
        }
        
        protected void LogWarning(string message)
        {
            UnityEngine.Debug.LogWarning(GetType().Name + " REJECTED: " + message);
        }

        protected void LogError(string message)
        {
            string name = GetType().Name;
            UnityEngine.Debug.LogError(name + " ERROR: " + message);
        }
    }
}


