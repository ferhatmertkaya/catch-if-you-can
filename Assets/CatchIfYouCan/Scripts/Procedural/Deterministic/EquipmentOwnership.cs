namespace CatchIfYouCan.Procedural.Deterministic
{
    /// <summary>Where a piece of equipment is, as far as ownership is concerned.</summary>
    public enum EquipmentHold
    {
        /// <summary>On the floor, dropped or never picked up. Anybody in reach may take it.</summary>
        InWorld = 0,

        /// <summary>In somebody's inventory. Theirs until they put it down.</summary>
        Carried,

        /// <summary>
        /// Set down deliberately - a tripod camera, a salt pile, a relic on a table. It
        /// remembers who placed it and anybody in reach may still pick it up, because a team
        /// that cannot move a camera one player left in the wrong room is a team arguing in
        /// voice chat instead of playing.
        /// </summary>
        Placed,
    }

    /// <summary>What happened to a claim on a piece of equipment.</summary>
    public enum EquipmentClaimVerdict
    {
        /// <summary>Taken. The claimant now carries it.</summary>
        Granted = 0,

        /// <summary>The claimant already carries it. Nothing changed, and nothing is wrong.</summary>
        AlreadyYours,

        /// <summary>Somebody else is carrying it. The one refusal that is a real contest.</summary>
        CarriedBySomebodyElse,

        /// <summary>The claim named nobody. A routing bug, not a player losing a race.</summary>
        InvalidClaimant,

        /// <summary>This process does not decide who owns things. A client that reaches this has a bug.</summary>
        NotAuthoritative,
    }

    /// <summary>
    /// Who is holding what, and who may act on it.
    ///
    /// <para>
    /// The project had no answer to "whose torch is that". An item knew it was equipped and
    /// knew which transform it was parented to, which is enough with one player and is exactly
    /// the shape of the mistake this repository keeps making: correct in single player and
    /// silently wrong with a second one. <c>AlreadyTaken</c> existed as a refusal reason with
    /// nothing to check it against.
    /// </para>
    ///
    /// <para>
    /// <b>Two players reaching for the same torch on the same frame is not an edge case.</b>
    /// The only way one of them loses is if exactly one machine decides, and this is what that
    /// machine consults. Pure, so it is exercised by the offline harness rather than by
    /// arranging two people and a torch.
    /// </para>
    /// </summary>
    public static class EquipmentOwnership
    {
        /// <summary>Nobody owns it. The same value everywhere: <see cref="MultiplayerProtocol.NoClientId"/>.</summary>
        public const int Nobody = MultiplayerProtocol.NoClientId;

        /// <summary>Whether anybody owns this at all.</summary>
        public static bool IsOwned(int ownerClientId) => ownerClientId != Nobody;

        /// <summary>Whether this exact player is carrying it.</summary>
        public static bool IsCarriedBy(EquipmentHold hold, int ownerClientId, int clientId) =>
            hold == EquipmentHold.Carried &&
            MultiplayerProtocol.IsPlayer(clientId) &&
            ownerClientId == clientId;

        /// <summary>
        /// What the authority answers when somebody reaches for this.
        ///
        /// <para>
        /// Reach is not checked here and deliberately so: how far away the claimant is depends
        /// on positions and on a tick of latency, and it belongs with the other spatial checks
        /// in <c>Session.AuthorityRequests</c>. This answers the question that is only about
        /// ownership, which is the half that a pure test can hold still.
        /// </para>
        /// </summary>
        public static EquipmentClaimVerdict Claim(EquipmentHold hold, int ownerClientId,
                                                  int claimantClientId)
        {
            if (!MultiplayerProtocol.IsPlayer(claimantClientId))
                return EquipmentClaimVerdict.InvalidClaimant;

            if (hold != EquipmentHold.Carried)
                return EquipmentClaimVerdict.Granted;

            return ownerClientId == claimantClientId
                ? EquipmentClaimVerdict.AlreadyYours
                : EquipmentClaimVerdict.CarriedBySomebodyElse;
        }

        /// <summary>Whether a verdict means the claimant ends up holding it.</summary>
        public static bool Holds(EquipmentClaimVerdict verdict) =>
            verdict == EquipmentClaimVerdict.Granted ||
            verdict == EquipmentClaimVerdict.AlreadyYours;

        /// <summary>Whether a verdict changed anything, which is what a host needs to broadcast.</summary>
        public static bool ChangesOwner(EquipmentClaimVerdict verdict) =>
            verdict == EquipmentClaimVerdict.Granted;

        /// <summary>
        /// Whether this player may use it.
        ///
        /// <para>
        /// A carried item answers to its carrier and to nobody else - pressing the button on
        /// somebody else's thermometer is not a thing that should be possible even by
        /// accident. An item on the floor or set down on a table is not anybody's, so use is
        /// a question of reach rather than of ownership, and reach is checked elsewhere.
        /// </para>
        /// </summary>
        public static bool MayUse(EquipmentHold hold, int ownerClientId, int clientId)
        {
            if (!MultiplayerProtocol.IsPlayer(clientId))
                return false;

            if (hold != EquipmentHold.Carried)
                return true;

            return ownerClientId == clientId;
        }

        /// <summary>Why, for a log or the network lab. Not shown to a player.</summary>
        public static string Describe(EquipmentClaimVerdict verdict)
        {
            switch (verdict)
            {
                case EquipmentClaimVerdict.Granted:
                    return "granted";
                case EquipmentClaimVerdict.AlreadyYours:
                    return "already carried by the claimant";
                case EquipmentClaimVerdict.CarriedBySomebodyElse:
                    return "somebody else is carrying it";
                case EquipmentClaimVerdict.InvalidClaimant:
                    return "the claim named nobody";
                case EquipmentClaimVerdict.NotAuthoritative:
                    return "this process does not decide ownership";
                default:
                    return "refused";
            }
        }
    }
}
