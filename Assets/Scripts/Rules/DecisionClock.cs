using System;

namespace InvadeEarth.Rules
{
    // Countdown for a question asked of one player (the defender's reaction window).
    // Times are seconds from any clock the host picks; a server and a device can each run one.
    // TimeoutSeconds <= 0 means no limit (local play).
    public sealed class DecisionClock
    {
        public const double OnlineReactionSeconds = 20;

        public double OpenedAt { get; private set; }
        public double TimeoutSeconds { get; private set; }
        public bool Running { get; private set; }

        public void Start(double now, double timeoutSeconds)
        {
            OpenedAt = now;
            TimeoutSeconds = timeoutSeconds;
            Running = timeoutSeconds > 0;
        }

        public void Stop() => Running = false;

        public double SecondsLeft(double now) =>
            Running ? Math.Max(0, TimeoutSeconds - (now - OpenedAt)) : double.PositiveInfinity;

        public bool Expired(double now) => Running && now - OpenedAt >= TimeoutSeconds;
    }
}
