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
        public const int Version = 1;

        /// <summary>Maximum players in one session, including the host (NETWORKING.md §2).</summary>
        public const int MaxPlayers = 4;

        /// <summary>A session is viable with the host alone.</summary>
        public const int MinPlayers = 1;

        /// <summary>Server simulation tick, in Hz (NETWORKING.md §2).</summary>
        public const int ServerTickHz = 20;

        /// <summary>True when one more peer fits, given how many are already admitted.</summary>
        public static bool HasCapacityFor(int currentPlayerCount) =>
            currentPlayerCount >= 0 && currentPlayerCount < MaxPlayers;
    }
}
