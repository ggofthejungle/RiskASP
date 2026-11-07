using player;

namespace Actions
{
    public class BidAction : PlayerAction
    {
        public int EnergyThePlayerBid { get; }

        public BidAction(Player player, int turn, int energyThePlayerBid) : base(player, turn)
        {
            EnergyThePlayerBid = energyThePlayerBid;
        }

        public override bool IsValid()
        {
            var gm = GameManager.Instance;
            if (!gm.IsBidding) return false;
            if (EnergyThePlayerBid < 0 || EnergyThePlayerBid > Player.GetEnergy()) return false;
            return base.IsValid();
        }
    }
}
