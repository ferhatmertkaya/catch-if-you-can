using UnityEngine;

namespace CatchIfYouCan.Session
{
    /// <summary>Why a request was refused, or that it was not.</summary>
    public enum RequestVerdict
    {
        /// <summary>Accepted, and carried out.</summary>
        Accepted = 0,

        /// <summary>The subject was gone by the time the authority looked.</summary>
        NoSubject,

        /// <summary>The requester was not close enough. Checked by the authority, not the asker.</summary>
        OutOfRange,

        /// <summary>The subject was not in a state this request makes sense for.</summary>
        WrongState,

        /// <summary>Somebody else got there first.</summary>
        AlreadyTaken,

        /// <summary>This process may not decide. A client that reaches this has a routing bug.</summary>
        NotAuthoritative,
    }

    /// <summary>
    /// The one place a player's intent becomes a change to the world.
    ///
    /// <para>
    /// <b>A request is not a command.</b> A client says "I want to pick that up" and the
    /// authority decides whether it can - because two players reaching for the same torch on
    /// the same frame is not an edge case, it is Tuesday, and the only way one of them loses
    /// is if exactly one machine decides.
    /// </para>
    ///
    /// <para>
    /// Every check here is done by the authority against the world it can see, never by the
    /// asker against the world it thinks it sees. Distance especially: a client that measures
    /// its own reach is a client that can be told to measure generously.
    /// </para>
    ///
    /// <para>
    /// Transport-neutral. Today the authority is this process, so a request is validated and
    /// carried out in the same call and single player behaves exactly as it always has. When
    /// there is a network, the routing changes here - the checks and their order do not.
    /// </para>
    /// </summary>
    public static class AuthorityRequests
    {
        /// <summary>
        /// How far a player may reach, in metres. One number, checked in one place.
        ///
        /// <para>
        /// Deliberately a little more than the interaction controller's own reach, because the
        /// authority is checking a position that travelled and is a tick or two stale. A hard
        /// equality here would refuse legitimate requests from anyone with a connection.
        /// </para>
        /// </summary>
        public const float MaxReachMetres = 3.5f;

        /// <summary>
        /// Whether this process may decide. Single player: always.
        /// </summary>
        public static bool CanDecide => Core.SessionAuthority.IsHost;

        /// <summary>
        /// Validates a reach from a requester to a subject, at the authority.
        ///
        /// <para>
        /// Both transforms are read here rather than trusting a distance the caller computed,
        /// which is the difference between a check and a formality.
        /// </para>
        /// </summary>
        public static RequestVerdict ValidateReach(Transform requester, Transform subject)
        {
            if (!CanDecide)
                return RequestVerdict.NotAuthoritative;

            if (requester == null || subject == null)
                return RequestVerdict.NoSubject;

            return Vector3.Distance(requester.position, subject.position) <= MaxReachMetres
                ? RequestVerdict.Accepted
                : RequestVerdict.OutOfRange;
        }

        /// <summary>Whether a verdict allows the change to happen.</summary>
        public static bool Allows(RequestVerdict verdict) => verdict == RequestVerdict.Accepted;

        /// <summary>Text for a log or the network lab. Not shown to a player.</summary>
        public static string Describe(RequestVerdict verdict)
        {
            switch (verdict)
            {
                case RequestVerdict.Accepted: return "accepted";
                case RequestVerdict.NoSubject: return "the subject was gone";
                case RequestVerdict.OutOfRange: return "out of reach";
                case RequestVerdict.WrongState: return "wrong state for this request";
                case RequestVerdict.AlreadyTaken: return "somebody else got there first";
                case RequestVerdict.NotAuthoritative: return "this process does not decide";
                default: return "refused";
            }
        }
    }
}
