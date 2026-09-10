using System.Collections.Generic;
using UnityEngine;

namespace CatchIfYouCan.Player
{
    /// <summary>
    /// One player in the house - this machine's or somebody else's - and the registry of all
    /// of them.
    ///
    /// <para>
    /// <b>The ghost could only ever see the host.</b> Everything that needed to know where a
    /// player was asked <c>LocalPlayerService</c>, which holds exactly one <c>Root</c>: the
    /// player on this machine. That is correct in single player and silently wrong the moment
    /// there is a second one - the ghost would roam toward the host, hunt the host, and treat
    /// three other people as furniture. It is not a bug that appears when netcode is added; it
    /// is a bug that is already written down, waiting.
    /// </para>
    ///
    /// <para>
    /// So the ghost asks this instead. In single player the registry holds exactly one entry,
    /// the local player, and every answer is identical to what LocalPlayerService gave - which
    /// is what keeps single player working unchanged while making the multiplayer answer
    /// possible at all.
    /// </para>
    ///
    /// <para>
    /// <see cref="LocalPlayerService"/> keeps its job: it is the one that answers "which of
    /// these is mine", for the camera, the listener, the HUD and the input. This answers "who
    /// is here". They are different questions and conflating them is what produced the bug.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Catch If You Can/Player Presence")]
    public sealed class PlayerPresence : MonoBehaviour
    {
        /// <summary>The id a networking layer knows this player by. Negative means local-only.</summary>
        public const int LocalOnlyClientId =
            Procedural.Deterministic.MultiplayerProtocol.LocalOnlyClientId;

        [Tooltip("Where the ghost looks when it looks at this player: eye height, not the feet.")]
        [SerializeField] private Transform eyePoint;

        [Tooltip("Height above the root to aim at when there is no eye point, in metres.")]
        [SerializeField, Min(0f)] private float fallbackEyeHeight = 1.55f;

        // Sized from the contract rather than left to grow. A full session is small and known,
        // so the list never reallocates during play - and the capacity is derived, not a second
        // copy of the number.
        private static readonly List<PlayerPresence> Present =
            new List<PlayerPresence>(Procedural.Deterministic.MultiplayerProtocol.MaxPlayers);

        /// <summary>Everyone in the house. Read-only; do not hold it across frames.</summary>
        public static IReadOnlyList<PlayerPresence> All => Present;

        /// <summary>How many players are in the house. One, in single player.</summary>
        public static int Count => Present.Count;

        /// <summary>This machine's player, or null.</summary>
        public static PlayerPresence Local
        {
            get
            {
                for (int i = 0; i < Present.Count; i++)
                {
                    if (Present[i] != null && Present[i].IsLocal)
                        return Present[i];
                }

                return null;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => Present.Clear();

        /// <summary>Whether this is the player on this machine.</summary>
        public bool IsLocal { get; private set; } = true;

        /// <summary>The networking layer's id for this player, or <see cref="LocalOnlyClientId"/>.</summary>
        public int ClientId { get; private set; } = LocalOnlyClientId;

        /// <summary>
        /// Whether the ghost may target this player at all. A dead or hidden player is present
        /// and not a target; the distinction is the reason this is a flag and not a null check.
        /// </summary>
        public bool IsTargetable { get; set; } = true;

        /// <summary>Where the ghost aims. Eye height, because feet are not where a face is.</summary>
        public Vector3 EyePosition =>
            eyePoint != null ? eyePoint.position : transform.position + Vector3.up * fallbackEyeHeight;

        /// <summary>
        /// Says whose this is. Called by the spawner: local for the player this machine drives,
        /// remote for everyone else, with the id a networking layer assigned.
        /// </summary>
        public void Bind(bool isLocal, int clientId)
        {
            // An offline session has one player and that player is this machine's. A remote
            // presence there is not a small inconsistency - it is a peer in a session that has
            // no peers, and everything downstream that asks "who is here" would believe it.
            //
            // The capacity check in OnEnable catches a SECOND player; it cannot catch a single
            // one that arrives already marked remote, because one is within capacity. This is
            // the check for that case.
            if (!isLocal &&
                !Session.SessionModeRules.AllowsRemotePlayers(
                    Session.MultiplayerSessionService.Mode))
            {
                Core.CIYCLog.Error(
                    "Refused to bind '" + name + "' as a remote player: the session is " +
                    Session.MultiplayerSessionService.Mode +
                    ", which has no remote players. Something is spawning peers offline.");

                // Removed rather than downgraded to local. Quietly promoting somebody else's
                // character into this machine's player would hand it the camera and the input.
                Present.Remove(this);
                return;
            }

            IsLocal = isLocal;
            ClientId = clientId;
        }

        /// <summary>Sets the eye transform, so nothing has to reach in and find it.</summary>
        public void SetEyePoint(Transform eye)
        {
            if (eye != null)
                eyePoint = eye;
        }

        /// <summary>
        /// Joins the registry, once.
        ///
        /// <para>
        /// The Contains guard is what stops a re-enabled player being counted twice. Population
        /// is what capacity is checked against, so a duplicate entry is a session that reports
        /// 9 of 8 and starts refusing legitimate peers - and the count would be wrong in a way
        /// nothing else in the game would notice.
        /// </para>
        ///
        /// <para>
        /// Registering more than the session's mode permits is refused and said out loud. It
        /// cannot happen through the normal spawn path; if it does, something is spawning
        /// players it should not, and a quiet extra entry would hide that.
        /// </para>
        /// </summary>
        private void OnEnable()
        {
            if (Present.Contains(this))
                return;

            int capacity = Session.SessionModeRules.MaxPlayers(
                Session.MultiplayerSessionService.Mode);

            if (Present.Count >= capacity)
            {
                Core.CIYCLog.Error(
                    "Refused to register '" + name + "': the session already holds " +
                    Present.Count + " of " + capacity + " players in " +
                    Session.MultiplayerSessionService.Mode + " mode. Something is spawning " +
                    "players the session cannot hold.");
                return;
            }

            Present.Add(this);
        }

        /// <summary>
        /// Leaves the registry, freeing the seat.
        ///
        /// <para>
        /// Capacity has no memory - it is a function of the current population - so a departure
        /// makes room immediately and the seat is genuinely reusable by the next peer.
        /// </para>
        /// </summary>
        private void OnDisable() => Present.Remove(this);

        /// <summary>
        /// The nearest targetable player to a point, or null when nobody is.
        ///
        /// <para>
        /// Nearest rather than "the local one" is the whole point: it is the same answer in
        /// single player and the right answer with four people in the house.
        /// </para>
        /// </summary>
        public static PlayerPresence Nearest(Vector3 to)
        {
            PlayerPresence best = null;
            float bestSqr = float.MaxValue;

            for (int i = 0; i < Present.Count; i++)
            {
                var candidate = Present[i];
                if (candidate == null || !candidate.IsTargetable)
                    continue;

                float sqr = (candidate.transform.position - to).sqrMagnitude;
                if (sqr >= bestSqr)
                    continue;

                bestSqr = sqr;
                best = candidate;
            }

            return best;
        }

        /// <summary>
        /// One targetable player chosen at random, or null.
        ///
        /// <para>
        /// For the ghost's own decisions, so that a hunt does not always go for whoever
        /// happens to be closest. Host-only by construction: the caller is inside a
        /// <c>SessionAuthority.CanSimulateGhost</c> gate, which is what stops four machines
        /// each rolling a different victim.
        /// </para>
        /// </summary>
        public static PlayerPresence RandomTargetable()
        {
            int targetable = 0;
            for (int i = 0; i < Present.Count; i++)
            {
                if (Present[i] != null && Present[i].IsTargetable)
                    targetable++;
            }

            if (targetable == 0)
                return null;

            int pick = Random.Range(0, targetable);
            for (int i = 0; i < Present.Count; i++)
            {
                var candidate = Present[i];
                if (candidate == null || !candidate.IsTargetable)
                    continue;

                if (pick-- == 0)
                    return candidate;
            }

            return null;
        }
    }
}
