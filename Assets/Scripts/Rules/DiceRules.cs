using System;

namespace InvadeEarth.Rules
{
    // Which dice a battle rolls. Land and Nuclear commanders attack with d8.
    // On defense, a space station makes every die a d8; otherwise each commander present rolls a d8.
    public static class DiceRules
    {
        // Attack die from the 2210 chart. Defense is separate: every commander rolls d8.
        // Land: the battle starts or ends on land. Naval: on water. Space: on the moon. Nuclear: always.
        // Diplomat never. "Coastal" is a water battle that touches land, not a land-to-land bonus.
        public static bool AttacksWithD8(string commander, string fromTerrain, string toTerrain)
        {
            if (commander == "NuclearCommander") return true;
            if (commander == "LandCommander") return fromTerrain == "Land" || toTerrain == "Land";
            if (commander == "NavalCommander") return fromTerrain == "Sea" || toTerrain == "Sea";
            if (commander == "SpaceCommander") return fromTerrain == "Moon" || toTerrain == "Moon";
            return false;
        }

        // The first `d8Count` dice are d8, the rest are d6, never more than `dice`.
        public static (int d8, int d6) Split(int dice, int d8Count)
        {
            if (dice < 0) dice = 0;
            var d8 = Math.Min(dice, Math.Max(0, d8Count));
            return (d8, dice - d8);
        }

        public static (int d8, int d6) DefenseDice(int dice, bool spaceStation, int commandersPresent)
        {
            if (dice < 0) dice = 0;
            if (spaceStation) return (dice, 0);
            return Split(dice, commandersPresent);
        }
    }
}
