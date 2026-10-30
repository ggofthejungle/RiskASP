using System.Collections.Generic;
using System.Linq;
using Expansions;

namespace Cards
{
    // The command-card decks (Risk 2210 A.D.: one deck per commander, with copies). Copy counts and
    // power-up costs follow the 2210 decks as modelled in the reference Java version (avioane/InvadeEarth,
    // read for rules only). Our map is land-only, so the Naval and Space decks and the water/Moon variants
    // of Nuclear cards are left out. Card names/texts will be rewritten before release (legal).
    public static class DeckLists
    {
        public static readonly IReadOnlyList<(CardType Type, int Copies)> Diplomat = new[]
        {
            (CardType.ColonyInfluenceD, 4),
            (CardType.TerritorialStation, 3),
            (CardType.DecoyRevealed, 2),
            (CardType.Evacuation, 2),
            (CardType.Redeployment, 3),
            (CardType.ModReduction, 2),
            (CardType.EnergyCrisis, 2),
            (CardType.CeaseFire, 2),
        };

        public static readonly IReadOnlyList<(CardType Type, int Copies)> Land = new[]
        {
            (CardType.AssembleModsL, 3),
            (CardType.ReinforcementsL, 3),
            (CardType.LandDeathTrap, 1),
            (CardType.StealthModsL, 5),
            (CardType.StealthStation, 1),
            (CardType.ScoutForces, 3),
            (CardType.ColonyInfluenceL, 2),
            (CardType.FrequencyJamL, 2),
        };

        // Land-only: no Rocket Strike / Scatter Bomb water+Moon, no Aqua Brother / Nicky Boy.
        public static readonly IReadOnlyList<(CardType Type, int Copies)> Nuclear = new[]
        {
            (CardType.Armageddon, 1),
            (CardType.RocketStrikeLand, 2),
            (CardType.ScatterBombLand, 3),
            (CardType.TheMother, 1),
            (CardType.AssassinBomb, 3),
        };

        public static IReadOnlyList<(CardType Type, int Copies)> For(CommanderType deck) => deck switch
        {
            CommanderType.Diplomat => Diplomat,
            CommanderType.LandCommander => Land,
            CommanderType.NuclearCommander => Nuclear,
            _ => System.Array.Empty<(CardType, int)>(),
        };

        public static IEnumerable<CommanderType> DecksInPlay => new[]
            { CommanderType.Diplomat, CommanderType.LandCommander, CommanderType.NuclearCommander };

        // Power-up cost (energy paid when the card is played).
        public static int CostOf(CardType type) => type switch
        {
            CardType.TerritorialStation => 1,
            CardType.ModReduction => 2,
            CardType.CeaseFire => 2,
            CardType.AssembleModsL or CardType.AssembleModsNa or CardType.AssembleModsS => 1,
            CardType.LandDeathTrap or CardType.WaterDeathTrap or CardType.OrbitalMines => 3,
            CardType.EnergyExtraction => 1,
            CardType.Armageddon => 4,
            CardType.RocketStrikeLand or CardType.RocketStrikeWater or CardType.RocketStrikeMoon => 2,
            CardType.ScatterBombLand or CardType.ScatterBombWater or CardType.ScatterBombMoon => 1,
            CardType.TheMother or CardType.AquaBrother or CardType.NickyBoy => 3,
            CardType.AssassinBomb => 1,
            _ => 0,
        };

        public static int TotalCards(CommanderType deck) => For(deck).Sum(e => e.Copies);
    }
}
