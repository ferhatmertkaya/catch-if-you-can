using UnityEngine;

namespace CatchIfYouCan.Ghost
{
    /// <summary>
    /// Draws the host's ghost on this machine, from replicated state.
    ///
    /// <para>
    /// <b>It is the same ghost.</b> The same prefab, the same rig, the same
    /// <see cref="GhostRigController"/> choosing the same clips, the same manifestation
    /// renderers. There is no second ghost implementation - what this replaces is the
    /// decision-making, which was already gated on <c>SessionAuthority.CanSimulateGhost</c>
    /// and simply left a client's ghost standing still and dormant.
    /// </para>
    ///
    /// <para>
    /// What it does not receive is the ghost's reasoning. Not its destination, not its target,
    /// not what it heard, not how long the hunt has left. A client that knew where the ghost
    /// was going could draw an arrow to it, and the game is about not knowing.
    /// </para>
    ///
    /// <para>
    /// Transport-neutral: nothing here knows what a network is. Whatever receives state calls
    /// <see cref="ReceiveState"/>; this smooths between what arrived and applies it. That is
    /// the seam a netcode layer plugs into without touching the ghost.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GhostController))]
    [AddComponentMenu("Catch If You Can/Remote Ghost Driver")]
    public sealed class RemoteGhostDriver : MonoBehaviour
    {
        [Tooltip("Seconds of interpolation delay. One server tick at 20 Hz is 50 ms; a little " +
                 "more buys smoothness at the cost of that much lag, which on a ghost nobody " +
                 "is supposed to be able to predict is not a fairness question.")]
        [SerializeField, Min(0f)] private float interpolationDelay = 0.1f;

        [Tooltip("How fast the ghost catches up to the received position, in metres per " +
                 "second of error.")]
        [SerializeField, Min(1f)] private float positionCatchUp = 12f;

        [Tooltip("Beyond this much error, teleport instead of catching up. A ghost gliding " +
                 "across a house to correct a desync is worse than one that blinks.")]
        [SerializeField, Min(0.5f)] private float teleportDistance = 4f;

        private GhostController _ghost;
        private GhostPresentationState _previous;
        private GhostPresentationState _latest;
        private float _receivedAt = -1f;
        private bool _hasState;

        private void Awake()
        {
            _ghost = GetComponent<GhostController>();

            // One-way. This ghost is the host's for its whole life.
            _ghost.DriveFromRemoteState();
        }

        /// <summary>
        /// One received frame. Called by whatever is receiving; this component does not poll,
        /// does not know where the state came from, and does not care.
        /// </summary>
        public void ReceiveState(in GhostPresentationState state)
        {
            _previous = _hasState ? _latest : state;
            _latest = state;
            _receivedAt = Time.time;
            _hasState = true;
        }

        private void Update()
        {
            if (!_hasState || _ghost == null)
                return;

            float t = interpolationDelay <= 0f
                ? 1f
                : Mathf.Clamp01((Time.time - _receivedAt) / interpolationDelay);

            var state = GhostPresentationState.Lerp(_previous, _latest, t);

            ApplyPosition(state.Position);
            transform.rotation = Quaternion.Euler(0f, state.Yaw, 0f);

            // The state is what the whole performance is rebuilt from: the rig controller
            // picks its clip from this and nothing else.
            _ghost.AdoptReplicatedState(state.State);

            // Visibility is told rather than inferred. A manifestation can be refused, and the
            // roll that refuses it happens on the host.
            if (_ghost.IsManifestationVisible != state.IsVisible)
                _ghost.SetManifestationVisible(state.IsVisible);
        }

        private void ApplyPosition(Vector3 target)
        {
            float error = Vector3.Distance(transform.position, target);

            if (error > teleportDistance)
            {
                transform.position = target;
                return;
            }

            transform.position = Vector3.MoveTowards(
                transform.position, target, positionCatchUp * Time.deltaTime);
        }
    }
}
