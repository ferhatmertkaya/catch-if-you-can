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

        [Header("Look follow")]
        [Tooltip("Let the head lead the turn and the body come after it. Off leaves the whole " +
                 "character welded to the camera's yaw, which is what it was.")]
        [SerializeField] private bool followLook = true;

        [SerializeField, Range(0f, 1f)] private float neckYawWeight = 0.4f;
        [SerializeField, Range(0f, 1f)] private float headYawWeight = 0.6f;

        [Tooltip("Anatomical stops, in degrees each way. Past their sum the shoulders have to " +
                 "come round, which is what the spine share below is for.")]
        [SerializeField, Range(0f, 60f)] private float maxNeckYaw = 25f;

        [SerializeField, Range(0f, 80f)] private float maxHeadYaw = 55f;

        [Tooltip("How far the head may be ahead of the shoulders, in degrees, before the chest " +
                 "starts helping.")]
        [SerializeField, Range(0f, 80f)] private float upperSpineContributionThreshold = 45f;

        [SerializeField, Range(0f, 1f)] private float upperSpineWeight = 0.15f;

        [Tooltip("Seconds the shoulders take to catch up with the head while the player is " +
                 "turning, and the slower time they take to settle once the turning stops.")]
        [SerializeField, Min(0.01f)] private float lookSmoothing = 0.1f;

        [SerializeField, Min(0.01f)] private float lookReturnSmoothing = 0.14f;

        [Header("Head")]
        [Tooltip("Fraction of the camera's pitch the head and neck take on. Not 1: the eyes lead " +
                 "the head, so a character whose skull follows the camera exactly looks like it " +
                 "is being steered rather than looking.")]
        [SerializeField, Range(0f, 1f)] private float headPitchFollow = 0.6f;

        [SerializeField, Range(0f, 1f)] private float neckPitchWeight = 0.4f;
        [SerializeField, Range(0f, 1f)] private float headPitchWeight = 0.6f;

        [Tooltip("How far the head may tip, in degrees. A neck goes further down than up.")]
        [SerializeField, Range(0f, 60f)] private float maxPitchUp = 30f;

        [SerializeField, Range(0f, 60f)] private float maxPitchDown = 40f;


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

        [Header("Held item arm")]
        [Tooltip("Raise the right arm into a torch-carrying pose whenever something is being " +
                 "carried. Off leaves the arm entirely to the walk clip.")]
        [SerializeField] private bool poseHeldItemArm = true;

        [Tooltip("The hand's goal. Created under the camera pivot at start-up if it is empty, " +
                 "named HeldItemHandTarget, and left in the hierarchy so it can be dragged and " +
                 "turned in Play Mode. Its position is where the fist goes; its rotation is the " +
                 "wrist, and therefore the torch and the beam: forward is the barrel, up is the " +
                 "back of the hand.")]
        [SerializeField] private Transform heldItemHandTarget;

        [Tooltip("The elbow's goal, roughly where the point of the elbow should sit. Created " +
                 "under the player root if it is empty, named HeldItemElbowHint. Only its " +
                 "position is used - it decides which way the arm bends, nothing else.")]
        [SerializeField] private Transform heldItemElbowHint;

        [Tooltip("Where the hand target starts, in the camera pivot's own axes. This is the " +
                 "value the created target is built with, and the one to overwrite once a better " +
                 "one has been found in Play Mode.")]
        [SerializeField] private Vector3 handTargetLocalPosition = new Vector3(0.2f, -0.02f, 0.06f);

        [Tooltip("Where the hand target starts turned to, in the camera pivot's own axes. Zero " +
                 "points the barrel exactly where the player is looking; the roll is what turns " +
                 "the palm down and inwards instead of leaving it flat.")]
        [SerializeField] private Vector3 handTargetLocalEuler = new Vector3(0f, 0f, -35f);

        [Tooltip("Where the elbow hint starts, in the player root's own axes: out to the right, " +
                 "up from the floor, forward. Below the shoulder and outside the ribs.")]
        [SerializeField] private Vector3 elbowHintLocalPosition = new Vector3(0.42f, 1.02f, 0.06f);

        [Tooltip("Lift of the right collarbone, degrees. Small: the shoulder should follow the " +
                 "arm, not shrug into the ear.")]
        [SerializeField, Range(0f, 14f)] private float clavicleLift = 5f;

        [Tooltip("Hold the elbow inside this bend, in degrees of flexion, by pulling the hand " +
                 "target in or out along its own direction from the shoulder. The direction the " +
                 "target is dragged to is always obeyed; only how far away it is gets clamped.")]
        [SerializeField] private bool enforceElbowRange = true;

        [SerializeField] private Vector2 elbowFlexionRange = new Vector2(95f, 110f);

        [Tooltip("How much of the twist between forearm and hand the forearm takes, rather than " +
                 "the wrist. This is what stops the arm looking wrung out: a two-bone solve only " +
                 "says which way the forearm points, so without this every degree of roll the " +
                 "grip needs is paid for at the wrist.")]
        [SerializeField, Range(0f, 1f)] private float forearmRollShare = 0.65f;

        [Tooltip("How far the wrist may turn away from simply following the forearm, in degrees. " +
                 "Past this the grip gives way rather than the wrist breaking.")]
        [SerializeField, Range(0f, 90f)] private float wristLimitDegrees = 55f;

        [Tooltip("Draw the hand target and elbow hint in the scene view, so they can be found " +
                 "and grabbed while the game is running.")]
        [SerializeField] private bool drawArmGizmos = true;

        [Tooltip("Write the right arm's resolved bone paths to the log once at start-up.")]
        [SerializeField] private bool logArmBones = true;

        [Header("Held item arm trim")]
        [Tooltip("Turns added to each arm bone after it has been solved, in its own local axes " +
                 "and in degrees. All zero is the solve untouched. These are here to be dragged " +
                 "in the Inspector while the game is running: the pose follows immediately, so a " +
                 "wrist that curls the wrong way can be dialled straight and the numbers kept, " +
                 "rather than guessed at in code by someone who cannot see the screen.")]
        [SerializeField] private Vector3 rightShoulderRotationOffset;

        [SerializeField] private Vector3 rightUpperArmRotationOffset;
        [SerializeField] private Vector3 rightLowerArmRotationOffset;
        [SerializeField] private Vector3 rightHandRotationOffset;

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

        [Tooltip("How this character's skeleton is named. Left empty the built-in default " +
                 "is used, which is Nathan's naming and exactly what this code used to " +
                 "have written into it as literals.")]
        [SerializeField] private Character.CharacterRigProfile rigProfile;

        /// <summary>
        /// The naming contract for the bound character. Never null: an unassigned profile
        /// falls back to the built-in one rather than leaving every bone lookup empty.
        /// </summary>
        public Character.CharacterRigProfile RigProfile =>
            rigProfile != null ? rigProfile : Character.CharacterRigProfile.Default;

        /// <summary>
        /// Sets the naming contract. Must be called before <see cref="BindAnimator"/>, which
        /// is when the bones are actually looked up.
        /// </summary>
        public void SetRigProfile(Character.CharacterRigProfile profile) => rigProfile = profile;
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
        private Quaternion _visualBaseRotation = Quaternion.identity;
        private Transform _view;
        private float _bodyYaw = float.NaN;
        private float _bodyYawVelocity;
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
            {
                _visualBasePosition = visualRoot.localPosition;
                _visualBaseRotation = visualRoot.localRotation;
            }

            var all = animator.GetComponentsInChildren<Transform>(true);

            // Every suffix comes from the rig profile now. The default profile carries the
            // exact strings that used to be written here, so Nathan binds to the same bones.
            var rig = RigProfile;

            _spine01 = Find(all, rig.Spine01);
            _spine02 = Find(all, rig.Spine02);
            _spine03 = Find(all, rig.Spine03);
            _neck = Find(all, rig.Neck);
            _head = Find(all, rig.Head);
            _upperLegL = Find(all, rig.UpperLegLeft);
            _upperLegR = Find(all, rig.UpperLegRight);
            _lowerLegL = Find(all, rig.LowerLegLeft);
            _lowerLegR = Find(all, rig.LowerLegRight);
            _footL = Find(all, rig.FootLeft);
            _footR = Find(all, rig.FootRight);
            _eyelidL = Find(all, rig.EyelidLeft);
            _eyelidR = Find(all, rig.EyelidRight);

            // Renderpeople calls the collarbone the shoulder and the shoulder the upper arm.
            // Another rig may not, which is why these are data rather than literals.
            _clavicleR = Find(all, rig.ClavicleRight);
            _upperArmR = Find(all, rig.UpperArmRight);
            _lowerArmR = Find(all, rig.LowerArmRight);
            _handR = Find(all, rig.HandRight);

            var digits = rig.FingerDigits;
            for (int i = 0; i < _fingers.Length; i++)
            {
                _fingers[i] = digits != null && i < digits.Length
                    ? FindChain(all, rig, digits[i])
                    : EmptyChain();
            }

            _thumb = FindChain(all, rig, rig.ThumbDigit);
            _indexRoot = _fingers[0][0];
            _pinkyRoot = _fingers[3][0];

            // The camera the player actually looks through, not the pivot it hangs under. The
            // idle scan and the breathing are applied on a transform *between* the two, so
            // anything read off the pivot cannot see them - which is exactly why the head sat
            // still while the view wandered.
            if (cameraRoot != null)
            {
                var camera = cameraRoot.GetComponentInChildren<Camera>();
                _view = camera != null ? camera.transform : cameraRoot;
            }

            BuildArmTargets();
            LogArmBones();

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
        /// Makes the two handles the arm is posed from, if they are not already assigned.
        ///
        /// <para>
        /// Real objects in the hierarchy, deliberately: they can be found, selected, dragged and
        /// turned while the game is running, and what the arm does follows immediately. The hand
        /// target hangs off the camera pivot, so it keeps its place beside the head as the player
        /// looks around and its numbers stay meaningful; the elbow hint hangs off the player
        /// root, because where an elbow should point is a fact about the body and not about the
        /// view.
        /// </para>
        ///
        /// <para>
        /// Values found in Play Mode are kept with <c>Copy live arm targets into defaults</c> on
        /// this component's context menu, then Copy Component on the header, Play Mode off, Paste
        /// Component Values. The two Vector3s below it are what the handles are rebuilt from.
        /// </para>
        /// </summary>
        private void BuildArmTargets()
        {
            Transform viewParent = cameraRoot != null ? cameraRoot : playerBody;

            if (heldItemHandTarget == null && viewParent != null)
            {
                var go = new GameObject("HeldItemHandTarget");
                heldItemHandTarget = go.transform;
                heldItemHandTarget.SetParent(viewParent, false);
                // Only on the frame it is made. Re-binding must never quietly undo a pose that
                // has just been dragged out by hand.
                heldItemHandTarget.localPosition = handTargetLocalPosition;
                heldItemHandTarget.localRotation = Quaternion.Euler(handTargetLocalEuler);
            }
            else if (heldItemHandTarget != null && viewParent != null &&
                     heldItemHandTarget.parent != viewParent)
            {
                heldItemHandTarget.SetParent(viewParent, false);
            }

            if (heldItemElbowHint == null && playerBody != null)
            {
                var go = new GameObject("HeldItemElbowHint");
                heldItemElbowHint = go.transform;
                heldItemElbowHint.SetParent(playerBody, false);
                heldItemElbowHint.localPosition = elbowHintLocalPosition;
            }
            else if (heldItemElbowHint != null && playerBody != null &&
                     heldItemElbowHint.parent != playerBody)
            {
                heldItemElbowHint.SetParent(playerBody, false);
            }
        }

        /// <summary>
        /// Copies whatever the two handles are currently at back into the serialized defaults, so
        /// a pose found by dragging them in Play Mode can be kept.
        /// </summary>
        [ContextMenu("Copy live arm targets into defaults")]
        public void CaptureArmTargets()
        {
            if (heldItemHandTarget != null)
            {
                handTargetLocalPosition = heldItemHandTarget.localPosition;
                handTargetLocalEuler = heldItemHandTarget.localEulerAngles;
            }

            if (heldItemElbowHint != null)
                elbowHintLocalPosition = heldItemElbowHint.localPosition;

            Debug.Log("[CIYC] Arm defaults captured. handTargetLocalPosition=" +
                      handTargetLocalPosition.ToString("F4") +
                      " handTargetLocalEuler=" + handTargetLocalEuler.ToString("F2") +
                      " elbowHintLocalPosition=" + elbowHintLocalPosition.ToString("F4"), this);
        }

        /// <summary>
        /// Says once, in the log, exactly which four transforms the arm is being driven through -
        /// so "which bones is it actually using" is never a question again. The twist bones are
        /// named too, to make it plain that they are not in the chain.
        /// </summary>
        private void LogArmBones()
        {
            if (!logArmBones)
                return;

            Debug.Log("[CIYC] Right arm chain:" +
                      "\n  clavicle : " + Path(_clavicleR) +
                      "\n  root     : " + Path(_upperArmR) +
                      "\n  mid      : " + Path(_lowerArmR) +
                      "\n  tip      : " + Path(_handR) +
                      "\n  index/pinky/middle roots: " + Path(_indexRoot) + " | " +
                      Path(_pinkyRoot) + " | " + Path(_fingers[1] != null ? _fingers[1][0] : null) +
                      "\n  twist bones are not driven.", this);
        }

        private static string Path(Transform bone)
        {
            if (bone == null)
                return "<missing>";

            string path = bone.name;
            for (Transform t = bone.parent; t != null; t = t.parent)
                path = t.name + "/" + path;
            return path;
        }

        private void OnDrawGizmos()
        {
            if (!drawArmGizmos)
                return;

            if (heldItemHandTarget != null)
            {
                Gizmos.color = new Color(0.35f, 0.9f, 1f);
                Gizmos.DrawWireSphere(heldItemHandTarget.position, 0.035f);
                Gizmos.DrawLine(heldItemHandTarget.position,
                                heldItemHandTarget.position + heldItemHandTarget.forward * 0.18f);
                Gizmos.color = new Color(1f, 0.8f, 0.3f);
                Gizmos.DrawLine(heldItemHandTarget.position,
                                heldItemHandTarget.position + heldItemHandTarget.up * 0.08f);
            }

            if (heldItemElbowHint != null)
            {
                Gizmos.color = new Color(1f, 0.45f, 0.35f);
                Gizmos.DrawWireSphere(heldItemElbowHint.position, 0.03f);
            }

            if (_upperArmR != null && _lowerArmR != null && _handR != null)
            {
                Gizmos.color = new Color(0.6f, 1f, 0.6f);
                Gizmos.DrawLine(_upperArmR.position, _lowerArmR.position);
                Gizmos.DrawLine(_lowerArmR.position, _handR.position);
            }
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

        /// <summary>The bones of one right-hand digit, by the profile's naming.</summary>
        private static Transform[] FindChain(Transform[] all, Character.CharacterRigProfile rig,
                                             string digit)
        {
            if (string.IsNullOrEmpty(digit))
                return EmptyChain();

            // The pose code indexes three joints. A profile that declares fewer leaves the
            // rest null, which every consumer already tolerates, rather than throwing here.
            var chain = EmptyChain();
            int joints = Mathf.Min(chain.Length, rig.JointsPerDigit);

            for (int i = 0; i < joints; i++)
            {
                var suffix = rig.DigitJointSuffix(digit, i);
                if (!string.IsNullOrEmpty(suffix))
                    chain[i] = Find(all, suffix);
            }

            return chain;
        }

        private static Transform[] EmptyChain() => new Transform[3];

        private static Transform Find(Transform[] all, string suffix)
        {
            if (string.IsNullOrEmpty(suffix))
                return null;

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

            // Before everything that reads a bone's world position - the arm solve above all -
            // because this moves the shoulders.
            ApplyLookYaw(up);

            ApplySprintLean(right);
            ApplyStrafe(right, up, moving);
            ApplyIdle(right, forward, crouch, moving);
            ApplyHeadPitch(right);
            ApplyHeldItemArm(right, up, forward);
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
        /// Lets the head lead the turn and the shoulders come after it.
        ///
        /// <para>
        /// The look's yaw is not missing from the character - it never was. It is written onto
        /// the player root, so turning the camera turns Nathan bodily, all of him, instantly.
        /// That is why he reads as stiff rather than as looking away: a person turning to look at
        /// something moves their eyes, then their head, then their shoulders, and the whole thing
        /// takes a moment. What was missing is the moment.
        /// </para>
        ///
        /// <para>
        /// So the body is held back rather than the head pushed forward. A smoothed yaw chases
        /// the root's, the visual is turned back by however far it is behind, and the neck, head
        /// and - past the point where a neck would complain - the upper chest take that same
        /// angle in the other direction, which leaves the face pointing exactly where the camera
        /// does while the shoulders are still coming round. It settles to nothing the instant the
        /// player stops turning, so it costs the game nothing: the collider, the movement and the
        /// camera never see it, and what it changes is only what the mirror and the shadow show.
        /// </para>
        ///
        /// <para>
        /// The catch-up is clamped as well as smoothed, so a fast spin drags the shoulders round
        /// with it rather than winding the neck up past its stops.
        /// </para>
        /// </summary>
        private void ApplyLookYaw(Vector3 up)
        {
            if (!_hasVisualRoot)
                return;

            if (!followLook)
            {
                if (visualRoot.localRotation != _visualBaseRotation)
                    visualRoot.localRotation = _visualBaseRotation;
                return;
            }

            float target = playerBody.eulerAngles.y;
            if (float.IsNaN(_bodyYaw))
                _bodyYaw = target;

            // Quicker while the turn is happening than while it is settling, which is the shape
            // the movement actually has.
            float behind = Mathf.Abs(Mathf.DeltaAngle(_bodyYaw, target));
            float smoothing = behind > 0.75f ? lookSmoothing : lookReturnSmoothing;
            _bodyYaw = Mathf.SmoothDampAngle(_bodyYaw, target, ref _bodyYawVelocity, smoothing);

            float limit = maxNeckYaw + maxHeadYaw;
            float lag = Mathf.Clamp(Mathf.DeltaAngle(_bodyYaw, target), -limit, limit);
            // Written back, so the shoulders are dragged along rather than left behind by a spin
            // the neck could never have followed.
            _bodyYaw = target - lag;

            visualRoot.localRotation = _visualBaseRotation * Quaternion.Euler(0f, -lag, 0f);

            // And whatever the view is doing on its own. The idle scan turns the camera without
            // turning the player, so none of it reaches the root yaw above; measured here as the
            // angle between where the body faces and where the eyes actually point, it reaches
            // the neck and the head the same way a real glance does - without the shoulders
            // moving at all, which is the point of a glance.
            lag += ViewGlanceYaw(up);

            if (Mathf.Abs(lag) < 0.01f)
                return;

            // Past the threshold the chest starts turning too, so the neck is not asked to do all
            // of a large angle on its own.
            float spine = 0f;
            float beyond = Mathf.Abs(lag) - upperSpineContributionThreshold;
            if (beyond > 0f)
                spine = Mathf.Sign(lag) * beyond * upperSpineWeight;

            float remainder = lag - spine;
            float neck = Mathf.Clamp(remainder * neckYawWeight, -maxNeckYaw, maxNeckYaw);
            float head = Mathf.Clamp(remainder * headYawWeight, -maxHeadYaw, maxHeadYaw);

            RotateWorld(_spine02, up, spine * 0.45f);
            RotateWorld(_spine03, up, spine * 0.55f);
            RotateWorld(_neck, up, neck);
            RotateWorld(_head, up, head);
        }

        /// <summary>
        /// How far the view is turned away from the body, on the level, in degrees. Zero while
        /// the player is only turning - that yaw is written onto the root and the body carries
        /// it - and non-zero whenever something moves the camera and not the player, which in
        /// practice is the idle scan.
        /// </summary>
        private float ViewGlanceYaw(Vector3 up)
        {
            if (_view == null)
                return 0f;

            Vector3 bodyForward = Vector3.ProjectOnPlane(playerBody.forward, up);
            Vector3 viewForward = Vector3.ProjectOnPlane(_view.forward, up);
            if (bodyForward.sqrMagnitude < 1e-6f || viewForward.sqrMagnitude < 1e-6f)
                return 0f;

            return Vector3.SignedAngle(bodyForward, viewForward, up);
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
        private void ApplyHeldItemArm(Vector3 right, Vector3 up, Vector3 forward)
        {
            if (!poseHeldItemArm || _grip < 0.01f)
                return;
            if (_upperArmR == null || _lowerArmR == null || _handR == null)
                return;
            if (heldItemHandTarget == null || heldItemElbowHint == null)
                return;

            float weight = Mathf.Clamp01(_grip);

            Vector3 shoulder = _upperArmR.position;
            Vector3 target = heldItemHandTarget.position;
            Vector3 pole = heldItemElbowHint.position;

            if (enforceElbowRange)
                target = ClampToElbowRange(shoulder, target);

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

            ApplyGripOrientation(weight);

            // Last, and in each bone's own axes, so a value dialled in against what is on screen
            // means the same thing next frame whatever the solve did.
            AddLocal(_clavicleR, rightShoulderRotationOffset, weight);
            AddLocal(_upperArmR, rightUpperArmRotationOffset, weight);
            AddLocal(_lowerArmR, rightLowerArmRotationOffset, weight);
            AddLocal(_handR, rightHandRotationOffset, weight);
        }

        private static void AddLocal(Transform bone, Vector3 degrees, float weight)
        {
            if (bone == null || degrees == Vector3.zero)
                return;

            bone.localRotation *= Quaternion.Slerp(Quaternion.identity,
                                                   Quaternion.Euler(degrees), weight);
        }

        /// <summary>
        /// Turns the hand to the target's own rotation, and gives the forearm its share of the
        /// twist first.
        ///
        /// <para>
        /// This is what the arm was being wrung out by. A two-bone solve only decides which
        /// <em>way</em> the forearm points; how it is rolled about its own length is left over
        /// from the walk clip. Matching only the barrel axis then left the roll wherever it
        /// happened to be, and matching the whole grip afterwards made the wrist pay for all of
        /// it at once. Here the whole orientation is solved - barrel down the target's forward,
        /// back of the hand along its up, both at once, so nothing is left free - and the part of
        /// the correction that is a twist about the forearm is handed to the forearm, which is
        /// the joint that actually does it in an arm.
        /// </para>
        ///
        /// <para>
        /// The two hand axes are measured off the knuckles rather than assumed from the bone's
        /// own orientation, for the same reason everything else here is: a bone's local axes are
        /// the exporter's business. Pinky to index is the axis a held cylinder lies along; the
        /// cross of that with the line of the fingers is the back of the hand.
        /// </para>
        /// </summary>
        private void ApplyGripOrientation(float weight)
        {
            if (_indexRoot == null || _pinkyRoot == null)
                return;

            Transform middleRoot = _fingers[1] != null ? _fingers[1][0] : null;
            if (middleRoot == null)
                return;

            Vector3 barrel = _indexRoot.position - _pinkyRoot.position;
            Vector3 fingers = middleRoot.position - _handR.position;
            if (barrel.sqrMagnitude < 1e-8f || fingers.sqrMagnitude < 1e-8f)
                return;

            barrel.Normalize();
            Vector3 backOfHand = Vector3.Cross(barrel, fingers.normalized);
            if (backOfHand.sqrMagnitude < 1e-8f)
                return;

            var current = Quaternion.LookRotation(barrel, backOfHand.normalized);
            var wanted = Quaternion.LookRotation(heldItemHandTarget.forward,
                                                 heldItemHandTarget.up);

            // The whole correction, as one turn, and how much of it is a roll about the forearm.
            Quaternion delta = wanted * Quaternion.Inverse(current);
            delta.ToAngleAxis(out float degrees, out Vector3 axis);
            if (degrees > 180f)
                degrees -= 360f;

            Vector3 forearmAxis = _handR.position - _lowerArmR.position;
            if (forearmAxis.sqrMagnitude > 1e-8f && axis.sqrMagnitude > 1e-8f)
            {
                forearmAxis.Normalize();
                float roll = degrees * Vector3.Dot(axis.normalized, forearmAxis);

                // About an axis through the elbow and along the forearm, so the wrist itself does
                // not move - only the twist the hand would otherwise have had to make.
                RotateWorld(_lowerArmR, forearmAxis, roll * forearmRollShare * weight);

                // The hand is a child of what just turned, so where its knuckles point has
                // changed. Measured again rather than reused: the whole point of the line above
                // is that the wrist now has less to do, and reusing the old measurement would
                // hand it the same amount twice.
                barrel = (_indexRoot.position - _pinkyRoot.position).normalized;
                fingers = middleRoot.position - _handR.position;
                backOfHand = Vector3.Cross(barrel, fingers.normalized);
                if (backOfHand.sqrMagnitude > 1e-8f)
                    current = Quaternion.LookRotation(barrel, backOfHand.normalized);
            }

            // Whatever the forearm did not take, the wrist takes, up to its limit. Measured from
            // where the hand sits now - which after the solve is the hand simply following the
            // forearm - so the limit is a real wrist angle and not a distance from some pose the
            // walk clip happened to be in.
            Quaternion following = _handR.rotation;
            Quaternion desired = wanted * Quaternion.Inverse(current) * following;
            desired = Quaternion.RotateTowards(following, desired, wristLimitDegrees);
            _handR.rotation = Quaternion.Slerp(following, desired, weight);
        }

        /// <summary>
        /// Pulls the hand target in or out along its own line from the shoulder until the elbow
        /// bend lands inside <see cref="elbowFlexionRange"/>. The direction is never touched, so
        /// dragging the target still puts the hand where it was dragged; only how far it reaches
        /// is held to something an elbow can do.
        /// </summary>
        private Vector3 ClampToElbowRange(Vector3 shoulder, Vector3 target)
        {
            float upperLength = Vector3.Distance(shoulder, _lowerArmR.position);
            float lowerLength = Vector3.Distance(_lowerArmR.position, _handR.position);
            if (upperLength < 1e-5f || lowerLength < 1e-5f)
                return target;

            Vector3 toTarget = target - shoulder;
            float reach = toTarget.magnitude;
            if (reach < 1e-5f)
                return target;

            // Flexion is the angle away from straight, so more flexion is a shorter reach.
            float minFlexion = Mathf.Min(elbowFlexionRange.x, elbowFlexionRange.y);
            float maxFlexion = Mathf.Max(elbowFlexionRange.x, elbowFlexionRange.y);
            float longest = ReachAtFlexion(upperLength, lowerLength, minFlexion);
            float shortest = ReachAtFlexion(upperLength, lowerLength, maxFlexion);

            float clamped = Mathf.Clamp(reach, shortest, longest);
            if (Mathf.Approximately(clamped, reach))
                return target;

            return shoulder + toTarget / reach * clamped;
        }

        /// <summary>Shoulder-to-hand distance at a given elbow flexion, by the law of cosines.</summary>
        private static float ReachAtFlexion(float upperLength, float lowerLength, float flexionDegrees)
        {
            float interior = (180f - flexionDegrees) * Mathf.Deg2Rad;
            float squared = upperLength * upperLength + lowerLength * lowerLength -
                            2f * upperLength * lowerLength * Mathf.Cos(interior);
            return Mathf.Sqrt(Mathf.Max(0.0001f, squared));
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

            // Taken off the camera itself rather than the pivot, and measured against the body,
            // so the idle breathing and the scan's own tilt are in it as well as the player's
            // deliberate look.
            Transform view = _view != null ? _view : cameraRoot;
            float pitch = -Vector3.SignedAngle(
                Vector3.ProjectOnPlane(view.forward, playerBody.up),
                view.forward,
                playerBody.right);

            // Positive pitch is looking down, which is the way a neck bends furthest.
            float applied = Mathf.Clamp(pitch * headPitchFollow, -maxPitchUp, maxPitchDown);
            RotateWorld(_neck, right, applied * neckPitchWeight);
            RotateWorld(_head, right, applied * headPitchWeight);
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
            {
                visualRoot.localPosition = _visualBasePosition;
                visualRoot.localRotation = _visualBaseRotation;
            }
        }
    }
}
