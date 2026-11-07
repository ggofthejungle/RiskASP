using System;
using System.Linq;

namespace InvadeEarth.Rules
{
    // Everything a device needs to draw the match after joining or rejoining.
    // Plain fields and arrays so JsonUtility (client) and System.Text.Json (server) both read it.
    [Serializable]
    public sealed class MatchSnapshot
    {
        public const int CurrentVersion = 1;

        public int Version = CurrentVersion;
        public int Year;
        public int Turn;
        public int CurrentPlayerId = -1;
        public int InputPlayerId = -1;
        public string Phase = "";
        public bool Bidding;
        public bool Over;
        public int[] TurnOrder = Array.Empty<int>();
        public SnapshotPlayer[] Players = Array.Empty<SnapshotPlayer>();
        public SnapshotTerritory[] Territories = Array.Empty<SnapshotTerritory>();
    }

    [Serializable]
    public sealed class SnapshotPlayer
    {
        public int PlayerId;
        public string Name;
        public bool Bot;
        public bool Connected = true;
        public int Energy;
        public int HandCount;
        // Filled in only for the player the snapshot is for (see SnapshotFilter).
        public int[] CardIds = Array.Empty<int>();
        public string[] ScoutedTerritories = Array.Empty<string>();
    }

    [Serializable]
    public sealed class SnapshotTerritory
    {
        public string Name;
        public int OwnerId = -1;
        public int Troops;
        public bool Station;
        public string[] Commanders = Array.Empty<string>();
    }

    public static class SnapshotFilter
    {
        public static MatchSnapshot ForPlayer(MatchSnapshot full, int viewerId)
        {
            return new MatchSnapshot
            {
                Version = full.Version,
                Year = full.Year,
                Turn = full.Turn,
                CurrentPlayerId = full.CurrentPlayerId,
                InputPlayerId = full.InputPlayerId,
                Phase = full.Phase,
                Bidding = full.Bidding,
                Over = full.Over,
                TurnOrder = full.TurnOrder.ToArray(),
                Territories = full.Territories.Select(t => new SnapshotTerritory
                {
                    Name = t.Name,
                    OwnerId = t.OwnerId,
                    Troops = t.Troops,
                    Station = t.Station,
                    Commanders = t.Commanders.ToArray(),
                }).ToArray(),
                Players = full.Players.Select(p => new SnapshotPlayer
                {
                    PlayerId = p.PlayerId,
                    Name = p.Name,
                    Bot = p.Bot,
                    Connected = p.Connected,
                    Energy = p.Energy,
                    HandCount = p.HandCount,
                    CardIds = p.PlayerId == viewerId ? p.CardIds.ToArray() : Array.Empty<int>(),
                    ScoutedTerritories = p.PlayerId == viewerId ? p.ScoutedTerritories.ToArray() : Array.Empty<string>(),
                }).ToArray(),
            };
        }
    }
}
