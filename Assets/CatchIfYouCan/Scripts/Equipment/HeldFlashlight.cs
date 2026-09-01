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

        [Tooltip("How long the torch is in the hand, in metres. The model is scaled to this " +
                 "whatever units it was exported in - this one is two units end to end, which " +
                 "would be a two metre torch taken at face value.")]
        [SerializeField, Min(0.05f)] private float torchLength = 0.24f;

        [Tooltip("Which way along the model the beam points, in the model's own axes. Measured " +
                 "from this FBX: it lies along X and its head - the fat end - is at +X.")]
        [SerializeField] private Vector3 modelBeamAxis = Vector3.right;

        [Tooltip("Fallback capsule size in metres: diameter, length, diameter.")]
        [SerializeField] private Vector3 size = new Vector3(0.052f, 0.24f, 0.052f);

        [Tooltip("Offset from the hand, in the player's own axes: right, up, forward.")]
        [SerializeField] private Vector3 gripOffset = new Vector3(0.02f, 0.01f, 0.06f);

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
        [Tooltip("How far in front of the player it lands, and how far above the floor it comes " +
                 "to rest.")]
        [SerializeField] private float dropForward = 0.75f;
        [SerializeField] private float dropHeight = 0.06f;

        [Tooltip("Layers the drop probe treats as floor.")]
        [SerializeField] private LayerMask groundMask = ~0;

        private Transform _handBone;
        private Transform _barrel;
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

        protected override void Awake()
        {
            base.Awake();

            if (playerController == null)
                playerController = Object.FindAnyObjectByType<PlayerController>();
            if (playerBody == null && playerController != null)
                playerBody = playerController.transform;

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
        /// Lays the torch down in front of the player, still burning if it was.
        ///
        /// <para>
        /// The base implementation refuses without a definition that allows dropping, and
        /// switches the device off on the way out. This does neither: a torch you cannot put down
        /// is not a torch, and one that goes dark the moment it leaves your hand is the opposite
        /// of what a dropped torch does.
        /// </para>
        /// </summary>
        public override void Drop(Vector3 position, Quaternion rotation)
        {
            if (IsEquipped)
                Unequip();

            transform.SetParent(null, true);
            transform.SetPositionAndRotation(FindRestingPlace(position), rotation);
            IsPlaced = false;
            _onGround = true;

            // Lying down, so the beam runs along the floor rather than pointing wherever the
            // hand last was.
            if (_barrel != null)
            {
                Vector3 along = rotation * Vector3.forward;
                along.y = 0f;
                if (along.sqrMagnitude < 0.0001f)
                    along = Vector3.forward;
                _barrel.rotation = Quaternion.LookRotation(along.normalized, Vector3.up) *
                                   Quaternion.Euler(90f, 0f, 0f);
                _barrel.position = transform.position;
            }

            ApplyLight();
            Core.GameEvents.EquipmentChanged();
        }

        /// <summary>
        /// Drops the torch to the floor under the intended spot, so it does not hang in the air
        /// over a stairwell or sink into a rug.
        /// </summary>
        private Vector3 FindRestingPlace(Vector3 intended)
        {
            Vector3 from = intended;
            if (playerBody != null)
                from = playerBody.position + Vector3.up * 1.1f + playerBody.forward * dropForward;

            if (Physics.Raycast(from, Vector3.down, out RaycastHit hit, 3f, groundMask,
                                QueryTriggerInteraction.Ignore))
                return hit.point + Vector3.up * dropHeight;

            return new Vector3(from.x, intended.y, from.z);
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
                    Debug.LogWarning("[CIYC] No torch model at Resources/" + modelResourcePath +
                                     "; falling back to a capsule.", this);
                }
                return BuildCapsule(shader);
            }

            var model = Instantiate(prefab, _barrel);
            model.name = "Body";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;

            var renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                Destroy(model);
                return BuildCapsule(shader);
            }

            // Measured, never assumed. The bounds are read while the pivot is at the identity,
            // so world and local agree and the numbers below are the model's own.
            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

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
            var lightGo = new GameObject("Beam");
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
            if (_barrel == null || !IsEquipped || playerBody == null)
                return;

            Transform anchor = _handBone != null ? _handBone : HandAnchor;
            if (anchor == null)
                return;

            // Aim, lagged. Smoothing the direction rather than the angle keeps the swing even
            // when the player spins right past 180 degrees, where an angle would unwind the long
            // way round.
            Vector3 target = Quaternion.AngleAxis(aimPitch, playerBody.right) * playerBody.forward;
            _aim = Vector3.SmoothDamp(_aim, target, ref _aimVelocity, aimLag);
            if (_aim.sqrMagnitude < 0.0001f)
                _aim = target;

            float speed = playerController != null ? playerController.CurrentSpeed : 0f;
            _bobPhase += Time.deltaTime * speed * walkBobRate * Mathf.PI * 2f;
            float bob = Mathf.Sin(_bobPhase) * walkBobDegrees * Mathf.Clamp01(speed * 0.5f);

            Vector3 aim = Quaternion.AngleAxis(bob, playerBody.right) * _aim.normalized;

            // LookRotation points local +Z along the aim; the extra quarter turn puts local +Y -
            // the capsule's length - there instead.
            _barrel.rotation = Quaternion.LookRotation(aim, playerBody.up) *
                               Quaternion.Euler(90f, 0f, 0f);

            _barrel.position = anchor.position +
                               playerBody.right * gripOffset.x +
                               playerBody.up * gripOffset.y +
                               playerBody.forward * gripOffset.z;
        }

        private void OnDestroy()
        {
            if (_bodyMaterial != null) Destroy(_bodyMaterial);
            if (_lensMaterial != null) Destroy(_lensMaterial);
        }
    }
}
