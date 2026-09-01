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
    /// <b>And <c>applyRootMotion</c> alone was not enough to achieve that.</b> Root motion is
    /// only <em>root motion</em> if the importer extracted it, and for a Generic rig that needs a
    /// Root Node naming the bone to lift it from. The FBX's <c>motionNodeName</c> was empty; the
    /// bone name sat in <c>rootMotionBoneName</c>, which only a Humanoid rig reads. So the walk's
    /// 2.866 m of forward travel was never lifted out — it stayed as ordinary transform animation
    /// on the root bone, where <c>applyRootMotion</c> has no say over it, and the skeleton walked
    /// out of the player and away in front of the camera, looping back every 2.2 s.
    /// </para>
    ///
    /// <para>
    /// So the root bone is pinned here instead, in LateUpdate, after the Animator has written.
    /// Position and rotation both, which is exactly what discarding correctly-extracted root
    /// motion would have produced. It costs one comparison and, when the clip has moved it, one
    /// transform write. The importer is being fixed too, and once that lands this pin finds the
    /// bone already at the bind pose and does nothing — which is the point: the thing that made
    /// this bug survive so long is that nothing was checking.
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
                 "actualSpeed / this. Measured from the clip, not guessed: Nathan's walk carries " +
                 "its root 2.866 m over its 2.233 s, which is 1.283 m/s.")]
        [SerializeField, Min(0.01f)] private float clipAuthoredSpeed = 1.283f;

        [SerializeField, Range(0.1f, 1f)] private float minAnimationSpeed = 0.6f;

        [Tooltip("Ceiling on playback rate. Walking at 1.9 m/s against a clip authored at 1.283 " +
                 "needs 1.48x and stays under this. Sprinting at 3.8 would need 2.96x, which is " +
                 "not a run, it is a cartoon; the cap holds it at a fast walk and accepts the " +
                 "foot slide until there is a real run clip to blend to.")]
        [SerializeField, Range(1f, 3f)] private float maxAnimationSpeed = 2f;

        [Header("Future run state")]
        [Tooltip("Bool parameter set while sprinting. Written only if the controller declares " +
                 "it, so adding a Run state later needs no code change here.")]
        [SerializeField] private string isRunningParameter = "IsRunning";

        [Header("Root bone")]
        [Tooltip("Bone whose animated travel is discarded, matched by name suffix. This is what " +
                 "keeps the character animating in place while the CharacterController does the " +
                 "actual moving. Clear it only if the clip genuinely carries no root travel.")]
        [SerializeField] private string rootBoneSuffix = "_root";

        private Transform _rootBone;
        private Vector3 _rootBindPosition;
        private Quaternion _rootBindRotation;
        private bool _hasRootBone;
        private bool _warnedAboutLoop;

        private int _speedHash;
        private int _isWalkingHash;
        private int _isRunningHash;
        private bool _hasSpeed;
        private bool _hasIsWalking;
        private bool _hasIsRunning;
        private PlayerController _playerController;
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

            if (_playerController == null)
                _playerController = GetComponentInParent<PlayerController>();

            if (animator != null)
            {
                // The controller moves the player. The animation only shows it happening.
                animator.applyRootMotion = false;
                CacheParameters();
                CacheRootBone();
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
            _hasIsRunning = false;

            if (animator == null || animator.runtimeAnimatorController == null)
                return;

            _speedHash = Animator.StringToHash(speedParameter);
            _isWalkingHash = Animator.StringToHash(isWalkingParameter);
            _isRunningHash = Animator.StringToHash(isRunningParameter);

            foreach (var p in animator.parameters)
            {
                if (!string.IsNullOrEmpty(speedParameter) &&
                    p.type == AnimatorControllerParameterType.Float && p.nameHash == _speedHash)
                    _hasSpeed = true;

                if (!string.IsNullOrEmpty(isWalkingParameter) &&
                    p.type == AnimatorControllerParameterType.Bool && p.nameHash == _isWalkingHash)
                    _hasIsWalking = true;

                if (!string.IsNullOrEmpty(isRunningParameter) &&
                    p.type == AnimatorControllerParameterType.Bool && p.nameHash == _isRunningHash)
                    _hasIsRunning = true;
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
            CacheRootBone();
        }

        /// <summary>
        /// Finds the root bone and remembers the pose it should be holding. Captured before the
        /// Animator has played a frame, so this is the bind pose rather than a walk frame.
        /// </summary>
        private void CacheRootBone()
        {
            _hasRootBone = false;
            _rootBone = null;

            if (animator == null || string.IsNullOrEmpty(rootBoneSuffix))
                return;

            var all = animator.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (!all[i].name.EndsWith(rootBoneSuffix, System.StringComparison.OrdinalIgnoreCase))
                    continue;

                _rootBone = all[i];
                _rootBindPosition = all[i].localPosition;
                _rootBindRotation = all[i].localRotation;
                _hasRootBone = true;
                return;
            }
        }

        /// <summary>
        /// Holds the root bone at its bind pose, after the Animator has written and before
        /// anything is drawn. Equivalent to discarding root motion, for a rig where the importer
        /// never extracted any to discard.
        /// </summary>
        private void LateUpdate()
        {
            if (!_hasRootBone || _rootBone == null)
                return;

            if (_rootBone.localPosition != _rootBindPosition)
                _rootBone.localPosition = _rootBindPosition;
            if (_rootBone.localRotation != _rootBindRotation)
                _rootBone.localRotation = _rootBindRotation;
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

            // Written for a Run state that does not exist yet. Harmless until one does, and it
            // means adding the clip is an Animator change rather than a code change.
            if (_hasIsRunning)
                animator.SetBool(_isRunningHash, IsWalking && _playerController != null &&
                                                 _playerController.IsSprinting);

            if (matchAnimationSpeedToMovement)
            {
                animator.speed = IsWalking
                    ? Mathf.Clamp(_smoothedSpeed / clipAuthoredSpeed, minAnimationSpeed, maxAnimationSpeed)
                    : 1f;
            }

            KeepWalkCycleRunning();
        }

        /// <summary>
        /// Restarts the walk if it has run off the end of a clip that does not loop.
        ///
        /// <para>
        /// The walk is split out of the FBX with Loop Time on, but that is an import setting on a
        /// generated asset, and this project has already been bitten twice by an import setting
        /// that was believed to be set and was not. A clip that does not loop plays once and then
        /// holds its last frame, which is a character that takes a few steps and stops dead while
        /// everything else carries on — and nothing anywhere reports it.
        /// </para>
        ///
        /// <para>
        /// This costs one state query per frame while walking and disables itself completely when
        /// the clip does loop: a looping state's normalised time wraps and never reaches 1, so the
        /// branch is never taken. It is a net, not a mechanism.
        /// </para>
        /// </summary>
        private void KeepWalkCycleRunning()
        {
            if (!IsWalking || animator.runtimeAnimatorController == null)
                return;

            if (animator.IsInTransition(0))
                return;

            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (state.loop || state.normalizedTime < 1f)
                return;

            if (!_warnedAboutLoop)
            {
                _warnedAboutLoop = true;
                Debug.LogWarning("[CIYC] The walk state is not looping, so it would stop after " +
                                 "one cycle. Restarting it each cycle as a stopgap; the real fix " +
                                 "is Loop Time on the Nathan_Walk clip in the model importer.",
                                 this);
            }

            animator.Play(state.fullPathHash, 0, state.normalizedTime % 1f);
        }

        private void OnDisable()
        {
            // Never leave the character frozen mid-stride at an odd playback rate.
            if (animator != null)
                animator.speed = 1f;
        }
    }
}
