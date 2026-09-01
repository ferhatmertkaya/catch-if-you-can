using UnityEngine;
using CatchIfYouCan.Input;

namespace CatchIfYouCan.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        // Nathan's only clip is authored at a measured 1.283 m/s. At the old 2.8 the walk cycle
        // had to play at 2.2x to keep the feet on the floor, which reads as a scurry; 1.9 lands
        // at about 1.5x, which still looks like walking.
        [SerializeField] private float walkSpeed = 1.9f;
        [SerializeField] private float sprintSpeed = 3.8f;
        [SerializeField] private float crouchSpeed = 1f;

        [Tooltip("Push the movement stick past this to sprint without the button. Zero disables " +
                 "it; the sprint button is the intended interaction and a stick threshold is easy " +
                 "to trip by accident.")]
        [SerializeField, Range(0f, 1f)] private float sprintStickThreshold;
        [SerializeField] private float gravity = -18f;
        [SerializeField] private float jumpHeight = 0f;

        [Header("Crouch")]
        [SerializeField] private float standingHeight = 1.8f;
        [SerializeField] private float crouchHeight = 1.0f;
        [SerializeField] private float crouchTransitionSpeed = 8f;

        [Header("References")]
        [SerializeField] private Transform cameraRoot;
        [SerializeField] private PlayerNoiseEmitter noiseEmitter;
        [SerializeField] private PlayerLook playerLook;

        private CharacterController _controller;
        private MobileInputController _input;
        private Vector3 _velocity;
        private float _currentHeight;
        private bool _movementEnabled = true;

        public bool IsSprinting { get; private set; }
        public bool IsCrouching { get; private set; }
        public bool IsGrounded { get; private set; }
        public float CurrentSpeed { get; private set; }
        public bool MovementEnabled
        {
            get => _movementEnabled;
            set => _movementEnabled = value;
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _currentHeight = standingHeight;
            _controller.height = standingHeight;
            _controller.center = new Vector3(0f, standingHeight * 0.5f, 0f);

            if (noiseEmitter == null)
                noiseEmitter = GetComponent<PlayerNoiseEmitter>();
            if (playerLook == null && cameraRoot != null)
                playerLook = cameraRoot.GetComponent<PlayerLook>();
        }

        private void Start()
        {
            _input = MobileInputController.Instance;
        }

        private void Update()
        {
            if (!_movementEnabled || _input == null)
                return;

            UpdateGrounded();
            UpdateCrouch();
            Move();
            EmitMovementNoise();
        }

        private void UpdateGrounded()
        {
            IsGrounded = _controller.isGrounded;
            if (IsGrounded && _velocity.y < 0f)
                _velocity.y = -2f;
        }

        private void UpdateCrouch()
        {
            bool wantsCrouch = _input.CrouchHeld;
            IsCrouching = wantsCrouch;
            float targetHeight = wantsCrouch ? crouchHeight : standingHeight;
            _currentHeight = Mathf.Lerp(_currentHeight, targetHeight, crouchTransitionSpeed * Time.deltaTime);
            _controller.height = _currentHeight;
            _controller.center = new Vector3(0f, _currentHeight * 0.5f, 0f);
        }

        private void Move()
        {
            Vector2 moveInput = _input.MoveInput;
            Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;

            bool stickSprint = sprintStickThreshold > 0f &&
                               moveInput.magnitude >= sprintStickThreshold;
            IsSprinting = (_input.SprintHeld || stickSprint) &&
                          moveInput.sqrMagnitude > 0.01f && !IsCrouching;
            float speed = IsCrouching ? crouchSpeed : IsSprinting ? sprintSpeed : walkSpeed;

            _controller.Move(move * speed * Time.deltaTime);

            if (jumpHeight > 0f && IsGrounded && UnityEngine.Input.GetButtonDown("Jump"))
                _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

            _velocity.y += gravity * Time.deltaTime;
            _controller.Move(_velocity * Time.deltaTime);

            CurrentSpeed = move.magnitude * speed;
        }

        private void EmitMovementNoise()
        {
            if (noiseEmitter == null)
                return;

            noiseEmitter.UpdateFromMovement(CurrentSpeed, IsSprinting, IsCrouching, IsGrounded);
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            _controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            _controller.enabled = true;
            _velocity = Vector3.zero;
            CurrentSpeed = 0f;
        }

        public void SetHidden(bool hidden)
        {
            _movementEnabled = !hidden;
            if (hidden)
                CurrentSpeed = 0f;
        }
    }
}
