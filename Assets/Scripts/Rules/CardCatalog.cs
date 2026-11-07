namespace InvadeEarth.Rules
{
    // When a command card may be played. Names match the Unity CardTiming enum.
    public enum RuleTiming
    {
        NotImplemented,
        BeforeFirstInvasion,
        OpponentInvades,
        EndOfTurn,
        EndOfGame,
    }

    public readonly struct CardRule
    {
        public readonly int PowerUpCost;
        public readonly RuleTiming Timing;

        public CardRule(int powerUpCost, RuleTiming timing)
        {
            PowerUpCost = powerUpCost;
            Timing = timing;
        }
    }

    // Costs and timings for the land-map cards. Buying is 1 energy, at most 4 a turn.
    public static class CardCatalog
    {
        public const int BuyCost = 1;
        public const int MaxBuysPerTurn = 4;

        public static CardRule For(string cardName) => cardName switch
        {
            "EnergyCrisis" => new CardRule(0, RuleTiming.BeforeFirstInvasion),
            "DecoyRevealed" => new CardRule(0, RuleTiming.BeforeFirstInvasion),
            "Armageddon" => new CardRule(4, RuleTiming.BeforeFirstInvasion),
            "CeaseFire" => new CardRule(2, RuleTiming.OpponentInvades),
            "TerritorialStation" => new CardRule(1, RuleTiming.BeforeFirstInvasion),
            "ModReduction" => new CardRule(2, RuleTiming.BeforeFirstInvasion),
            "Redeployment" => new CardRule(0, RuleTiming.EndOfTurn),
            "ColonyInfluenceD" => new CardRule(0, RuleTiming.EndOfGame),
            "Evacuation" => new CardRule(0, RuleTiming.OpponentInvades),
            "AssembleModsL" => new CardRule(1, RuleTiming.BeforeFirstInvasion),
            "StealthModsL" => new CardRule(0, RuleTiming.OpponentInvades),
            "ReinforcementsL" => new CardRule(0, RuleTiming.BeforeFirstInvasion),
            "FrequencyJamL" => new CardRule(0, RuleTiming.BeforeFirstInvasion),
            "ScoutForces" => new CardRule(0, RuleTiming.BeforeFirstInvasion),
            "ColonyInfluenceL" => new CardRule(0, RuleTiming.EndOfGame),
            "RocketStrikeLand" => new CardRule(2, RuleTiming.BeforeFirstInvasion),
            "ScatterBombLand" => new CardRule(1, RuleTiming.BeforeFirstInvasion),
            "TheMother" => new CardRule(3, RuleTiming.BeforeFirstInvasion),
            "AssassinBomb" => new CardRule(1, RuleTiming.BeforeFirstInvasion),
            "LandDeathTrap" => new CardRule(3, RuleTiming.OpponentInvades),
            "StealthStation" => new CardRule(0, RuleTiming.OpponentInvades),
            _ => new CardRule(0, RuleTiming.NotImplemented),
        };
    }
}
