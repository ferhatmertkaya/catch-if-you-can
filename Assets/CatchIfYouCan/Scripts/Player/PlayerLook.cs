using UnityEngine;
using CatchIfYouCan.Input;

namespace CatchIfYouCan.Player
{
    /// <summary>
    /// Turns look input into yaw on the player and pitch on the camera.
    ///
    /// <para>
    /// The split matters: yaw goes on the player root so the character, its collider and its
    /// facing all turn together, while pitch stays on this transform alone. Pitching the body
    /// would tip the capsule and lean the visible character over every time you glanced at the
    /// floor.
    /// </para>
    ///
    /// <para>
    /// Input arrives as a delta, never an absolute position, so lifting a thumb and putting it
    /// down elsewhere contributes nothing and the view does not jump.
    /// </para>
    /// </summary>
    public class PlayerLook : MonoBehaviour
    {
        [SerializeField] private Transform playerBody;

        [Header("Sensitivity")]
        [Tooltip("Degrees of yaw per reference pixel of drag. At 0.28 a 500 px thumb swipe - a " +
                 "comfortable one on a 1080p landscape phone - turns about 140 degrees.")]
        [SerializeField] private float sensitivityX = 0.28f;

        [Tooltip("Degrees of pitch per reference pixel. Held at about 0.8 of the yaw figure: " +
                 "the pitch range is only 160 degrees end to end, so matching horizontal would " +
                 "make the whole range too easy to cross by accident.")]
        [SerializeField] private float sensitivityY = 0.22f;

        [SerializeField] private bool invertY;

        [Header("Feel")]
        [Tooltip("Seconds of smoothing. Whatever is not applied this frame is carried to the " +
                 "next, so smoothing changes when the rotation arrives but never how much of it " +
                 "does. Zero disables it entirely.")]
        [SerializeField, Range(0f, 0.12f)] private float lookSmoothing = 0.02f;

        [Header("Limits")]
        [SerializeField] private float minPitch = -80f;
        [SerializeField] private float maxPitch = 80f;

        [SerializeField] private bool allowLook = true;

        private MobileInputController _input;
        private float _pitch;

        // Rotation that has arrived but not yet been applied. Draining a buffer rather than
        // smoothing towards a target is what keeps the smoothing honest: SmoothDamp chases a
        // per-frame delta that drops back to zero the moment the thumb stops, so it never
        // catches up and quietly swallows part of every short swipe.
        private Vector2 _pending;

        public bool AllowLook
        {
            get => allowLook;
            set
            {
                allowLook = value;
                if (!value)
                {
                    // Drop anything still queued, otherwise re-enabling look replays the last
                    // flick as a lurch.
                    _pending = Vector2.zero;
                }
            }
        }

        /// <summary>Yaw sensitivity. Kept for callers that only tune one axis.</summary>
        public float Sensitivity
        {
            get => sensitivityX;
            set => sensitivityX = Mathf.Max(0.01f, value);
        }

        public float SensitivityX
        {
            get => sensitivityX;
            set => sensitivityX = Mathf.Max(0.01f, value);
        }

        public float SensitivityY
        {
            get => sensitivityY;
            set => sensitivityY = Mathf.Max(0.01f, value);
        }

        public bool InvertY
        {
            get => invertY;
            set => invertY = value;
        }

        /// <summary>Current pitch in degrees, negative looking down.</summary>
        public float Pitch => _pitch;

        private void Start()
        {
            _input = MobileInputController.Instance;
            _pitch = transform.localEulerAngles.x;
            if (_pitch > 180f)
                _pitch -= 360f;
        }

        private void LateUpdate()
        {
            if (!allowLook)
                return;

            if (_input == null)
            {
                _input = MobileInputController.Instance;
                if (_input == null)
                    return;
            }

            Vector2 raw = _input.LookDelta;
            _pending += new Vector2(raw.x * sensitivityX, raw.y * sensitivityY);

            // Exponential drain, so the rate is the same whatever the frame rate. The delta is
            // already per-frame, so it is deliberately NOT multiplied by deltaTime again here;
            // doing that is what turns a working look into one that barely moves.
            Vector2 step = lookSmoothing > 0f
                ? _pending * (1f - Mathf.Exp(-Time.deltaTime / lookSmoothing))
                : _pending;

            _pending -= step;
            if (_pending.sqrMagnitude < 0.000001f)
                _pending = Vector2.zero;

            if (step.sqrMagnitude < 0.0000001f)
                return;

            if (playerBody != null)
                playerBody.Rotate(Vector3.up, step.x, Space.World);

            float pitchDelta = invertY ? step.y : -step.y;
            _pitch = Mathf.Clamp(_pitch + pitchDelta, minPitch, maxPitch);
            transform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        public void SnapTo(Quaternion bodyRotation, float pitch)
        {
            if (playerBody != null)
                playerBody.rotation = bodyRotation;

            _pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            transform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            _pending = Vector2.zero;
        }
    }
}
