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

        [Header("Auto-run")]
        [Tooltip("Hold the stick strongly forward for this long and the character breaks into a " +
                 "run on its own. Walking starts immediately - the timer only decides when the " +
                 "run takes over, so nothing is standing still waiting for it.")]
        [SerializeField, Min(0f)] private float autoRunHoldDuration = 0.7f;

        [Tooltip("How far the stick must be pushed before the hold counts at all.")]
        [SerializeField, Range(0.1f, 1f)] private float autoRunStickMagnitude = 0.85f;

        [Tooltip("How closely the stick must point forward, as the cosine of the angle off " +
                 "straight ahead. 0.8 is about a 37 degree cone, so forward-left and " +
                 "forward-right still count and a hard strafe does not.")]
        [SerializeField, Range(0.1f, 1f)] private float autoRunForwardDot = 0.8f;

        [Tooltip("Seconds to blend between walking and running speed, so the change of pace is " +
                 "not an instant jump.")]
        [SerializeField, Range(0f, 0.5f)] private float speedBlendTime = 0.15f;
        [Header("Crouch")]
        // Matches PlayerFactory.CapsuleHeight. Raised with the character so the capsule, the
        // camera and the visible body stay in proportion rather than one drifting from the rest.
        [SerializeField] private float standingHeight = 1.86f;

        [Tooltip("Capsule height while crouched. Normally overwritten at spawn by " +
                 "PlayerBodyMotion.SetCrouchDepth with what the character's legs can actually " +
                 "fold to, so what the player can duck under and what the body looks like doing " +
                 "it are the same crouch.")]
        [SerializeField] private float crouchHeight = 1.27f;
        [SerializeField] private float crouchTransitionSpeed = 8f;

        [Tooltip("How far the camera drops when crouching, in metres. Left at the difference " +
                 "between the two capsule heights, so the eyes keep exactly the same distance " +
                 "below the top of the capsule as they do standing - a camera that drops less " +
                 "than the collider ends up looking out through a ceiling the player can walk " +
                 "under, and one that drops more puts the view in the character's own chest.")]
        [SerializeField, Min(0f)] private float cameraCrouchDrop = 0.59f;

        [Header("References")]
        [SerializeField] private Transform cameraRoot;
        [SerializeField] private PlayerNoiseEmitter noiseEmitter;
        [SerializeField] private PlayerLook playerLook;

        private CharacterController _controller;
        private MobileInputController _input;
        private Vector3 _velocity;
        private float _currentHeight;
        private bool _movementEnabled = true;
        private float _forwardHoldTime;
        private bool _autoRunning;
        private float _blendedSpeed;
        private float _speedBlendVelocity;
        private float _standingCameraHeight;
        private bool _hasCameraRoot;
        private PlayerBodyMotion _bodyMotion;

        public bool IsSprinting { get; private set; }

        /// <summary>
        /// How far into the crouch the player is, 0 standing and 1 fully down. Follows the same
        /// smoothing as the capsule, so anything driven from it - the camera, the visible body -
        /// arrives with the collider rather than snapping ahead of it.
        /// </summary>
        public float CrouchAmount01 { get; private set; }

        /// <summary>
        /// The movement stick in the player's own axes: x strafe, y forward. Handed out for
        /// animation, which needs to know that a sideways walk is sideways; the controller itself
        /// has already turned it into world movement by the time anyone asks.
        /// </summary>
        public Vector2 LocalMoveInput { get; private set; }

        /// <summary>True while the run was started by holding forward rather than by the button.</summary>
        public bool IsAutoRunning => _autoRunning;
        public bool IsCrouching { get; private set; }
        public bool IsGrounded { get; private set; }
        public float CurrentSpeed { get; private set; }
        public bool MovementEnabled
        {
            get => _movementEnabled;
            set
            {
                _movementEnabled = value;
                if (!value)
                    CancelAutoRun();
            }
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _blendedSpeed = walkSpeed;
            _currentHeight = standingHeight;
            _controller.height = standingHeight;
            _controller.center = new Vector3(0f, standingHeight * 0.5f, 0f);

            if (noiseEmitter == null)
                noiseEmitter = GetComponent<PlayerNoiseEmitter>();
            if (playerLook == null && cameraRoot != null)
                playerLook = cameraRoot.GetComponent<PlayerLook>();

            // Read rather than assumed. PlayerFactory owns where the eyes are and has already
            // placed them by now; hard-coding the standing height here would mean two files that
            // have to be changed together and one that nobody remembers.
            _hasCameraRoot = cameraRoot != null;
            if (_hasCameraRoot)
                _standingCameraHeight = cameraRoot.localPosition.y;
        }

        private void Start()
        {
            _input = MobileInputController.Instance;
            // Looked up here rather than in Awake: PlayerFactory adds this component before it
            // adds the body motion, so in Awake there is nothing to find yet. Once, and cached.
            _bodyMotion = GetComponent<PlayerBodyMotion>();
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

        /// <summary>
        /// Shrinks the capsule and drops the camera with it.
        ///
        /// <para>
        /// The camera used to be left where it was, and that is the whole of "crouching does
        /// nothing you can see": the collider halved, the character could pass under things, and
        /// the view stayed at standing height the entire time. The eyes move on the same smoothed
        /// value as the capsule rather than on a second timer, so the view and the collider are
        /// never briefly disagreeing about how tall the player is.
        /// </para>
        /// </summary>
        private void UpdateCrouch()
        {
            bool wantsCrouch = _input.CrouchHeld;
            IsCrouching = wantsCrouch;
            float targetHeight = wantsCrouch ? crouchHeight : standingHeight;
            _currentHeight = Mathf.Lerp(_currentHeight, targetHeight, crouchTransitionSpeed * Time.deltaTime);
            _controller.height = _currentHeight;
            _controller.center = new Vector3(0f, _currentHeight * 0.5f, 0f);

            CrouchAmount01 = standingHeight > crouchHeight
                ? Mathf.Clamp01((standingHeight - _currentHeight) / (standingHeight - crouchHeight))
                : 0f;

            if (!_hasCameraRoot)
                return;

            // Only Y. The forward offset that puts the camera in front of the character's own
            // neck is not a crouch concern and must survive it untouched.
            //
            // The drop is the character's own head bone where there is one to read, and the
            // leg-length figure only as a fallback. Those are not the same number: the crouch
            // folds the legs, which drops the hips, and then leans the torso forward over the
            // knees, which drops the head again. Dropping the camera by the first alone left the
            // view hanging several centimetres above the eyes of the body it belongs to.
            Vector3 local = cameraRoot.localPosition;
            float drop = _bodyMotion != null && _bodyMotion.MeasuredHeadDrop > 0.001f
                ? _bodyMotion.MeasuredHeadDrop
                : cameraCrouchDrop * CrouchAmount01;
            local.y = _standingCameraHeight - drop;
            cameraRoot.localPosition = local;
        }

        private void Move()
        {
            Vector2 moveInput = _input.MoveInput;
            LocalMoveInput = moveInput;
            Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;

            UpdateAutoRun(moveInput);

            bool stickSprint = sprintStickThreshold > 0f &&
                               moveInput.magnitude >= sprintStickThreshold;
            IsSprinting = (_input.SprintHeld || stickSprint || _autoRunning) &&
                          moveInput.sqrMagnitude > 0.01f && !IsCrouching;

            float targetSpeed = IsCrouching ? crouchSpeed : IsSprinting ? sprintSpeed : walkSpeed;
            // Blended rather than switched, so breaking into a run reads as picking up pace
            // instead of the world suddenly moving faster.
            _blendedSpeed = speedBlendTime > 0f
                ? Mathf.SmoothDamp(_blendedSpeed, targetSpeed, ref _speedBlendVelocity, speedBlendTime)
                : targetSpeed;

            float speed = _blendedSpeed;

            if (jumpHeight > 0f && IsGrounded && UnityEngine.Input.GetButtonDown("Jump"))
                _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

            _velocity.y += gravity * Time.deltaTime;

            // One Move per frame, horizontal and vertical together, and that is not a tidy-up.
            // CharacterController.velocity reports the displacement of the *last* Move call
            // divided by delta time. Moving horizontally and then calling Move again with only
            // gravity left every reader of that property seeing a purely vertical velocity of
            // about (0, -2, 0): strip the Y, as an animator or a footstep controller must, and
            // the planar speed is zero no matter how fast the player is actually walking. That
            // is why the walk animation never started and the footsteps never fired. Combining
            // them also gives the controller a single collision sweep to resolve instead of two.
            _controller.Move((move * speed + _velocity) * Time.deltaTime);

            CurrentSpeed = move.magnitude * speed;
        }

        /// <summary>
        /// Watches for the stick being held forward, and starts a run when it has been held long
        /// enough.
        ///
        /// <para>
        /// The direction test is a cone rather than an axis check, so running forward-left or
        /// forward-right still qualifies while a hard strafe or anything backward does not. The
        /// hold resets the instant the stick leaves the cone, which is what makes pulling back
        /// cancel immediately rather than after a delay.
        /// </para>
        /// </summary>
        private void UpdateAutoRun(Vector2 moveInput)
        {
            if (IsCrouching || autoRunHoldDuration <= 0f)
            {
                _forwardHoldTime = 0f;
                _autoRunning = false;
                return;
            }

            float magnitude = moveInput.magnitude;
            bool forward = magnitude >= autoRunStickMagnitude &&
                           moveInput.y / Mathf.Max(magnitude, 0.0001f) >= autoRunForwardDot;

            if (!forward)
            {
                _forwardHoldTime = 0f;
                _autoRunning = false;
                return;
            }

            _forwardHoldTime += Time.deltaTime;
            if (_forwardHoldTime >= autoRunHoldDuration)
                _autoRunning = true;
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
            {
                CurrentSpeed = 0f;
                CancelAutoRun();
            }
        }

        /// <summary>
        /// Sets how deep a crouch is, in metres, from what the visible character can actually
        /// fold to.
        ///
        /// <para>
        /// Called once at spawn by <see cref="PlayerBodyMotion"/>, which measures the rig's own
        /// leg lengths. Before this existed the three heights that make up a crouch - the
        /// capsule, the camera and the folded body - were three numbers in two files, and there
        /// was nothing keeping them in agreement: the capsule ducked to 1.03 m while the legs
        /// could only fold about 0.19 m, so the collider crouched and the character barely
        /// dipped, and the camera went somewhere in between.
        /// </para>
        /// </summary>
        public void SetCrouchDepth(float metres)
        {
            if (metres <= 0.01f)
                return;

            cameraCrouchDrop = metres;
            // Leaves the eyes the same distance below the top of the capsule as they are
            // standing, which is what stops a crouched player looking out through a ceiling
            // they can walk under.
            crouchHeight = Mathf.Max(0.5f, standingHeight - metres);
        }

        /// <summary>
        /// Drops the run and the hold that produced it. Called when movement is taken away, so
        /// the player is never handed control back already sprinting.
        /// </summary>
        public void CancelAutoRun()
        {
            _forwardHoldTime = 0f;
            _autoRunning = false;
            _blendedSpeed = walkSpeed;
            _speedBlendVelocity = 0f;
        }
    }
}
