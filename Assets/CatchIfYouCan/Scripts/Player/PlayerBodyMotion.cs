using UnityEngine;

namespace CatchIfYouCan.Player
{
    /// <summary>
    /// Everything the character does that the one imported clip cannot: crouching, sidestepping,
    /// looking where the player looks, breathing, and blinking.
    ///
    /// <para>
    /// The Renderpeople delivery contains exactly one take — a forward walk — so there is no
    /// crouch clip to blend to and no strafe clip to blend with. Waiting for more clips would
    /// mean a character that stands bolt upright while the player creeps, and slides sideways
    /// facing forward like a chess piece. This layer is what fills that in, and it is deliberately
    /// a layer rather than a replacement: it never decides the pose, it only bends the pose the
    /// Animator already wrote.
    /// </para>
    ///
    /// <para>
    /// <b>Every rotation here is applied in world space, about the player's own axes.</b> That is
    /// not a stylistic choice, it is the only way this can be correct without opening the rig: a
    /// bone's local axes are whatever the artist's exporter happened to produce, so "bend the knee"
    /// as a local Euler angle is a guess that is wrong on most rigs. A knee bends about the
    /// character's left-right axis whatever the bone's own orientation is, and
    /// <see cref="Transform.Rotate(Vector3,float,Space)"/> with <see cref="Space.World"/> says
    /// exactly that.
    /// </para>
    ///
    /// <para>
    /// It runs in LateUpdate, after the Animator has written the frame, and it writes offsets
    /// rather than absolute poses. Nothing accumulates: the Animator overwrites every bone from
    /// the clip on the next frame before this runs again. It touches spine, neck, head, legs and
    /// eyelids; <see cref="PlayerVisualAnimator"/> pins the root bone in its own LateUpdate and
    /// the two sets do not overlap, so the order they run in does not matter.
    /// </para>
    ///
    /// <para>
    /// Every bone is found by name suffix and every effect is independent. A rig missing eyelids
    /// simply does not blink; it does not throw, and it does not stop the legs bending.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Catch If You Can/Player Body Motion")]
    public sealed class PlayerBodyMotion : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The character's Animator. Falls back to one in the children of this object.")]
        [SerializeField] private Animator animator;

        [Tooltip("Transform the character hangs from. Dropped during a crouch so the folded legs " +
                 "keep their feet on the floor. Falls back to the animator's parent.")]
        [SerializeField] private Transform visualRoot;

        [Tooltip("The player root, whose forward and right define every axis used here. Falls " +
                 "back to the PlayerController's transform.")]
        [SerializeField] private Transform playerBody;

        [SerializeField] private PlayerController playerController;

        [Tooltip("Camera pivot, read for pitch so the head looks where the player looks.")]
        [SerializeField] private Transform cameraRoot;

        [Header("Crouch")]
        [Tooltip("Thigh swing at full crouch, degrees. Negative carries the knee forward.")]
        [SerializeField] private float crouchThighDegrees = -68f;

        [Tooltip("Shin swing at full crouch. Larger than the thigh, and the other way, which is " +
                 "what folds the leg instead of tipping the whole character over backwards. At " +
                 "these two the knee travels forward by very nearly what the ankle travels back, " +
                 "so the foot stays under the hip rather than sliding out in front of it.")]
        [SerializeField] private float crouchShinDegrees = 140f;

        [Tooltip("Ankle correction. Set to cancel the shin's tilt exactly (thigh + shin + this " +
                 "= 0), which is what keeps the sole flat on the floor instead of the character " +
                 "crouching on its toes.")]
        [SerializeField] private float crouchFootDegrees = -72f;

        [Tooltip("Forward lean of the torso at full crouch. A crouch that keeps the back vertical " +
                 "reads as sitting on an invisible chair.")]
        [SerializeField] private float crouchLeanDegrees = 22f;

        [Tooltip("Extra drop of the whole character at full crouch, beyond what folding the legs " +
                 "already accounts for. Small: the fold is measured from the rig, this is trim.")]
        [SerializeField] private float crouchExtraDrop = 0.03f;

        [Tooltip("Hand the measured crouch depth to the PlayerController, so the capsule, the " +
                 "camera and the visible legs all agree on how far down a crouch is. Without " +
                 "this the three are three numbers typed in three files, and the first time the " +
                 "character is rescaled two of them are wrong.")]
        [SerializeField] private bool driveControllerCrouchDepth = true;

        [Header("Strafe")]
        [Tooltip("How far the legs turn towards the direction of travel, in degrees, when the " +
                 "stick is pushed fully sideways. The forward walk cycle is then walking the way " +
                 "the player is actually going instead of skating sideways facing forward. " +
                 "Thirty-odd degrees, not sixty: past that the feet are pointing somewhere the " +
                 "body plainly is not going, and no amount of torso work rescues it.")]
        [SerializeField, Range(0f, 90f)] private float strafeLegYaw = 34f;

        [Tooltip("How much of that turn the torso takes back. Low on purpose. Taking nearly all " +
                 "of it back - which is what this used to do - separates the legs from the chest " +
                 "completely and reads as a mannequin swivelling at the hips; leaving most of it " +
                 "in lets the body turn with the step, the way a person actually sidesteps.")]
        [SerializeField, Range(0f, 1f)] private float strafeTorsoCounter = 0.35f;

        [Tooltip("How much of the chest's remaining turn the neck and head take back, so the " +
                 "face still looks roughly where the camera does with a glance towards the step.")]
        [SerializeField, Range(0f, 1f)] private float strafeHeadCounter = 0.6f;

        [Tooltip("Sideways lean into the step, degrees at full strafe.")]
        [SerializeField] private float strafeLeanDegrees = 7f;

        [SerializeField, Min(0.01f)] private float strafeSmoothing = 0.14f;

        [Header("Run")]
        [Tooltip("Forward lean of the torso while sprinting, degrees. A run with a vertical back " +
                 "reads as a fast walk; leaning into it is most of what makes the difference " +
                 "visible from behind and in the shadow.")]
        [SerializeField] private float sprintLeanDegrees = 9f;

        [SerializeField, Min(0.01f)] private float sprintLeanSmoothing = 0.25f;

        [Header("Head")]
        [Tooltip("Fraction of the camera's pitch the head and neck take on. Not 1: the eyes lead " +
                 "the head, so a character whose skull follows the camera exactly looks like it " +
                 "is being steered rather than looking.")]
        [SerializeField, Range(0f, 1f)] private float headPitchFollow = 0.55f;

        [Tooltip("Ceiling on that, degrees. A neck does not bend as far as a camera pitches.")]
        [SerializeField] private float headPitchLimit = 38f;

        [Header("Idle life")]
        [Tooltip("Breath rate at rest, cycles per second. About twelve breaths a minute.")]
        [SerializeField, Min(0.01f)] private float breathRate = 0.2f;

        [Tooltip("Chest rise, degrees. Deliberately tiny - this should be noticed only when it " +
                 "stops.")]
        [SerializeField] private float breathDegrees = 1.4f;

        [Tooltip("Slow weight shift while standing still, degrees.")]
        [SerializeField] private float idleSwayDegrees = 1.1f;

        [Header("Grip")]
        [Tooltip("Curl the right hand's fingers while the player is carrying something, so a " +
                 "torch is held rather than balanced on a flat palm.")]
        [SerializeField] private bool gripWhenCarrying = true;

        [Tooltip("Curl of the knuckle, the middle joint and the fingertip at a full grip, in " +
                 "degrees. Spread across three joints rather than folded at one, which is what " +
                 "stops the fingers reading as a hinge.")]
        [SerializeField] private Vector3 gripDegrees = new Vector3(42f, 52f, 36f);

        [Tooltip("The thumb, which closes less and later than the fingers.")]
        [SerializeField] private Vector3 thumbGripDegrees = new Vector3(24f, 20f, 14f);

        [Tooltip("How much of the grip the index finger takes. Less than the rest, because a " +
                 "torch is held with the index lying along the barrel near the switch rather " +
                 "than wrapped round it like the other three - which is most of the difference " +
                 "between holding a torch and making a fist round one.")]
        [SerializeField, Range(0.2f, 1f)] private float gripIndexFraction = 0.62f;

        [Tooltip("Which way the fingers fold. The axis itself is measured from the rig every " +
                 "frame - across the knuckles, from the index finger's own direction and the " +
                 "line to the little finger - so it is correct whatever the bones' local axes " +
                 "are. Only the sign cannot be derived; flip it if the hand opens backwards.")]
        [SerializeField] private float gripSign = 1f;

        [SerializeField, Min(0.01f)] private float gripSmoothing = 0.12f;

        [Tooltip("Extra knee fold while crouched and standing still, degrees. A crouch you are " +
                 "holding is deeper than one you are moving in: still, you settle onto your " +
                 "heels; creeping, you have to keep enough leg under you to take a step.")]
        [SerializeField, Range(0f, 30f)] private float crouchIdleExtraFold = 11f;

        [Tooltip("Extra forward lean while crouched and moving, degrees. This is most of what " +
                 "makes a creep read as a creep rather than as a walk done low.")]
        [SerializeField, Range(0f, 30f)] private float crouchMoveLeanDegrees = 13f;

        [Tooltip("Seconds the crouch takes to change its mind about whether it is moving.")]
        [SerializeField, Min(0.01f)] private float crouchMoveSmoothing = 0.22f;

        [Header("Flashlight arm")]
        [Tooltip("Raise the right arm into a torch-carrying pose whenever something is being " +
                 "carried. Off leaves the arm entirely to the walk clip.")]
        [SerializeField] private bool poseFlashlightArm = true;

        [Tooltip("Where the hand ends up, in the camera's own axes, measured from the head bone: " +
                 "out to the right, up, and forward, in metres. About twenty centimetres out " +
                 "puts the fist a hand's width clear of the temple.")]
        [SerializeField] private Vector3 handOffset = new Vector3(0.2f, 0.02f, 0.07f);

        [Tooltip("Where the elbow is pulled towards, in the player's own axes, measured from the " +
                 "shoulder. Down and out, so the arm makes a triangle with the ribs instead of " +
                 "flaring sideways like a chicken wing.")]
        [SerializeField] private Vector3 elbowHint = new Vector3(0.24f, -0.36f, 0.02f);

        [Tooltip("Lift of the right collarbone, degrees. Small: the shoulder should follow the " +
                 "arm, not shrug into the ear.")]
        [SerializeField, Range(0f, 14f)] private float clavicleLift = 5f;

        [Tooltip("How much of the look's pitch the whole arm follows. One means the hand orbits " +
                 "the head with the view, which is what keeps the wrist straight when the player " +
                 "looks up: the arm moves rather than the hand bending back.")]
        [SerializeField, Range(0f, 1f)] private float armFollowPitch = 1f;

        [Tooltip("Largest turn the wrist may make to line the torch up with the look, degrees. " +
                 "Past this the beam gives way rather than the wrist breaking.")]
        [SerializeField, Range(0f, 90f)] private float wristAlignLimit = 52f;

        [Header("Blink")]
        [Tooltip("How far the eyelids swing to close.")]
        [SerializeField] private float blinkDegrees = 34f;

        [Tooltip("A blink, closed and open, in seconds.")]
        [SerializeField, Min(0.02f)] private float blinkDuration = 0.13f;

        [Tooltip("Seconds between blinks, rolled per blink.")]
        [SerializeField] private Vector2 blinkInterval = new Vector2(2.2f, 6.5f);

        [Tooltip("Chance a blink is a double.")]
        [SerializeField, Range(0f, 1f)] private float doubleBlinkChance = 0.22f;

        // ---- bones -------------------------------------------------------------------------

        private Transform _spine01, _spine02, _spine03, _neck, _head;
        private Transform _upperLegL, _upperLegR, _lowerLegL, _lowerLegR, _footL, _footR;
        private Transform _eyelidL, _eyelidR;
        private Transform _clavicleR, _upperArmR, _lowerArmR, _handR;

        // Right hand only: it is the hand the torch and every other item are held in.
        private readonly Transform[][] _fingers = new Transform[4][];
        private Transform[] _thumb;
        private Transform _indexRoot, _pinkyRoot;
        private PlayerInventory _inventory;
        private float _grip;
        private float _gripVelocity;

        private bool _bound;

        // Measured from the rig at bind time, so the crouch drop is the rig's own leg length
        // rather than a number typed in that happens to suit one character.
        private float _thighLength;
        private float _shinLength;

        /// <summary>
        /// How far the character actually sinks at full crouch, in metres, from this rig's own
        /// leg lengths and the authored fold. Zero until the character has been bound.
        /// </summary>
        public float FullCrouchDrop { get; private set; }

        /// <summary>
        /// How far the character's head has actually ended up below where it sits standing, in
        /// metres, measured off the head bone after the crouch has been folded in.
        ///
        /// <para>
        /// The camera follows this rather than the leg-length figure, because the two are not the
        /// same number: a crouch drops the hips by what the legs fold, and then leans the torso
        /// forward over the knees, which drops the head again by the length of the spine times
        /// the cosine of the lean. Dropping the camera by only the first of those leaves the view
        /// floating several centimetres above the character's own eyes.
        /// </para>
        /// </summary>
        public float MeasuredHeadDrop { get; private set; }

        /// <summary>
        /// Runs at the very end of this component's LateUpdate, after every bone has been posed.
        /// Anything that has to sit in the character's hand hangs off this rather than off its
        /// own LateUpdate: two LateUpdates in the same frame have no defined order, and a torch
        /// placed before the arm that carries it has been posed is a torch one frame behind the
        /// hand for as long as the game runs.
        /// </summary>
        public void SetPoseListener(System.Action callback) => _afterPose = callback;

        /// <summary>
        /// The frame a cylinder held in the right fist occupies: the middle of the palm, the axis
        /// the fingers close around, and the way out of the back of the hand.
        ///
        /// <para>
        /// Measured off the actual finger bones every frame rather than authored as a local
        /// offset, for the same reason every rotation in this file is a world-space one: a bone's
        /// own axes are whatever the exporter produced. The knuckles cannot lie about which way
        /// they point.
        /// </para>
        /// </summary>
        public bool TryGetGrip(out Vector3 palm, out Vector3 barrelAxis, out Vector3 palmNormal)
        {
            palm = default;
            barrelAxis = default;
            palmNormal = default;

            if (_handR == null || _indexRoot == null || _pinkyRoot == null)
                return false;

            Transform middleRoot = _fingers[1] != null ? _fingers[1][0] : null;
            if (middleRoot == null)
                return false;

            Vector3 across = _indexRoot.position - _pinkyRoot.position;
            Vector3 along = middleRoot.position - _handR.position;
            if (across.sqrMagnitude < 1e-8f || along.sqrMagnitude < 1e-8f)
                return false;

            barrelAxis = across.normalized;
            palmNormal = Vector3.Cross(barrelAxis, along.normalized);
            if (palmNormal.sqrMagnitude < 1e-8f)
                return false;
            palmNormal.Normalize();

            // Half way along the proximal bones rather than at the wrist: that is where a fist
            // actually closes, and where the middle of a torch has to sit to be held rather than
            // pinched at the base of the fingers.
            palm = _handR.position + along * 0.55f;
            return true;
        }

        private Vector3 _visualBasePosition;
        private bool _hasVisualRoot;

        private float _strafe;
        private float _strafeVelocity;
        private float _forward;
        private float _forwardVelocity;
        private float _sprint;
        private float _sprintVelocity;

        private float _crouchMove;
        private float _crouchMoveVelocity;
        private float _standingHeadLocalY = float.NaN;
        private System.Action _afterPose;

        private float _blinkTimer;
        private float _blinkPhase = -1f;
        private int _blinksLeft;
        private System.Random _rng;

        private void Awake()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
            if (playerController == null)
                playerController = GetComponentInParent<PlayerController>();
            if (playerBody == null && playerController != null)
                playerBody = playerController.transform;

            // A stream of its own per instance, so two of these never blink or glance in
            // lockstep. Seeded from a Guid rather than the instance id: the id was only ever
            // a convenient unique number, it is not identity that matters here, and Unity 6.5
            // made GetInstanceID an error in favour of an API that does not exist in older
            // versions. A Guid is unique, needs no engine call, and cannot be deprecated.
            _rng = new System.Random(System.Guid.NewGuid().GetHashCode());
            _blinkTimer = Range(blinkInterval);

            Bind();
        }

        /// <summary>
        /// Points this at a character instantiated after the player was built, which is how the
        /// visual actually arrives.
        /// </summary>
        public void BindAnimator(Animator target)
        {
            animator = target;
            Bind();
        }

        private void Bind()
        {
            _bound = false;
            if (animator == null)
                return;

            if (visualRoot == null)
                visualRoot = animator.transform.parent;

            _hasVisualRoot = visualRoot != null;
            if (_hasVisualRoot)
                _visualBasePosition = visualRoot.localPosition;

            var all = animator.GetComponentsInChildren<Transform>(true);

            _spine01 = Find(all, "_spine_01");
            _spine02 = Find(all, "_spine_02");
            _spine03 = Find(all, "_spine_03");
            _neck = Find(all, "_neck");
            _head = Find(all, "_head");
            _upperLegL = Find(all, "_upperleg_l");
            _upperLegR = Find(all, "_upperleg_r");
            _lowerLegL = Find(all, "_lowerleg_l");
            _lowerLegR = Find(all, "_lowerleg_r");
            _footL = Find(all, "_foot_l");
            _footR = Find(all, "_foot_r");
            _eyelidL = Find(all, "_eyelid_l");
            _eyelidR = Find(all, "_eyelid_r");

            // Renderpeople calls the collarbone the shoulder and the shoulder the upper arm.
            _clavicleR = Find(all, "_shoulder_r");
            _upperArmR = Find(all, "_upperarm_r");
            _lowerArmR = Find(all, "_lowerarm_r");
            _handR = Find(all, "_hand_r");

            _fingers[0] = FindChain(all, "index");
            _fingers[1] = FindChain(all, "middle");
            _fingers[2] = FindChain(all, "ring");
            _fingers[3] = FindChain(all, "pinky");
            _thumb = FindChain(all, "thumb");
            _indexRoot = _fingers[0][0];
            _pinkyRoot = _fingers[3][0];

            MeasureLegs();
            FullCrouchDrop = ComputeCrouchDrop(1f);

            // One measured number, three consumers. The controller sizes its capsule and drops
            // its camera by exactly what the legs turned out to be able to do, so a crouch can
            // never be a deep duck for collision and a polite bob for the body.
            if (driveControllerCrouchDepth && playerController != null && FullCrouchDrop > 0.01f)
                playerController.SetCrouchDepth(FullCrouchDrop);

            _bound = true;
        }

        /// <summary>
        /// Vertical the leg loses at a given amount of crouch. The thigh ends up
        /// <c>|thigh|</c> off vertical and the shin, which inherits the thigh's turn,
        /// <c>|shin + thigh|</c>, so what the leg still spans is the sum of the two cosines.
        /// </summary>
        private float ComputeCrouchDrop(float crouch) =>
            ComputeCrouchDrop(crouch, crouchThighDegrees, crouchShinDegrees);

        private float ComputeCrouchDrop(float crouch, float thighDegrees, float shinDegrees)
        {
            float thighTilt = Mathf.Abs(thighDegrees * crouch) * Mathf.Deg2Rad;
            float shinTilt = Mathf.Abs((shinDegrees + thighDegrees) * crouch) * Mathf.Deg2Rad;
            float folded = _thighLength * Mathf.Cos(thighTilt) + _shinLength * Mathf.Cos(shinTilt);
            float straight = _thighLength + _shinLength;
            return Mathf.Max(0f, straight - folded) + crouchExtraDrop * crouch;
        }

        /// <summary>
        /// Reads the leg out of the rig in world units, so the crouch knows how far the hips
        /// actually travel when the knee bends. Measured rather than authored: the character is
        /// scaled by the player factory, and a hard-coded drop would be wrong the moment that
        /// scale changed.
        /// </summary>
        private void MeasureLegs()
        {
            _thighLength = 0f;
            _shinLength = 0f;

            if (_upperLegL != null && _lowerLegL != null)
                _thighLength = Vector3.Distance(_upperLegL.position, _lowerLegL.position);
            if (_lowerLegL != null && _footL != null)
                _shinLength = Vector3.Distance(_lowerLegL.position, _footL.position);
        }

        /// <summary>The three bones of one right-hand digit, by the rig's naming.</summary>
        private static Transform[] FindChain(Transform[] all, string digit)
        {
            return new[]
            {
                Find(all, "_" + digit + "_01_r"),
                Find(all, "_" + digit + "_02_r"),
                Find(all, "_" + digit + "_03_r")
            };
        }

        private static Transform Find(Transform[] all, string suffix)
        {
            for (int i = 0; i < all.Length; i++)
                if (all[i].name.EndsWith(suffix, System.StringComparison.OrdinalIgnoreCase))
                    return all[i];
            return null;
        }

        private float Range(Vector2 range) =>
            Mathf.Lerp(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y), (float)_rng.NextDouble());

        // ---- per frame ---------------------------------------------------------------------

        private void Update()
        {
            // Input smoothing belongs in Update, where the input is fresh. LateUpdate only
            // applies what this decided.
            Vector2 move = playerController != null ? playerController.LocalMoveInput : Vector2.zero;
            _strafe = Mathf.SmoothDamp(_strafe, Mathf.Clamp(move.x, -1f, 1f), ref _strafeVelocity, strafeSmoothing);
            _forward = Mathf.SmoothDamp(_forward, Mathf.Clamp(move.y, -1f, 1f), ref _forwardVelocity, strafeSmoothing);

            float sprinting = playerController != null && playerController.IsSprinting ? 1f : 0f;
            _sprint = Mathf.SmoothDamp(_sprint, sprinting, ref _sprintVelocity, sprintLeanSmoothing);

            UpdateBlinkTimer();

            if (_inventory == null)
                _inventory = GetComponentInParent<PlayerInventory>();

            bool carrying = gripWhenCarrying && _inventory != null &&
                            _inventory.GetSelectedItem() != null;
            _grip = Mathf.SmoothDamp(_grip, carrying ? 1f : 0f, ref _gripVelocity, gripSmoothing);
        }

        private void UpdateBlinkTimer()
        {
            if (_blinkPhase >= 0f)
            {
                _blinkPhase += Time.deltaTime / blinkDuration;
                if (_blinkPhase < 1f)
                    return;

                _blinkPhase = -1f;
                _blinksLeft--;
                // A double blink is two blinks with almost nothing between them, not one long
                // one; that difference is most of what makes a blink read as alive.
                _blinkTimer = _blinksLeft > 0 ? 0.07f : Range(blinkInterval);
                return;
            }

            _blinkTimer -= Time.deltaTime;
            if (_blinkTimer > 0f)
                return;

            if (_blinksLeft <= 0)
                _blinksLeft = _rng.NextDouble() < doubleBlinkChance ? 2 : 1;

            _blinkPhase = 0f;
        }

        private void LateUpdate()
        {
            if (!_bound || playerBody == null)
                return;

            Vector3 right = playerBody.right;
            Vector3 up = playerBody.up;
            Vector3 forward = playerBody.forward;

            float crouch = playerController != null ? playerController.CrouchAmount01 : 0f;
            float moving = Mathf.Clamp01(new Vector2(_strafe, _forward).magnitude);

            _crouchMove = Mathf.SmoothDamp(_crouchMove, moving > 0.12f ? 1f : 0f,
                                           ref _crouchMoveVelocity, crouchMoveSmoothing);

            ApplyCrouch(crouch, right, _crouchMove);
            // Measured here, between the crouch and everything else, so it is the crouch's own
            // drop and not a breath or a sprint lean.
            MeasureHeadDrop(crouch);

            ApplySprintLean(right);
            ApplyStrafe(right, up, moving);
            ApplyIdle(right, forward, crouch, moving);
            ApplyHeadPitch(right);
            ApplyFlashlightArm(right, up, forward);
            ApplyGrip();
            ApplyBlink(right);

            _afterPose?.Invoke();
        }

        /// <summary>
        /// Reads the head bone's height off the rig and keeps the standing value as a reference,
        /// so <see cref="MeasuredHeadDrop"/> is a measurement rather than an estimate.
        /// </summary>
        private void MeasureHeadDrop(float crouch)
        {
            if (_head == null)
                return;

            float y = playerBody.InverseTransformPoint(_head.position).y;

            if (crouch <= 0.001f)
            {
                // Followed slowly rather than sampled once: the idle clip moves the head a
                // centimetre or so, and a single sample taken on the wrong frame of it would
                // become a permanent error in where the camera thinks the eyes are.
                _standingHeadLocalY = float.IsNaN(_standingHeadLocalY)
                    ? y
                    : Mathf.Lerp(_standingHeadLocalY, y, 0.04f);
                MeasuredHeadDrop = 0f;
                return;
            }

            if (float.IsNaN(_standingHeadLocalY))
                _standingHeadLocalY = y;

            MeasuredHeadDrop = Mathf.Max(0f, _standingHeadLocalY - y);
        }

        /// <summary>
        /// Folds the legs and drops the character by as much as the fold shortened them, so the
        /// feet stay on the floor rather than the whole body sinking through it or hovering.
        /// </summary>
        private void ApplyCrouch(float crouch, Vector3 right, float creep)
        {
            if (crouch <= 0.001f)
            {
                if (_hasVisualRoot && visualRoot.localPosition != _visualBasePosition)
                    visualRoot.localPosition = _visualBasePosition;
                return;
            }

            // Two crouches, not one. Held still it settles deeper onto the heels; moving it comes
            // up a little and leans further over the front foot, because a full squat is a pose
            // you can hold and not one you can walk in.
            float still = 1f - Mathf.Clamp01(creep);
            float thighDegrees = crouchThighDegrees - crouchIdleExtraFold * still;
            float shinDegrees = crouchShinDegrees + crouchIdleExtraFold * still * 1.4f;

            float thigh = thighDegrees * crouch;
            float shin = shinDegrees * crouch;
            float foot = crouchFootDegrees * crouch;

            RotateWorld(_upperLegL, right, thigh);
            RotateWorld(_upperLegR, right, thigh);
            RotateWorld(_lowerLegL, right, shin);
            RotateWorld(_lowerLegR, right, shin);
            RotateWorld(_footL, right, foot);
            RotateWorld(_footR, right, foot);

            // Lean the torso forward over the knees, spread down the spine so no single joint
            // has to do all of it, and further while creeping.
            float lean = (crouchLeanDegrees + crouchMoveLeanDegrees * Mathf.Clamp01(creep)) * crouch;
            RotateWorld(_spine01, right, lean * 0.45f);
            RotateWorld(_spine02, right, lean * 0.35f);
            RotateWorld(_spine03, right, lean * 0.2f);

            if (!_hasVisualRoot)
                return;

            // Dropped by what the legs at *these* angles actually lost, not by the standing
            // figure: the two crouches fold by different amounts, and a drop that does not follow
            // is feet through the floor in one of them and hovering in the other.
            Vector3 local = _visualBasePosition;
            local.y -= ComputeCrouchDrop(crouch, thighDegrees, shinDegrees);
            visualRoot.localPosition = local;
        }

        /// <summary>
        /// Leans the torso into a run. Spread down the spine rather than hinged at the waist, so
        /// the character bends rather than tipping over like a signpost.
        /// </summary>
        private void ApplySprintLean(Vector3 right)
        {
            if (_sprint < 0.01f)
                return;

            float lean = sprintLeanDegrees * _sprint;
            RotateWorld(_spine01, right, lean * 0.4f);
            RotateWorld(_spine02, right, lean * 0.35f);
            RotateWorld(_spine03, right, lean * 0.25f);

            // And bring the head back up, or a sprinting character runs staring at its own feet.
            RotateWorld(_neck, right, -lean * 0.55f);
            RotateWorld(_head, right, -lean * 0.35f);
        }

        /// <summary>
        /// Turns the legs towards where the player is actually going and turns the chest back, so
        /// a sidestep is a sidestep. The walk cycle is a forward walk; pointing it along the
        /// direction of travel is what makes it one.
        /// </summary>
        private void ApplyStrafe(Vector3 right, Vector3 up, float moving)
        {
            if (moving < 0.05f || Mathf.Abs(_strafe) < 0.02f)
                return;

            // Signed angle of the stick off straight ahead, so walking backwards turns the legs
            // right around rather than folding the character in half.
            float yaw = Mathf.Atan2(_strafe, Mathf.Max(_forward, 0.001f)) * Mathf.Rad2Deg;
            yaw = Mathf.Clamp(yaw, -strafeLegYaw, strafeLegYaw) * Mathf.Clamp01(moving);

            RotateWorld(_upperLegL, up, yaw);
            RotateWorld(_upperLegR, up, yaw);

            // The spine takes back only part of the leg turn, so what is left carries the chest
            // round with the step. Cancelling nearly all of it - which is what this did - is the
            // pose that reads as wrong: legs swung fully sideways under a torso bolted facing
            // forward. A person sidestepping turns; they just turn less than their feet do, and
            // then look back the way they were going.
            float counter = -yaw * strafeTorsoCounter;
            RotateWorld(_spine01, up, counter * 0.4f);
            RotateWorld(_spine02, up, counter * 0.35f);
            RotateWorld(_spine03, up, counter * 0.25f);

            // The head keeps facing the way the player is looking, minus a glance towards the
            // step. Without this the chest turn would drag the face round with it.
            float chest = yaw + counter;
            RotateWorld(_neck, up, -chest * strafeHeadCounter * 0.5f);
            RotateWorld(_head, up, -chest * strafeHeadCounter * 0.5f);

            // And lean into it. Rolling about forward is a lean towards the leading foot.
            float lean = -strafeLeanDegrees * _strafe;
            RotateWorld(_spine01, playerBody.forward, lean * 0.5f);
            RotateWorld(_spine02, playerBody.forward, lean * 0.5f);
        }

        /// <summary>
        /// Puts the right arm into a torch-carrying pose: collarbone lifted a little, the hand
        /// beside the temple, and the elbow low and out.
        ///
        /// <para>
        /// Solved rather than authored. The pose is described by where the hand has to be and
        /// where the elbow has to point, and a two-bone solve turns those into the two rotations
        /// that put it there - which is the only way to hit "the fist sits twelve centimetres
        /// from the temple" on a rig whose bone lengths are the model's, not mine. It is applied
        /// on top of whatever the walk clip wrote, weighted by the same smoothed value that
        /// closes the fingers, so the arm rises as the torch is taken out and settles back into
        /// the clip when it is put away.
        /// </para>
        ///
        /// <para>
        /// The target is built in the <em>camera's</em> axes, not the body's, and that is what
        /// keeps the wrist straight: when the player looks up, the whole arm swings up with the
        /// view and the hand keeps its own relationship to the head, instead of the hand staying
        /// put and the wrist bending back to aim the beam.
        /// </para>
        /// </summary>
        private void ApplyFlashlightArm(Vector3 right, Vector3 up, Vector3 forward)
        {
            if (!poseFlashlightArm || _grip < 0.01f)
                return;
            if (_upperArmR == null || _lowerArmR == null || _handR == null || _head == null)
                return;

            float weight = Mathf.Clamp01(_grip);

            Transform view = cameraRoot != null ? cameraRoot : playerBody;
            Vector3 camForward = view.forward;
            Vector3 camRight = view.right;
            Vector3 camUp = view.up;

            if (armFollowPitch < 0.999f)
            {
                Vector3 flat = Vector3.ProjectOnPlane(camForward, up);
                if (flat.sqrMagnitude > 1e-6f)
                {
                    Quaternion level = Quaternion.LookRotation(flat.normalized, up);
                    Quaternion full = Quaternion.LookRotation(camForward, camUp);
                    Quaternion blend = Quaternion.Slerp(level, full, armFollowPitch);
                    camForward = blend * Vector3.forward;
                    camRight = blend * Vector3.right;
                    camUp = blend * Vector3.up;
                }
            }

            Vector3 target = _head.position +
                             camRight * handOffset.x +
                             camUp * handOffset.y +
                             camForward * handOffset.z;

            Vector3 shoulder = _upperArmR.position;
            Vector3 pole = shoulder +
                           right * elbowHint.x +
                           up * elbowHint.y +
                           forward * elbowHint.z;

            // Positive about the player's forward axis lifts the right collarbone: turning the
            // bone's own outward direction, +X, towards up.
            if (_clavicleR != null)
                RotateWorld(_clavicleR, forward, clavicleLift * weight);

            Quaternion upperBefore = _upperArmR.rotation;
            Quaternion lowerBefore = _lowerArmR.rotation;

            SolveTwoBone(_upperArmR, _lowerArmR, _handR, target, pole);

            Quaternion upperSolved = _upperArmR.rotation;
            Quaternion lowerSolved = _lowerArmR.rotation;

            // Set parent first, then child, both absolutely in world space: the child's world
            // rotation is written after it has already inherited the parent's, so the blend does
            // not fight itself.
            _upperArmR.rotation = Quaternion.Slerp(upperBefore, upperSolved, weight);
            _lowerArmR.rotation = Quaternion.Slerp(lowerBefore, lowerSolved, weight);

            AlignGripToBeam(camForward, weight);
        }

        /// <summary>
        /// Turns the hand so the axis the fingers close around points down the beam, within a
        /// limit past which the wrist gives up rather than breaking.
        /// </summary>
        private void AlignGripToBeam(Vector3 beam, float weight)
        {
            if (_indexRoot == null || _pinkyRoot == null)
                return;

            Vector3 barrel = _indexRoot.position - _pinkyRoot.position;
            if (barrel.sqrMagnitude < 1e-8f)
                return;

            Quaternion align = Quaternion.FromToRotation(barrel.normalized, beam);
            align = Quaternion.RotateTowards(Quaternion.identity, align, wristAlignLimit);
            _handR.rotation = Quaternion.Slerp(_handR.rotation, align * _handR.rotation, weight);
        }

        /// <summary>
        /// The two rotations that put <paramref name="tip"/> on <paramref name="target"/> with the
        /// joint between them bent towards <paramref name="pole"/>. Analytic, not iterative: with
        /// two bones the elbow's position is a triangle with three known sides, so there is
        /// nothing to converge on.
        /// </summary>
        private static void SolveTwoBone(Transform root, Transform mid, Transform tip,
                                         Vector3 target, Vector3 pole)
        {
            Vector3 a = root.position;
            float upperLength = Vector3.Distance(a, mid.position);
            float lowerLength = Vector3.Distance(mid.position, tip.position);
            if (upperLength < 1e-5f || lowerLength < 1e-5f)
                return;

            Vector3 toTarget = target - a;
            float reach = toTarget.magnitude;
            if (reach < 1e-5f)
                return;

            // Kept off both ends of the range: fully straight has no bend plane to speak of and
            // fully folded is a joint turned inside out.
            float min = Mathf.Abs(upperLength - lowerLength) + 0.01f;
            float max = upperLength + lowerLength - 0.01f;
            float distance = Mathf.Clamp(reach, min, max);
            Vector3 direction = toTarget / reach;

            Vector3 poleDirection = pole - a;
            Vector3 bendNormal = Vector3.Cross(direction, poleDirection);
            if (bendNormal.sqrMagnitude < 1e-8f)
                bendNormal = Vector3.Cross(direction, Vector3.up);
            if (bendNormal.sqrMagnitude < 1e-8f)
                return;
            bendNormal.Normalize();

            float cosine = Mathf.Clamp(
                (upperLength * upperLength + distance * distance - lowerLength * lowerLength) /
                (2f * upperLength * distance), -1f, 1f);
            float openingDegrees = Mathf.Acos(cosine) * Mathf.Rad2Deg;

            // Turning the aim towards the pole about the plane's normal is what decides which
            // side the elbow ends up on.
            Vector3 upperDirection = Quaternion.AngleAxis(openingDegrees, bendNormal) * direction;
            Vector3 elbow = a + upperDirection * upperLength;

            root.rotation = Quaternion.FromToRotation(mid.position - a, elbow - a) * root.rotation;

            // Re-read after the root turned: the elbow has moved, and the second rotation is
            // measured from where it actually is.
            Vector3 m = mid.position;
            mid.rotation = Quaternion.FromToRotation(tip.position - m, target - m) * mid.rotation;
        }

        /// <summary>
        /// Breath and a slow weight shift, faded out as the player starts moving so it never
        /// fights the walk cycle. Both are small enough to be invisible on their own and obvious
        /// by their absence — a body that is perfectly still reads as a mannequin.
        /// </summary>
        private void ApplyIdle(Vector3 right, Vector3 forward, float crouch, float moving)
        {
            float rest = 1f - Mathf.Clamp01(moving * 1.6f);
            if (rest <= 0.01f)
                return;

            // Breathing carries on while crouched, faster and shallower.
            float rate = breathRate * Mathf.Lerp(1f, 1.45f, crouch);
            float breath = Mathf.Sin(Time.time * rate * Mathf.PI * 2f);
            RotateWorld(_spine03, right, -breathDegrees * breath * rest);
            RotateWorld(_spine02, right, -breathDegrees * 0.4f * breath * rest);

            // Two incommensurate periods, so the sway never settles into a visible loop.
            float sway = Mathf.Sin(Time.time * 0.37f) * 0.7f + Mathf.Sin(Time.time * 0.23f) * 0.3f;
            RotateWorld(_spine01, forward, idleSwayDegrees * sway * rest);
            RotateWorld(_neck, forward, -idleSwayDegrees * 0.5f * sway * rest);
        }

        /// <summary>
        /// Bends the neck and head towards where the camera is pointing. The local player mostly
        /// sees this in their own shadow; a remote player sees it as the character looking at
        /// what its owner is looking at.
        /// </summary>
        private void ApplyHeadPitch(Vector3 right)
        {
            if (cameraRoot == null || headPitchFollow <= 0f)
                return;

            float pitch = cameraRoot.localEulerAngles.x;
            if (pitch > 180f)
                pitch -= 360f;

            float applied = Mathf.Clamp(pitch * headPitchFollow, -headPitchLimit, headPitchLimit);
            RotateWorld(_neck, right, applied * 0.4f);
            RotateWorld(_head, right, applied * 0.6f);
        }

        /// <summary>
        /// Folds the right hand round whatever it is holding.
        ///
        /// <para>
        /// The fold axis is measured from the rig each frame rather than assumed: the index
        /// finger's own direction crossed with the line across the knuckles to the little finger
        /// is the axis the fingers actually bend about, whatever the exporter called the bones'
        /// local axes. That is the same reasoning as everything else here, and it is why this
        /// works on a rig nobody opened.
        /// </para>
        /// </summary>
        private void ApplyGrip()
        {
            if (_grip < 0.01f || _indexRoot == null || _pinkyRoot == null)
                return;

            Transform indexMid = _fingers[0][1];
            if (indexMid == null)
                return;

            Vector3 along = indexMid.position - _indexRoot.position;
            Vector3 across = _pinkyRoot.position - _indexRoot.position;
            Vector3 axis = Vector3.Cross(along, across);
            if (axis.sqrMagnitude < 0.0000001f)
                return;

            axis = axis.normalized * Mathf.Sign(gripSign == 0f ? 1f : gripSign);

            for (int f = 0; f < _fingers.Length; f++)
            {
                // Index is _fingers[0]; it stays straighter than the rest.
                float amount = _grip * (f == 0 ? gripIndexFraction : 1f);
                RotateWorld(_fingers[f][0], axis, gripDegrees.x * amount);
                RotateWorld(_fingers[f][1], axis, gripDegrees.y * amount);
                RotateWorld(_fingers[f][2], axis, gripDegrees.z * amount);
            }

            if (_thumb == null)
                return;

            RotateWorld(_thumb[0], axis, thumbGripDegrees.x * _grip);
            RotateWorld(_thumb[1], axis, thumbGripDegrees.y * _grip);
            RotateWorld(_thumb[2], axis, thumbGripDegrees.z * _grip);
        }

        private void ApplyBlink(Vector3 right)
        {
            if (_blinkPhase < 0f || (_eyelidL == null && _eyelidR == null))
                return;

            // Closed at the middle of the phase, open at both ends.
            float close = Mathf.Sin(_blinkPhase * Mathf.PI);
            float angle = blinkDegrees * close;

            RotateWorld(_eyelidL, right, angle);
            RotateWorld(_eyelidR, right, angle);
        }

        private static void RotateWorld(Transform bone, Vector3 axis, float degrees)
        {
            if (bone == null || Mathf.Abs(degrees) < 0.001f)
                return;

            bone.Rotate(axis, degrees, Space.World);
        }

        private void OnDisable()
        {
            // Never leave the character folded into a crouch it can no longer come out of.
            if (_hasVisualRoot && visualRoot != null)
                visualRoot.localPosition = _visualBasePosition;
        }
    }
}
