using UnityEngine;
using CatchIfYouCan.Input;

namespace CatchIfYouCan.Player
{
    /// <summary>
    /// A stand-in torch, held in the character's right hand.
    ///
    /// <para>
    /// Deliberately a capsule. There is no torch model yet, and a placeholder's job is to answer
    /// the questions a model cannot be designed without — is it the right size in the hand, does
    /// it read at all with the body in the way, does it swing believably when you turn — none of
    /// which need geometry. Swapping it for a real mesh later is one field.
    /// </para>
    ///
    /// <para>
    /// It hangs off the hand bone for position but takes its rotation from the player's own axes
    /// rather than the wrist. That is on purpose twice over: a bone's local axes are whatever the
    /// exporter produced, so "point the torch forward" as a local angle is a guess, and a torch
    /// that faithfully follows a walk cycle's wrist waves its beam around like a conductor. Aim
    /// lags the body through a smoothed direction, which is what gives it the swing when the
    /// player turns, and a walk bob scaled by actual speed.
    /// </para>
    ///
    /// <para>
    /// The light is real but starts off. Nothing in the HUD presses
    /// <see cref="MobileInputController.PressFlashlight"/> yet, so switching it on by default
    /// would silently relight a room whose lamps were tuned by hand; instead it listens for that
    /// event and is ready the moment a control exists to send it.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Catch If You Can/Hand Flashlight")]
    public sealed class HandFlashlight : MonoBehaviour
    {
        [Header("Attachment")]
        [Tooltip("Bone the torch is held by, matched by name suffix. Falls back to the anchor.")]
        [SerializeField] private string handBoneSuffix = "_hand_r";

        [Tooltip("Where to hang it if the bone is not found - the viewmodel hand anchor.")]
        [SerializeField] private Transform fallbackAnchor;

        [Tooltip("Character root to search for the hand bone.")]
        [SerializeField] private Transform characterVisual;

        [Tooltip("Player root, whose forward the beam is aimed along.")]
        [SerializeField] private Transform playerBody;

        [SerializeField] private PlayerController playerController;

        [Header("Body")]
        [Tooltip("Torch size in metres: diameter, length, diameter.")]
        [SerializeField] private Vector3 size = new Vector3(0.052f, 0.21f, 0.052f);

        [Tooltip("Offset from the hand bone, in the player's own axes: right, up, forward.")]
        [SerializeField] private Vector3 gripOffset = new Vector3(0.02f, 0.01f, 0.06f);

        [SerializeField] private Color bodyColor = new Color(0.18f, 0.19f, 0.2f);
        [SerializeField] private Color lensColor = new Color(0.85f, 0.82f, 0.66f);

        [Header("Aim")]
        [Tooltip("Downward tilt of the beam from level, degrees. A torch carried at hip height " +
                 "points at the floor a few metres ahead, not at the horizon.")]
        [SerializeField] private float aimPitch = 12f;

        [Tooltip("Seconds the aim lags the body. This is the swing: turn quickly and the torch " +
                 "arrives a moment later.")]
        [SerializeField, Min(0.01f)] private float aimLag = 0.16f;

        [Tooltip("How far the torch bobs while walking, degrees at full speed.")]
        [SerializeField] private float walkBobDegrees = 4.5f;

        [Tooltip("Bob cycles per metre per second, so the swing keeps step with the walk.")]
        [SerializeField] private float walkBobRate = 1.15f;

        [Header("Light")]
        [SerializeField] private bool lightOnByDefault;
        [SerializeField] private float lightRange = 11f;
        [SerializeField] private float lightIntensity = 2.6f;
        [SerializeField] private float lightSpotAngle = 46f;
        [SerializeField] private Color lightColor = new Color(1f, 0.94f, 0.82f);

        private Transform _anchor;
        private Transform _bodyTransform;
        private Light _light;
        private Vector3 _aim = Vector3.forward;
        private Vector3 _aimVelocity;
        private float _bobPhase;
        private Material _bodyMaterial;
        private Material _lensMaterial;

        /// <summary>Whether the beam is on. Safe to set at any time.</summary>
        public bool LightOn
        {
            get => _light != null && _light.enabled;
            set { if (_light != null) _light.enabled = value; }
        }

        private void Awake()
        {
            if (playerController == null)
                playerController = GetComponentInParent<PlayerController>();
            if (playerBody == null && playerController != null)
                playerBody = playerController.transform;

            ResolveAnchor();
            Build();

            if (playerBody != null)
                _aim = playerBody.forward;
        }

        private void OnEnable()
        {
            var input = MobileInputController.Instance;
            if (input != null)
                input.OnFlashlightTap += Toggle;
        }

        private void OnDisable()
        {
            var input = MobileInputController.Instance;
            if (input != null)
                input.OnFlashlightTap -= Toggle;
        }

        /// <summary>Re-finds the hand after the character visual arrives.</summary>
        public void BindCharacter(Transform visual)
        {
            characterVisual = visual;
            ResolveAnchor();

            if (_bodyTransform != null && _anchor != null)
                _bodyTransform.SetParent(_anchor, false);
        }

        private void Toggle() => LightOn = !LightOn;

        private void ResolveAnchor()
        {
            _anchor = null;

            if (characterVisual != null && !string.IsNullOrEmpty(handBoneSuffix))
            {
                var all = characterVisual.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    if (!all[i].name.EndsWith(handBoneSuffix, System.StringComparison.OrdinalIgnoreCase))
                        continue;
                    _anchor = all[i];
                    break;
                }
            }

            if (_anchor == null)
                _anchor = fallbackAnchor != null ? fallbackAnchor : transform;
        }

        private void Build()
        {
            if (_bodyTransform != null)
                return;

            // Explicit rather than ??: UnityEngine.Object overloads equality, and mixing that
            // with null-coalescing is the sort of thing that works until the day a shader is
            // destroyed rather than missing.
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            var barrel = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            barrel.name = "Flashlight_Placeholder";
            // A collider on something held inside the player's own capsule is nothing but a
            // source of contacts to resolve.
            DestroyCollider(barrel);

            _bodyTransform = barrel.transform;
            _bodyTransform.SetParent(_anchor, false);
            _bodyTransform.localScale = new Vector3(size.x, size.y * 0.5f, size.z);

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
            lens.transform.SetParent(_bodyTransform, false);
            lens.transform.localScale = new Vector3(0.92f, 0.42f, 0.92f);
            lens.transform.localPosition = new Vector3(0f, 1f, 0f);

            if (shader != null)
            {
                _lensMaterial = new Material(shader) { name = "Flashlight_Lens_Runtime" };
                _lensMaterial.color = lensColor;
                lens.GetComponent<Renderer>().sharedMaterial = _lensMaterial;
            }

            var lightGo = new GameObject("Beam");
            lightGo.transform.SetParent(_bodyTransform, false);
            // The capsule's long axis is local Y and a spot light shines down local Z, so the
            // light has to be turned a quarter turn to agree with the barrel it is inside.
            lightGo.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            lightGo.transform.localPosition = new Vector3(0f, 1f, 0f);

            _light = lightGo.AddComponent<Light>();
            _light.type = LightType.Spot;
            _light.range = lightRange;
            _light.intensity = lightIntensity;
            _light.spotAngle = lightSpotAngle;
            _light.color = lightColor;
            // Additional-light shadows are off in the URP asset, so asking for them here would
            // cost the sort and give nothing back.
            _light.shadows = LightShadows.None;
            _light.enabled = lightOnByDefault;
        }

        private static void DestroyCollider(GameObject go)
        {
            var collider = go.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);
        }

        private void LateUpdate()
        {
            if (_bodyTransform == null || playerBody == null)
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
            _bodyTransform.rotation = Quaternion.LookRotation(aim, playerBody.up) *
                                      Quaternion.Euler(90f, 0f, 0f);

            _bodyTransform.position = _anchor.position +
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
