using System.Collections.Generic;
using System.Linq;
using Cards;
using InvadeEarth.Rules;
using Map;
using player;

namespace Expansions
{
    // End of game (after year 5), Risk 2210: 1 point per territory, plus continent bonuses, plus 3 for each
    // Colony Influence card held whose commander is still alive. Ties: most energy, then most units.
    // If only one player is left alive, they win outright.
    public static class Scoring
    {
        public const int ColonyInfluencePoints = ScoringRules.ColonyInfluencePoints;

        public static int ScoreOf(Player p) => ScoringRules.Score(LineFor(p));

        // Players from first to last place.
        public static List<Player> Ranking(IEnumerable<Player> players)
        {
            var list = players.ToList();
            var lines = list.Select(LineFor).ToArray();
            return ScoringRules.Rank(lines).Select(i => list[i]).ToList();
        }

        static ScoreLine LineFor(Player p)
        {
            var continents = ContinentRepository.Instance != null
                ? ContinentRepository.Instance.Continents.Values
                : Enumerable.Empty<Continent>();
            return new ScoreLine
            {
                Alive = !p.IsDead(),
                Territories = p.GetTerritoryCount(),
                ContinentBonus = continents.Where(c => c.IsOwnedByPlayer(p)).Sum(c => c.BonusTroops),
                ColonyCards = p.Cards.Count(c => c.Timing == CardTiming.EndOfGame && p.HasCommanderInPlay(c.Commander)),
                Energy = p.GetEnergy(),
                Troops = p.GetTroopsCountInTerritories(),
            };
        }
    }
}
