using UnityEngine;
using CatchIfYouCan.Input;

namespace CatchIfYouCan.Player
{
    /// <summary>
    /// The small amount the view moves when the player is doing nothing: breathing, and the
    /// occasional glance around.
    ///
    /// <para>
    /// A first-person camera that is perfectly still between inputs is the one thing that most
    /// reliably reads as "this is a camera, not a person". Breath is the cheapest fix and the one
    /// nobody consciously notices; the glance is what makes standing in a dark room feel like
    /// standing in a dark room rather than pausing a game.
    /// </para>
    ///
    /// <para>
    /// It lives on its own transform between the pitch pivot and the camera, and that is the
    /// whole reason it can exist without fighting anything. <see cref="PlayerLook"/> owns the
    /// pivot's rotation, <see cref="PlayerController"/> owns the pivot's height while crouching,
    /// and <see cref="FearSystem"/> owns the camera's own local position for its fear bob. Three
    /// owners, three transforms, and this one is the fourth — so nothing here has to read, cache
    /// or restore a value another system also writes.
    /// </para>
    ///
    /// <para>
    /// Everything decays to zero the moment the player does anything. A glance that carried on
    /// while someone was trying to aim would be motion sickness with extra steps, so any look
    /// input, any movement, cancels it — and the cancel is a fast ease rather than a snap, so
    /// touching the screen never jerks the view.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Catch If You Can/Camera Idle Motion")]
    public sealed class CameraIdleMotion : MonoBehaviour
    {
        [Header("Breathing")]
        [Tooltip("Breaths per second. 0.22 is about thirteen a minute - a person standing still, " +
                 "not one who has been running.")]
        [SerializeField, Min(0.01f)] private float breathRate = 0.22f;

        [Tooltip("How far the head rises and falls, in metres. About a centimetre: enough that " +
                 "the room drifts, not enough to notice as movement.")]
        [SerializeField] private float breathHeight = 0.011f;

        [Tooltip("Forward and back component, a quarter-period out of phase with the rise so the " +
                 "path is an ellipse rather than a line. A straight up-and-down bob reads as a " +
                 "lift, not a breath.")]
        [SerializeField] private float breathSway = 0.005f;

        [Tooltip("Breath amplitude while moving, as a fraction of the resting amplitude. Not " +
                 "zero: walking does not hold your breath.")]
        [SerializeField, Range(0f, 1f)] private float breathWhileMoving = 0.45f;

        [Header("Glances")]
        [Tooltip("How long the player has to be still before the character starts looking around " +
                 "on its own.")]
        [SerializeField, Min(0f)] private float idleBeforeGlancing = 5f;

        [Tooltip("Seconds between glances, rolled each time.")]
        [SerializeField] private Vector2 glanceInterval = new Vector2(7f, 19f);

        [Tooltip("How far a glance turns, in degrees. Small on purpose: this is a look, not a " +
                 "head turn, and the player must never feel the camera taken off them.")]
        [SerializeField] private Vector2 glanceYawRange = new Vector2(4f, 11f);

        [SerializeField] private Vector2 glancePitchRange = new Vector2(-4f, 3f);

        [Tooltip("Turn out, hold, and drift back, in seconds.")]
        [SerializeField] private Vector3 glanceTiming = new Vector3(1.1f, 0.9f, 1.6f);

        [Header("Cancelling")]
        [Tooltip("Seconds to unwind the glance when the player takes over. Fast, but not a snap.")]
        [SerializeField, Min(0.01f)] private float cancelTime = 0.25f;

        [Tooltip("Look input below this, in reference pixels per frame, does not count as the " +
                 "player taking over. Stops a resting thumb's jitter from killing the effect.")]
        [SerializeField, Min(0f)] private float lookDeadZone = 0.6f;

        [SerializeField] private PlayerController playerController;

        private MobileInputController _input;

        private float _idleTime;
        private float _glanceTimer;
        private float _glancePhase = -1f;
        private Vector2 _glanceTarget;
        private Vector2 _glance;
        private Vector2 _glanceVelocity;

        private System.Random _rng;

        private void Awake()
        {
            // A stream of its own per instance, so two of these never blink or glance in
            // lockstep. Seeded from a Guid rather than the instance id: the id was only ever
            // a convenient unique number, it is not identity that matters here, and Unity 6.5
            // made GetInstanceID an error in favour of an API that does not exist in older
            // versions. A Guid is unique, needs no engine call, and cannot be deprecated.
            _rng = new System.Random(System.Guid.NewGuid().GetHashCode());
            _glanceTimer = Range(glanceInterval);

            if (playerController == null)
                playerController = GetComponentInParent<PlayerController>();
        }

        private void LateUpdate()
        {
            if (_input == null)
                _input = MobileInputController.Instance;

            float dt = Time.deltaTime;
            bool busy = IsPlayerBusy();

            if (busy)
            {
                _idleTime = 0f;
                // Abandon a glance in progress rather than finishing it politely: the player is
                // steering now.
                _glancePhase = -1f;
            }
            else
            {
                _idleTime += dt;
            }

            UpdateGlance(dt, busy);
            Apply();
        }

        private bool IsPlayerBusy()
        {
            if (_input != null && _input.LookDelta.sqrMagnitude > lookDeadZone * lookDeadZone)
                return true;

            if (playerController != null && playerController.LocalMoveInput.sqrMagnitude > 0.01f)
                return true;

            return false;
        }

        private void UpdateGlance(float dt, bool busy)
        {
            if (_glancePhase >= 0f)
            {
                _glancePhase += dt;
                float total = glanceTiming.x + glanceTiming.y + glanceTiming.z;
                if (_glancePhase >= total)
                {
                    _glancePhase = -1f;
                    _glanceTimer = Range(glanceInterval);
                }
                else
                {
                    _glance = Vector2.SmoothDamp(_glance, _glanceTarget * GlanceEnvelope(_glancePhase),
                                                 ref _glanceVelocity, 0.28f);
                    return;
                }
            }

            _glance = Vector2.SmoothDamp(_glance, Vector2.zero, ref _glanceVelocity,
                                         busy ? cancelTime : 0.4f);

            if (busy || _idleTime < idleBeforeGlancing)
                return;

            _glanceTimer -= dt;
            if (_glanceTimer > 0f)
                return;

            _glancePhase = 0f;
            float yaw = Range(glanceYawRange) * (_rng.Next(2) == 0 ? -1f : 1f);
            float pitch = Mathf.Lerp(glancePitchRange.x, glancePitchRange.y, (float)_rng.NextDouble());
            _glanceTarget = new Vector2(yaw, pitch);
        }

        /// <summary>Out, hold, back. Smoothstepped at both ends so nothing starts or stops dead.</summary>
        private float GlanceEnvelope(float t)
        {
            if (t < glanceTiming.x)
                return Smooth(t / Mathf.Max(0.01f, glanceTiming.x));

            if (t < glanceTiming.x + glanceTiming.y)
                return 1f;

            float back = (t - glanceTiming.x - glanceTiming.y) / Mathf.Max(0.01f, glanceTiming.z);
            return 1f - Smooth(Mathf.Clamp01(back));
        }

        private static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        private void Apply()
        {
            float moving = playerController != null
                ? Mathf.Clamp01(playerController.LocalMoveInput.magnitude)
                : 0f;
            float amplitude = Mathf.Lerp(1f, breathWhileMoving, moving);

            float phase = Time.time * breathRate * Mathf.PI * 2f;
            float rise = Mathf.Sin(phase) * breathHeight * amplitude;
            float sway = Mathf.Cos(phase) * breathSway * amplitude;

            transform.localPosition = new Vector3(0f, rise, sway);
            transform.localRotation = Quaternion.Euler(_glance.y, _glance.x, 0f);
        }

        private float Range(Vector2 range) =>
            Mathf.Lerp(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y), (float)_rng.NextDouble());

        private void OnDisable()
        {
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            _glance = Vector2.zero;
            _glanceVelocity = Vector2.zero;
            _glancePhase = -1f;
        }
    }
}
