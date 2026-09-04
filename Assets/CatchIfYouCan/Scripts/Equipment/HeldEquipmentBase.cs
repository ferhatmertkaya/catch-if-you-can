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
    public abstract class HeldEquipmentBase : EquipmentBase, IHeldEquipment
    {
        [Header("Carry")]
        [Tooltip("Bone the item is held by, matched by name suffix. Falls back to the anchor " +
                 "the inventory equipped it to.")]
        // "hand_r", not "_hand_r", and the leading underscore was the whole bug rather than a
        // detail. Nathan's bones are named hand_l and hand_r exactly - no prefix - so
        // "hand_r".EndsWith("_hand_r") is FALSE and this NEVER resolved. Every held item fell
        // through to HandAnchor, a bare transform beside the camera pivot, while
        // PlayerBodyMotion went on dragging the real arm to its own target somewhere else. The
        // torch was not invisible; it was parented to a point the hand never reaches.
        [SerializeField] protected string handBoneSuffix = "hand_r";

        [Tooltip("Character root searched for that bone.")]
        [SerializeField] protected Transform characterVisual;

        [Tooltip("Player root, whose axes the item is aimed along.")]
        [SerializeField] protected Transform playerBody;

        [SerializeField] protected PlayerController playerController;

        [Header("Grip")]
        [Tooltip("Overrides the grip this item would otherwise resolve from its definition. " +
                 "For trying something in the lab; production grips belong on the definition, " +
                 "because how an item sits in a fist is a fact about the item and not about " +
                 "one copy of it.")]
        [SerializeField] private EquipmentGripProfile gripProfileOverride;

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

        /// <summary>
        /// The character bone this item ended up parented to, or null when the suffix matched
        /// nothing. Exposed for diagnostics: an unresolved bone is the difference between an
        /// item in the hand and an item at a fallback anchor the hand never reaches, and that
        /// is exactly the failure this project spent weeks not seeing.
        /// </summary>
        protected Transform HandBone => _handBone;
        private Transform _view;
        private PlayerBodyMotion _bodyMotion;
        protected CapsuleCollider _dropCollider;
        private Rigidbody _dropBody;
        private int _placedFrame = -1;
        private Vector3 _aim = Vector3.forward;
        private Vector3 _aimVelocity;
        private float _bobPhase;
        private bool _onGround;
        private Collider[] _pickupColliders;

        /// <summary>
        /// Where this item is. One value rather than the pair of booleans on
        /// <see cref="EquipmentBase"/>, which could not tell a stowed item from one lying on
        /// the floor - both were "not equipped, not placed".
        /// </summary>
        public EquipmentLifecycleState LifecycleState { get; private set; } =
            EquipmentLifecycleState.World;

        public bool IsDeviceActive => DeviceActive;

        /// <summary>
        /// Also ticks while lying in the room with the device still running.
        ///
        /// <para>
        /// EquipmentBase ticks only what is held or placed, which is right for a device that
        /// switches itself off when it leaves the hand. A torch does not: it is thrown down
        /// still burning, on purpose. Without this it burns on a battery that never drains.
        /// </para>
        /// </summary>
        protected override bool ShouldTick =>
            base.ShouldTick ||
            (LifecycleState == EquipmentLifecycleState.World && DeviceActive);

        public Transform WorldPose => transform;

        /// <summary>
        /// The transform actually laid in the hand, with its local +Y along its length and its
        /// origin at the grip. Built from content by <see cref="BuildCarried"/>; a subclass
        /// that assembles something more elaborate overrides that rather than this.
        /// </summary>
        protected virtual Transform Carried => CarriedRoot;

        /// <summary>The visual root this class built. Null until <see cref="Awake"/>.</summary>
        protected Transform CarriedRoot { get; private set; }

        /// <summary>
        /// How this item looks, from its definition. An item with no profile gets an honest
        /// placeholder rather than an invisible hand.
        /// </summary>
        protected EquipmentVisualProfile VisualProfile =>
            definition != null && definition.VisualProfile != null
                ? definition.VisualProfile
                : EquipmentVisualProfile.Fallback;

        /// <summary>
        /// How long the carried item is, in metres. Used to centre it when dropped and to size
        /// the capsule it lands on. Defaults to the grip profile's length; an item that
        /// measures its own mesh overrides this with the measurement, which is better data.
        /// </summary>
        protected virtual float CarriedLength =>
            _measuredLength > 0f ? _measuredLength : Grip.Length;

        /// <summary>True while it is lying in the room rather than carried.</summary>
        public bool IsOnGround => _onGround;

        /// <summary>
        /// How this item sits in a hand: the override if one is set, the definition's profile
        /// if it has one, a migration of the definition's deprecated hand pose if it carries
        /// one, and otherwise the shared default.
        ///
        /// <para>
        /// One store, resolved in one place. There were three - the definition's
        /// HandLocalPosition/Rotation applied as a local transform, this class's own serialized
        /// offsets applied in the hand's measured axes, and CharacterRigProfile's grip offsets
        /// which nothing read - and they did not agree about what space they were even in.
        /// </para>
        ///
        /// <para>
        /// The default is the flashlight's grip, unchanged, because it is the only grip in this
        /// project that has ever been tuned against a real character in a real hand.
        /// </para>
        /// </summary>
        public EquipmentGripProfile Grip
        {
            get
            {
                if (gripProfileOverride != null)
                    return gripProfileOverride;

                if (definition != null && definition.GripProfile != null)
                    return definition.GripProfile;

                // Deliberately NOT migrating the definition's deprecated HandLocalPosition and
                // HandLocalRotation. All eleven carry the identical (0.08, -0.05, 0.22) and
                // (0, -90, 0), because one line in EquipmentDefinitionFactory.Create wrote the
                // same guess onto every item - it is not per-item tuning, it is one number
                // copied eleven times. Migrating it would give every item the same grip and
                // would move the flashlight, whose real grip is this default and is the only
                // one in the project that has been tuned against a real hand.
                return EquipmentGripProfile.Default;
            }
        }

        /// <summary>
        /// The character-wide grip correction, which is a fact about whose hand this is rather
        /// than about what is in it. Zero when no character is selected, which is what it has
        /// always effectively been.
        ///
        /// <para>
        /// Resolved when the item is bound to a character rather than every frame: this is read
        /// from the presentation, which runs in LateUpdate for every held item, and a catalog
        /// lookup per item per frame is exactly the kind of cost that is invisible until there
        /// are eleven of them.
        /// </para>
        /// </summary>
        protected Character.CharacterRigProfile RigProfile => _rigProfile;

        private Character.CharacterRigProfile _rigProfile;

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

        /// <summary>
        /// Builds the item's visual. Called once, from <see cref="Awake"/>.
        ///
        /// <para>
        /// The default is the whole implementation for most items: the visual comes out of
        /// content, so an item's class does not describe its own appearance. Override to add
        /// something the model cannot be - the flashlight's lens and beam - and call base
        /// first.
        /// </para>
        /// </summary>
        protected virtual void BuildCarried()
        {
            if (CarriedRoot != null)
                return;

            CarriedRoot = EquipmentVisualFactory.Build(
                VisualProfile, transform,
                definition != null ? definition.DisplayName : name,
                out float measured);

            _measuredLength = measured;
            BuildDropCollider(measured);
        }

        /// <summary>
        /// Rebuilds the visual once the definition has arrived.
        ///
        /// <para>
        /// <b>This is why every code-spawned item was a placeholder.</b> An item is created with
        /// <c>AddComponent</c> and told what it is on the NEXT line - and AddComponent runs Awake
        /// synchronously, so <see cref="BuildCarried"/> had already run with <c>definition</c>
        /// still null. <see cref="VisualProfile"/> therefore returned the fallback, the factory
        /// built its honest placeholder capsule, and <c>BuildCarried</c>'s
        /// <c>if (CarriedRoot != null) return;</c> guard meant it never ran again. Binding the
        /// definition afterwards set the stats and left the capsule standing.
        /// </para>
        ///
        /// <para>
        /// The flashlight is the visible case - the finished CIYC_Flashlight model is in
        /// Resources and was never reached - but every item built by
        /// <c>EquipmentRuntimeFactory</c> follows the same two lines and had the same result.
        /// </para>
        ///
        /// <para>
        /// Only when the visual really was built from the fallback and the definition really
        /// does carry a profile. An item constructed correctly - definition first - never
        /// rebuilds, so nothing that already works pays for this.
        /// </para>
        /// </summary>
        public override void BindDefinition(EquipmentDefinition def)
        {
            bool builtBlind = CarriedRoot != null && definition == null;

            base.BindDefinition(def);

            if (!builtBlind || def == null || def.VisualProfile == null)
                return;

            RebuildCarried();
        }

        /// <summary>
        /// Throws away the visual and builds it again from the definition now in hand.
        /// Subclasses rebuild whatever they hang off it - the torch its lens and beam - because
        /// this goes back through <see cref="BuildCarried"/>, which they already override.
        /// </summary>
        protected void RebuildCarried()
        {
            if (CarriedRoot != null)
            {
                Object.Destroy(CarriedRoot.gameObject);
                CarriedRoot = null;
            }

            // BuildDropCollider adds one unconditionally, so without this a rebuild leaves the
            // item wearing two capsules - and the stale one is sized for the placeholder.
            if (_dropCollider != null)
            {
                Object.Destroy(_dropCollider);
                _dropCollider = null;
            }

            BuildCarried();
            OnCarriedRebuilt();
        }

        /// <summary>
        /// Called after the visual has been replaced, so anything holding a reference into the
        /// old one can pick the new one up.
        /// </summary>
        protected virtual void OnCarriedRebuilt() { }

        private float _measuredLength;

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

            // Cached here rather than read per frame. Which character is being played does not
            // change while an item is in the hand.
            _rigProfile = Character.CharacterService.Resolve()?.RigProfile;

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

            SetPresentationVisible(true);
            SetLifecycleState(EquipmentLifecycleState.Equipped);
            OnCarryChanged();
        }

        /// <summary>
        /// Taken out of the hand. Where it goes next is the caller's business - stowed by
        /// <see cref="TryHolster"/>, thrown by <see cref="Drop"/> - so this only stops it being
        /// held and leaves the state alone for them to set.
        /// </summary>
        public override void Unequip()
        {
            base.Unequip();
            OnCarryChanged();
        }

        /// <summary>
        /// Stowed: still owned, no longer in the hand, and not left behind in the room.
        ///
        /// <para>
        /// <see cref="EquipmentBase.Unequip"/> unparents to world space, which for an item
        /// going into a bag means leaving it hovering wherever the player was standing. With
        /// one item in the inventory that never showed; with three it is every item but the
        /// selected one. A stowed item stays parented to the hand so it travels with its
        /// owner, and its presentation and pickup colliders are switched off so it is neither
        /// visible nor pickable while it is in a bag.
        /// </para>
        /// </summary>
        public EquipmentActionResult TryHolster() => TryHolster(HandAnchor);

        /// <summary>
        /// Stows against a named owner anchor. The inventory calls this one, because an item
        /// picked up straight into an unselected slot has never been in a hand and so has no
        /// anchor of its own to stow against.
        /// </summary>
        public EquipmentActionResult TryHolster(Transform ownerAnchor)
        {
            if (LifecycleState == EquipmentLifecycleState.Holstered)
                return EquipmentActionResult.Success;

            Transform anchor = ownerAnchor != null ? ownerAnchor : HandAnchor;
            if (anchor == null)
                return EquipmentActionResult.Fail(
                    EquipmentActionStatus.NoAuthority,
                    "nothing to stow against; the owner has no hand anchor");

            ReleasePhysics();
            _onGround = false;

            CancelPlacementInternal();
            base.Unequip();
            SetDeviceActive(false);
            IsPlaced = false;

            transform.SetParent(anchor, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;

            SetPresentationVisible(false);
            SetLifecycleState(EquipmentLifecycleState.Holstered);
            OnCarryChanged();
            return EquipmentActionResult.Success;
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

            SetPresentationVisible(true);
            StartPhysics(throwDirection);

            SetLifecycleState(EquipmentLifecycleState.World);
            OnCarryChanged();
            Core.GameEvents.EquipmentChanged();
        }

        // ---- lifecycle -----------------------------------------------------------------------

        /// <summary>
        /// Changes the lifecycle state and tells the subclass, so an item that has expensive
        /// work to switch off has one place to do it.
        /// </summary>
        private void SetLifecycleState(EquipmentLifecycleState next)
        {
            if (LifecycleState == next)
                return;

            var previous = LifecycleState;
            LifecycleState = next;
            OnLifecycleStateChanged(previous, next);
        }

        /// <summary>
        /// Called on every lifecycle change. Anything costly - a projection, a camera feed, a
        /// scan - is switched off here rather than being left running in a bag.
        /// </summary>
        protected virtual void OnLifecycleStateChanged(EquipmentLifecycleState from,
                                                       EquipmentLifecycleState to) { }

        /// <summary>
        /// Shows or hides everything this item renders, and stops it being pickable while it is
        /// stowed. The carried transform is the whole visual, so one SetActive covers the mesh,
        /// the lens and anything hanging off it.
        /// </summary>
        protected virtual void SetPresentationVisible(bool visible)
        {
            var carried = Carried;
            if (carried != null && carried.gameObject.activeSelf != visible)
                carried.gameObject.SetActive(visible);

            if (_pickupColliders == null)
                _pickupColliders = GetComponents<Collider>();

            for (int i = 0; i < _pickupColliders.Length; i++)
            {
                var collider = _pickupColliders[i];
                // The drop capsule is owned by the physics path and must not be switched on
                // here; it is off while carried and on only once the item has been thrown.
                if (collider == null || collider == _dropCollider)
                    continue;

                collider.enabled = visible;
            }
        }

        /// <summary>
        /// Takes an item off the floor into an inventory.
        ///
        /// <para>
        /// The lifecycle check below is the race resolver, and it is why picking up is an
        /// authority decision rather than a local one. Two players reaching for the same torch
        /// on the same frame is not an edge case; the only way one of them loses is if exactly
        /// one machine decides, and the loser gets WrongState because the item is no longer in
        /// the world by the time their request is looked at.
        /// </para>
        ///
        /// <para>
        /// Reach is validated here, against this item's real position, rather than trusted from
        /// the asker. A client that measures its own reach is a client that can be told to
        /// measure generously.
        /// </para>
        /// </summary>
        public EquipmentActionResult TryPickup(Player.PlayerInventory into)
        {
            if (into == null)
                return EquipmentActionResult.Fail(
                    EquipmentActionStatus.NoAuthority, "no inventory to pick this up into");

            if (!Session.AuthorityRequests.CanDecide)
                return EquipmentActionResult.Fail(
                    EquipmentActionStatus.NoAuthority, "the host decides who picks this up");

            // Checked before the state test, so an out-of-reach request cannot consume the
            // item's transition and make a legitimate nearer request fail as "already owned".
            var reach = Session.AuthorityRequests.ValidateReach(into.transform, transform);
            if (!Session.AuthorityRequests.Allows(reach))
                return EquipmentActionResult.Fail(
                    EquipmentActionStatus.OutOfRange, Session.AuthorityRequests.Describe(reach));

            if (LifecycleState != EquipmentLifecycleState.World)
                return EquipmentActionResult.Fail(
                    EquipmentActionStatus.WrongState,
                    "already owned (" + LifecycleState + ")");

            if (!into.HasFreeSlot)
                return EquipmentActionResult.Fail(EquipmentActionStatus.NoInventorySpace);

            ReleasePhysics();
            _onGround = false;

            // The inventory decides which slot, and equips or holsters accordingly - so the
            // state this item lands in is set by that call, not guessed here.
            return into.AddItem(this)
                ? EquipmentActionResult.Success
                : EquipmentActionResult.Fail(EquipmentActionStatus.NoInventorySpace);
        }

        public EquipmentActionResult TryEquip(Transform handAnchor)
        {
            if (LifecycleState == EquipmentLifecycleState.Placed)
                return EquipmentActionResult.Fail(
                    EquipmentActionStatus.WrongState, "placed; pick it up first");

            Equip(handAnchor);
            return EquipmentActionResult.Success;
        }

        public EquipmentActionResult TryUse()
        {
            if (definition == null)
                return EquipmentActionResult.Fail(
                    EquipmentActionStatus.MissingContent, "no definition bound");

            if (!definition.CanUse)
                return EquipmentActionResult.Fail(EquipmentActionStatus.NotAllowedByDefinition);

            if (Durability <= 0f)
                return EquipmentActionResult.Fail(EquipmentActionStatus.Broken);

            if (definition.BatteryUsagePerSecond > 0f && BatteryLevel <= 0f)
                return EquipmentActionResult.Fail(EquipmentActionStatus.NoBattery);

            if (LifecycleState != EquipmentLifecycleState.Equipped &&
                LifecycleState != EquipmentLifecycleState.Using &&
                LifecycleState != EquipmentLifecycleState.Placed &&
                LifecycleState != EquipmentLifecycleState.PlacementPreview)
            {
                return EquipmentActionResult.Fail(
                    EquipmentActionStatus.WrongState, "not held or placed (" + LifecycleState + ")");
            }

            // Committing a placement is what Use means while a preview is up. Anything else
            // would need a second button for the one action the player is obviously taking.
            if (LifecycleState == EquipmentLifecycleState.PlacementPreview)
                return TryPlace();

            Use();
            return EquipmentActionResult.Success;
        }

        public virtual EquipmentActionResult TryBeginPlacement()
        {
            if (definition == null || !definition.CanPlace)
                return EquipmentActionResult.Fail(EquipmentActionStatus.NotAllowedByDefinition);

            if (LifecycleState != EquipmentLifecycleState.Equipped)
                return EquipmentActionResult.Fail(
                    EquipmentActionStatus.WrongState, "must be in hand to place");

            SetLifecycleState(EquipmentLifecycleState.PlacementPreview);
            return EquipmentActionResult.Success;
        }

        public virtual EquipmentActionResult TryCancelPlacement()
        {
            if (LifecycleState != EquipmentLifecycleState.PlacementPreview)
                return EquipmentActionResult.Fail(
                    EquipmentActionStatus.WrongState, "no placement in progress");

            CancelPlacementInternal();
            SetLifecycleState(EquipmentLifecycleState.Equipped);
            return EquipmentActionResult.Success;
        }

        /// <summary>Tears down any preview visuals. Subclasses that draw one override this.</summary>
        protected virtual void CancelPlacementInternal() { }

        /// <summary>
        /// Commits the current placement. The base cannot know where - that is the placement
        /// system's and the subclass's - so an item that has not implemented placement says so
        /// rather than dropping itself at the origin.
        /// </summary>
        public virtual EquipmentActionResult TryPlace()
        {
            if (definition == null || !definition.CanPlace)
                return EquipmentActionResult.Fail(EquipmentActionStatus.NotAllowedByDefinition);

            return EquipmentActionResult.Fail(
                EquipmentActionStatus.NotSupported,
                GetType().Name + " is placeable by its definition but has no placement " +
                "implementation");
        }

        /// <summary>
        /// Takes a placed item back, with its battery, durability and settings intact. It is
        /// the same logical item, not a fresh one built from the definition.
        /// </summary>
        public EquipmentActionResult TryPickupPlaced(Player.PlayerInventory into)
        {
            if (LifecycleState != EquipmentLifecycleState.Placed)
                return EquipmentActionResult.Fail(
                    EquipmentActionStatus.WrongState, "not placed");

            if (into == null)
                return EquipmentActionResult.Fail(
                    EquipmentActionStatus.NoAuthority, "no inventory to pick this up into");

            if (!into.HasFreeSlot)
                return EquipmentActionResult.Fail(EquipmentActionStatus.NoInventorySpace);

            // Removing an installed item is the same world-state change as installing one, and
            // is gated the same way. Picking a held item out of your own bag is not: that is
            // owner-predicted and deliberately does not ask.
            if (!EquipmentAuthority.CanChangeWorldState(this))
                return EquipmentActionResult.Fail(
                    EquipmentActionStatus.NoAuthority, "the host decides what leaves the room");

            // Claimed while it is still Placed, which is the state that makes it takeable by
            // anybody in reach. Un-placing first would leave it looking like the placer's
            // carried item and refuse everybody else - a camera one player set up in the wrong
            // room would be nobody else's to move.
            var claim = TryClaim(into.OwnerClientId);
            if (!Procedural.Deterministic.EquipmentOwnership.Holds(claim))
                return EquipmentActionResult.Fail(
                    EquipmentActionStatus.NoAuthority,
                    Procedural.Deterministic.EquipmentOwnership.Describe(claim));

            IsPlaced = false;
            OnPickedUpFromPlacement();

            return into.AddItem(this)
                ? EquipmentActionResult.Success
                : EquipmentActionResult.Fail(EquipmentActionStatus.NoInventorySpace);
        }

        /// <summary>Called as a placed item is taken back. Stop whatever placement started.</summary>
        protected virtual void OnPickedUpFromPlacement() { }

        public EquipmentActionResult TryDrop()
        {
            if (definition == null || !definition.CanDrop)
                return EquipmentActionResult.Fail(EquipmentActionStatus.NotAllowedByDefinition);

            if (LifecycleState == EquipmentLifecycleState.World)
                return EquipmentActionResult.Fail(
                    EquipmentActionStatus.WrongState, "already in the world");

            // Where it lands is the inventory's to decide, because the drop origin is on the
            // player. This is the item agreeing that it can be dropped.
            return EquipmentActionResult.Success;
        }

        /// <summary>
        /// Marks this item as installed in the room. Called by a subclass once it has actually
        /// put itself somewhere, so the base is never the thing deciding where.
        /// </summary>
        protected void EnterPlacedState()
        {
            IsPlaced = true;
            IsEquipped = false;
            _onGround = false;
            SetPresentationVisible(true);
            SetLifecycleState(EquipmentLifecycleState.Placed);
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

            var grip = Grip;
            var rig = RigProfile;

            if (_bodyMotion != null &&
                _bodyMotion.TryGetGrip(out Vector3 palm, out Vector3 barrel, out Vector3 palmNormal))
            {
                EquipmentPresentation.SolveMeasuredHand(
                    palm, barrel, palmNormal,
                    grip.PositionOffset, grip.WristHint, grip.RotationOffset,
                    rig != null ? rig.GripPositionOffset : Vector3.zero,
                    rig != null ? rig.GripRotationOffset : Vector3.zero,
                    grip.Backset,
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
                _aim, ref _aimVelocity, look, playerBody.right, grip.AimPitch, grip.AimLag);

            float speed = playerController != null ? playerController.CurrentSpeed : 0f;
            _bobPhase = EquipmentPresentation.AdvanceBobPhase(
                _bobPhase, speed, grip.WalkBobRate, Time.deltaTime);
            float bob = EquipmentPresentation.BobDegrees(_bobPhase, speed, grip.WalkBobDegrees);

            Vector3 aim = Quaternion.AngleAxis(bob, playerBody.right) * _aim.normalized;

            EquipmentPresentation.SolveAimed(
                anchor.position, aim,
                playerBody.right, playerBody.up, playerBody.forward,
                grip.AnchorOffset, grip.RotationOffset, grip.Backset,
                out Vector3 aimedPosition, out Quaternion aimedRotation);

            carried.rotation = aimedRotation;
            carried.position = aimedPosition;
        }
    }
}
