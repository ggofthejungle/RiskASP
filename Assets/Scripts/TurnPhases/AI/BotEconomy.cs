using System.Collections.Generic;
using System.Linq;
using Actions;
using Cards;
using Expansions;
using Map;
using player;

// What a bot spends energy on at the start of its turn (MM-2): commanders, cards from its hand, a space
// station, new cards. Plain C#, no solver, so it also runs on mobile. The solver (desktop) still places
// MODs and attacks; this only adds the 2210 economy it doesn't know about.
// The actions are planned against a running energy count and queued, so they reach the game (and, online,
// the other devices) like any other bot move.
public static class BotEconomy
{
    // Kept back for defence cards in other players' turns.
    public const int Reserve = 2;
    public const int MaxBuys = 2;

    static readonly CommanderType[] HireOrder =
        { CommanderType.LandCommander, CommanderType.NuclearCommander, CommanderType.Diplomat };

    public static List<PlayerAction> Plan(GameManager gm, Player p)
    {
        var plan = new List<PlayerAction>();
        if (gm == null || p == null || !gm.IsCommandStep(p)) return plan;
        var energy = p.GetEnergy();
        var turn = gm.Turn;
        var inPlay = new HashSet<CommanderType>(HireOrder.Where(p.HasCommanderInPlay));

        // 1. Get a lost commander back: it unlocks its deck and its d8.
        foreach (var type in HireOrder)
        {
            if (inPlay.Contains(type) || energy - Purchases.CommanderCost < Reserve) continue;
            var where = Safest(p);
            if (where == null || gm.Purchases.WhyCantHire(p, type, where) != null) continue;
            plan.Add(new HireCommanderAction(p, turn, type, where));
            energy -= Purchases.CommanderCost;
            inPlay.Add(type);
        }

        // 2. Cards already in hand.
        foreach (var card in p.Cards.ToList())
        {
            if (card.IsHidden || card.Timing != CardTiming.BeforeFirstInvasion) continue;
            if (!inPlay.Contains(card.Commander) || energy - card.PowerUpCost < 0) continue;
            var targets = TargetsFor(gm, p, card);
            if (targets == null) continue;
            if (gm.CommandCards.WhyCantPlay(p, card, targets) != null) continue;
            plan.Add(new PlayCommandCardAction(p, turn, card, targets));
            energy -= card.PowerUpCost;
        }

        // 3. A space station: +1 MOD every turn for the rest of the game, worth it early on.
        if (gm.Year < GameManager.MaxYears && energy - Purchases.StationCost >= Reserve + 1
            && Purchases.StationsOf(p) < Purchases.MaxStations)
        {
            var where = p.Territories.Where(t => !t.containsSpaceStation)
                .OrderBy(EnemyNeighbours).ThenByDescending(t => t.Troops).FirstOrDefault();
            if (where != null && gm.Purchases.WhyCantBuild(p, where) == null)
            {
                plan.Add(new BuildSpaceStationAction(p, turn, where));
                energy -= Purchases.StationCost;
            }
        }

        // 4. New cards, spread over the decks it can buy from.
        var decks = HireOrder.Where(d => inPlay.Contains(d) && CommandCards.IsDeckInPlay(d)).ToList();
        var bought = gm.CommandCards.BuysThisTurn;
        for (var i = 0; decks.Count > 0 && bought < System.Math.Min(MaxBuys, CommandCards.MaxBuysPerTurn)
                        && energy - CommandCards.BuyCost >= Reserve; i++)
        {
            var deck = decks[i % decks.Count];
            if (gm.CommandCards.CardsLeftInDeck(deck) == 0)
            {
                decks.Remove(deck);
                continue;
            }
            plan.Add(new BuyCommandCardAction(p, turn, deck));
            energy -= CommandCards.BuyCost;
            bought++;
        }
        return plan;
    }

    // The card's arguments, or null to keep it for later. Cards that stop the game to ask other players
    // (MOD Reduction, Armageddon) are left out: the solver's queued moves would be refused meanwhile.
    static object[] TargetsFor(GameManager gm, Player p, Card card)
    {
        switch (card.CardType)
        {
            case CardType.ModReduction:
            case CardType.Armageddon:
            case CardType.DecoyRevealed:
                return null;
            case CardType.AssembleModsL:
                return One(Border(p).FirstOrDefault());
            case CardType.ReinforcementsL:
                var three = Border(p).Concat(p.Territories).Distinct().Take(3).Cast<object>().ToArray();
                return three.Length == 3 ? three : null;
            case CardType.TerritorialStation:
                return One(p.Territories.Where(t => !t.containsSpaceStation).OrderBy(EnemyNeighbours).FirstOrDefault());
            case CardType.FrequencyJamL:
                var leader = gm.Players.Where(o => o != p && !o.IsDead()).OrderByDescending(o => o.Territories.Count())
                    .FirstOrDefault();
                return leader != null ? new object[] { leader } : null;
            case CardType.RocketStrikeLand:
                return One(p.Territories.SelectMany(t => t.NeighbourTerritories)
                    .Where(t => t.Owner != null && t.Owner != p).OrderByDescending(t => t.Troops).FirstOrDefault());
            case CardType.AssassinBomb:
                var mark = TerritoryRepository.Instance.Territories
                    .Where(t => t.Owner != null && t.Owner != p && t.CommandersList.Count > 0)
                    .OrderByDescending(t => t.CommandersList.Any(c => c.CommanderType == CommanderType.NuclearCommander))
                    .FirstOrDefault();
                return mark != null ? new object[] { mark, CommandCards.AssassinTarget(mark) } : null;
            default:
                return card.Spec.CheckTargets == null ? new object[0] : null;
        }
    }

    static object[] One(Territory t) => t != null ? new object[] { t } : null;

    static int EnemyNeighbours(Territory t) => t.NeighbourTerritories.Count(n => n.Owner != null && n.Owner != t.Owner);

    // Own territories facing enemies, most threatened (and weakest) first.
    static IEnumerable<Territory> Border(Player p) =>
        p.Territories.Where(t => EnemyNeighbours(t) > 0).OrderByDescending(EnemyNeighbours).ThenBy(t => t.Troops);

    // Where a new commander is least likely to die: the biggest stack with the fewest enemies next to it.
    static Territory Safest(Player p) =>
        p.Territories.OrderBy(EnemyNeighbours).ThenByDescending(t => t.Troops).FirstOrDefault();
}
