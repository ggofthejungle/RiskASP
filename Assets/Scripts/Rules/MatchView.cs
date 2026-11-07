using System.Linq;

namespace InvadeEarth.Rules
{
    // What one device is allowed to see. Card ids and scouted territories are filled in
    // only for that device's player. Everyone else gets a hand count.
    public sealed class PlayerView
    {
        public int PlayerId;
        public string Name;
        public int Energy;
        public int TerritoryCount;
        public int TroopCount;
        public int HandCount;
        public int[] CardIds;
        public string[] SecretTerritories;
    }

    public sealed class MatchView
    {
        public int Year;
        public int Turn;
        public int InputPlayerId;
        public string Phase;
        public bool Bidding;
        public PlayerView[] Players;
        public int[] LastAttackerRolls;
        public int[] LastDefenderRolls;
        public bool HasLastBattle;
        public int LastAttackerLosses;
        public int LastDefenderLosses;
        public bool LastConquered;
    }

    public static class HiddenInfo
    {
        public static MatchView ForPlayer(MatchView full, int viewerId)
        {
            return new MatchView
            {
                Year = full.Year,
                Turn = full.Turn,
                InputPlayerId = full.InputPlayerId,
                Phase = full.Phase,
                Bidding = full.Bidding,
                LastAttackerRolls = full.LastAttackerRolls,
                LastDefenderRolls = full.LastDefenderRolls,
                HasLastBattle = full.HasLastBattle,
                LastAttackerLosses = full.LastAttackerLosses,
                LastDefenderLosses = full.LastDefenderLosses,
                LastConquered = full.LastConquered,
                Players = full.Players.Select(p => new PlayerView
                {
                    PlayerId = p.PlayerId,
                    Name = p.Name,
                    Energy = p.Energy,
                    TerritoryCount = p.TerritoryCount,
                    TroopCount = p.TroopCount,
                    HandCount = p.HandCount,
                    CardIds = p.PlayerId == viewerId ? p.CardIds : null,
                    SecretTerritories = p.PlayerId == viewerId ? p.SecretTerritories : null,
                }).ToArray()
            };
        }
    }
}
