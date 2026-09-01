using CatchIfYouCan.Player;
using UnityEngine;

namespace CatchIfYouCan.Equipment
{
    /// <summary>
    /// The stand-in torch: a capsule with a real spot light, carried in the character's hand,
    /// and droppable while still burning.
    ///
    /// <para>
    /// Deliberately a capsule. There is no torch model yet, and a placeholder's job is to answer
    /// the questions a model cannot be designed without - is it the right size in the hand, does
    /// it read at all with the body in the way, does the beam land where you expect - none of
    /// which need geometry. Swapping it for a real mesh later is one field.
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
    [AddComponentMenu("Catch If You Can/Placeholder Flashlight")]
    public sealed class PlaceholderFlashlight : EquipmentBase
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
        [Tooltip("Torch size in metres: diameter, length, diameter.")]
        [SerializeField] private Vector3 size = new Vector3(0.052f, 0.21f, 0.052f);

        [Tooltip("Offset from the hand, in the player's own axes: right, up, forward.")]
        [SerializeField] private Vector3 gripOffset = new Vector3(0.02f, 0.01f, 0.06f);

        [SerializeField] private Color bodyColor = new Color(0.18f, 0.19f, 0.2f);
        [SerializeField] private Color lensColor = new Color(0.85f, 0.82f, 0.66f);

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
            _light.enabled = _lit && (IsEquipped || _onGround);

            Input.MobileInputController.Instance?.ReportFlashlightState(_lit);
        }

        // ---- construction --------------------------------------------------------------------

        private void Build()
        {
            if (_barrel != null)
                return;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            var barrel = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            barrel.name = "Barrel";
            DestroyCollider(barrel);

            _barrel = barrel.transform;
            _barrel.SetParent(transform, false);
            _barrel.localScale = new Vector3(size.x, size.y * 0.5f, size.z);

            if (shader != null)
            {
                _bodyMaterial = new Material(shader) { name = "Flashlight_Body_Runtime" };
                _bodyMaterial.color = bodyColor;
                barrel.GetComponent<Renderer>().sharedMaterial = _bodyMaterial;
            }

            // A pale cap at the business end, so which way it is pointing is readable at a
            // glance. Without it a grey pill has no front.
            var lens = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lens.name = "Lens";
            DestroyCollider(lens);
            lens.transform.SetParent(_barrel, false);
            lens.transform.localScale = new Vector3(0.92f, 0.42f, 0.92f);
            lens.transform.localPosition = new Vector3(0f, 1f, 0f);

            if (shader != null)
            {
                _lensMaterial = new Material(shader) { name = "Flashlight_Lens_Runtime" };
                _lensMaterial.color = lensColor;
                lens.GetComponent<Renderer>().sharedMaterial = _lensMaterial;
            }

            var lightGo = new GameObject("Beam");
            lightGo.transform.SetParent(_barrel, false);
            // The capsule's long axis is local Y and a spot light shines down local Z, so the
            // light is turned a quarter turn to agree with the barrel it sits in.
            lightGo.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            lightGo.transform.localPosition = new Vector3(0f, 1f, 0f);

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
