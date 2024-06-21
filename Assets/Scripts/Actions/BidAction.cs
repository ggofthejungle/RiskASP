using player;
using Map;
using UnityEngine;

namespace Actions
{
    public class BidAction : PlayerAction
    {
        public int EnergyThePlayerBid{ get; }
        
        public BidAction(Player player, int turn, int energyThePlayerBid) : base(player, turn)
        {
            //need input from the user. Look at how ReinforceAction is called in the GameManager and do the same here
            EnergyThePlayerBid = energyThePlayerBid;
        }
        
        public override bool IsValid()
        {
            var gm = GameManager.Instance;
            if (gm.CurrentPhase != gm.BiddingPhase)
            {
                LogError($"Current phase ({gm.CurrentPhase.Name}) is not BiddingPhase ({gm.BiddingPhase.Name})");
                return false;
            }
            
            var EnergyThePlayerHas = Player.GetEnergy();
            if (EnergyThePlayerBid > EnergyThePlayerHas)
            {
                LogError(
                    $"BiddingPhase: Player ({Player.Name}) tried to bid more energyThePlayerBid ({EnergyThePlayerBid}) than they have left ({EnergyThePlayerHas})");
                return false;
            }
            return base.IsValid();
        }
    }
}