using CatchIfYouCan.Player;
using UnityEngine;

namespace CatchIfYouCan.Equipment
{
    /// <summary>
    /// The torch: a modelled body with a real spot light, carried in the character's hand, and
    /// droppable while still burning.
    ///
    /// <para>
    /// The mesh is loaded and <b>measured</b> rather than assumed - its long axis, its length
    /// and where its grip ends are read off the renderer bounds at spawn - which is why
    /// swapping the model is a matter of pointing the visual profile somewhere else. None of
    /// that lives here any more: it is <see cref="EquipmentVisualFactory"/>'s, driven by the
    /// definition's <see cref="EquipmentVisualProfile"/>, so the other ten items get the same
    /// treatment without ten copies of it.
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
    /// Being carried, aimed and thrown is <see cref="HeldEquipmentBase"/>'s, not this class's.
    /// What is left here is what makes it a torch rather than a held object in general: the
    /// measured model, the lens, the beam, the switch and the flat battery. The beam does not
    /// need to be aimed at anything, because it is a child of the barrel, and the barrel is
    /// what the base lays in the hand.
    /// </para>
    /// </summary>
    [AddComponentMenu("Catch If You Can/Held Flashlight")]
    public sealed class HeldFlashlight : HeldEquipmentBase
    {
        [SerializeField] private Color lensColor = new Color(0.85f, 0.82f, 0.66f);

        [Tooltip("How wide the glowing lens is, as a fraction of the torch's length.")]
        [SerializeField, Range(0.05f, 0.6f)] private float lensFraction = 0.19f;

        [Tooltip("Emission of the lens while lit. This is what makes the front of the torch " +
                 "read as switched on from the outside, rather than only the beam it throws.")]
        [SerializeField] private Color lensEmission = new Color(1f, 0.93f, 0.78f);

        [SerializeField, Min(0f)] private float lensEmissionStrength = 4.5f;

        [Header("Beam")]
        [Tooltip("How far the beam reaches. 14 m stopped inside most rooms, which reads as the " +
                 "torch being weak rather than the room being deep; 22 m carries down a hallway " +
                 "and still falls off long before it lights a house.")]
        [SerializeField] private float lightRange = 22f;

        [Tooltip("Lower than it was, because the cone is tighter and the range is longer. A " +
                 "torch that washes out a whole room makes the room's own lights pointless, " +
                 "which is the balance this number is really setting.")]
        [SerializeField] private float lightIntensity = 3.4f;

        [Tooltip("Kelvin. A modern LED torch is cool-white and slightly blue against the warm " +
                 "practicals in the house, which is what makes the beam read as YOURS.")]
        [SerializeField, Range(1500f, 12000f)] private float lightTemperature = 4300f;

        [Tooltip("Shadows from the torch are what make a doorway frame a shape and a bannister " +
                 "throw bars across a wall. Real cost, so it is a quality-tier decision: on " +
                 "above the lowest tier, off at it.")]
        [SerializeField] private bool beamShadows = true;

        [Tooltip("Cone of the beam. Narrow enough to be a torch rather than a lamp, wide enough " +
                 "that walking a corridor does not feel like looking down a straw.")]
        [SerializeField] private float lightSpotAngle = 42f;

        [Tooltip("Inner cone, as a fraction of the outer. The soft edge is most of what makes a " +
                 "beam read as a beam.")]
        [SerializeField, Range(0f, 1f)] private float lightInnerFraction = 0.32f;

        [SerializeField] private Color lightColor = new Color(1f, 0.95f, 0.85f);

        [Header("Battery")]
        [Tooltip("Fraction of battery below which the beam browns out and stutters. Only ever " +
                 "reached by a torch bound to a definition that actually has a battery.")]
        [SerializeField, Range(0f, 1f)] private float flickerThreshold = 0.12f;

        [Tooltip("How fast the low-battery stutter runs, in noise samples per second.")]
        [SerializeField, Min(0f)] private float flickerSpeed = 12f;

        [Tooltip("How loudly the torch reads on a detector while it is switched on. Carried " +
                 "over from the retired FlashlightEquipment, which was the only place a " +
                 "flashlight's interference was ever tuned; the 0.35 on EquipmentBase is the " +
                 "generic default for equipment in general, not a value chosen for a torch.")]
        [SerializeField, Range(0f, 1f)] private float interferenceMultiplier = 0.2f;
        [SerializeField] private bool litOnSpawn;

        private Transform _head;
        private Light _light;
        private Material _lensMaterial;
        private Renderer _lensRenderer;
        private bool _lit;

        /// <summary>Whether the switch is on. The beam also needs the torch to be held or down.</summary>
        public bool LightOn
        {
            get => _lit;
            set
            {
                bool next = value && (IsPowered || !HasBattery);
                if (_lit == next)
                    return;

                _lit = next;
                ApplyLight();
            }
        }

        /// <summary>Whether this torch runs off a battery at all. The lobby's does not.</summary>
        private bool HasBattery => definition != null && definition.MaxBattery > 0f;

        /// <summary>
        /// The lamp end of the torch, and the one thing the beam is allowed to come out of. It
        /// is a child of the barrel at the far end of the measured mesh, so the light and the
        /// visible head cannot disagree about which way the torch is pointing however the grip
        /// is tuned.
        /// </summary>
        public Transform BeamOrigin => _head;

        /// <summary>
        /// The spot light the beam comes out of. Exposed so systems that need the player's
        /// actual light source - <see cref="Player.FearSystem"/>, which asks whether the player
        /// is standing in their own light - can be handed it directly instead of reaching in
        /// through reflection for a private field.
        /// </summary>
        public Light Beam => _light;

        protected override void Awake()
        {
            // The base resolves the player, the view and the hand, and calls BuildCarried.
            base.Awake();

            _lit = litOnSpawn;
            ApplyLight();
        }

        /// <summary>The beam follows the torch wherever it ends up: hand, bag or floor.</summary>
        protected override void OnCarryChanged() => ApplyLight();

        /// <summary>The visual was replaced once the definition arrived; relight the new one.</summary>
        protected override void OnCarriedRebuilt()
        {
            ApplyLight();
            _visualReported = false;
        }

        private bool _visualReported;

        /// <summary>
        /// Says exactly what ended up in the player's hand, once.
        ///
        /// <para>
        /// Every way this can go wrong - no model loaded, a renderer disabled, zero scale, the
        /// wrong layer, a mesh the camera cannot see - shows up on screen as "there is no
        /// flashlight", and they are not the same bug. One line separates them.
        /// </para>
        ///
        /// <para>
        /// Deferred to the first frame the torch is actually held, so it reports the finished
        /// article rather than whatever existed a line into construction.
        /// </para>
        /// </summary>
        // A second in, so the inventory, the definition and any late rebuild have all happened.
        private float _reportAfter = 1f;

        private void ReportVisual()
        {
            _visualReported = true;

            if (CarriedRoot == null)
            {
                Core.CIYCLog.Error("[CIYC][FlashlightVisual] state=" + LifecycleState +
                                   " NO VISUAL WAS BUILT AT ALL. Either BuildCarried never ran " +
                                   "or the root was destroyed after it did.");
                return;
            }

            var renderers = CarriedRoot.GetComponentsInChildren<Renderer>(true);
            var bounds = new Bounds(CarriedRoot.position, Vector3.zero);
            int enabled = 0;
            string materials = "<none>";

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i].enabled)
                {
                    enabled++;
                    bounds.Encapsulate(renderers[i].bounds);
                }
                if (i == 0 && renderers[i].sharedMaterial != null)
                    materials = renderers[i].sharedMaterial.name;
            }

            Camera view = Core.LocalPlayerService.ResolveViewCamera();
            string canSee = "no camera";
            if (view != null && enabled > 0)
            {
                bool inMask = (view.cullingMask & (1 << CarriedRoot.gameObject.layer)) != 0;
                GeometryUtility.CalculateFrustumPlanes(view, _visualFrustum);
                canSee = (inMask && GeometryUtility.TestPlanesAABB(_visualFrustum, bounds))
                    ? "true" : "false (mask=" + inMask + ")";
            }

            var profile = definition != null ? definition.VisualProfile : null;
            string modelPath = profile == null
                ? "<no profile>"
                : profile.IsDevPlaceholder
                    ? "DEV PLACEHOLDER (no model)"
                    : "Resources/" + profile.ModelResourcePath;
            string materialPath = profile == null || string.IsNullOrEmpty(profile.ModelMaterialPath)
                ? "<none>"
                : "Resources/" + profile.ModelMaterialPath;

            // How far the thing actually is from the bone that is supposed to be holding it.
            // A torch that built correctly and was never placed reads as a perfectly healthy
            // renderer sitting a metre from the fingers, which from the player's side is a hand
            // holding nothing - so this is the number that separates "not built" from
            // "built and left behind".
            float fromHand = HandBone != null
                ? Vector3.Distance(CarriedRoot.position, HandBone.position)
                : -1f;

            Core.CIYCLog.Info(
                "[CIYC][FlashlightVisual] state=" + LifecycleState +
                " definition=" + (definition != null ? definition.Id : "<NULL>") +
                " modelPath=" + modelPath +
                " materialPath=" + materialPath +
                " handBone=" + (HandBone != null ? HandBone.name : "<UNRESOLVED>") +
                " parent=" + (CarriedRoot.parent != null ? CarriedRoot.parent.name : "<none>") +
                " instance=" + CarriedRoot.name +
                " renderers=" + enabled + "/" + renderers.Length +
                " material=" + materials +
                " bounds=" + bounds.size.ToString("F3") +
                " localPosition=" + CarriedRoot.localPosition.ToString("F3") +
                " localRotation=" + CarriedRoot.localEulerAngles.ToString("F1") +
                " localScale=" + CarriedRoot.localScale.ToString("F3") +
                " worldPosition=" + CarriedRoot.position.ToString("F2") +
                " distanceFromHandBone=" + fromHand.ToString("F3") + "m" +
                " layer=" + LayerMask.LayerToName(CarriedRoot.gameObject.layer) +
                " cameraCanSee=" + canSee);

            // Each of these is a different bug that looks identical on screen, so each says so
            // in its own words rather than leaving the reader to compare numbers in the line
            // above.
            if (renderers.Length == 0)
                Core.CIYCLog.Error("[CIYC][FlashlightVisual] NO RENDERERS. The model resource " +
                                   "loaded nothing, or every renderer was stripped from it.");
            else if (enabled == 0)
                Core.CIYCLog.Error("[CIYC][FlashlightVisual] Every renderer is DISABLED.");

            if (renderers.Length > 0 && renderers[0].sharedMaterial == null)
                Core.CIYCLog.Error("[CIYC][FlashlightVisual] The renderer has NO MATERIAL. " +
                                   "Nothing is drawn at all - not even magenta. Expected " +
                                   materialPath + ".");

            Vector3 sc = CarriedRoot.lossyScale;
            if (Mathf.Abs(sc.x) < 1e-4f || Mathf.Abs(sc.y) < 1e-4f || Mathf.Abs(sc.z) < 1e-4f)
                Core.CIYCLog.Error("[CIYC][FlashlightVisual] World scale is effectively ZERO (" +
                                   sc.ToString("F5") + "). The model is there and has no size.");

            if (CarriedRoot.parent == null)
                Core.CIYCLog.Error("[CIYC][FlashlightVisual] The carried root has NO PARENT, so " +
                                   "nothing is carrying it.");

            // A held torch's pivot is the grip, so it should be within a hand's width of the
            // bone. Anything past that is the item having been built but never placed.
            if (fromHand > 0.35f)
                Core.CIYCLog.Warn("[CIYC][FlashlightVisual] The torch is " +
                                  fromHand.ToString("F2") + " m from " + HandBone.name +
                                  ", which is not in the hand. It was built correctly and then " +
                                  "left where it was parented - PlaceInHand is what moves it, " +
                                  "and something is stopping that from running.");
        }

        private readonly Plane[] _visualFrustum = new Plane[6];

        [Header("Aim")]
        [Tooltip("How far down the crosshair ray the beam is aimed when nothing is hit. Far " +
                 "enough that the beam reads as parallel to the view rather than converging.")]
        [SerializeField, Min(1f)] private float aimDistance = 25f;

        [Tooltip("Nearest the beam will converge on something it hits. Below this the wall is " +
                 "closer than the torch is long and aiming at it would swing the cone wildly " +
                 "for a few centimetres of hand movement.")]
        [SerializeField, Min(0.2f)] private float minimumAimDistance = 1.2f;

        [Tooltip("How quickly the aim point slides between distances. A hard cut between a wall " +
                 "at two metres and open air at twenty-five is a visible flick of the whole cone " +
                 "every time the crosshair leaves a door frame.")]
        [SerializeField, Min(0.01f)] private float aimSmoothTime = 0.08f;

        private Camera _aimCamera;
        private float _aimDistanceSmoothed = -1f;
        private float _aimDistanceVelocity;

        /// <summary>
        /// Points the beam through the middle of the screen.
        ///
        /// <para>
        /// <b>The torch is held naturally and the light is aimed separately.</b> A torch lies in
        /// the hand at whatever angle the grip gives it, and a beam bolted rigidly to that
        /// barrel lands well off the crosshair - which reads as the light being broken rather
        /// than as the hand being realistic. So the pose is left exactly alone (no hand, arm or
        /// grip maths is touched anywhere) and only the light's own rotation is solved, from the
        /// centre of the viewport outward.
        /// </para>
        ///
        /// <para>
        /// Only while the torch is in the hand. On the floor or in a bag it keeps the barrel's
        /// own direction, which is what makes a dropped torch light the wall it fell facing.
        /// </para>
        /// </summary>
        /// <summary>
        /// <b>override, not a new private method.</b> This was <c>private void LateUpdate()</c>,
        /// which HID <see cref="HeldEquipmentBase.LateUpdate"/> rather than extending it - and
        /// Unity dispatches a message to the most-derived declaration by name, so the base one
        /// simply never ran for the torch.
        ///
        /// <para>
        /// What the base does there is the whole bug: it calls <c>PlaceInHand()</c> for any
        /// frame the body motion's pose callback did not already place. Hidden, the torch had
        /// no fallback at all - it was placed only when a procedural body layer existed AND was
        /// driving the callback, and otherwise stayed at the anchor's own origin instead of
        /// being solved onto the measured grip. A hand that animates normally, holding nothing,
        /// with the torch sitting off at the anchor below the view. Which is the reported
        /// symptom exactly.
        /// </para>
        ///
        /// <para>
        /// The flashlight is the only one of the nine HeldEquipmentBase subclasses that declared
        /// its own LateUpdate, which is why it is the only item this happened to. The C# warning
        /// for it is CS0108, and the offline typecheck harness was not printing warnings.
        /// </para>
        ///
        /// <para>
        /// The base call comes FIRST: place the item, then aim the beam out of where it ended up.
        /// </para>
        /// </summary>
        protected override void LateUpdate()
        {
            base.LateUpdate();

            // Reported once, WHATEVER the state. This used to wait for Equipped, which is why
            // nobody has ever seen this line: a torch sitting in the inventory unselected is
            // never equipped, so the one diagnostic that could say what is wrong with it stayed
            // silent, and the visual was debugged by guessing instead of by reading.
            if (!_visualReported && Time.time > _reportAfter)
                ReportVisual();

            if (_light == null || _head == null)
                return;

            if (LifecycleState != EquipmentLifecycleState.Equipped &&
                LifecycleState != EquipmentLifecycleState.Using)
                return;

            Camera view = ResolveAimCamera();
            if (view == null)
                return;

            // What is actually in front of the crosshair. Against a near wall the beam has to
            // converge on the wall, or the cone lands beside the reticle by however far the hand
            // sits from the eye; in open air it aims far, so the beam reads as parallel to the
            // view rather than crossing it.
            Vector3 eye = view.transform.position;
            Vector3 forward = view.transform.forward;

            float wanted = aimDistance;
            if (Physics.Raycast(eye, forward, out RaycastHit hit, aimDistance,
                                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                wanted = Mathf.Max(minimumAimDistance, hit.distance);
            }

            // Smoothed, because the distance is what jumps: a crosshair crossing a door frame
            // goes from two metres to twenty-five in one frame, and pointing the cone straight
            // at each in turn is a visible flick.
            if (_aimDistanceSmoothed < 0f)
                _aimDistanceSmoothed = wanted;
            _aimDistanceSmoothed = Mathf.SmoothDamp(_aimDistanceSmoothed, wanted,
                                                    ref _aimDistanceVelocity, aimSmoothTime);

            Vector3 target = eye + forward * _aimDistanceSmoothed;
            Vector3 toTarget = target - _light.transform.position;
            if (toTarget.sqrMagnitude < 0.0001f)
                return;

            _light.transform.rotation = Quaternion.LookRotation(toTarget, view.transform.up);
        }

        /// <summary>
        /// The player's camera, cached. Resolved through <see cref="Core.LocalPlayerService"/>
        /// rather than by searching the scene, and re-asked only when the cached one has gone.
        /// </summary>
        private Camera ResolveAimCamera()
        {
            if (_aimCamera != null)
                return _aimCamera;

            _aimCamera = Core.LocalPlayerService.ResolveViewCamera();
            return _aimCamera;
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
        private void OnFlashlightRequested()
        {
            // The torch now lives in its own place rather than in one of the three
            // investigation slots, so the player can be holding the EMF when they reach for it.
            // A stowed torch refuses to light, deliberately - so bring it out first. One tap,
            // torch in hand and on, which is what the button has always appeared to promise.
            //
            // This is a selection, not a special case in the carry rules: nothing below changes,
            // the torch still goes dark when it is stowed, and the hand still holds one item.
            if (LifecycleState == EquipmentLifecycleState.Holstered)
                ResolveInventory()?.SelectTorch();

            // Through the lifecycle rather than EquipmentBase.Use, so a refusal has a reason -
            // flat, broken, or stowed in a bag - and so the torch obeys the same gate as every
            // other item.
            var result = TryUse();
            if (!result.Ok)
                Core.CIYCLog.Info("Flashlight: " + result);
        }

        private PlayerInventory _inventory;

        /// <summary>
        /// The bag this torch is in, found through the hierarchy it is parented into. Cached,
        /// because it is asked on a button press and the answer does not change while the torch
        /// is carried; re-resolved if the torch changes hands.
        /// </summary>
        private PlayerInventory ResolveInventory()
        {
            if (_inventory == null)
                _inventory = GetComponentInParent<PlayerInventory>();
            return _inventory;
        }

        // ---- equipment ---------------------------------------------------------------------

        protected override void OnUse() => LightOn = !LightOn;

        protected override float GetInterferenceMultiplier() => interferenceMultiplier;

        /// <summary>
        /// Flicking a switch does not wear a torch out. The inherited one point per use, over
        /// a hundred points of durability, meant a hundred toggles broke the torch permanently
        /// - and since it refuses to switch on with no durability left, that is a torch that
        /// can never be turned on again in the same run.
        /// </summary>
        protected override float DurabilityLossPerUse => 0f;

        protected override void OnBatteryDepleted()
        {
            base.OnBatteryDepleted();
            LightOn = false;
        }

        /// <summary>
        /// The low-battery brown-out, and the electrical noise that comes with it.
        ///
        /// <para>
        /// Carried over from the retired FlashlightEquipment, which was the only place
        /// <see cref="Evidence.EvidenceType.ElectronicDistortion"/> was ever raised by a torch.
        /// It is gated on the torch having a real battery: <c>BatteryPercent</c> reports 0 when
        /// no definition is bound, and a torch built without one - the lobby's - would otherwise
        /// read as permanently flat and stutter a beam that is meant to be steady.
        /// </para>
        /// </summary>
        protected override void TickEquipped(float deltaTime)
        {
            if (!_lit || _light == null || definition == null || definition.MaxBattery <= 0f)
                return;

            float charge = BatteryPercent;
            if (charge > flickerThreshold)
            {
                _light.intensity = lightIntensity;
                return;
            }

            // Same 60-100% of full brightness the old implementation stuttered between, stated
            // as a fraction of this torch's own intensity rather than its hard-coded 2.
            _light.intensity =
                lightIntensity * (0.6f + Mathf.PerlinNoise(Time.time * flickerSpeed, 0f) * 0.4f);

            if (charge <= flickerThreshold * 0.5f)
            {
                // An observation, not a finding. A browning-out torch near a ghost that does
                // not distort electronics proves nothing, and used to prove everything.
                Observe(Evidence.EvidenceType.ElectronicDistortion,
                        1f - charge / Mathf.Max(0.0001f, flickerThreshold));
            }
        }

        private void ApplyLight()
        {
            if (_light == null)
                return;

            // Held or lying in the room it burns; stowed in a slot it does not. A torch in a bag
            // is the one case where going dark is right.
            bool burning = _lit && (IsEquipped || IsOnGround);
            _light.enabled = burning;

            // The device flag is whether the beam is actually emitting, and it is set here
            // because here is the only place that knows.
            //
            // Nothing set it before. EquipmentBase.DrainBattery returns early without it, so
            // this torch's battery had never drained once - which in turn meant the low-battery
            // brown-out could not happen and the ElectronicDistortion it raises could not
            // either, and the torch read as an inert object to the EMF detector because
            // InterferenceStrength is zero unless the device is active. Deriving it from the
            // beam rather than from the switch also keeps a stowed lit torch from draining in
            // a bag, and keeps a re-equipped one draining again.
            SetDeviceActive(burning);

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
        /// <summary>
        /// The model comes from content; the lens and the beam do not.
        ///
        /// <para>
        /// Loading the mesh, measuring it, scaling it to length, pinning its material and
        /// falling back to a primitive were all written here first, and were all about being
        /// an object rather than about being a torch. They are
        /// <see cref="EquipmentVisualFactory"/>'s now, driven by the definition's visual
        /// profile, so swapping the flashlight FBX is a content change and the other ten items
        /// get the same treatment for free.
        /// </para>
        ///
        /// <para>
        /// What is left here is what a mesh cannot be: a lens that lights up independently of
        /// the body, and a real spot light.
        /// </para>
        /// </summary>
        protected override void BuildCarried()
        {
            if (CarriedRoot != null)
                return;

            base.BuildCarried();

            float length = CarriedLength;

            var shader = Art.CiycShaders.FindLit();
            BuildLens(shader, length);
            BuildBeam(length);
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
            lens.transform.SetParent(CarriedRoot, false);

            float width = length * lensFraction;
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
            lightGo.transform.SetParent(CarriedRoot, false);
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
            _light.useColorTemperature = true;
            _light.colorTemperature = lightTemperature;

            // A quality-tier decision, taken the same way PortalSurface takes its buffer size:
            // the lowest tier is the mobile profile and pays for no beam shadows, everything
            // above it gets them. Gameplay is identical either way - this is cost, not design.
            int levels = Mathf.Max(1, QualitySettings.names != null ? QualitySettings.names.Length : 1);
            bool lowestTier = QualitySettings.GetQualityLevel() <= 0 && levels > 1;
            _light.shadows = beamShadows && !lowestTier ? LightShadows.Soft : LightShadows.None;
            _light.shadowStrength = 0.75f;
            _light.shadowBias = 0.02f;
            // Additional-light shadows are off in the URP asset, so asking for them here would
            // cost the sort and give nothing back.
            _light.enabled = false;
        }

        private static void DestroyCollider(GameObject go)
        {
            var collider = go.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (_lensMaterial != null) Destroy(_lensMaterial);
        }
    }
}
