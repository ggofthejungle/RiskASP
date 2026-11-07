using System;
using System.Linq;
using Actions;
using InvadeEarth.Rules;

// The in-process server. Clients send ActionData; this validates, applies, and rolls.
// A device then reads ViewFor(its player) so other hands and scouted territories stay hidden.
// The same InvadeEarth.Rules assembly is what a Cloud Code host will reference later.
public sealed class GameAuthority
{
    readonly GameManager _gm;

    public GameAuthority(GameManager gm) => _gm = gm;

    public bool Submit(ActionData data, out string error)
    {
        error = null;
        if (data == null)
        {
            error = "No action.";
            return false;
        }
        try
        {
            var action = ActionCodec.FromData(data, _gm);
            if (_gm.HandlePlayerAction(action)) return true;
            error = "Rejected.";
            return false;
        }
        catch (Exception e)
        {
            error = e.Message;
            return false;
        }
    }

    public MatchView ViewFor(int playerId) => HiddenInfo.ForPlayer(FullView(), playerId);

    // Join or rejoin: the whole board, with only this player's hand and scouting filled in.
    public MatchSnapshot SnapshotFor(int playerId) => SnapshotFilter.ForPlayer(MatchSnapshots.Capture(_gm), playerId);

    // Online timing: the defender's reaction window auto-passes after this many seconds.
    public void UseOnlineReactionTimer(double seconds = DecisionClock.OnlineReactionSeconds) =>
        _gm.ReactionTimeoutSeconds = (float)seconds;

    // A dropped device. Its questions go to the AI until it reconnects and takes the seat back.
    public void Disconnect(int playerId) => _gm.SetConnected(_gm.GetPlayerById(playerId), false);

    public MatchSnapshot Reconnect(int playerId)
    {
        _gm.SetConnected(_gm.GetPlayerById(playerId), true);
        return SnapshotFor(playerId);
    }

    MatchView FullView()
    {
        int[] attack = null;
        int[] defense = null;
        var hasBattle = false;
        var attackerLosses = 0;
        var defenderLosses = 0;
        var conquered = false;
        if (_gm.AttackPhase != null && _gm.AttackPhase.AttackResults.Any())
        {
            var last = _gm.AttackPhase.LastAttackResult;
            attack = last.AttackerRolls;
            defense = last.DefenderRolls;
            hasBattle = true;
            attackerLosses = last.AttackerLosses;
            defenderLosses = last.DefenderLosses;
            conquered = last.HasAttackerWonTerritory();
        }

        return new MatchView
        {
            Year = _gm.Year,
            Turn = _gm.Turn,
            InputPlayerId = _gm.GetPlayerId(_gm.InputPlayer),
            Phase = _gm.CurrentPhase != null ? _gm.CurrentPhase.Name : "",
            Bidding = _gm.IsBidding,
            LastAttackerRolls = attack,
            LastDefenderRolls = defense,
            HasLastBattle = hasBattle,
            LastAttackerLosses = attackerLosses,
            LastDefenderLosses = defenderLosses,
            LastConquered = conquered,
            Players = _gm.Players.Select(p => new PlayerView
            {
                PlayerId = _gm.GetPlayerId(p),
                Name = p.Name,
                Energy = p.GetEnergy(),
                TerritoryCount = p.GetTerritoryCount(),
                TroopCount = p.GetTroopsCountInTerritories(),
                HandCount = p.Cards.Count,
                CardIds = p.Cards.Select(c => c.Id).ToArray(),
                SecretTerritories = _gm.CommandCards.ScoutedBy(p).Select(t => t.Name).ToArray(),
            }).ToArray()
        };
    }
}
