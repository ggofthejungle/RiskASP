using System.Linq;

namespace InvadeEarth.Rules
{
    // End-of-game score from plain numbers. The Unity side counts territories, continent
    // bonuses and colony cards; this is the arithmetic a server can run.
    public struct ScoreLine
    {
        public bool Alive;
        public int Territories;
        public int ContinentBonus;
        public int ColonyCards;
        public int Energy;
        public int Troops;
    }

    public static class ScoringRules
    {
        public const int ColonyInfluencePoints = 3;

        public static int Score(ScoreLine line) =>
            line.Alive
                ? line.Territories + line.ContinentBonus + line.ColonyCards * ColonyInfluencePoints
                : 0;

        // Indexes of `lines`, first place first.
        // One player left alive wins outright. Otherwise: score, then energy, then units.
        public static int[] Rank(ScoreLine[] lines)
        {
            var alive = Enumerable.Range(0, lines.Length).Where(i => lines[i].Alive).ToArray();
            if (alive.Length == 1)
                return alive.Concat(Enumerable.Range(0, lines.Length).Where(i => !lines[i].Alive)).ToArray();

            return Enumerable.Range(0, lines.Length)
                .OrderByDescending(i => Score(lines[i]))
                .ThenByDescending(i => lines[i].Energy)
                .ThenByDescending(i => lines[i].Troops)
                .ToArray();
        }
    }
}
