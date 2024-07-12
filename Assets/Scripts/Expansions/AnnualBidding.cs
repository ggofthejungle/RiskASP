using Actions;
using player;
using TurnPhases;
using UnityEngine;

namespace Expansions
{
    public class AnnualBidding: IPhase
    {
        public string Name => "Bidding";
        
        private readonly GameManager _gm;
        
        public AnnualBidding(GameManager gm)
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
                Debug.LogError($"AnnualBidding: Received action of type {action.GetType().Name}");
        }

        public void End(Player player)
        {
            throw new System.NotImplementedException();
        }
    }
}