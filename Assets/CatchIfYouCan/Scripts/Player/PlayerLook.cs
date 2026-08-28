using UnityEngine;
using CatchIfYouCan.Input;

namespace CatchIfYouCan.Player
{
    public class PlayerLook : MonoBehaviour
    {
        [SerializeField] private Transform playerBody;
        [SerializeField] private float sensitivity = 1.2f;
        [SerializeField] private bool invertY;
        [SerializeField] private float minPitch = -85f;
        [SerializeField] private float maxPitch = 85f;
        [SerializeField] private bool allowLook = true;

        private MobileInputController _input;
        private float _pitch;

        public bool AllowLook
        {
            get => allowLook;
            set => allowLook = value;
        }

        public float Sensitivity
        {
            get => sensitivity;
            set => sensitivity = Mathf.Max(0.01f, value);
        }

        public bool InvertY
        {
            get => invertY;
            set => invertY = value;
        }

        private void Start()
        {
            _input = MobileInputController.Instance;
            _pitch = transform.localEulerAngles.x;
            if (_pitch > 180f)
                _pitch -= 360f;
        }

        private void LateUpdate()
        {
            if (!allowLook || _input == null)
                return;

            Vector2 lookDelta = _input.LookDelta * sensitivity;
            if (lookDelta.sqrMagnitude < 0.0001f)
                return;

            float yaw = lookDelta.x;
            float pitchDelta = invertY ? lookDelta.y : -lookDelta.y;

            if (playerBody != null)
                playerBody.Rotate(Vector3.up, yaw, Space.World);

            _pitch = Mathf.Clamp(_pitch + pitchDelta, minPitch, maxPitch);
            transform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        public void SnapTo(Quaternion bodyRotation, float pitch)
        {
            if (playerBody != null)
                playerBody.rotation = bodyRotation;

            _pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            transform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }
    }
}
