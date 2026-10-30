using Cards;
using Expansions;
using player;

namespace Actions
{
    // Buy one random card from a commander's deck (1 energy).
    public class BuyCommandCardAction : PlayerAction
    {
        public CommanderType Deck { get; }

        public BuyCommandCardAction(Player player, int turn, CommanderType deck) : base(player, turn)
        {
            Deck = deck;
        }

        public override bool IsValid()
        {
            if (!base.IsValid()) return false;
            var why = GameManager.Instance.CommandCards.WhyCantBuy(Player, Deck);
            if (why != null)
            {
                LogWarning(why);
                return false;
            }
            return true;
        }

        public override string ToString() => $"BuyCommandCard({Player?.Name}: {Deck})";
    }

    // Play a command card from your hand. Targets are the card's own arguments (the owner is added automatically
    // for cards like Energy Crisis that act on behalf of the player).
    public class PlayCommandCardAction : PlayerAction
    {
        public Card Card { get; }
        public object[] Targets { get; }

        public PlayCommandCardAction(Player player, int turn, Card card, params object[] targets) : base(player, turn)
        {
            Card = card;
            Targets = targets ?? new object[0];
        }

        public override bool IsValid()
        {
            if (!base.IsValid()) return false;
            var why = GameManager.Instance.CommandCards.WhyCantPlay(Player, Card, Targets);
            if (why != null)
            {
                LogWarning(why);
                return false;
            }
            return true;
        }

        public override string ToString() => $"PlayCommandCard({Player?.Name}: {Card?.Name})";
    }
}
