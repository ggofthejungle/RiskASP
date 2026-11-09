using System.Collections.Generic;
using System.Linq;
using Actions;
using Cards;
using Expansions;
using Map;
using player;

// What a bot spends energy on at the start of its turn (MM-2): commanders, cards from its hand, a space
// station, new cards. Plain C#, no solver, so it also runs on mobile. MODs and attacks come after it,
// from HeuristicBrain / BotTactics or the desktop solver.
// The actions are planned against a running energy count and queued, so they reach the game (and, online,
// the other devices) like any other bot move.
public static class BotEconomy
{
    // Kept back for defence cards in other players' turns.
    public const int Reserve = 2;
    public const int MaxBuys = 2;

    static readonly CommanderType[] HireOrder =
    {
        CommanderType.LandCommander, CommanderType.NuclearCommander, CommanderType.Diplomat,
        CommanderType.NavalCommander, CommanderType.SpaceCommander,
    };

    public static List<PlayerAction> Plan(GameManager gm, Player p)
    {
        var plan = new List<PlayerAction>();
        if (gm == null || p == null || !gm.IsCommandStep(p)) return plan;
        var cards = new List<PlayerAction>();
        gm.AllowCardsBeforeShop = true;
        var energy = p.GetEnergy();
        var turn = gm.Turn;
        var inPlay = new HashSet<CommanderType>(HireOrder.Where(p.HasCommanderInPlay));

        // 1. Get a lost commander back: it unlocks its deck and its d8.
        foreach (var type in HireOrder)
        {
            if (inPlay.Contains(type) || energy - Purchases.CommanderCost < Reserve) continue;
            var where = p.Territories.Where(t => Purchases.CanStand(type, t))
                .OrderBy(EnemyNeighbours).ThenByDescending(t => t.Troops).FirstOrDefault();
            if (where == null || gm.Purchases.WhyCantHire(p, type, where) != null) continue;
            plan.Add(new HireCommanderAction(p, turn, type, where));
            energy -= Purchases.CommanderCost;
            inPlay.Add(type);
        }

        // 2. Cards already in hand. Checked against the board as it is now, so two Assassin Bombs in one
        // plan must not aim at the same commander (the first one kills it).
        var aimed = new HashSet<object>();
        PlayCommandCardAction stopper = null;
        // Armageddon first: its energy is set aside, and the other nuclear cards wait to be played for free.
        var armageddon = p.Cards.FirstOrDefault(c => c.CardType == CardType.Armageddon && !c.IsHidden
            && inPlay.Contains(c.Commander) && energy >= c.PowerUpCost && WorthStopping(gm, p, c)
            && gm.CommandCards.WhyCantPlay(p, c, new object[0]) == null);
        if (armageddon != null)
        {
            stopper = new PlayCommandCardAction(p, turn, armageddon, new object[0]);
            energy -= armageddon.PowerUpCost;
        }
        foreach (var card in p.Cards.ToList())
        {
            if (card.IsHidden || card.Timing != CardTiming.BeforeFirstInvasion) continue;
            if (armageddon != null && card.Commander == CommanderType.NuclearCommander) continue;
            if (!inPlay.Contains(card.Commander) || energy - card.PowerUpCost < 0) continue;
            if (StopsTheGame(card.CardType))
            {
                // At most one per turn: the bot waits for everyone's answers before it places its MODs.
                if (stopper != null || !WorthStopping(gm, p, card)) continue;
                if (gm.CommandCards.WhyCantPlay(p, card, new object[0]) != null) continue;
                stopper = new PlayCommandCardAction(p, turn, card, new object[0]);
                energy -= card.PowerUpCost;
                continue;
            }
            var targets = TargetsFor(gm, p, card);
            if (targets == null) continue;
            if (card.CardType == CardType.AssassinBomb && !aimed.Add(targets[0])) continue;
            if (gm.CommandCards.WhyCantPlay(p, card, targets) != null) continue;
            cards.Add(new PlayCommandCardAction(p, turn, card, targets));
            energy -= card.PowerUpCost;
        }

        // 3. A space station: +1 MOD every turn for the rest of the game, worth it early on.
        if (gm.Year < GameManager.MaxYears && energy - Purchases.StationCost >= Reserve + 1
            && Purchases.StationsOf(p) < Purchases.MaxStations)
        {
            var where = p.Territories.Where(t => !t.containsSpaceStation && t.Kind == TerrainKind.Land)
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

        // 5. Cards come after hiring and buying. Close the shop, then play them.
        gm.AllowCardsBeforeShop = false;
        if (cards.Count > 0 || stopper != null)
            plan.Add(new CloseShopAction(p, turn));
        plan.AddRange(cards);
        // Last, so nothing of the bot's is still queued while other players answer.
        if (stopper != null) plan.Add(stopper);
        return plan;
    }

    // Cards that ask other players questions before the turn can go on.
    public static bool StopsTheGame(CardType type) => type is CardType.ModReduction or CardType.Armageddon;

    public static bool StopsTheGame(PlayerAction action) =>
        action is PlayCommandCardAction play && play.Card != null && StopsTheGame(play.Card.CardType);

    static bool WorthStopping(GameManager gm, Player p, Card card)
    {
        switch (card.CardType)
        {
            case CardType.ModReduction:
                // Each opponent loses 4 MODs, the bot 2.
                return gm.Players.Any(o => o != p && !o.IsDead() && o.Territories.Any(t => t.Troops > 1));
            case CardType.Armageddon:
                // Everyone gets free nuclear cards, so only when the bot has at least two of its own to fire.
                return p.Cards.Count(c => c != card && !c.IsHidden && gm.CommandCards.WhyCantPlayFree(p, c) == null) >= 2;
            default:
                return false;
        }
    }

    // Decoys Revealed: commanders standing where the enemy next door outnumbers the units go to the safest
    // territory. Null when nobody is in danger (keep the card).
    static object[] DecoyTargets(Player p)
    {
        var safe = Safest(p);
        if (safe == null) return null;
        var moves = new List<object>();
        foreach (var t in p.Territories)
        {
            if (t == safe || t.CommandersList.Count == 0) continue;
            var threat = t.NeighbourTerritories.Where(n => n.Owner != null && n.Owner != p).Select(n => n.Troops)
                .DefaultIfEmpty(0).Max();
            if (threat <= t.Troops) continue;
            foreach (var c in t.CommandersList)
                moves.AddRange(new object[] { t, c.CommanderType, safe });
        }
        return moves.Count > 0 ? moves.ToArray() : null;
    }

    // The card's arguments, or null to keep it for later.
    static object[] TargetsFor(GameManager gm, Player p, Card card)
    {
        switch (card.CardType)
        {
            case CardType.DecoyRevealed:
                return DecoyTargets(p);
            case CardType.AssembleModsL:
            case CardType.AssembleModsNa:
            case CardType.AssembleModsS:
                return One(OfKind(Border(p).Concat(p.Territories), card).FirstOrDefault());
            case CardType.ReinforcementsL:
            case CardType.ReinforcementsNa:
            case CardType.ReinforcementsS:
                var three = OfKind(Border(p).Concat(p.Territories), card).Distinct().Take(3).Cast<object>().ToArray();
                return three.Length > 0 ? three : null;
            case CardType.TerritorialStation:
                return One(p.Territories.Where(t => !t.containsSpaceStation).OrderBy(EnemyNeighbours).FirstOrDefault());
            case CardType.FrequencyJamL:
            case CardType.FrequencyJamNa:
            case CardType.FrequencyJamS:
                var leader = gm.Players.Where(o => o != p && !o.IsDead()).OrderByDescending(o => o.Territories.Count())
                    .FirstOrDefault();
                return leader != null ? new object[] { leader } : null;
            case CardType.RocketStrikeLand:
            case CardType.RocketStrikeWater:
            case CardType.RocketStrikeMoon:
                return One(OfKind(TerritoryRepository.Instance.Territories.Where(t => t.Owner != null && t.Owner != p), card)
                    .OrderByDescending(t => t.Troops).FirstOrDefault());
            case CardType.AquaBrother:
                return TerritoryRepository.Instance.Territories.Any(t => t.Kind == TerrainKind.Sea && t.Owner != p && t.Troops > 1)
                    ? new object[0] : null;
            case CardType.NickyBoy:
                return TerritoryRepository.Instance.Territories.Any(t => t.Kind == TerrainKind.Moon && t.Owner != p && t.Troops > 1)
                    ? new object[0] : null;
            case CardType.HiddenEnergy:
                return TerritoryRepository.Instance.Territories.Any(t => t.Kind == TerrainKind.Sea) ? new object[0] : null;
            case CardType.EnergyExtraction:
                var moon = ContinentRepository.Instance != null
                    ? ContinentRepository.Instance.Continents.Values.FirstOrDefault(c => c.Name == "moon")
                    : null;
                return moon != null && moon.IsOwnedByPlayer(p) ? new object[0] : null;
            case CardType.InvadeSurface:
                return p.Territories.Any(t => t.Kind == TerrainKind.Moon && t.GetAvailableTroops() > 0)
                    ? new object[0] : null;
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

    static IEnumerable<Territory> OfKind(IEnumerable<Territory> territories, Card card)
    {
        var kind = Frontiers.CardTerrain(card.CardType);
        return kind == null ? territories : territories.Where(t => t.Kind == kind);
    }

    static int EnemyNeighbours(Territory t) => t.NeighbourTerritories.Count(n => n.Owner != null && n.Owner != t.Owner);

    // Own territories facing enemies, most threatened (and weakest) first.
    static IEnumerable<Territory> Border(Player p) =>
        p.Territories.Where(t => EnemyNeighbours(t) > 0).OrderByDescending(EnemyNeighbours).ThenBy(t => t.Troops);

    // Where a new commander is least likely to die: the biggest stack with the fewest enemies next to it.
    static Territory Safest(Player p) =>
        p.Territories.OrderBy(EnemyNeighbours).ThenByDescending(t => t.Troops).FirstOrDefault();
}
