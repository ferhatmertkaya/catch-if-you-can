namespace CatchIfYouCan.Procedural.Deterministic
{
    /// <summary>
    /// The single authoritative source for the multiplayer constants that both the session
    /// layer and the UI must agree on.
    ///
    /// These lived only in <c>Docs/NETWORKING.md</c> §2 prose until now, which meant the UI
    /// had nothing to derive a capacity from and connection approval had nothing to check
    /// against. Anything that needs a player count reads it from here; nothing re-declares it.
    ///
    /// This type is transport-neutral on purpose. It states what the protocol agrees on, not
    /// how bytes reach the wire, so choosing or replacing the netcode package does not
    /// invalidate it.
    /// </summary>
    public static class MultiplayerProtocol
    {
        /// <summary>
        /// Wire/protocol revision. Bump whenever the meaning or layout of anything exchanged
        /// during the join handshake changes, including <see cref="MatchConfig"/>.
        ///
        /// This is deliberately not the application version: gameplay compatibility can break
        /// without a marketing version change, and a marketing version can change without
        /// breaking compatibility.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>1 -> 2 at V4.1</b>, because <see cref="MaxPlayers"/> went from four to eight and
        /// that is a compatibility change, not a tuning one. <see cref="SessionCompatibility"/>
        /// evaluates capacity with the constant compiled into whichever build is doing the
        /// evaluating, so a four-max host and an eight-max client disagree about when a session
        /// is full: the client would show room for eight and the host would refuse the fifth
        /// peer with no explanation the client could give. Protocol is checked before anything
        /// else in the handshake, so with this bump the two builds refuse each other cleanly and
        /// say why.
        /// </para>
        /// </remarks>
        public const int Version = 2;

        /// <summary>
        /// Maximum players in one online session, <b>including the host</b>.
        ///
        /// <para>
        /// Eight means one host plus up to seven clients, not a host plus eight. The host
        /// occupies one of these places, which is why <see cref="HasCapacityFor"/> takes the
        /// current population rather than a client count.
        /// </para>
        ///
        /// <para>
        /// This is the only place the number lives. Lobby capacity, relay allocation,
        /// connection approval, the development lab's spawn pads and any player-count UI all
        /// derive from it - a second constant is a second answer, and the two disagree the
        /// first time one of them is edited.
        /// </para>
        /// </summary>
        public const int MaxPlayers = 8;

        /// <summary>
        /// A session is viable with the host alone.
        ///
        /// <para>
        /// One is deliberate and is not the same as the co-op design target. An online host
        /// may create a session and sit in it at 1/8 - waiting for friends, setting up a
        /// private lobby, or testing. What matchmaking chooses to advertise as a good co-op
        /// size is a separate question from what the session contract permits.
        /// </para>
        /// </summary>
        public const int MinPlayers = 1;

        /// <summary>Server simulation tick, in Hz (NETWORKING.md §2).</summary>
        public const int ServerTickHz = 20;

        /// <summary>
        /// True when one more peer fits, given how many are already admitted.
        ///
        /// <para>
        /// The current population includes the host, so a session at
        /// <see cref="MaxPlayers"/> has no room. A negative count is refused rather than
        /// clamped: it cannot arise from counting real players, so it means the caller is
        /// confused, and quietly treating -1 as "plenty of room" would admit peers into a
        /// session nobody can describe.
        /// </para>
        /// </summary>
        public static bool HasCapacityFor(int currentPlayerCount) =>
            currentPlayerCount >= 0 && currentPlayerCount < MaxPlayers;
    }
}
