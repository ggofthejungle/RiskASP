using System;
using System.Collections.Generic;
using System.Linq;

namespace Decisions
{
    // What kind of question the game is asking a player who is NOT necessarily the turn owner.
    public enum DecisionKind
    {
        DefenseReaction, // defender may react before an attack's dice are rolled
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
    [Serializable]
    public sealed class PendingDecision
    {
        public int PlayerId { get; }
        public DecisionKind Kind { get; }
        public IReadOnlyList<string> Options { get; }

        public PendingDecision(int playerId, DecisionKind kind, IEnumerable<string> options)
        {
            PlayerId = playerId;
            Kind = kind;
            Options = (options ?? Enumerable.Empty<string>()).ToList().AsReadOnly();
        }

        public bool HasOption(string option) => Options.Contains(option);

        public override string ToString() => $"{Kind} for player {PlayerId} [{string.Join(", ", Options)}]";
    }
}
