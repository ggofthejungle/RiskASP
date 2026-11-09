namespace InvadeEarth.Rules
{
    // What a player receives at the start of their turn, and at the start of the game.
    public static class IncomeRules
    {
        public const int StartingEnergy = 3;
        public const int StationTroopGrant = 1;

        // The Score Chart's lower-right number is this: territories / 3, fraction dropped, at least 3.
        // That many MODs, and the same amount of energy. A space station adds one MOD and no energy.
        // Continent and colony bonuses are added on top; those numbers are printed on the board.
        public static int ScoreChart(int territoryCount) => System.Math.Max(3, territoryCount / 3);

        public static int EnergyIncome(int territoryCount, int continentBonus = 0) =>
            ScoreChart(territoryCount) + continentBonus;

        public static int TerritoryTroopBonus(int territoryCount) => ScoreChart(territoryCount);

        public static int TroopBonus(int territoryCount, int continentBonus) =>
            TerritoryTroopBonus(territoryCount) + continentBonus;

        // Conquering 3 territories in one turn earns 1 energy and 1 command card, once per turn.
        public const int ConquestBonusTerritories = 3;
        public const int ConquestBonusEnergy = 1;

        public static bool EarnsConquestBonus(int conqueredThisTurn) =>
            conqueredThisTurn == ConquestBonusTerritories;
    }
}
