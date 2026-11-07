using System;
using player;

// Online, other players' hands are hidden: this device holds face-down cards for them. Every rule whose
// outcome depends on a hand it may not see goes through here. The host works the answer out and sends it
// with the action; the other devices use the host's answer. With no source (local games) everything is
// worked out right here.
public interface ISecretSource
{
    // This device sees the real cards of p.
    bool Knows(Player p);
    // This device works hidden things out (the online host); the others replay its answers.
    bool Decides { get; }
    // Host only: the stream for card draws. Its numbers are never sent, so nobody can tell which card came out.
    IRandom DrawRandom { get; }
    // Host only: called before working out anything hidden, so face-down cards it holds can be resolved first.
    void BeforeDeciding();
    void PutPublic(string value);
    void PutPrivate(Player owner, string value);
    // Replaying devices: the host's next answer, or null when there isn't one.
    string TakePublic();
    string TakePrivate();
}

public static class Secrets
{
    public static ISecretSource Source;

    public static bool Knows(Player p) => Source == null || Source.Knows(p);

    public static bool Decides => Source == null || Source.Decides;

    public static IRandom DrawRandom => Source != null && Source.Decides ? Source.DrawRandom : GameRandom.Current;

    // An answer every device must agree on, worked out from information only the host has.
    public static string Public(Func<string> work)
    {
        if (Source == null) return work();
        if (Source.Decides)
        {
            Source.BeforeDeciding();
            var value = work();
            Source.PutPublic(value);
            return value;
        }
        var replayed = Source.TakePublic();
        if (replayed != null) return replayed;
        UnityEngine.Debug.LogError("Secrets: the host's answer is missing; working it out locally (may not match).");
        return work();
    }

    public static bool PublicBool(Func<bool> work) => Public(() => work() ? "1" : "0") == "1";

    // Something only the owner's device may learn (the card they drew). Other devices get null.
    public static string Private(Player owner, Func<string> work)
    {
        if (Source == null) return work();
        if (Source.Decides)
        {
            Source.BeforeDeciding();
            var value = work();
            Source.PutPrivate(owner, value);
            return value;
        }
        return Source.Knows(owner) ? Source.TakePrivate() : null;
    }
}
