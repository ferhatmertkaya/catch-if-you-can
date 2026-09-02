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
                 "the player is actually going instead of skating sideways facing forward.")]
        [SerializeField, Range(0f, 90f)] private float strafeLegYaw = 62f;

        [Tooltip("How much of that turn the torso takes back, so the chest and head keep facing " +
                 "the way the camera is pointing. 1 gives full leg-torso separation.")]
        [SerializeField, Range(0f, 1f)] private float strafeTorsoCounter = 0.85f;

        [Tooltip("Sideways lean into the step, degrees at full strafe.")]
        [SerializeField] private float strafeLeanDegrees = 5f;

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

        private Vector3 _visualBasePosition;
        private bool _hasVisualRoot;

        private float _strafe;
        private float _strafeVelocity;
        private float _forward;
        private float _forwardVelocity;
        private float _sprint;
        private float _sprintVelocity;

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
        private float ComputeCrouchDrop(float crouch)
        {
            float thighTilt = Mathf.Abs(crouchThighDegrees * crouch) * Mathf.Deg2Rad;
            float shinTilt = Mathf.Abs((crouchShinDegrees + crouchThighDegrees) * crouch) * Mathf.Deg2Rad;
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

            ApplyCrouch(crouch, right);
            ApplySprintLean(right);
            ApplyStrafe(right, up, moving);
            ApplyIdle(right, forward, crouch, moving);
            ApplyHeadPitch(right);
            ApplyGrip();
            ApplyBlink(right);
        }

        /// <summary>
        /// Folds the legs and drops the character by as much as the fold shortened them, so the
        /// feet stay on the floor rather than the whole body sinking through it or hovering.
        /// </summary>
        private void ApplyCrouch(float crouch, Vector3 right)
        {
            if (crouch <= 0.001f)
            {
                if (_hasVisualRoot && visualRoot.localPosition != _visualBasePosition)
                    visualRoot.localPosition = _visualBasePosition;
                return;
            }

            float thigh = crouchThighDegrees * crouch;
            float shin = crouchShinDegrees * crouch;
            float foot = crouchFootDegrees * crouch;

            RotateWorld(_upperLegL, right, thigh);
            RotateWorld(_upperLegR, right, thigh);
            RotateWorld(_lowerLegL, right, shin);
            RotateWorld(_lowerLegR, right, shin);
            RotateWorld(_footL, right, foot);
            RotateWorld(_footR, right, foot);

            // Lean the torso forward over the knees, spread down the spine so no single joint
            // has to do all of it.
            float lean = crouchLeanDegrees * crouch;
            RotateWorld(_spine01, right, lean * 0.45f);
            RotateWorld(_spine02, right, lean * 0.35f);
            RotateWorld(_spine03, right, lean * 0.2f);

            if (!_hasVisualRoot)
                return;

            Vector3 local = _visualBasePosition;
            local.y -= ComputeCrouchDrop(crouch);
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

            float counter = -yaw * strafeTorsoCounter;
            RotateWorld(_spine01, up, counter * 0.4f);
            RotateWorld(_spine02, up, counter * 0.35f);
            RotateWorld(_spine03, up, counter * 0.25f);

            // And lean into it. Rolling about forward is a lean towards the leading foot.
            float lean = -strafeLeanDegrees * _strafe;
            RotateWorld(_spine01, playerBody.forward, lean * 0.5f);
            RotateWorld(_spine02, playerBody.forward, lean * 0.5f);
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
