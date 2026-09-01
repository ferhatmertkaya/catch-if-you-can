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
        [Tooltip("Degrees of yaw per unit of look input.")]
        [SerializeField] private float sensitivityX = 1.2f;

        [Tooltip("Degrees of pitch per unit of look input. Slightly below horizontal on purpose: " +
                 "vertical aim needs less travel than turning around does.")]
        [SerializeField] private float sensitivityY = 1.0f;

        [SerializeField] private bool invertY;

        [Header("Feel")]
        [Tooltip("Seconds of smoothing. Small values take the stair-stepping off a low frame rate " +
                 "without the aim feeling like it is on a spring; zero disables it entirely.")]
        [SerializeField, Range(0f, 0.12f)] private float lookSmoothing = 0.035f;

        [Header("Limits")]
        [SerializeField] private float minPitch = -80f;
        [SerializeField] private float maxPitch = 80f;

        [SerializeField] private bool allowLook = true;

        private MobileInputController _input;
        private float _pitch;
        private Vector2 _smoothed;
        private Vector2 _smoothVelocity;

        public bool AllowLook
        {
            get => allowLook;
            set
            {
                allowLook = value;
                if (!value)
                {
                    // Drop any in-flight smoothing, otherwise re-enabling look replays the last
                    // flick as a lurch.
                    _smoothed = Vector2.zero;
                    _smoothVelocity = Vector2.zero;
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
            raw = new Vector2(raw.x * sensitivityX, raw.y * sensitivityY);

            if (lookSmoothing > 0f)
                _smoothed = Vector2.SmoothDamp(_smoothed, raw, ref _smoothVelocity, lookSmoothing);
            else
                _smoothed = raw;

            if (_smoothed.sqrMagnitude < 0.000001f)
                return;

            if (playerBody != null)
                playerBody.Rotate(Vector3.up, _smoothed.x, Space.World);

            float pitchDelta = invertY ? _smoothed.y : -_smoothed.y;
            _pitch = Mathf.Clamp(_pitch + pitchDelta, minPitch, maxPitch);
            transform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        public void SnapTo(Quaternion bodyRotation, float pitch)
        {
            if (playerBody != null)
                playerBody.rotation = bodyRotation;

            _pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            transform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            _smoothed = Vector2.zero;
            _smoothVelocity = Vector2.zero;
        }
    }
}
