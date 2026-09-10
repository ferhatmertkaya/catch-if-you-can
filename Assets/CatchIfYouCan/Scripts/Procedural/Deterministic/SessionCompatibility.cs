namespace CatchIfYouCan.Procedural.Deterministic
{
    /// <summary>
    /// Why a peer was refused, or admitted. Distinguishing these is the point: collapsing
    /// every failure into "Connection failed" makes a content-revision mismatch and a full
    /// lobby indistinguishable in the field.
    /// </summary>
    public enum JoinVerdict
    {
        Admit = 0,

        /// <summary>Different handshake revision. Nothing else can be trusted to mean the same thing.</summary>
        ProtocolMismatch,

        /// <summary>Different generation algorithm. The same seed would produce different houses.</summary>
        GenerationVersionMismatch,

        /// <summary>Different baked content revision.</summary>
        ContentMismatch,

        /// <summary>Different map definition.</summary>
        MapMismatch,

        /// <summary>Config carries no host-rolled seed.</summary>
        SeedMissing,

        /// <summary>Config is structurally incomplete.</summary>
        MalformedConfig,

        /// <summary>Session is at <see cref="MultiplayerProtocol.MaxPlayers"/>.</summary>
        LobbyFull,
    }

    /// <summary>Outcome of comparing two independently generated layouts.</summary>
    public enum LayoutVerdict
    {
        Match = 0,
        Mismatch,
    }

    /// <summary>
    /// The join handshake and mismatch protocol of Docs/NETWORKING.md §3 and §5, with no
    /// transport in it.
    ///
    /// Keeping this transport-neutral is deliberate. It is the part of multiplayer that is
    /// most expensive to get wrong and cheapest to test, and it must not have to be rewritten
    /// when the netcode package is chosen. The standalone harness executes it without Unity.
    ///
    /// The two-stage shape is normative, not stylistic. §5 requires that a content mismatch
    /// aborts <em>before</em> generating, and explicitly forbids falling through to a layout
    /// compare afterwards: the layout compare would also fail, and would report "could not
    /// sync the house layout" for what is really a version mismatch, sending the reader
    /// hunting a generator bug that does not exist.
    /// </summary>
    public static class SessionCompatibility
    {
        /// <summary>
        /// Stage one, run on the host before anyone generates anything.
        ///
        /// Order matters. Protocol is checked first because a peer that disagrees about the
        /// handshake layout cannot be assumed to mean the same thing by any later field.
        /// </summary>
        public static JoinVerdict CheckJoin(in MatchConfig authority, in MatchConfig peer, int currentPlayerCount)
        {
            if (!MultiplayerProtocol.HasCapacityFor(currentPlayerCount))
                return JoinVerdict.LobbyFull;

            if (peer.ProtocolVersion != authority.ProtocolVersion)
                return JoinVerdict.ProtocolMismatch;

            if (!authority.IsWellFormed || !peer.IsWellFormed)
                return !authority.HasSeed || !peer.HasSeed
                    ? JoinVerdict.SeedMissing
                    : JoinVerdict.MalformedConfig;

            if (peer.GenerationVersion != authority.GenerationVersion)
                return JoinVerdict.GenerationVersionMismatch;

            if (!string.Equals(peer.MapDefinitionId, authority.MapDefinitionId, System.StringComparison.Ordinal))
                return JoinVerdict.MapMismatch;

            if (peer.ContentHash != authority.ContentHash)
                return JoinVerdict.ContentMismatch;

            return JoinVerdict.Admit;
        }

        /// <summary>
        /// Stage two, run once both peers have generated. Only legal after
        /// <see cref="CheckJoin"/> returned <see cref="JoinVerdict.Admit"/> — see the type
        /// remarks for why running it earlier produces a misleading diagnosis.
        ///
        /// <paramref name="diagnostic"/> names the first differing section, which is the
        /// difference between a five-minute fix and a week of bisecting.
        /// </summary>
        public static LayoutVerdict CheckLayout(in LayoutHash authority, in LayoutHash peer, out string diagnostic)
        {
            if (authority.FinalHash == peer.FinalHash)
            {
                diagnostic = null;
                return LayoutVerdict.Match;
            }

            diagnostic = authority.DescribeDifference(peer);
            return LayoutVerdict.Mismatch;
        }

        /// <summary>True when the verdict permits the peer into the session.</summary>
        public static bool IsAdmitted(JoinVerdict verdict) => verdict == JoinVerdict.Admit;

        /// <summary>
        /// Whether this verdict means the peer should never have started generating.
        /// A caller that gets <c>true</c> must abort before generation, not after.
        /// </summary>
        public static bool AbortsBeforeGeneration(JoinVerdict verdict) =>
            verdict != JoinVerdict.Admit;

        /// <summary>
        /// User-facing text. Kept deliberately vague where the cause is not the player's
        /// business, while the verdict enum and the logs carry the precise cause
        /// (NETWORKING.md §5).
        /// </summary>
        public static string Describe(JoinVerdict verdict)
        {
            switch (verdict)
            {
                case JoinVerdict.Admit:
                    return "Connected.";
                case JoinVerdict.LobbyFull:
                    return "This session is full.";
                case JoinVerdict.ProtocolMismatch:
                case JoinVerdict.GenerationVersionMismatch:
                case JoinVerdict.ContentMismatch:
                case JoinVerdict.MapMismatch:
                    return "This session is running a different game version.";
                case JoinVerdict.SeedMissing:
                case JoinVerdict.MalformedConfig:
                    return "This session could not be joined.";
                default:
                    return "This session could not be joined.";
            }
        }

        /// <summary>
        /// User-facing text for a layout divergence. This one is a generator bug rather than
        /// a network fault, and §5 requires it be reported as one, with the per-section
        /// breakdown from <see cref="LayoutHash.ToReport"/> attached.
        /// </summary>
        public static string DescribeLayoutMismatch() => "Could not sync the house layout.";
    }
}
