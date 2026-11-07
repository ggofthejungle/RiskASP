using System;

// One random stream for the rules: dice, card draws, the opening deal, bid tie-breaks.
// Seed it and the same calls come out in the same order, which is what a server and a replay need.
public interface IRandom
{
    // Max is exclusive, matching System.Random.Next.
    int Range(int minInclusive, int maxExclusive);
    float Value { get; }
}

public static class GameRandom
{
    public static IRandom Current { get; private set; } = NewUnseeded();

    public static void UseSeed(int seed) => Current = new SeededRandom(seed);

    public static void UseUnseeded() => Current = NewUnseeded();

    public static void Use(IRandom random) => Current = random ?? NewUnseeded();

    static IRandom NewUnseeded() => new SeededRandom(Environment.TickCount);
}

// Server side of secret randomness: draws from a stream nobody else knows and remembers every number,
// so they can be sent along with the action that used them.
public sealed class RecordingRandom : IRandom
{
    readonly IRandom _inner;
    readonly System.Collections.Generic.List<int> _drawn = new();

    public RecordingRandom(IRandom inner) => _inner = inner;

    public int Range(int minInclusive, int maxExclusive)
    {
        var v = _inner.Range(minInclusive, maxExclusive);
        _drawn.Add(v);
        return v;
    }

    public float Value
    {
        get
        {
            var v = _inner.Value;
            _drawn.Add(BitConverter.SingleToInt32Bits(v));
            return v;
        }
    }

    // Start of one action: anything drawn before it (by nothing that changes the game) is dropped.
    public void Begin() => _drawn.Clear();

    public int[] Take()
    {
        var values = _drawn.ToArray();
        _drawn.Clear();
        return values;
    }
}

// Client side: while an action from the server is applied, hands back the server's numbers in order.
// Outside that, a local stream (for anything that doesn't change the game).
public sealed class ReplayRandom : IRandom
{
    readonly IRandom _local = new SeededRandom(Environment.TickCount);
    int[] _values;
    int _next;

    public bool Replaying => _values != null;
    // Set when an action wanted more numbers than the server used: the devices no longer agree.
    public bool Overran { get; private set; }

    public void Begin(int[] values)
    {
        _values = values ?? Array.Empty<int>();
        _next = 0;
        Overran = false;
    }

    // Returns how many of the server's numbers were not used (0 when the devices agree).
    public int End()
    {
        var left = _values == null ? 0 : _values.Length - _next;
        _values = null;
        return left;
    }

    int NextRaw(out bool ok)
    {
        ok = _values != null && _next < _values.Length;
        if (ok) return _values[_next++];
        if (_values != null) Overran = true;
        return 0;
    }

    public int Range(int minInclusive, int maxExclusive)
    {
        if (_values == null) return _local.Range(minInclusive, maxExclusive);
        var v = NextRaw(out var ok);
        return ok && v >= minInclusive && (v < maxExclusive || maxExclusive <= minInclusive) ? v : minInclusive;
    }

    public float Value
    {
        get
        {
            if (_values == null) return _local.Value;
            var v = NextRaw(out var ok);
            return ok ? BitConverter.Int32BitsToSingle(v) : 0f;
        }
    }
}

public sealed class SeededRandom : IRandom
{
    readonly Random _rng;

    public SeededRandom(int seed) => _rng = new Random(seed);

    public int Range(int minInclusive, int maxExclusive) => _rng.Next(minInclusive, maxExclusive);

    public float Value => (float)_rng.NextDouble();
}
