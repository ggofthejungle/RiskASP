using Actions;
using player;
using UnityEngine;

namespace TurnPhases
{
    public class BiddingPhase: IPhase
    {
        public string Name => "Bidding";
        
        private readonly GameManager _gm;
        
        public BiddingPhase(GameManager gm)
        {
            _gm = gm;
        }
        public void Start(Player player)
        {
            
        }

        public void OnAction(Player player, PlayerAction action)
        {
            if (action is BidAction bidAction)
            {
                //bidAction.Player.Bid(bidAction.energy);
                //if all players have bid, go to next phase

            }
            else if (action is EndPhaseAction)
            {
                _gm.NextTurnPhase();
            }
            else
                Debug.LogError($"ReinforcePhase: Received action of type {action.GetType().Name}");
        }

        public void End(Player player)
        {
            throw new System.NotImplementedException();
        }
    }
}