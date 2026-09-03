using CatchIfYouCan.Player;
using UnityEngine;

namespace CatchIfYouCan.Equipment
{
    /// <summary>
    /// Equipment that is carried in the character's hand and can be put down again.
    ///
    /// <para>
    /// Everything about being held is here and nothing about what the item does: finding the
    /// hand bone, riding the arm pose, aiming, the walk bob, and being thrown out of the hand
    /// with a body on it so physics decides where it lands. The torch worked all of this out
    /// first, alone, and every item after it - the EMF reader, the camera, the thermometer -
    /// would have had to work it out again. It is here so they do not.
    /// </para>
    ///
    /// <para>
    /// A subclass builds its own <see cref="Carried"/> transform and says how long it is. The
    /// convention is the one <see cref="EquipmentPresentation"/> documents: <b>local +Y is the
    /// long axis</b>, the origin is the grip rather than the middle, and it points away from
    /// the hand.
    /// </para>
    ///
    /// <para>
    /// <b>The pose is not ours.</b> <see cref="PlayerBodyMotion"/> poses the arm and hands out
    /// the palm it arrived at; this only lays the item on it. The two run in LateUpdate, where
    /// the order between components is whatever Unity feels like, so rather than guess, the
    /// item is placed from the end of the pose itself - and <see cref="LateUpdate"/> here is
    /// only the path for a character with no procedural body layer at all.
    /// </para>
    /// </summary>
    public abstract class HeldEquipmentBase : EquipmentBase
    {
        [Header("Carry")]
        [Tooltip("Bone the item is held by, matched by name suffix. Falls back to the anchor " +
                 "the inventory equipped it to.")]
        [SerializeField] protected string handBoneSuffix = "_hand_r";

        [Tooltip("Character root searched for that bone.")]
        [SerializeField] protected Transform characterVisual;

        [Tooltip("Player root, whose axes the item is aimed along.")]
        [SerializeField] protected Transform playerBody;

        [SerializeField] protected PlayerController playerController;

        [Header("Grip")]
        [Tooltip("Offset from the hand, in the player's own axes: right, up, forward. Only used " +
                 "on the fallback path, when there is no character whose knuckles can be " +
                 "measured. The field below is the one that moves the item in the real hand.")]
        [SerializeField] protected Vector3 anchorGripOffset = new Vector3(0.02f, 0.01f, 0.06f);

        [Tooltip("Where the item sits in the fist, in the hand's own measured axes and in " +
                 "metres: X along the item towards its far end, Y out of the back of the hand, " +
                 "Z towards the fingertips. Drag it in the Inspector while the game is running " +
                 "and the item moves in the hand immediately.")]
        [SerializeField] protected Vector3 handGripPositionOffset;

        [Tooltip("Turn added to the item after it has been laid on the fist, in its own axes. " +
                 "Live-tunable in the same way.")]
        [SerializeField] protected Vector3 handGripRotationOffset;

        [Tooltip("How far back along the item the fist closes, in metres. The origin is the " +
                 "tail, so without this the hand grips thin air at the very end of the handle " +
                 "and the whole item hangs off the front of it. A fist is about eight " +
                 "centimetres across.")]
        [SerializeField, Min(0f)] protected float gripBackset = 0.085f;

        [Tooltip("Turn applied to the item after it has been aimed, in degrees about its own " +
                 "axes. Zero is level and pointing down the aim; this is here so the pose can " +
                 "be tuned against the hand without touching the aiming.")]
        [SerializeField] protected Vector3 gripRotationOffset = Vector3.zero;

        [Header("Aim")]
        [Tooltip("Downward tilt from level, degrees. Something carried at chest height points " +
                 "at the floor a few metres ahead, not at the horizon.")]
        [SerializeField] protected float aimPitch = 10f;

        [Tooltip("Seconds the aim lags the body. This is the swing.")]
        [SerializeField, Min(0.01f)] protected float aimLag = 0.16f;

        [SerializeField] protected float walkBobDegrees = 4.5f;
        [SerializeField] protected float walkBobRate = 1.15f;

        [Header("Dropped")]
        [Tooltip("How far in front of the player it leaves the hand, and how far above chest " +
                 "height, in metres. Where the throw starts, not where it ends up - where it " +
                 "ends up is physics' business.")]
        [SerializeField] protected float dropForward = 0.42f;
        [SerializeField] protected float dropHeight = 0.06f;

        [Tooltip("Mass once it leaves the hand, in kilograms.")]
        [SerializeField, Min(0.02f)] protected float dropMass = 0.32f;

        [Tooltip("How hard it is thrown, in newton-seconds: forward along the player's look, " +
                 "and up. Small - it is put down, not hurled.")]
        [SerializeField] protected Vector2 dropImpulse = new Vector2(1.1f, 0.35f);

        [Tooltip("Spin given to the throw, in newton-metre-seconds. This is what decides " +
                 "whether it lands and stays or lands and rolls a hand's width.")]
        [SerializeField] protected float dropSpin = 0.09f;

        [SerializeField, Min(0f)] protected float dropLinearDamping = 0.4f;
        [SerializeField, Min(0f)] protected float dropAngularDamping = 3.2f;

        [Tooltip("Radius of the physics collider it lands on, in metres.")]
        [SerializeField, Min(0.005f)] protected float dropRadius = 0.026f;

        private Transform _handBone;
        private Transform _view;
        private PlayerBodyMotion _bodyMotion;
        private CapsuleCollider _dropCollider;
        private Rigidbody _dropBody;
        private int _placedFrame = -1;
        private Vector3 _aim = Vector3.forward;
        private Vector3 _aimVelocity;
        private float _bobPhase;
        private bool _onGround;

        /// <summary>
        /// The transform actually laid in the hand. Built by the subclass, with its local +Y
        /// along its length and its origin at the grip.
        /// </summary>
        protected abstract Transform Carried { get; }

        /// <summary>How long the carried item is, in metres. Used to centre it when dropped.</summary>
        protected abstract float CarriedLength { get; }

        /// <summary>True while it is lying in the room rather than carried.</summary>
        public bool IsOnGround => _onGround;

        /// <summary>The view the fallback aim is taken from, when there is one.</summary>
        protected Transform ViewTransform => _view;

        protected override void Awake()
        {
            base.Awake();

            if (playerController == null)
                playerController = Object.FindAnyObjectByType<PlayerController>();
            if (playerBody == null && playerController != null)
                playerBody = playerController.transform;

            // Cached once. The aim follows the look rather than the body, so this is read every
            // frame and must never be a search.
            var view = Core.LocalPlayerService.ResolveViewCamera();
            if (view != null)
                _view = view.transform;

            BuildCarried();

            if (playerBody != null)
                _aim = playerBody.forward;
        }

        /// <summary>Builds the item's own geometry. Called once, from <see cref="Awake"/>.</summary>
        protected abstract void BuildCarried();

        /// <summary>
        /// Called whenever the item changes between being held, stowed and lying on the floor.
        /// Anything whose visible state depends on where the item is goes here.
        /// </summary>
        protected virtual void OnCarryChanged() { }

        /// <summary>Points the item at the character it is being carried by.</summary>
        public virtual void BindCharacter(Transform visual, Transform body)
        {
            characterVisual = visual;
            if (body != null)
                playerBody = body;
            ResolveHandBone();

            // The body motion poses the arm that carries this, and both run in LateUpdate, where
            // the order between two components is whatever Unity feels like. Rather than guess,
            // the item is placed from the end of the pose itself.
            _bodyMotion = playerBody != null ? playerBody.GetComponent<PlayerBodyMotion>() : null;
            if (_bodyMotion != null)
                _bodyMotion.SetPoseListener(PlaceInHand);
        }

        private void ResolveHandBone()
        {
            _handBone = null;
            if (characterVisual == null || string.IsNullOrEmpty(handBoneSuffix))
                return;

            var all = characterVisual.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (!all[i].name.EndsWith(handBoneSuffix, System.StringComparison.OrdinalIgnoreCase))
                    continue;
                _handBone = all[i];
                return;
            }
        }

        // ---- carrying ------------------------------------------------------------------------

        public override void Equip(Transform handAnchor)
        {
            // Physics first: the base implementation reparents, and reparenting a body that is
            // still simulating is how an item ends up flying across the room as it is picked up.
            ReleasePhysics();
            base.Equip(handAnchor);
            _onGround = false;
            OnCarryChanged();
        }

        public override void Unequip()
        {
            base.Unequip();
            OnCarryChanged();
        }

        /// <summary>
        /// Throws the item out of the hand and lets physics decide where it comes to rest.
        ///
        /// <para>
        /// The equipment base refuses without a definition that allows dropping, and switches
        /// the device off on the way out. This does neither: an item you cannot put down is not
        /// an item, and whether it keeps running on the floor is the subclass's call, made in
        /// <see cref="OnCarryChanged"/>.
        /// </para>
        ///
        /// <para>
        /// It is given a body, a capsule down its own length, a small shove and a little spin,
        /// and lands how it lands - which is the point, because a capsule dropped on its side
        /// rolls a few centimetres and stops, and one dropped on its end does not roll at all.
        /// </para>
        /// </summary>
        public override void Drop(Vector3 position, Quaternion rotation)
        {
            if (IsEquipped)
                Unequip();

            transform.SetParent(null, true);

            Vector3 throwDirection = rotation * Vector3.forward;
            throwDirection.y = 0f;
            if (throwDirection.sqrMagnitude < 0.0001f)
                throwDirection = playerBody != null ? playerBody.forward : Vector3.forward;
            throwDirection.Normalize();

            Vector3 from = position;
            if (playerBody != null)
                from = playerBody.position + Vector3.up * (1.1f + dropHeight) +
                       throwDirection * dropForward;

            // The root carries the item now rather than the other way round, so the body can
            // move one transform and take the mesh and everything on it along.
            transform.SetPositionAndRotation(
                from, Quaternion.LookRotation(throwDirection, Vector3.up) *
                      Quaternion.Euler(90f, 0f, 0f));

            var carried = Carried;
            if (carried != null)
            {
                carried.localRotation = Quaternion.identity;
                carried.localPosition = new Vector3(0f, -CarriedLength * 0.5f, 0f);
            }

            IsPlaced = false;
            _onGround = true;
            _placedFrame = Time.frameCount;

            StartPhysics(throwDirection);

            OnCarryChanged();
            Core.GameEvents.EquipmentChanged();
        }

        /// <summary>
        /// The shape it lands on: a capsule down its own length, switched off while it is being
        /// carried so a thing in the player's hand is not also a thing in the player's way. The
        /// trigger the pickup ray uses is a separate collider and stays on.
        /// </summary>
        protected void BuildDropCollider(float length)
        {
            _dropCollider = gameObject.AddComponent<CapsuleCollider>();
            _dropCollider.direction = 1;                       // down the item's own Y
            _dropCollider.radius = dropRadius;
            _dropCollider.height = length + dropRadius * 2f;
            _dropCollider.center = Vector3.zero;
            _dropCollider.enabled = false;
        }

        /// <summary>Gives the item a body and throws it.</summary>
        private void StartPhysics(Vector3 throwDirection)
        {
            if (_dropCollider != null)
                _dropCollider.enabled = true;

            if (_dropBody == null)
                _dropBody = gameObject.AddComponent<Rigidbody>();

            _dropBody.isKinematic = false;
            _dropBody.useGravity = true;
            _dropBody.mass = dropMass;
            _dropBody.linearDamping = dropLinearDamping;
            _dropBody.angularDamping = dropAngularDamping;
            _dropBody.interpolation = RigidbodyInterpolation.Interpolate;
            _dropBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            _dropBody.AddForce(throwDirection * dropImpulse.x + Vector3.up * dropImpulse.y,
                               ForceMode.Impulse);

            // About its own long axis and across it, so it tumbles rather than spinning flat.
            Vector3 spin = transform.right * dropSpin + transform.forward * (dropSpin * 0.4f);
            _dropBody.AddTorque(spin, ForceMode.Impulse);
        }

        /// <summary>Takes the body back off, so the hand can carry it again.</summary>
        protected void ReleasePhysics()
        {
            if (_dropBody != null)
            {
                // Stopped before it is destroyed. Destroy is deferred to the end of the frame,
                // and a body still simulating while the transform it owns is being reparented
                // into a hand is how a picked-up item shoots across the room.
                _dropBody.isKinematic = true;
                Destroy(_dropBody);
                _dropBody = null;
            }

            if (_dropCollider != null)
                _dropCollider.enabled = false;

            var carried = Carried;
            if (carried != null)
            {
                carried.localPosition = Vector3.zero;
                carried.localRotation = Quaternion.identity;
            }
        }

        // ---- per frame -----------------------------------------------------------------------

        protected virtual void LateUpdate()
        {
            // Normally already done from the body motion's pose callback; this is the path for a
            // character with no procedural body layer at all.
            if (_placedFrame != Time.frameCount)
                PlaceInHand();
        }

        /// <summary>
        /// Puts the item in the hand for this frame.
        ///
        /// <para>
        /// Where the rig can be measured, it is, and nothing here decides where the hand points
        /// - the arm pose does that, and it aims the hand down the player's own line of sight -
        /// so the item ends up facing wherever the player is looking without being told to.
        /// Otherwise it falls back to the anchor the inventory equipped it to, aimed off the
        /// camera with a lag and a walk bob.
        /// </para>
        /// </summary>
        public void PlaceInHand()
        {
            _placedFrame = Time.frameCount;

            var carried = Carried;
            if (carried == null || !IsEquipped || playerBody == null)
                return;

            if (_bodyMotion != null &&
                _bodyMotion.TryGetGrip(out Vector3 palm, out Vector3 barrel, out Vector3 palmNormal))
            {
                EquipmentPresentation.SolveMeasuredHand(
                    palm, barrel, palmNormal,
                    handGripPositionOffset, handGripRotationOffset, gripRotationOffset,
                    gripBackset,
                    out Vector3 handPosition, out Quaternion handRotation);

                carried.rotation = handRotation;
                carried.position = handPosition;
                return;
            }

            Transform anchor = _handBone != null ? _handBone : HandAnchor;
            if (anchor == null)
                return;

            // Taken from the camera rather than the body so it goes where the player is looking
            // rather than only where they are facing.
            Vector3 look = _view != null ? _view.forward : playerBody.forward;
            _aim = EquipmentPresentation.AdvanceAim(
                _aim, ref _aimVelocity, look, playerBody.right, aimPitch, aimLag);

            float speed = playerController != null ? playerController.CurrentSpeed : 0f;
            _bobPhase = EquipmentPresentation.AdvanceBobPhase(
                _bobPhase, speed, walkBobRate, Time.deltaTime);
            float bob = EquipmentPresentation.BobDegrees(_bobPhase, speed, walkBobDegrees);

            Vector3 aim = Quaternion.AngleAxis(bob, playerBody.right) * _aim.normalized;

            EquipmentPresentation.SolveAimed(
                anchor.position, aim,
                playerBody.right, playerBody.up, playerBody.forward,
                anchorGripOffset, gripRotationOffset, gripBackset,
                out Vector3 aimedPosition, out Quaternion aimedRotation);

            carried.rotation = aimedRotation;
            carried.position = aimedPosition;
        }
    }
}
