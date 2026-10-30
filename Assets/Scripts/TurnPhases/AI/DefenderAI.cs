using System.Linq;
using Cards;
using Decisions;

// Simple instant heuristic for an AI defender's reaction window (RW-4). C#, so it works on mobile too.
public static class DefenderAI
{
    public static string Choose(GameManager gm, PendingDecision decision)
    {
        var defender = gm.GetPlayerById(decision.PlayerId);

        // MOD Reduction: lose the unit where it hurts least (the biggest stack).
        if (decision.Kind == DecisionKind.RemoveUnits)
            return decision.Options
                .OrderByDescending(name => Map.TerritoryRepository.Instance.FromName(name).Troops)
                .First();

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
}
