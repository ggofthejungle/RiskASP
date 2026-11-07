using System;
using System.Linq;
using Cards;
using Expansions;
using InvadeEarth.Rules;
using Map;
using UnityEngine;

// Scene <-> MatchSnapshot. The authority captures; a rejoining device applies the board.
// Applying sets owners, units, stations, commanders, energy and (for this device's player) the hand.
// Year, turn and phase come from the authority's next view; they are not rewound here.
public static class MatchSnapshots
{
    public static MatchSnapshot Capture(GameManager gm)
    {
        var territories = TerritoryRepository.Instance.Territories;
        return new MatchSnapshot
        {
            Year = gm.Year,
            Turn = gm.Turn,
            CurrentPlayerId = gm.GetPlayerId(gm.CurrentPlayer),
            InputPlayerId = gm.GetPlayerId(gm.InputPlayer),
            Phase = gm.CurrentPhase != null ? gm.CurrentPhase.Name : "",
            Bidding = gm.IsBidding,
            Over = gm.GameState == GameState.Over,
            TurnOrder = gm.GetPlayersInTurnOrder().Select(gm.GetPlayerId).ToArray(),
            Players = gm.Players.Select(p => new SnapshotPlayer
            {
                PlayerId = gm.GetPlayerId(p),
                Name = p.Name,
                Bot = p.IsBot(),
                Connected = gm.IsConnected(p),
                Energy = p.GetEnergy(),
                HandCount = p.Cards.Count,
                CardIds = p.Cards.Select(c => c.Id).ToArray(),
                ScoutedTerritories = gm.CommandCards.ScoutedBy(p).Select(t => t.Name).ToArray(),
            }).ToArray(),
            Territories = territories.Select(t => new SnapshotTerritory
            {
                Name = t.Name,
                OwnerId = gm.GetPlayerId(t.Owner),
                Troops = t.Troops,
                Station = t.containsSpaceStation,
                Commanders = t.CommandersList.Select(c => c.CommanderType.ToString()).ToArray(),
            }).ToArray(),
        };
    }

    public static string ToJson(MatchSnapshot snapshot) => JsonUtility.ToJson(snapshot);

    public static MatchSnapshot FromJson(string json)
    {
        var snapshot = JsonUtility.FromJson<MatchSnapshot>(json);
        if (snapshot == null) throw new ArgumentException("Not a match snapshot.");
        if (snapshot.Version > MatchSnapshot.CurrentVersion)
            throw new ArgumentException($"Snapshot version {snapshot.Version} is newer than this build ({MatchSnapshot.CurrentVersion}).");
        return snapshot;
    }

    public static void ApplyBoard(GameManager gm, MatchSnapshot snapshot)
    {
        var repo = TerritoryRepository.Instance;
        foreach (var st in snapshot.Territories)
        {
            var t = repo.FromName(st.Name);
            if (t == null) throw new ArgumentException($"Unknown territory {st.Name}.");
            var owner = gm.GetPlayerById(st.OwnerId);
            if (owner != null && t.Owner != owner) t.SetOwner(owner);
            if (t.Troops != st.Troops) t.SetTroops(st.Troops);

            if (st.Station && !t.containsSpaceStation) t.AddSpaceStation();
            else if (!st.Station && t.containsSpaceStation) t.RemoveSpaceStation();

            var have = t.CommandersList.Select(c => c.CommanderType.ToString()).OrderBy(s => s);
            if (!have.SequenceEqual(st.Commanders.OrderBy(s => s)))
            {
                foreach (var c in t.CommandersList.ToList()) t.RemoveCommander(c);
                foreach (var type in st.Commanders)
                    t.AddCommander(new Commander((CommanderType)Enum.Parse(typeof(CommanderType), type)));
            }
        }

        var allCards = CardRepository.Instance.AllCards;
        foreach (var sp in snapshot.Players)
        {
            var p = gm.GetPlayerById(sp.PlayerId);
            if (p == null) continue;
            p.SetEnergy(sp.Energy);
            gm.SetConnected(p, sp.Connected);
            // Other players' hands arrive empty (hidden), so only a hand we can see is rebuilt.
            if (sp.CardIds.Length == 0 && sp.HandCount > 0) continue;
            var wanted = sp.CardIds.Select(id => allCards.First(c => c.Id == id)).ToList();
            var extra = p.Cards.Where(c => !wanted.Contains(c)).ToList();
            if (extra.Count > 0) p.RemoveCards(extra);
            foreach (var card in wanted.Where(c => !p.Cards.Contains(c)))
            {
                foreach (var other in gm.Players.Where(o => o != p && o.Cards.Contains(card)))
                    other.RemoveCard(card);
                CardRepository.Instance.CardsInDeck.Remove(card);
                p.AddCard(card);
            }
        }
    }
}
