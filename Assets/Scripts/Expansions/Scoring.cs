using System.Collections.Generic;
using System.Linq;
using Cards;
using Map;
using player;

namespace Expansions
{
    // End of game (after year 5), Risk 2210: 1 point per territory, plus continent bonuses, plus 3 for each
    // Colony Influence card held whose commander is still alive. Ties: most energy, then most units.
    // If only one player is left alive, they win outright.
    public static class Scoring
    {
        public const int ColonyInfluencePoints = 3;

        public static int ScoreOf(Player p)
        {
            if (p.IsDead()) return 0;
            var score = p.GetTerritoryCount();
            var continents = ContinentRepository.Instance != null
                ? ContinentRepository.Instance.Continents.Values
                : Enumerable.Empty<Continent>();
            score += continents.Where(c => c.IsOwnedByPlayer(p)).Sum(c => c.BonusTroops);
            score += p.Cards.Count(c => c.Timing == CardTiming.EndOfGame && p.HasCommanderInPlay(c.Commander))
                     * ColonyInfluencePoints;
            return score;
        }

        // Players from first to last place.
        public static List<Player> Ranking(IEnumerable<Player> players)
        {
            var list = players.ToList();
            var alive = list.Where(p => !p.IsDead()).ToList();
            if (alive.Count == 1)
                return alive.Concat(list.Where(p => p.IsDead())).ToList();
            return list
                .OrderByDescending(ScoreOf)
                .ThenByDescending(p => p.GetEnergy())
                .ThenByDescending(p => p.GetTroopsCountInTerritories())
                .ToList();
        }
    }
}
