using System.Linq;
using Map;
using player;
using UnityEngine;

namespace Expansions
{
    // 2210 commanders and space stations: starting units, hiring/building in the command step,
    // and what happens to them when their territory is conquered.
    public class Purchases
    {
        public const int CommanderCost = 3;
        public const int StationCost = 5;
        public const int MaxStations = 4;

        // Land-only map: Naval and Space commanders have nothing to do.
        public static readonly CommanderType[] HireableCommanders =
            { CommanderType.Diplomat, CommanderType.LandCommander, CommanderType.NuclearCommander };

        private readonly GameManager _gm;

        public System.Action<Player, Commander, Territory> OnCommanderHired;
        public System.Action<Player, Territory> OnStationBuilt;

        public Purchases(GameManager gm)
        {
            _gm = gm;
        }

        public static int StationsOf(Player p) => p.Territories.Count(t => t.containsSpaceStation);

        // ---------------- Hire / build ----------------

        public string WhyCantHire(Player p, CommanderType type, Territory where)
        {
            if (!HireableCommanders.Contains(type)) return $"{type} isn't used: this map has no water or Moon territories.";
            if (!_gm.IsCommandStep(p)) return "You can only hire commanders on your turn, before your first invasion.";
            if (where == null || where.Owner != p) return "Place the commander on a territory you occupy.";
            if (p.HasCommanderInPlay(type)) return $"You already have a {type}.";
            if (p.GetEnergy() < CommanderCost) return $"A commander costs {CommanderCost} energy.";
            return null;
        }

        public void Hire(Player p, CommanderType type, Territory where)
        {
            var why = WhyCantHire(p, type, where);
            if (why != null) throw new System.InvalidOperationException(why);
            p.SetEnergy(p.GetEnergy() - CommanderCost);
            var commander = new Commander(type);
            where.AddCommander(commander);
            Debug.Log($"{p.Name} hired a {type} in {where.Name}.");
            OnCommanderHired?.Invoke(p, commander, where);
        }

        public string WhyCantBuild(Player p, Territory where)
        {
            if (!_gm.IsCommandStep(p)) return "You can only build space stations on your turn, before your first invasion.";
            if (where == null || where.Owner != p) return "Build the station on a territory you occupy.";
            if (where.containsSpaceStation) return $"{where.Name} already has a space station.";
            if (StationsOf(p) >= MaxStations) return $"You can have at most {MaxStations} space stations.";
            if (p.GetEnergy() < StationCost) return $"A space station costs {StationCost} energy.";
            return null;
        }

        public void Build(Player p, Territory where)
        {
            var why = WhyCantBuild(p, where);
            if (why != null) throw new System.InvalidOperationException(why);
            p.SetEnergy(p.GetEnergy() - StationCost);
            where.AddSpaceStation();
            Debug.Log($"{p.Name} built a space station in {where.Name}.");
            OnStationBuilt?.Invoke(p, where);
        }

        // ---------------- Game start ----------------

        // Every player starts with a Diplomat, a Land Commander and a Space Station (2210).
        // We place them automatically: commanders on the player's strongest border territory,
        // the station on their next strongest territory.
        public static void PlaceStartingUnits(Player p)
        {
            var byStrength = p.Territories
                .OrderByDescending(t => t.HasAnyNeighbourEnemy())
                .ThenByDescending(t => t.Troops)
                .ToList();
            if (byStrength.Count == 0) return;

            var front = byStrength[0];
            front.AddCommander(new Commander(CommanderType.Diplomat));
            front.AddCommander(new Commander(CommanderType.LandCommander));
            var stationAt = byStrength.Count > 1 ? byStrength[1] : front;
            stationAt.AddSpaceStation();
        }

        // ---------------- Conquest ----------------

        // Called just before a territory changes hands. Commanders in it are destroyed
        // (they're only at risk once no units are left to protect them, which is the case on conquest).
        // A space station is captured by the attacker, or destroyed if they already have the maximum.
        public static void OnTerritoryConquered(Territory territory, Player conqueror)
        {
            foreach (var commander in territory.CommandersList.ToList())
            {
                territory.RemoveCommander(commander);
                Debug.Log($"{territory.Owner?.Name}'s {commander.CommanderType} was destroyed in {territory.Name}.");
            }

            if (territory.containsSpaceStation && StationsOf(conqueror) >= MaxStations)
            {
                territory.RemoveSpaceStation();
                Debug.Log($"The space station in {territory.Name} was destroyed ({conqueror.Name} already has {MaxStations}).");
            }
        }
    }
}
