using System.Linq;
using Cards;
using Decisions;

// Simple instant heuristic for an AI defender's reaction window (RW-4). C#, so it works on mobile too.
public static class DefenderAI
{
    public static string Choose(GameManager gm, PendingDecision decision)
    {
        var defender = gm.GetPlayerById(decision.PlayerId);

        // Two dice when the defender has them. One die is the only choice with a single unit.
        if (decision.Kind == DecisionKind.DefenseDice)
            return decision.Options.Contains("2") ? "2" : "1";

        // Take-turns setup: grow next to what it already holds, aiming at continents it is closest to finishing.
        if (decision.Kind == DecisionKind.ClaimTerritory)
        {
            return decision.Options
                .Select(name => Map.TerritoryRepository.Instance.FromName(name))
                .OrderByDescending(t => t.NeighbourTerritories.Count(n => n.Owner == defender))
                .ThenByDescending(t => ContinentShare(t.Continent, defender))
                .ThenBy(t => t.NeighbourTerritories.Count(n => n.Owner != null && n.Owner != defender))
                .First().Name;
        }
        // Spread the remaining army across the borders. One territory used to take every leftover MOD.
        if (decision.Kind == DecisionKind.PlaceStartingTroops)
        {
            return decision.Options
                .Select(name => Map.TerritoryRepository.Instance.FromName(name))
                .OrderByDescending(t => t.NeighbourTerritories.Any(n => n.Owner != null && n.Owner != defender))
                .ThenBy(t => t.Troops)
                .ThenByDescending(t => t.NeighbourTerritories.Count(n => n.Owner != null && n.Owner != defender))
                .First().Name;
        }

        // Starting units: commanders on the strongest border territory; the station on the next strongest.
        if (decision.Kind == DecisionKind.PlaceStartingUnit)
        {
            var ranked = decision.Options
                .Select(name => Map.TerritoryRepository.Instance.FromName(name))
                .OrderByDescending(t => t.HasAnyNeighbourEnemy())
                .ThenByDescending(t => t.Troops)
                .ToList();
            if (decision.Subject == "SpaceStation")
                return (ranked.FirstOrDefault(t => t.CommandersList.Count == 0) ?? ranked[0]).Name;
            return ranked[0].Name;
        }

        if (decision.Kind == DecisionKind.EvacuationDestination)
        {
            var attacked = Map.TerritoryRepository.Instance.FromName(decision.Subject);
            var best = Cards.CommandCards.EvacuationDestination(defender, attacked);
            return best != null && decision.HasOption(best.Name) ? best.Name : decision.Options[0];
        }

        // MOD Reduction: lose the unit where it hurts least (the biggest stack).
        if (decision.Kind == DecisionKind.RemoveUnits)
            return decision.Options
                .OrderByDescending(name => Map.TerritoryRepository.Instance.FromName(name).Troops)
                .First();

        // Armageddon: a bot plays every free nuclear card it can. A human who is away passes
        // (playing their cards for them would spend cards they may have wanted to keep).
        if (decision.Kind == DecisionKind.ArmageddonCard)
        {
            if (defender == null || !defender.IsBot()) return DecisionOptions.Pass;
            return decision.Options.FirstOrDefault(o => o != DecisionOptions.Pass) ?? DecisionOptions.Pass;
        }
        // Its target: the opponent territory with the most commanders, then the most units.
        if (decision.Kind == DecisionKind.ArmageddonTarget)
            return decision.Options
                .Select(name => Map.TerritoryRepository.Instance.FromName(name))
                .OrderByDescending(t => t.CommandersList.Count)
                .ThenByDescending(t => t.Troops)
                .First().Name;

        var attack = gm.AttackPhase.PendingAttack;
        if (defender == null || attack == null) return DecisionOptions.Pass;

        var cards = decision.Options
            .Select(o => (option: o, card: gm.CommandCards.CardFromOption(defender, o)))
            .Where(x => x.card != null)
            .ToList();
        string Pick(CardType type) => cards.FirstOrDefault(x => x.card.CardType == type).option;

        var attackers = attack.Origin.Troops;
        var defenders = attack.Target.Troops;

        // Hopeless defence of a big stack: get the units out.
        if (defenders >= 3 && attackers >= 3 * defenders && Pick(CardType.Evacuation) is { } evac) return evac;
        // Heavy attack: stop it.
        if (attackers >= 2 * defenders && attackers >= 5 && Pick(CardType.CeaseFire) is { } cease) return cease;
        // Big attacking stack: halve it.
        if (attackers >= 6 && Pick(CardType.LandDeathTrap) is { } trap) return trap;
        // Free help.
        if (Pick(CardType.StealthStation) is { } station) return station;
        if (Pick(CardType.StealthModsL) is { } mods) return mods;

        return DecisionOptions.Pass;
    }

    static float ContinentShare(Map.Continent continent, player.Player p)
    {
        if (continent == null || continent.Territories.Count == 0) return 0f;
        return continent.Territories.Count(t => t.Owner == p) / (float)continent.Territories.Count;
    }
}
