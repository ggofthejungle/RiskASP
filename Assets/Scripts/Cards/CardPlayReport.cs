using System.Collections.Generic;
using player;

namespace Cards
{
    // What a played card did, for the other players' screens (and logs). Built right after the card resolves.
    public sealed class CardPlayReport
    {
        public Player Player;
        public Card Card;
        public object[] Targets;
        public bool Reaction;            // played in the reaction window against an invasion
        public int? Roll;                // Rocket Strike, The Mother, Assassin Bomb
        public string Continent;         // The Mother
        public List<Map.Territory> Drawn; // Scatter Bomb
        public string Note;               // extra line (a drawn sea, a launch target)
        public readonly List<(Map.Territory Territory, int Before, int After)> Changes = new();
    }
}
