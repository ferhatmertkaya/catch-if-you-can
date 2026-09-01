using UnityEngine;

namespace CatchIfYouCan.Player
{
    /// <summary>
    /// Drives the player's character animation from how fast the player is actually moving.
    ///
    /// <para>
    /// The speed is read from <see cref="CharacterController.velocity"/> — the movement that
    /// actually happened — rather than from input or from
    /// <see cref="PlayerController.CurrentSpeed"/>, which is <c>input * walkSpeed</c> and stays
    /// high while you hold forward against a wall. Reading the result instead of the intent is
    /// what makes the character stop walking when the controller is blocked, and it means the
    /// mobile joystick, a gamepad and the keyboard all drive the animation identically without
    /// this component knowing any of them exist.
    /// </para>
    ///
    /// <para>
    /// Root motion is forced off. The controller owns where the player is; the animation only
    /// depicts it. Letting a baked walk cycle move the transform would fight the
    /// CharacterController, desync the collider from the mesh, and walk the character through
    /// walls the controller had already stopped it at.
    /// </para>
    ///
    /// <para>
    /// Parameters are looked up once and only written if the controller actually declares them,
    /// so this is safe to attach before an Animator Controller exists — it simply does nothing
    /// rather than logging a warning every frame.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Catch If You Can/Player Visual Animator")]
    public sealed class PlayerVisualAnimator : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The character's Animator. Falls back to one in the children of this object.")]
        [SerializeField] private Animator animator;

        [Tooltip("The player's CharacterController. Falls back to one on this object or a parent.")]
        [SerializeField] private CharacterController characterController;

        [Header("Animator parameters")]
        [Tooltip("Float parameter fed the horizontal speed in metres per second. Left empty to skip.")]
        [SerializeField] private string speedParameter = "Speed";

        [Tooltip("Bool parameter set while the player is actually moving. Left empty to skip.")]
        [SerializeField] private string isWalkingParameter = "IsWalking";

        [Header("Tuning")]
        [Tooltip("Speed in m/s above which the character counts as walking. Small enough to " +
                 "catch a slow joystick nudge, large enough to ignore gravity settling and the " +
                 "sub-centimetre drift of standing on a slope.")]
        [SerializeField, Min(0f)] private float walkThreshold = 0.15f;

        [Tooltip("Smoothing on the speed parameter, in seconds. Stops a blend tree twitching " +
                 "when the controller is momentarily blocked.")]
        [SerializeField, Min(0f)] private float speedSmoothing = 0.12f;

        [Header("Foot sliding")]
        [Tooltip("Scale the animation playback rate with actual speed, so the feet keep up with " +
                 "the floor instead of skating or sprinting on the spot.")]
        [SerializeField] private bool matchAnimationSpeedToMovement = true;

        [Tooltip("The speed the walk clip was authored to move at. Playback is scaled by " +
                 "actualSpeed / this. Set from the clip, not guessed.")]
        [SerializeField, Min(0.01f)] private float clipAuthoredSpeed = 1.4f;

        [SerializeField, Range(0.1f, 1f)] private float minAnimationSpeed = 0.6f;
        [SerializeField, Range(1f, 3f)] private float maxAnimationSpeed = 1.8f;

        private int _speedHash;
        private int _isWalkingHash;
        private bool _hasSpeed;
        private bool _hasIsWalking;
        private float _smoothedSpeed;
        private float _smoothVelocity;

        /// <summary>Horizontal speed actually achieved last frame, in metres per second.</summary>
        public float CurrentPlanarSpeed { get; private set; }

        /// <summary>True while the player is genuinely moving, not merely pushing.</summary>
        public bool IsWalking { get; private set; }

        private void Awake()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            if (characterController == null)
                characterController = GetComponentInParent<CharacterController>();

            if (animator != null)
            {
                // The controller moves the player. The animation only shows it happening.
                animator.applyRootMotion = false;
                CacheParameters();
            }
        }

        /// <summary>
        /// Re-reads the Animator's parameter list. Call after swapping the controller at runtime;
        /// the character visual is loaded on demand, so the Animator may arrive after Awake.
        /// </summary>
        public void CacheParameters()
        {
            _hasSpeed = false;
            _hasIsWalking = false;

            if (animator == null || animator.runtimeAnimatorController == null)
                return;

            _speedHash = Animator.StringToHash(speedParameter);
            _isWalkingHash = Animator.StringToHash(isWalkingParameter);

            foreach (var p in animator.parameters)
            {
                if (!string.IsNullOrEmpty(speedParameter) &&
                    p.type == AnimatorControllerParameterType.Float && p.nameHash == _speedHash)
                    _hasSpeed = true;

                if (!string.IsNullOrEmpty(isWalkingParameter) &&
                    p.type == AnimatorControllerParameterType.Bool && p.nameHash == _isWalkingHash)
                    _hasIsWalking = true;
            }
        }

        /// <summary>
        /// Points this at a character that was instantiated after the player was built.
        /// </summary>
        public void BindAnimator(Animator target)
        {
            animator = target;
            if (animator != null)
                animator.applyRootMotion = false;
            CacheParameters();
        }

        private void Update()
        {
            if (characterController == null)
                return;

            // The movement that actually happened, with gravity removed: falling is not walking.
            Vector3 v = characterController.velocity;
            v.y = 0f;
            CurrentPlanarSpeed = v.magnitude;

            _smoothedSpeed = Mathf.SmoothDamp(_smoothedSpeed, CurrentPlanarSpeed,
                                              ref _smoothVelocity, speedSmoothing);

            IsWalking = CurrentPlanarSpeed > walkThreshold;

            if (animator == null)
                return;

            if (_hasSpeed) animator.SetFloat(_speedHash, _smoothedSpeed);
            if (_hasIsWalking) animator.SetBool(_isWalkingHash, IsWalking);

            if (matchAnimationSpeedToMovement)
            {
                animator.speed = IsWalking
                    ? Mathf.Clamp(_smoothedSpeed / clipAuthoredSpeed, minAnimationSpeed, maxAnimationSpeed)
                    : 1f;
            }
        }

        private void OnDisable()
        {
            // Never leave the character frozen mid-stride at an odd playback rate.
            if (animator != null)
                animator.speed = 1f;
        }
    }
}
