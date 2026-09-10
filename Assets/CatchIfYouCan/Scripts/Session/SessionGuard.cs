using CatchIfYouCan.Procedural.Deterministic;

namespace CatchIfYouCan.Session
{
    /// <summary>What the host decided about one peer, and what to tell everybody.</summary>
    public readonly struct JoinDecision
    {
        /// <summary>Whether the connection is approved.</summary>
        public readonly bool Approved;

        /// <summary>The precise cause, for logs and the network lab.</summary>
        public readonly JoinVerdict Verdict;

        /// <summary>What the rejected player is shown. Deliberately vaguer than the verdict.</summary>
        public readonly string PlayerFacingReason;

        public JoinDecision(bool approved, JoinVerdict verdict, string playerFacingReason)
        {
            Approved = approved;
            Verdict = verdict;
            PlayerFacingReason = playerFacingReason;
        }
    }

    /// <summary>
    /// The host's side of the join handshake: decode what arrived, compare it, decide.
    ///
    /// <para>
    /// The comparison itself is <see cref="SessionCompatibility.CheckJoin"/>, which lives in
    /// the deterministic assembly and has no transport in it. This is the adapter around it -
    /// the part that handles bytes from a machine nobody has admitted yet - and it is
    /// deliberately the only thing between the wire and that pure function.
    /// </para>
    ///
    /// <para>
    /// Every <see cref="JoinVerdict"/> maps to an approval result and a readable reason, which
    /// is the acceptance criterion for this phase. The verdict is precise and goes in the log;
    /// the player sees something vaguer, because which of four version fields disagreed is not
    /// their business and telling them is telling anyone who asks.
    /// </para>
    /// </summary>
    public static class SessionGuard
    {
        /// <summary>
        /// Decides on a peer from its raw connection payload.
        ///
        /// <para>
        /// A payload this cannot decode is <see cref="JoinVerdict.MalformedConfig"/> and
        /// nothing more is attempted with it. A peer that sends bytes the codec rejects has
        /// already failed the only test that matters at this stage, and looking closer at a
        /// hostile payload is how a host gets hurt.
        /// </para>
        /// </summary>
        public static JoinDecision Evaluate(in MatchConfig authority, byte[] peerPayload,
                                            int currentPlayerCount)
        {
            if (!JoinPayloadCodec.TryDecode(peerPayload, out MatchConfig peer))
                return Decide(JoinVerdict.MalformedConfig);

            return Evaluate(authority, peer, currentPlayerCount);
        }

        /// <summary>The same, with an already-decoded config. For the lab and for tests.</summary>
        public static JoinDecision Evaluate(in MatchConfig authority, in MatchConfig peer,
                                            int currentPlayerCount)
        {
            return Decide(SessionCompatibility.CheckJoin(authority, peer, currentPlayerCount));
        }

        /// <summary>
        /// The verdict as a decision. Kept separate so the mapping is one place, and so the
        /// lab can ask for the decision belonging to a verdict it wants to demonstrate.
        /// </summary>
        public static JoinDecision Decide(JoinVerdict verdict)
        {
            return new JoinDecision(
                SessionCompatibility.IsAdmitted(verdict),
                verdict,
                SessionCompatibility.Describe(verdict));
        }

        /// <summary>
        /// Whether this verdict means the peer must not begin generating.
        ///
        /// <para>
        /// NETWORKING.md §5 requires a content mismatch to abort <em>before</em> generation and
        /// forbids falling through to the layout compare afterwards - the layout compare would
        /// also fail, and would report "could not sync the house layout" for what is really a
        /// version mismatch, sending the reader hunting a generator bug that does not exist.
        /// </para>
        /// </summary>
        public static bool MustAbortBeforeGeneration(JoinVerdict verdict) =>
            SessionCompatibility.AbortsBeforeGeneration(verdict);
    }
}
