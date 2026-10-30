using player;

namespace Actions
{
    // A player's answer to the GameManager's current PendingDecision.
    public class DecisionResponseAction : PlayerAction
    {
        public string Option { get; }

        public DecisionResponseAction(Player player, int turn, string option) : base(player, turn)
        {
            Option = option;
        }

        public override bool IsValid()
        {
            var gm = GameManager.Instance;
            var decision = gm.PendingDecision;
            if (decision == null)
            {
                LogWarning("There is no pending decision to answer");
                return false;
            }

            if (!decision.HasOption(Option))
            {
                LogWarning($"Option '{Option}' is not one of [{string.Join(", ", decision.Options)}]");
                return false;
            }

            return base.IsValid(); // checks Player is the input owner (the one being asked)
        }

        public override string ToString() => $"DecisionResponse({Player?.Name}: {Option})";
    }
}
