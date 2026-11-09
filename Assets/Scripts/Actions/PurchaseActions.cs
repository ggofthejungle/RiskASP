using Expansions;
using Map;
using player;

namespace Actions
{
    // The player has finished hiring and buying. Command cards that say "before your first invasion"
    // are played after this, not before the shop.
    public class CloseShopAction : PlayerAction
    {
        public CloseShopAction(Player player, int turn) : base(player, turn) { }

        public override bool IsValid()
        {
            if (!base.IsValid()) return false;
            var gm = GameManager.Instance;
            if (gm == null || !gm.IsCommandStep(Player))
            {
                LogWarning("Finish hiring and buying on your turn, before your first invasion.");
                return false;
            }
            if (gm.ShopClosed)
            {
                LogWarning("You've already finished hiring and buying.");
                return false;
            }
            return true;
        }
    }

    public class HireCommanderAction : PlayerAction
    {
        public CommanderType Type { get; }
        public Territory Territory { get; }

        public HireCommanderAction(Player player, int turn, CommanderType type, Territory territory) : base(player, turn)
        {
            Type = type;
            Territory = territory;
        }

        public override bool IsValid()
        {
            if (!base.IsValid()) return false;
            var why = GameManager.Instance.Purchases.WhyCantHire(Player, Type, Territory);
            if (why != null) { LogWarning(why); return false; }
            return true;
        }
    }

    public class BuildSpaceStationAction : PlayerAction
    {
        public Territory Territory { get; }

        public BuildSpaceStationAction(Player player, int turn, Territory territory) : base(player, turn)
        {
            Territory = territory;
        }

        public override bool IsValid()
        {
            if (!base.IsValid()) return false;
            var why = GameManager.Instance.Purchases.WhyCantBuild(Player, Territory);
            if (why != null) { LogWarning(why); return false; }
            return true;
        }
    }
}
