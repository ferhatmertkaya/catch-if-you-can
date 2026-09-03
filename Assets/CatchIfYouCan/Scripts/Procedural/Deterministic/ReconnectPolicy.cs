namespace CatchIfYouCan.Procedural.Deterministic
{
    /// <summary>Where a dropped peer is in getting back.</summary>
    public enum ReconnectState
    {
        /// <summary>In the session. Nothing to do.</summary>
        Connected = 0,

        /// <summary>Dropped, waiting out the backoff before the next attempt.</summary>
        Waiting,

        /// <summary>An attempt is in flight.</summary>
        Retrying,

        /// <summary>Out of attempts. The player is told, and goes back to the menu.</summary>
        GaveUp,

        /// <summary>
        /// The host stopped holding the seat. Distinct from running out of attempts: the
        /// player did everything right and the session moved on without them.
        /// </summary>
        SeatLost,
    }

    /// <summary>
    /// When a dropped player tries again, how often, and how long the host keeps their place.
    ///
    /// <para>
    /// <b>NOT PRODUCTION READY.</b> This is the policy, not the mechanism. There is no
    /// transport in this project to reconnect over, nothing calls it, and it has never
    /// reconnected anything. What it is for is that the policy questions - how many attempts,
    /// how long a seat is held, what a player is told when it runs out - get answered once, in
    /// a place that can be tested, rather than invented inside a retry loop later. When a
    /// transport arrives, the loop asks this; the numbers do not get re-decided.
    /// </para>
    ///
    /// <para>
    /// Pure and engine-free, so the offline harness exercises it without a network, which is
    /// the only way any of it is exercised today.
    /// </para>
    /// </summary>
    public static class ReconnectPolicy
    {
        /// <summary>
        /// How many attempts before giving up.
        ///
        /// <para>
        /// Four, over roughly fifteen seconds of backoff. Enough for a phone changing cell or
        /// a router blinking; not so many that somebody stares at "reconnecting" for a minute
        /// while the mission they were in has finished without them.
        /// </para>
        /// </summary>
        public const int MaxAttempts = 4;

        /// <summary>The first wait, in seconds. Doubles from here.</summary>
        public const int FirstBackoffSeconds = 1;

        /// <summary>The longest single wait, in seconds. The doubling stops here.</summary>
        public const int MaxBackoffSeconds = 8;

        /// <summary>
        /// How long the host keeps a dropped player's seat, in seconds.
        ///
        /// <para>
        /// Longer than the backoff schedule on purpose, so a player who uses every attempt
        /// still has somewhere to land. A seat held forever is a seat the rest of the team
        /// cannot fill, in a game with eight of them.
        /// </para>
        /// </summary>
        public const int SeatHeldSeconds = 45;

        /// <summary>
        /// How long to wait before attempt number <paramref name="attempt"/>, counting from 1.
        ///
        /// <para>
        /// Doubling and capped. An immediate retry into a network that just dropped is a retry
        /// that fails for the same reason, and four of them in a row is four failures in a
        /// second followed by a message the player cannot act on.
        /// </para>
        /// </summary>
        public static int BackoffSeconds(int attempt)
        {
            if (attempt <= 1)
                return FirstBackoffSeconds;

            int seconds = FirstBackoffSeconds;
            for (int i = 1; i < attempt && seconds < MaxBackoffSeconds; i++)
                seconds *= 2;

            return seconds > MaxBackoffSeconds ? MaxBackoffSeconds : seconds;
        }

        /// <summary>The whole schedule, for a test or a readout. Seconds from the drop.</summary>
        public static int TotalBackoffSeconds(int attempts)
        {
            int total = 0;
            for (int i = 1; i <= attempts; i++)
                total += BackoffSeconds(i);

            return total;
        }

        /// <summary>Whether another attempt is allowed after this many have been made.</summary>
        public static bool ShouldRetry(int attemptsMade) =>
            attemptsMade >= 0 && attemptsMade < MaxAttempts;

        /// <summary>Whether the host has stopped holding the seat.</summary>
        public static bool SeatExpired(int secondsSinceDrop) =>
            secondsSinceDrop >= SeatHeldSeconds;

        /// <summary>
        /// What a dropped peer should be doing now.
        ///
        /// <para>
        /// The seat is checked first. A player whose place is gone has nothing to reconnect
        /// to, and telling them "attempt 3 of 4" while the host has already filled their seat
        /// is a message that is wrong in a way they cannot see.
        /// </para>
        /// </summary>
        public static ReconnectState Next(int attemptsMade, int secondsSinceDrop)
        {
            if (SeatExpired(secondsSinceDrop))
                return ReconnectState.SeatLost;

            if (!ShouldRetry(attemptsMade))
                return ReconnectState.GaveUp;

            return secondsSinceDrop >= TotalBackoffSeconds(attemptsMade + 1)
                ? ReconnectState.Retrying
                : ReconnectState.Waiting;
        }

        /// <summary>Whether this state is one nothing more will happen from.</summary>
        public static bool IsTerminal(ReconnectState state) =>
            state == ReconnectState.GaveUp || state == ReconnectState.SeatLost;

        /// <summary>What the player is told. The two failures are deliberately not the same text.</summary>
        public static string Describe(ReconnectState state)
        {
            switch (state)
            {
                case ReconnectState.Connected: return "connected";
                case ReconnectState.Waiting: return "reconnecting";
                case ReconnectState.Retrying: return "reconnecting now";
                case ReconnectState.GaveUp: return "could not reconnect";
                case ReconnectState.SeatLost: return "the session carried on without you";
                default: return "unknown";
            }
        }
    }
}
