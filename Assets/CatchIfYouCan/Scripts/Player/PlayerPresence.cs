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
        public const int LocalOnlyClientId = -1;

        [Tooltip("Where the ghost looks when it looks at this player: eye height, not the feet.")]
        [SerializeField] private Transform eyePoint;

        [Tooltip("Height above the root to aim at when there is no eye point, in metres.")]
        [SerializeField, Min(0f)] private float fallbackEyeHeight = 1.55f;

        private static readonly List<PlayerPresence> Present = new List<PlayerPresence>();

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
            IsLocal = isLocal;
            ClientId = clientId;
        }

        /// <summary>Sets the eye transform, so nothing has to reach in and find it.</summary>
        public void SetEyePoint(Transform eye)
        {
            if (eye != null)
                eyePoint = eye;
        }

        private void OnEnable()
        {
            if (!Present.Contains(this))
                Present.Add(this);
        }

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
