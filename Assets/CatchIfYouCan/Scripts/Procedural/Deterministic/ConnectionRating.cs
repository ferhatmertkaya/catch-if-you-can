namespace CatchIfYouCan.Procedural.Deterministic
{
    /// <summary>
    /// How a connection is doing, as the few words a player is shown.
    ///
    /// <para>
    /// <see cref="Unknown"/> and <see cref="NotApplicable"/> are the two that matter most, and
    /// they are different: one means nobody has measured yet, the other means there is nothing
    /// to measure because this is single player. Collapsing either into a number would put a
    /// confident "0 ms" on screen for a game that has never sent a packet.
    /// </para>
    /// </summary>
    public enum ConnectionQuality
    {
        /// <summary>Online, but nothing has measured yet. Not the same as good.</summary>
        Unknown = 0,

        /// <summary>Offline solo. There is no connection, so there is no latency.</summary>
        NotApplicable,

        Good,
        Fair,
        Poor,

        /// <summary>Measured, and beyond what the session can work with.</summary>
        Lost,
    }

    /// <summary>
    /// Turns a measured round trip into the word a player is shown.
    ///
    /// <para>
    /// The bands are here rather than in a HUD because two readouts with two sets of
    /// thresholds are two answers, and the first one edited is the one that disagrees. Pure,
    /// so they are exercised by the offline harness.
    /// </para>
    ///
    /// <para>
    /// Chosen for a co-operative game where nothing is contested frame by frame. Nobody is
    /// duelling; what latency costs here is a door opening late, and the bands are generous
    /// accordingly. They are not the bands a shooter would pick.
    /// </para>
    /// </summary>
    public static class ConnectionRating
    {
        /// <summary>What a measurement that has not happened looks like. Never a real reading.</summary>
        public const int NoMeasurement = -1;

        /// <summary>At or under this, nothing is noticeable. Milliseconds.</summary>
        public const int GoodUpToMs = 90;

        /// <summary>At or under this, playable. Milliseconds.</summary>
        public const int FairUpToMs = 200;

        /// <summary>At or under this, unpleasant but a session. Beyond it, treated as lost.</summary>
        public const int PoorUpToMs = 600;

        /// <summary>
        /// The band a measurement falls in.
        ///
        /// <para>
        /// A negative reading is <see cref="ConnectionQuality.Unknown"/>, not zero and not
        /// good: the one thing a diagnostic must never do is report health it did not measure.
        /// </para>
        /// </summary>
        public static ConnectionQuality Rate(int roundTripMs)
        {
            if (roundTripMs < 0)
                return ConnectionQuality.Unknown;

            if (roundTripMs <= GoodUpToMs)
                return ConnectionQuality.Good;

            if (roundTripMs <= FairUpToMs)
                return ConnectionQuality.Fair;

            if (roundTripMs <= PoorUpToMs)
                return ConnectionQuality.Poor;

            return ConnectionQuality.Lost;
        }

        /// <summary>Whether this quality came from an actual measurement.</summary>
        public static bool IsMeasured(ConnectionQuality quality) =>
            quality != ConnectionQuality.Unknown && quality != ConnectionQuality.NotApplicable;

        /// <summary>Whether a session in this state can still be played.</summary>
        public static bool IsPlayable(ConnectionQuality quality) =>
            quality != ConnectionQuality.Lost;

        /// <summary>What a player is shown. Short, because it sits in a corner of the screen.</summary>
        public static string Describe(ConnectionQuality quality)
        {
            switch (quality)
            {
                case ConnectionQuality.Unknown: return "measuring";
                case ConnectionQuality.NotApplicable: return "offline";
                case ConnectionQuality.Good: return "good";
                case ConnectionQuality.Fair: return "fair";
                case ConnectionQuality.Poor: return "poor";
                case ConnectionQuality.Lost: return "lost";
                default: return "unknown";
            }
        }
    }
}
