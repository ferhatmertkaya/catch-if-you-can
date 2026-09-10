using UnityEngine;

namespace CatchIfYouCan.Player
{
    /// <summary>
    /// Drives another player's body on this machine, from replicated state.
    ///
    /// <para>
    /// <b>It is the same character.</b> The same prefab, the same rig, the same rigged hands
    /// and fingers, the same Animator, the same <see cref="PlayerBodyMotion"/>. There is no
    /// second character implementation and no second animation stack: this writes the received
    /// state onto the properties a local player computes and lets everything downstream do
    /// exactly what it already does.
    /// </para>
    ///
    /// <para>
    /// What it does not receive is a pose. Not one bone, not one finger, not the hand target or
    /// the elbow hint. A body that is handed a yaw, a pitch, a stick direction and a speed can
    /// rebuild the whole thing locally, and sending the pose instead would be a hundred
    /// transforms a tick to say what eight bytes already said.
    /// </para>
    ///
    /// <para>
    /// Transport-neutral: nothing here knows what a network is. Whatever receives state calls
    /// <see cref="ReceiveState"/>; this smooths between what arrived and applies it. That is
    /// the seam a netcode layer plugs into without touching the character.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerController))]
    [AddComponentMenu("Catch If You Can/Remote Player Driver")]
    public sealed class RemotePlayerDriver : MonoBehaviour
    {
        [Tooltip("Where the head is, for applying replicated look pitch. The camera root on " +
                 "the local player; on a remote one there is no camera under it.")]
        [SerializeField] private Transform headPivot;

        [Tooltip("Seconds of interpolation delay. One network tick at 20 Hz is 50 ms; a little " +
                 "more buys smoothness at the cost of that much lag, which nobody can see on " +
                 "another player's body.")]
        [SerializeField, Min(0f)] private float interpolationDelay = 0.1f;

        [Tooltip("How fast the body catches up to the received position, in metres per second " +
                 "of error. A hard snap every tick reads as stuttering.")]
        [SerializeField, Min(1f)] private float positionCatchUp = 12f;

        [Tooltip("Beyond this much error, teleport instead of catching up. A body that walks " +
                 "smoothly across a house to correct a desync is worse than one that blinks.")]
        [SerializeField, Min(0.5f)] private float teleportDistance = 4f;

        private PlayerController _controller;
        private PlayerPresentationState _previous;
        private PlayerPresentationState _latest;
        private Vector3 _previousPosition;
        private Vector3 _latestPosition;
        private float _receivedAt = -1f;
        private bool _hasState;

        private void Awake()
        {
            _controller = GetComponent<PlayerController>();

            // One-way. This body is somebody else's for its whole life.
            _controller.DriveFromRemoteState();

            if (headPivot == null)
                headPivot = transform.Find("CameraRoot");

            StripLocalOnly();
        }

        /// <summary>
        /// Takes off everything that belongs to the machine its owner is sitting at.
        ///
        /// <para>
        /// A remote player must not bring a second camera, a second AudioListener or a second
        /// set of on-screen controls into this process. Two AudioListeners is not a subtle bug -
        /// Unity warns and the mix goes wrong - and two cameras rendering is the frame budget
        /// spent twice.
        /// </para>
        ///
        /// <para>
        /// Destroyed rather than disabled. A disabled camera is a camera somebody re-enables.
        /// </para>
        /// </summary>
        private void StripLocalOnly()
        {
            var listener = GetComponentInChildren<AudioListener>(true);
            if (listener != null)
                Destroy(listener);

            var camera = GetComponentInChildren<Camera>(true);
            if (camera != null)
                Destroy(camera);

            var look = GetComponentInChildren<PlayerLook>(true);
            if (look != null)
                Destroy(look);

            // The full body, head included: this is somebody else's character being looked at
            // rather than looked out of.
            var visibility = GetComponentInChildren<LocalPlayerBodyVisibility>(true);
            if (visibility != null)
                visibility.Mode = LocalPlayerBodyVisibility.BodyMode.FullBody;
        }

        /// <summary>
        /// One received frame. Called by whatever is receiving; this component does not poll,
        /// does not know where the state came from, and does not care.
        /// </summary>
        public void ReceiveState(in PlayerPresentationState state, Vector3 position)
        {
            _previous = _hasState ? _latest : state;
            _previousPosition = _hasState ? _latestPosition : position;

            _latest = state;
            _latestPosition = position;
            _receivedAt = Time.time;
            _hasState = true;
        }

        private void Update()
        {
            if (!_hasState)
                return;

            // Where between the last two frames we should be, given how long ago they arrived.
            float t = interpolationDelay <= 0f
                ? 1f
                : Mathf.Clamp01((Time.time - _receivedAt) / interpolationDelay);

            var state = PlayerPresentationState.Lerp(_previous, _latest, t);
            Vector3 target = Vector3.Lerp(_previousPosition, _latestPosition, t);

            ApplyPosition(target);

            // The body faces where the owner faces. PlayerBodyMotion reads the root's rotation
            // for its own strafe and lean maths, so this is all it needs.
            transform.rotation = Quaternion.Euler(0f, state.Yaw, 0f);

            if (headPivot != null)
            {
                // Pitch on the head pivot only, exactly where PlayerLook puts it on a local
                // player, so the head-look the body motion layers on top lands the same way.
                headPivot.localRotation = Quaternion.Euler(state.Pitch, 0f, 0f);
            }

            // And onto the same properties a local player computes, which is what lets the
            // existing body motion drive this rig without knowing it is remote.
            _controller.ApplyRemoteState(state);
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
