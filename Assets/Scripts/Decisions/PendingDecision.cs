using System;
using System.Collections.Generic;
using System.Linq;

namespace Decisions
{
    // What kind of question the game is asking a player who is NOT necessarily the turn owner.
    public enum DecisionKind
    {
        DefenseReaction, // defender may react before an attack's dice are rolled
        DefenseDice,     // defender chooses 1 or 2 dice before the roll
        RemoveUnits,     // MOD Reduction: pick one of your territories to lose a unit (options = territory names)
        PlaceStartingUnit, // game start: pick the territory for a starting unit (Subject = Diplomat / LandCommander / SpaceStation)
        EvacuationDestination, // Evacuation: pick where the units go (options = your other territories, Subject = attacked territory)
        ArmageddonCard,   // Armageddon: play a nuclear card for free (options = "Card:<id>" + Pass)
        ArmageddonTarget, // Armageddon: the territory for that card (options = territory names, Subject = card name)
        ClaimTerritory,      // take-turns setup: claim an empty territory with 1 MOD (options = empty territories)
        PlaceStartingTroops, // take-turns setup: place 1 MOD (options = your territories, Subject = MODs left in this batch)
    }

    // Option ids a player can answer with. Defense cards will be added later as "Card:<id>".
    public static class DecisionOptions
    {
        public const string Pass = "Pass";
        public const string PassForRestOfTurn = "PassForRestOfTurn";
    }

    // A question the game is waiting on. While one is open, the player it names is the
    // "input owner": only they may act, and only by answering it (DecisionResponseAction).
    // Plain C# (ids and strings only) so it can be sent over the network later.
    // Not [Serializable]: Unity would rebuild it empty (Options null) after a script reload in Play Mode.
    public sealed class PendingDecision
    {
        public int PlayerId { get; }
        public DecisionKind Kind { get; }
        public IReadOnlyList<string> Options { get; }
        public string Subject { get; } // what the decision is about (e.g. which starting unit), optional
        // Online, on a device that can't see the asked player's hand: their card options aren't listed, so any
        // "Card:<id>" answer is accepted here (the host has already checked it).
        public bool AnyCard { get; }

        public PendingDecision(int playerId, DecisionKind kind, IEnumerable<string> options, string subject = null,
            bool anyCard = false)
        {
            PlayerId = playerId;
            Kind = kind;
            Options = (options ?? Enumerable.Empty<string>()).ToList().AsReadOnly();
            Subject = subject;
            AnyCard = anyCard;
        }

        public bool HasOption(string option) =>
            Options.Contains(option) || (AnyCard && option != null && option.StartsWith("Card:"));

        public override string ToString() => $"{Kind}{(Subject != null ? $"({Subject})" : "")} for player {PlayerId} [{string.Join(", ", Options)}]";
    }
}
