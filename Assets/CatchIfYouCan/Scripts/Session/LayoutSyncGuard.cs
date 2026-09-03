using CatchIfYouCan.Procedural.Deterministic;

namespace CatchIfYouCan.Session
{
    /// <summary>The result of comparing two independently generated houses.</summary>
    public readonly struct LayoutSyncResult
    {
        public readonly bool Matches;
        public readonly LayoutVerdict Verdict;

        /// <summary>Which section first differed. Null on a match.</summary>
        public readonly string Diagnostic;

        /// <summary>What the player is shown. This one really is a generator bug.</summary>
        public readonly string PlayerFacingReason;

        public LayoutSyncResult(bool matches, LayoutVerdict verdict, string diagnostic,
                                string playerFacingReason)
        {
            Matches = matches;
            Verdict = verdict;
            Diagnostic = diagnostic;
            PlayerFacingReason = playerFacingReason;
        }
    }

    /// <summary>
    /// Stage two of the handshake: both peers have generated, so compare what they built.
    ///
    /// <para>
    /// Only legal after <see cref="SessionGuard"/> admitted the peer. Running it earlier
    /// produces a misleading diagnosis - see NETWORKING.md §5 and the remarks on
    /// <see cref="SessionCompatibility"/>.
    /// </para>
    ///
    /// <para>
    /// A mismatch here is not a network fault and must not be reported as one. Both machines
    /// agreed on the protocol, the generation version, the map and the content, and then built
    /// different houses from the same seed: that is a determinism violation, and the
    /// per-section diagnostic is the difference between a five-minute fix and a week of
    /// bisecting.
    /// </para>
    /// </summary>
    public static class LayoutSyncGuard
    {
        public static LayoutSyncResult Compare(in LayoutHash authority, in LayoutHash peer)
        {
            var verdict = SessionCompatibility.CheckLayout(authority, peer, out string diagnostic);

            return new LayoutSyncResult(
                verdict == LayoutVerdict.Match,
                verdict,
                diagnostic,
                verdict == LayoutVerdict.Match
                    ? null
                    : SessionCompatibility.DescribeLayoutMismatch());
        }
    }
}
