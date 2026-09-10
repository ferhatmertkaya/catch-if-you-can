namespace CatchIfYouCan.Session
{
    /// <summary>
    /// Which of the two products the player chose. Decided once, then fixed.
    ///
    /// <para>
    /// <b>This is chosen, never inferred.</b> Not from the player count, not from whether a
    /// NetworkManager exists, not from Relay or Lobby or Authentication state, not from the
    /// scene name, not from the platform, and above all not from whether the device currently
    /// has a working internet connection.
    /// </para>
    ///
    /// <para>
    /// The reason is a specific failure this contract exists to prevent. If mode is inferred
    /// from connectivity, then a solo player whose Wi-Fi drops mid-mission has their session
    /// silently reclassified, and a player who chose online and lost their connection is
    /// quietly told they are playing single player instead of being told the truth. Both are
    /// worse than an error message. So connectivity appearing does not turn Offline into
    /// Online, connectivity disappearing does not turn Online into Offline, and an online
    /// setup that fails reports an online failure.
    /// </para>
    ///
    /// <para>
    /// Distinct from <see cref="SessionState"/> and <see cref="SessionRole"/>, which describe
    /// what is happening rather than what was chosen. An online session that has not connected
    /// yet is Online mode in Connecting state; an online session that failed is Online mode in
    /// Failed state, and it is emphatically not offline.
    /// </para>
    /// </summary>
    public enum SessionMode
    {
        /// <summary>
        /// One local player, and no dependency on anything outside the device.
        ///
        /// <para>
        /// No Authentication, no Lobby, no Relay, no transport, no backend, no account. The
        /// whole mission loop - boot, menu, lobby, loadout, mission, result, progression - has
        /// to work in airplane mode, and that is a product requirement rather than a nice
        /// property.
        /// </para>
        /// </summary>
        Offline = 0,

        /// <summary>
        /// One to <see cref="Procedural.Deterministic.MultiplayerProtocol.MaxPlayers"/> players,
        /// one of whom is the host.
        ///
        /// <para>
        /// A host alone at 1/8 is a valid online session, not a degenerate one - it is what
        /// waiting for friends looks like.
        /// </para>
        /// </summary>
        Online,
    }

    /// <summary>What the mode permits, asked rather than assumed at each call site.</summary>
    public static class SessionModeRules
    {
        /// <summary>
        /// Whether this mode may touch Authentication, Lobby, Relay or a transport.
        ///
        /// <para>
        /// False offline, and that is the whole point: offline gameplay must not fail because
        /// Wi-Fi is off, mobile data is off, or Unity's services are unreachable. Anything that
        /// would initialise an online service asks this first.
        /// </para>
        /// </summary>
        public static bool AllowsOnlineServices(SessionMode mode) => mode == SessionMode.Online;

        /// <summary>Whether a peer that is not this machine's player can exist in this mode.</summary>
        public static bool AllowsRemotePlayers(SessionMode mode) => mode == SessionMode.Online;

        /// <summary>
        /// The most players this mode permits. One offline; the protocol's maximum online.
        ///
        /// <para>
        /// Derived, never restated. The eight lives in
        /// <see cref="Procedural.Deterministic.MultiplayerProtocol.MaxPlayers"/> and nowhere
        /// else.
        /// </para>
        /// </summary>
        public static int MaxPlayers(SessionMode mode) =>
            mode == SessionMode.Online
                ? Procedural.Deterministic.MultiplayerProtocol.MaxPlayers
                : 1;

        /// <summary>The fewest players a session in this mode is viable with. One, either way.</summary>
        public static int MinPlayers(SessionMode mode) =>
            mode == SessionMode.Online
                ? Procedural.Deterministic.MultiplayerProtocol.MinPlayers
                : 1;

        /// <summary>Whether a population is legal for this mode.</summary>
        public static bool IsValidPopulation(SessionMode mode, int playerCount) =>
            playerCount >= MinPlayers(mode) && playerCount <= MaxPlayers(mode);
    }
}
