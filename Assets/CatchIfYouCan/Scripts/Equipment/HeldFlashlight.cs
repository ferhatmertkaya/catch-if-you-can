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

        [SerializeField] private Color bodyColor = new Color(0.18f, 0.19f, 0.2f);
        [SerializeField] private Color lensColor = new Color(0.85f, 0.82f, 0.66f);

        [Tooltip("How wide the glowing lens is, as a fraction of the torch's length.")]
        [SerializeField, Range(0.05f, 0.6f)] private float lensFraction = 0.19f;

        [Tooltip("Emission of the lens while lit. This is what makes the front of the torch " +
                 "read as switched on from the outside, rather than only the beam it throws.")]
        [SerializeField] private Color lensEmission = new Color(1f, 0.93f, 0.78f);

        [SerializeField, Min(0f)] private float lensEmissionStrength = 4.5f;

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

        private Transform _barrel;
        private Transform _head;
        private Light _light;
        private Material _bodyMaterial;
        private Material _lensMaterial;
        private Renderer _lensRenderer;
        private bool _lit;

        /// <summary>The torch itself: its local +Y runs from the grip to the lens.</summary>
        protected override Transform Carried => _barrel;

        protected override float CarriedLength => torchLength;

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

        protected override void BuildCarried() => Build();

        /// <summary>The beam follows the torch wherever it ends up: hand, bag or floor.</summary>
        protected override void OnCarryChanged() => ApplyLight();

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

        // ---- equipment ---------------------------------------------------------------------

        protected override void OnUse() => LightOn = !LightOn;

        protected override float GetInterferenceMultiplier() => interferenceMultiplier;

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

            if (charge <= flickerThreshold * 0.5f
                && Core.ServiceLocator.TryGet<Evidence.EvidenceManager>(out var evidence))
            {
                evidence.RegisterEvidence(Evidence.EvidenceType.ElectronicDistortion);
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

            var shader = Art.CiycShaders.FindLit();

            var pivot = new GameObject("Torch");
            _barrel = pivot.transform;
            _barrel.SetParent(transform, false);

            float length = BuildBody(shader);

            BuildLens(shader, length);
            BuildBeam(length);
            BuildDropCollider(length);
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

        private void OnDestroy()
        {
            if (_bodyMaterial != null) Destroy(_bodyMaterial);
            if (_lensMaterial != null) Destroy(_lensMaterial);
        }
    }
}
