using Expansions;
using Map;
using player;

namespace Actions
{
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
