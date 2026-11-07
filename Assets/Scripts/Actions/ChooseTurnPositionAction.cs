using player;

namespace Actions
{
    // The highest bidder, then the next, picks a free seat for this year. Position is 1-based.
    public class ChooseTurnPositionAction : PlayerAction
    {
        public int Position { get; }

        public ChooseTurnPositionAction(Player player, int turn, int position) : base(player, turn)
        {
            Position = position;
        }

        public override bool IsValid()
        {
            var gm = GameManager.Instance;
            if (!gm.IsChoosingOrder) return false;
            if (!gm.AvailableTurnPositions.Contains(Position)) return false;
            return base.IsValid();
        }
    }
}
