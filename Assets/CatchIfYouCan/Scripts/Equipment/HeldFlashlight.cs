using CatchIfYouCan.Player;
using UnityEngine;

namespace CatchIfYouCan.Equipment
{
    /// <summary>
    /// The torch: a modelled body with a real spot light, carried in the character's hand, and
    /// droppable while still burning.
    ///
    /// <para>
    /// The mesh is loaded from Resources and <b>measured</b> rather than assumed. Its long axis,
    /// its length and where its grip ends are all read off the renderer bounds at spawn and used
    /// to scale it to <see cref="torchLength"/> and to place the lens and the beam at the end of
    /// it. That is why swapping the model is a matter of dropping a different FBX in: nothing
    /// here knows how big this one is, only which way along it the beam points. If the model is
    /// missing entirely it falls back to a capsule, which is what this was before there was a
    /// model, and everything else still works.
    /// </para>
    ///
    /// <para>
    /// It is an <see cref="EquipmentBase"/> rather than a component bolted to the player, and
    /// that is what makes it behave like a thing rather than like a feature: the existing
    /// inventory carries it, the existing <see cref="Interaction.InteractivePickup"/> lets it be
    /// taken, and the existing <see cref="PlayerInventory.DropSelected"/> puts it down. No part
    /// of carrying, dropping or picking up is new code.
    /// </para>
    ///
    /// <para>
    /// <b>The light does not follow the device-active flag, and that is on purpose.</b>
    /// <see cref="EquipmentBase.Unequip"/> clears that flag and <see cref="EquipmentBase.Drop"/>
    /// clears it again, so a torch put down while lit would go out in the player's hand on the
    /// way to the floor. This keeps its own switch instead, and lights whenever it is on and
    /// either held or lying in the room. Stowed in a bag it goes dark, which is the one case
    /// where a torch genuinely should.
    /// </para>
    ///
    /// <para>
    /// Aim is taken from the player's own axes rather than the wrist, lagged through a smoothed
    /// direction. That is on purpose twice over: a bone's local axes are whatever the exporter
    /// produced, so "point the torch forward" as a local angle is a guess, and a torch that
    /// faithfully follows a walk cycle's wrist waves its beam about like a conductor. The lag is
    /// what gives it the swing when the player turns.
    /// </para>
    /// </summary>
    [AddComponentMenu("Catch If You Can/Held Flashlight")]
    public sealed class HeldFlashlight : EquipmentBase
    {
        [Header("Carry")]
        [Tooltip("Bone the torch is held by, matched by name suffix. Falls back to the anchor " +
                 "the inventory equipped it to.")]
        [SerializeField] private string handBoneSuffix = "_hand_r";

        [Tooltip("Character root searched for that bone.")]
        [SerializeField] private Transform characterVisual;

        [Tooltip("Player root, whose forward the beam is aimed along.")]
        [SerializeField] private Transform playerBody;

        [SerializeField] private PlayerController playerController;

        [Header("Body")]
        [Tooltip("Resources path of the torch mesh. Empty, or missing, falls back to a capsule.")]
        [SerializeField] private string modelResourcePath = "Props/CIYC_Flashlight";

        [Tooltip("Resources path of the material to pin onto the torch mesh. The FBX carries its " +
                 "own material remap, and a remap that quietly did not take is a torch that is " +
                 "there and cannot be seen; loading the real one by path removes that whole class " +
                 "of failure. Empty leaves the model with whatever it imported with.")]
        [SerializeField] private string modelMaterialPath = "Props/MAT_Flashlight";

        [Tooltip("Log once what the torch actually ended up being: whether the mesh loaded, how " +
                 "many renderers it has, what shader they are on and how big it came out.")]
        [SerializeField] private bool logState = true;

        [Tooltip("How long the torch is in the hand, in metres. The model is scaled to this " +
                 "whatever units it was exported in - this one is two units end to end, which " +
                 "would be a two metre torch taken at face value.")]
        [SerializeField, Min(0.05f)] private float torchLength = 0.24f;

        [Tooltip("Which way along the model the beam points, in the model's own axes as Unity " +
                 "imports them. Measured from the mesh: the barrel lies along X at a radius of " +
                 "0.096 and flares to 0.19 over the last quarter, so the head is at the FBX's " +
                 "+X - which is Unity's -X, because the importer negates X on the way in from " +
                 "the file's right-handed axes.")]
        [SerializeField] private Vector3 modelBeamAxis = Vector3.left;

        [Tooltip("Fallback capsule size in metres: diameter, length, diameter.")]
        [SerializeField] private Vector3 size = new Vector3(0.052f, 0.24f, 0.052f);

        [Tooltip("Offset from the hand, in the player's own axes: right, up, forward. Only used " +
                 "on the fallback path, when there is no character whose knuckles can be " +
                 "measured. The field below is the one that moves the torch in the real hand.")]
        [SerializeField] private Vector3 gripOffset = new Vector3(0.02f, 0.01f, 0.06f);

        [Tooltip("Where the torch sits in the fist, in the hand's own measured axes and in " +
                 "metres: X along the barrel towards the head, Y out of the back of the hand, Z " +
                 "towards the fingertips. Drag it in the Inspector while the game is running and " +
                 "the torch moves in the hand immediately.")]
        [SerializeField] private Vector3 flashlightGripPositionOffset;

        [Tooltip("Turn added to the torch after it has been laid on the fist, in its own axes. " +
                 "Live-tunable in the same way.")]
        [SerializeField] private Vector3 flashlightGripRotationOffset;

        [Tooltip("How far back along the barrel the fist closes, in metres. The pivot is the " +
                 "tail of the torch, so without this the hand grips thin air at the very end of " +
                 "the handle and the whole torch hangs off the front of it. A fist is about " +
                 "eight centimetres across, which puts the tail behind the little finger and the " +
                 "head well clear of the thumb.")]
        [SerializeField, Min(0f)] private float gripBackset = 0.085f;

        [Tooltip("Turn applied to the torch after it has been aimed, in degrees about its own " +
                 "axes. Zero is the torch level and pointing down the aim; this is here so the " +
                 "pose can be tuned against the hand without touching the aiming.")]
        [SerializeField] private Vector3 gripRotationOffset = Vector3.zero;

        [SerializeField] private Color bodyColor = new Color(0.18f, 0.19f, 0.2f);
        [SerializeField] private Color lensColor = new Color(0.85f, 0.82f, 0.66f);

        [Tooltip("How wide the glowing lens is, as a fraction of the torch's length.")]
        [SerializeField, Range(0.05f, 0.6f)] private float lensFraction = 0.19f;

        [Tooltip("Emission of the lens while lit. This is what makes the front of the torch " +
                 "read as switched on from the outside, rather than only the beam it throws.")]
        [SerializeField] private Color lensEmission = new Color(1f, 0.93f, 0.78f);

        [SerializeField, Min(0f)] private float lensEmissionStrength = 4.5f;

        [Header("Aim")]
        [Tooltip("Downward tilt of the beam from level, degrees. A torch carried at chest height " +
                 "points at the floor a few metres ahead, not at the horizon.")]
        [SerializeField] private float aimPitch = 10f;

        [Tooltip("Seconds the aim lags the body. This is the swing.")]
        [SerializeField, Min(0.01f)] private float aimLag = 0.16f;

        [SerializeField] private float walkBobDegrees = 4.5f;
        [SerializeField] private float walkBobRate = 1.15f;

        [Header("Beam")]
        [SerializeField] private float lightRange = 14f;
        [SerializeField] private float lightIntensity = 4.2f;

        [Tooltip("Cone of the beam. Narrow enough to be a torch rather than a lamp, wide enough " +
                 "that walking a corridor does not feel like looking down a straw.")]
        [SerializeField] private float lightSpotAngle = 52f;

        [Tooltip("Inner cone, as a fraction of the outer. The soft edge is most of what makes a " +
                 "beam read as a beam.")]
        [SerializeField, Range(0f, 1f)] private float lightInnerFraction = 0.45f;

        [SerializeField] private Color lightColor = new Color(1f, 0.95f, 0.85f);
        [SerializeField] private bool litOnSpawn;

        [Header("Dropped")]
        [Tooltip("How far in front of the player it leaves the hand, and how far above chest " +
                 "height, in metres. Where the throw starts, not where it ends up - where it ends " +
                 "up is physics' business.")]
        [SerializeField] private float dropForward = 0.42f;
        [SerializeField] private float dropHeight = 0.06f;

        [Tooltip("Mass of the torch once it leaves the hand, in kilograms.")]
        [SerializeField, Min(0.02f)] private float dropMass = 0.32f;

        [Tooltip("How hard it is thrown, in newton-seconds: forward along the player's look, and " +
                 "up. Small - it is put down, not hurled.")]
        [SerializeField] private Vector2 dropImpulse = new Vector2(1.1f, 0.35f);

        [Tooltip("Spin given to the throw, in newton-metre-seconds. This is what decides whether " +
                 "it lands and stays or lands and rolls a hand's width.")]
        [SerializeField] private float dropSpin = 0.09f;

        [SerializeField, Min(0f)] private float dropLinearDamping = 0.4f;
        [SerializeField, Min(0f)] private float dropAngularDamping = 3.2f;

        [Tooltip("Radius of the physics collider it lands on, in metres. A torch is a cylinder, " +
                 "so this is a capsule down its own length: it can roll a little across itself " +
                 "and cannot roll along itself, which is exactly what a dropped torch does.")]
        [SerializeField, Min(0.005f)] private float dropRadius = 0.026f;

        private Transform _handBone;
        private Transform _barrel;
        private Transform _head;
        private Transform _view;
        private PlayerBodyMotion _bodyMotion;
        private CapsuleCollider _dropCollider;
        private Rigidbody _dropBody;
        private int _placedFrame = -1;
        private Light _light;
        private Vector3 _aim = Vector3.forward;
        private Vector3 _aimVelocity;
        private float _bobPhase;
        private Material _bodyMaterial;
        private Material _lensMaterial;
        private Renderer _lensRenderer;
        private bool _lit;
        private bool _onGround;

        /// <summary>Whether the switch is on. The beam also needs the torch to be held or down.</summary>
        public bool LightOn
        {
            get => _lit;
            set
            {
                if (_lit == value)
                    return;

                _lit = value;
                ApplyLight();
            }
        }

        /// <summary>True while it is lying in the room rather than carried.</summary>
        public bool IsOnGround => _onGround;

        /// <summary>
        /// The lamp end of the torch, and the one thing the beam is allowed to come out of. It
        /// is a child of the barrel at the far end of the measured mesh, so the light and the
        /// visible head cannot disagree about which way the torch is pointing however the grip
        /// is tuned.
        /// </summary>
        public Transform BeamOrigin => _head;

        protected override void Awake()
        {
            base.Awake();

            if (playerController == null)
                playerController = Object.FindAnyObjectByType<PlayerController>();
            if (playerBody == null && playerController != null)
                playerBody = playerController.transform;

            // Cached once. The beam follows the look rather than the body, so this is read every
            // frame and must never be a search.
            var main = Camera.main;
            if (main != null)
                _view = main.transform;

            Build();
            _lit = litOnSpawn;
            ApplyLight();

            if (playerBody != null)
                _aim = playerBody.forward;
        }

        private void OnEnable()
        {
            var input = Input.MobileInputController.Instance;
            if (input == null)
                return;

            input.OnFlashlightTap += OnFlashlightRequested;
            // Report on the way in as well as on every change, so a HUD built after this
            // component does not come up showing the opposite of the truth.
            input.ReportFlashlightState(_lit);
        }

        private void OnDisable()
        {
            var input = Input.MobileInputController.Instance;
            if (input != null)
                input.OnFlashlightTap -= OnFlashlightRequested;
        }

        /// <summary>
        /// The torch button, and the G key, both arrive here.
        ///
        /// <para>
        /// Routed through <see cref="EquipmentBase.Use"/> rather than flipping the switch
        /// directly, so the switch obeys the same rules as any other equipment: a flat battery or
        /// a broken torch refuses, and - the part that matters here - a torch lying on the floor
        /// refuses too, because it is neither equipped nor placed. That is what makes a dropped
        /// torch stay lit until it is picked back up, without a single line of special-casing.
        /// </para>
        /// </summary>
        private void OnFlashlightRequested() => Use();

        /// <summary>Points the torch at the character it is being carried by.</summary>
        public void BindCharacter(Transform visual, Transform body)
        {
            characterVisual = visual;
            if (body != null)
                playerBody = body;
            ResolveHandBone();

            // The body motion poses the arm that carries this, and both run in LateUpdate, where
            // the order between two components is whatever Unity feels like. Rather than guess,
            // the torch is placed from the end of the pose itself.
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

        // ---- equipment ---------------------------------------------------------------------

        protected override void OnUse() => LightOn = !LightOn;

        public override void Equip(Transform handAnchor)
        {
            // Physics first: the base implementation reparents, and reparenting a body that is
            // still simulating is how an item ends up flying across the room as it is picked up.
            ReleasePhysics();
            base.Equip(handAnchor);
            _onGround = false;
            ApplyLight();
        }

        public override void Unequip()
        {
            base.Unequip();
            // Base clears the device flag; the switch is ours and survives. Whether the beam is
            // actually on now depends on where the torch ended up, which Drop decides.
            ApplyLight();
        }

        /// <summary>
        /// Throws the torch out of the hand and lets physics decide where it comes to rest,
        /// still burning if it was.
        ///
        /// <para>
        /// The base implementation refuses without a definition that allows dropping, and
        /// switches the device off on the way out. This does neither: a torch you cannot put down
        /// is not a torch, and one that goes dark the moment it leaves your hand is the opposite
        /// of what a dropped torch does.
        /// </para>
        ///
        /// <para>
        /// It used to be placed: a ray straight down and the torch set on whatever it hit,
        /// pointing along the floor. That is tidy and reads as nothing having happened. This
        /// gives it a body, a capsule down its own length, a small shove and a little spin, and
        /// lets it land how it lands - which is the whole point, because a capsule dropped on its
        /// side rolls a few centimetres and stops, and one dropped on its end does not roll at
        /// all. The beam goes with it, so where it finishes pointing is where it lights.
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

            // The root carries the torch now rather than the other way round, so the body can
            // move one transform and take the mesh, the lens and the beam with it.
            transform.SetPositionAndRotation(
                from, Quaternion.LookRotation(throwDirection, Vector3.up) *
                      Quaternion.Euler(90f, 0f, 0f));

            if (_barrel != null)
            {
                _barrel.localRotation = Quaternion.identity;
                _barrel.localPosition = new Vector3(0f, -torchLength * 0.5f, 0f);
            }

            IsPlaced = false;
            _onGround = true;
            _placedFrame = Time.frameCount;

            StartPhysics(throwDirection);

            ApplyLight();
            Core.GameEvents.EquipmentChanged();
        }

        /// <summary>Gives the torch a body and throws it.</summary>
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
        private void ReleasePhysics()
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

            if (_barrel != null)
            {
                _barrel.localPosition = Vector3.zero;
                _barrel.localRotation = Quaternion.identity;
            }
        }

        private void ApplyLight()
        {
            if (_light == null)
                return;

            // Held or lying in the room it burns; stowed in a slot it does not. A torch in a bag
            // is the one case where going dark is right.
            bool burning = _lit && (IsEquipped || _onGround);
            _light.enabled = burning;

            // The front of the torch lights up too, not just the beam it throws. Without this a
            // torch lying lit on the floor is a cone of light coming from an unlit object.
            if (_lensMaterial != null)
            {
                _lensMaterial.SetColor("_EmissionColor",
                    burning ? lensEmission * lensEmissionStrength : Color.black);
            }

            Input.MobileInputController.Instance?.ReportFlashlightState(_lit);
        }

        // ---- construction --------------------------------------------------------------------

        /// <summary>
        /// Builds the torch: a pivot at the grip, the body along its local +Y, and the lens and
        /// beam at the far end of it.
        ///
        /// <para>
        /// The pivot's origin is the grip, not the middle, which is what lets the aiming code
        /// simply put the pivot in the hand and point its +Y down the beam. The model is turned
        /// so its own long axis becomes that +Y and slid so its near end sits at the origin, so
        /// none of the aiming has to know anything about the mesh.
        /// </para>
        /// </summary>
        private void Build()
        {
            if (_barrel != null)
                return;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            var pivot = new GameObject("Torch");
            _barrel = pivot.transform;
            _barrel.SetParent(transform, false);

            float length = BuildBody(shader);

            BuildLens(shader, length);
            BuildBeam(length);
            BuildDropCollider(length);
        }

        /// <summary>
        /// The shape the torch lands on: a capsule down its own length, switched off while it is
        /// being carried so a thing in the player's hand is not also a thing in the player's way.
        /// The trigger sphere the pickup ray uses is a separate collider and stays on.
        /// </summary>
        private void BuildDropCollider(float length)
        {
            _dropCollider = gameObject.AddComponent<CapsuleCollider>();
            _dropCollider.direction = 1;                       // down the torch's own Y
            _dropCollider.radius = dropRadius;
            _dropCollider.height = length + dropRadius * 2f;
            _dropCollider.center = Vector3.zero;
            _dropCollider.enabled = false;
        }

        /// <summary>
        /// Puts the mesh on the pivot and returns how long the torch ended up, in metres.
        /// Falls back to a capsule when there is no model to load.
        /// </summary>
        private float BuildBody(Shader shader)
        {
            var prefab = string.IsNullOrEmpty(modelResourcePath)
                ? null
                : Resources.Load<GameObject>(modelResourcePath);

            if (prefab == null)
            {
                if (!string.IsNullOrEmpty(modelResourcePath))
                {
                    Debug.LogError("[CIYC] No torch model at Resources/" + modelResourcePath +
                                   ". What you are seeing in the hand is the fallback capsule and " +
                                   "its lens, not the flashlight.", this);
                }
                return BuildCapsule(shader);
            }

            // Spawned loose, at the world origin with no rotation and no scale, so that the
            // renderer bounds read below are the model's own numbers. Spawned into the hand
            // instead - which is what this did - they are the model's bounds plus wherever in
            // the level the player happens to be standing, and the slide at the end of this
            // method then pushes the torch that whole distance away: at the room's spawn point
            // it put the mesh two and a quarter metres behind the player, which is exactly the
            // "the button works but there is no torch" this is here to fix.
            var model = Instantiate(prefab);
            model.name = "Body";

            var renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                Destroy(model);
                return BuildCapsule(shader);
            }

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            model.transform.SetParent(_barrel, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;

            // Made visible and given a material that is known to exist. An object the importer
            // decided was hidden, a renderer that arrived switched off, and a material whose
            // textures were never in the delivery all look the same from the player's side: a
            // hand holding nothing.
            Material pinned = string.IsNullOrEmpty(modelMaterialPath)
                ? null
                : Resources.Load<Material>(modelMaterialPath);

            if (pinned == null && !string.IsNullOrEmpty(modelMaterialPath))
            {
                Debug.LogWarning("[CIYC] No torch material at Resources/" + modelMaterialPath +
                                 "; keeping whatever the model imported with.", this);
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                if (!renderers[i].gameObject.activeSelf)
                    renderers[i].gameObject.SetActive(true);
                renderers[i].enabled = true;

                if (pinned == null)
                    continue;

                int slots = Mathf.Max(1, renderers[i].sharedMaterials.Length);
                var materials = new Material[slots];
                for (int m = 0; m < slots; m++)
                    materials[m] = pinned;
                renderers[i].sharedMaterials = materials;
            }

            Vector3 axis = modelBeamAxis.sqrMagnitude < 0.0001f ? Vector3.right : modelBeamAxis.normalized;
            float along = Mathf.Abs(Vector3.Dot(bounds.size, Abs(axis)));
            if (along < 0.0001f)
                along = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));

            float scale = torchLength / along;
            model.transform.localScale = Vector3.one * scale;

            // Turn the model's own long axis into the pivot's +Y, so everything downstream -
            // aiming, the lens, the beam, the capsule fallback - shares one convention.
            model.transform.localRotation = Quaternion.FromToRotation(axis, Vector3.up);

            // And slide it so the grip end sits on the pivot rather than its middle.
            Vector3 centre = model.transform.localRotation * (bounds.center * scale);
            model.transform.localPosition = new Vector3(-centre.x, torchLength * 0.5f - centre.y, -centre.z);

            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                renderers[i].receiveShadows = true;
            }

            if (logState)
            {
                var used = renderers[0].sharedMaterial != null
                    ? renderers[0].sharedMaterial.shader
                    : null;
                Debug.Log("[CIYC] Torch model: renderers=" + renderers.Length +
                          " active=" + model.activeInHierarchy +
                          " shader=" + (used != null ? used.name : "<none>") +
                          " measured=" + bounds.size.ToString("F3") +
                          " scale=" + scale.ToString("F4") +
                          " length=" + torchLength.ToString("F3"), this);
            }

            return torchLength;
        }

        private static Vector3 Abs(Vector3 v) =>
            new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        /// <summary>The torch as it was before there was a model: a capsule with a pale cap.</summary>
        private float BuildCapsule(Shader shader)
        {
            var barrel = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            barrel.name = "Body";
            DestroyCollider(barrel);
            barrel.transform.SetParent(_barrel, false);
            barrel.transform.localScale = new Vector3(size.x, size.y * 0.5f, size.z);
            barrel.transform.localPosition = new Vector3(0f, size.y * 0.5f, 0f);

            if (shader != null)
            {
                _bodyMaterial = new Material(shader) { name = "Flashlight_Body_Runtime" };
                _bodyMaterial.color = bodyColor;
                barrel.GetComponent<Renderer>().sharedMaterial = _bodyMaterial;
            }

            return size.y;
        }

        /// <summary>
        /// The glowing lens. A small disc of its own rather than emission on the body material:
        /// the model is one mesh with one atlas, so lighting up "the front" through the material
        /// would light up the whole torch.
        /// </summary>
        private void BuildLens(Shader shader, float length)
        {
            var lens = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lens.name = "Lens";
            DestroyCollider(lens);
            lens.transform.SetParent(_barrel, false);

            float width = torchLength * lensFraction;
            lens.transform.localScale = new Vector3(width, width * 0.45f, width);
            lens.transform.localPosition = new Vector3(0f, length - width * 0.18f, 0f);

            if (shader == null)
                return;

            _lensMaterial = new Material(shader) { name = "Flashlight_Lens_Runtime" };
            _lensMaterial.color = lensColor;
            _lensMaterial.EnableKeyword("_EMISSION");
            _lensRenderer = lens.GetComponent<Renderer>();
            _lensRenderer.sharedMaterial = _lensMaterial;
            _lensRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private void BuildBeam(float length)
        {
            var lightGo = new GameObject("FlashlightHead");
            _head = lightGo.transform;
            lightGo.transform.SetParent(_barrel, false);
            // The torch runs along local Y and a spot light shines down local Z, so the light is
            // turned a quarter turn to agree with the body it sits in.
            lightGo.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            lightGo.transform.localPosition = new Vector3(0f, length, 0f);

            _light = lightGo.AddComponent<Light>();
            _light.type = LightType.Spot;
            _light.range = lightRange;
            _light.intensity = lightIntensity;
            _light.spotAngle = lightSpotAngle;
            _light.innerSpotAngle = lightSpotAngle * lightInnerFraction;
            _light.color = lightColor;
            // Additional-light shadows are off in the URP asset, so asking for them here would
            // cost the sort and give nothing back.
            _light.shadows = LightShadows.None;
            _light.enabled = false;
        }

        private static void DestroyCollider(GameObject go)
        {
            var collider = go.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);
        }

        // ---- per frame -----------------------------------------------------------------------

        private void LateUpdate()
        {
            // Normally already done from the body motion's pose callback; this is the path for a
            // character with no procedural body layer at all.
            if (_placedFrame != Time.frameCount)
                PlaceInHand();
        }

        /// <summary>
        /// Puts the torch in the hand for this frame.
        ///
        /// <para>
        /// Where the rig can be measured, it is: the fist's own knuckles say which axis a
        /// cylinder held in it lies along and where the middle of the palm is, and the torch is
        /// laid on that. Nothing here decides where the hand points - the arm pose does that, and
        /// it aims the hand down the player's own line of sight - so the barrel, the lens and the
        /// beam all end up facing wherever the player is looking without any of them being told
        /// to.
        /// </para>
        ///
        /// <para>
        /// The fallback below is the old behaviour, for a player with no character visual: aimed
        /// off the camera, hung off whatever anchor the inventory equipped it to.
        /// </para>
        /// </summary>
        public void PlaceInHand()
        {
            _placedFrame = Time.frameCount;

            if (_barrel == null || !IsEquipped || playerBody == null)
                return;

            if (_bodyMotion != null &&
                _bodyMotion.TryGetGrip(out Vector3 palm, out Vector3 barrel, out Vector3 palmNormal))
            {
                _barrel.rotation = Quaternion.LookRotation(barrel, palmNormal) *
                                   Quaternion.Euler(90f, 0f, 0f) *
                                   Quaternion.Euler(gripRotationOffset) *
                                   Quaternion.Euler(flashlightGripRotationOffset);

                // Slid in the hand's own frame rather than the player's, so "towards the
                // fingertips" keeps meaning that however the wrist is turned.
                Vector3 towardsFingers = Vector3.Cross(palmNormal, barrel);
                _barrel.position = palm
                                   - barrel * gripBackset
                                   + barrel * flashlightGripPositionOffset.x
                                   + palmNormal * flashlightGripPositionOffset.y
                                   + towardsFingers * flashlightGripPositionOffset.z;
                return;
            }

            Transform anchor = _handBone != null ? _handBone : HandAnchor;
            if (anchor == null)
                return;

            // Aim, lagged. Smoothing the direction rather than the angle keeps the swing even
            // when the player spins right past 180 degrees, where an angle would unwind the long
            // way round. Taken from the camera rather than the body so the beam goes where the
            // player is looking rather than only where they are facing.
            Vector3 look = _view != null ? _view.forward : playerBody.forward;
            Vector3 target = Quaternion.AngleAxis(aimPitch, playerBody.right) * look;
            _aim = Vector3.SmoothDamp(_aim, target, ref _aimVelocity, aimLag);
            if (_aim.sqrMagnitude < 0.0001f)
                _aim = target;

            float speed = playerController != null ? playerController.CurrentSpeed : 0f;
            _bobPhase += Time.deltaTime * speed * walkBobRate * Mathf.PI * 2f;
            float bob = Mathf.Sin(_bobPhase) * walkBobDegrees * Mathf.Clamp01(speed * 0.5f);

            Vector3 aim = Quaternion.AngleAxis(bob, playerBody.right) * _aim.normalized;

            // LookRotation points local +Z along the aim; the extra quarter turn puts local +Y -
            // the torch's length - there instead.
            _barrel.rotation = Quaternion.LookRotation(aim, playerBody.up) *
                               Quaternion.Euler(90f, 0f, 0f) *
                               Quaternion.Euler(gripRotationOffset);

            // Slid back down its own barrel so the hand closes around the handle rather than
            // around the very end of it. The pivot is the tail of the torch, so this is the one
            // number that decides how much of it sticks out of the front of the fist.
            _barrel.position = anchor.position +
                               playerBody.right * gripOffset.x +
                               playerBody.up * gripOffset.y +
                               playerBody.forward * gripOffset.z -
                               aim * gripBackset;
        }

        private void OnDestroy()
        {
            if (_bodyMaterial != null) Destroy(_bodyMaterial);
            if (_lensMaterial != null) Destroy(_lensMaterial);
        }
    }
}
