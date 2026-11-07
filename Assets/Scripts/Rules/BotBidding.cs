namespace InvadeEarth.Rules
{
    // A bot's yearly bid. A high bid only buys an earlier pick of seats, and energy also pays for
    // cards and commanders, so bots keep a reserve and spend about a third of what is left.
    // The last year matters most (no later turns to make up for a bad seat), so they push harder then.
    public static class BotBidding
    {
        public const int Reserve = 3;

        public static int Choose(int energy, int year, int maxYears, IRandom random)
        {
            if (energy <= 0) return 0;
            var spare = energy - Reserve;
            if (spare <= 0) return 0;
            var bid = spare / 3 + random.Range(0, 2);
            if (year >= maxYears) bid = spare / 2 + random.Range(0, 2);
            if (bid > spare) bid = spare;
            return bid < 0 ? 0 : bid;
        }
    }
}
