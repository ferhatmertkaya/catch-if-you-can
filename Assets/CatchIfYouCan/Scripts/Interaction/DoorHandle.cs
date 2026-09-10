using CatchIfYouCan.Input;
using UnityEngine;

namespace CatchIfYouCan.Interaction
{
    /// <summary>
    /// A door lever you can work even though the door will not open.
    ///
    /// <para>
    /// The door is locked and stays locked. What the player gets is the handle: press and the
    /// lever swings down, let go and it springs back, and the door does not move. That is not a
    /// placeholder for opening the door - it is the thing that tells the player the door is real
    /// and that it is locked, which a door that ignores you entirely never manages.
    /// </para>
    ///
    /// <para>
    /// The lever is built here rather than modelled, and it takes the door's own material, so it
    /// is the same wood as the door instead of the chrome that comes with most door assets.
    /// </para>
    ///
    /// <para>
    /// It reads the held state rather than acting on the interact event, because "down while you
    /// hold it" is not something a one-shot <see cref="IInteractable.Interact"/> can express. It
    /// is down while it is the interaction target and the interact input is held - which the HUD
    /// button and the E key both drive - and springs back the instant either stops being true.
    /// Nothing new was added to the input layer for this.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Catch If You Can/Door Handle")]
    public sealed class DoorHandle : MonoBehaviour, IInteractable
    {
        [Header("Interaction")]
        [SerializeField] private string lockedPrompt = "Locked";
        [SerializeField] private float interactDistance = 2.2f;

        [Tooltip("Unlocking is not implemented yet - the door is meant to stay shut for now - " +
                 "but the flag is here so the day it opens is a state change rather than a " +
                 "rewrite.")]
        [SerializeField] private bool locked = true;

        [Tooltip("Build the lever. Off for a door whose own model already has a handle on it: " +
                 "the interaction, the prompt and the locked door all still work, there is simply " +
                 "no second handle drawn on top of the first.")]
        [SerializeField] private bool buildLever = true;

        [Header("Placement")]
        [Tooltip("Where the handle sits on the door, in the door's own local space. The default " +
                 "is the room-facing side, latch edge, at handle height.")]
        [SerializeField] private Vector3 handleLocalPosition = new Vector3(-0.36f, -0.06f, -0.62f);

        [Tooltip("Which way out of the door the handle sticks, in the door's local space.")]
        [SerializeField] private Vector3 handleLocalNormal = new Vector3(0f, 0f, -1f);

        [Tooltip("Which way the lever arm points when it is up, in the door's local space.")]
        [SerializeField] private Vector3 leverLocalDirection = new Vector3(1f, 0f, 0f);

        [Header("Shape, in metres")]
        [SerializeField] private float rosetteRadius = 0.032f;
        [SerializeField] private float rosetteDepth = 0.014f;
        [SerializeField] private float spindleLength = 0.05f;
        [SerializeField] private float leverLength = 0.115f;
        [SerializeField] private float leverThickness = 0.019f;

        [Header("Motion")]
        [Tooltip("How far the lever swings when pressed. A real lever travels about this far " +
                 "before the latch clears.")]
        [SerializeField] private float pressedDegrees = 32f;

        [Tooltip("Seconds to press. Short - a handle answers immediately.")]
        [SerializeField, Min(0.01f)] private float pressTime = 0.08f;

        [Tooltip("Seconds to spring back. Slower than the press, which is what a sprung latch " +
                 "does and what makes the release readable.")]
        [SerializeField, Min(0.01f)] private float releaseTime = 0.16f;

        private Transform _lever;
        private Quaternion _leverRest;
        private Vector3 _hingeLocalAxis;
        private float _pressed;
        private InteractionController _interaction;
        private MobileInputController _input;

        /// <summary>How far down the lever is, 0 at rest and 1 fully pressed.</summary>
        public float Pressed => _pressed;

        /// <summary>Whether the door is still locked.</summary>
        public bool Locked
        {
            get => locked;
            set => locked = value;
        }

        // ---- IInteractable -------------------------------------------------------------------

        public string Prompt => lockedPrompt;
        public float HoldDuration => 0f;
        public InteractionType InteractionType => InteractionType.Open;
        public float Distance => interactDistance;

        public bool CanInteract(GameObject interactor) => true;

        public void Interact(GameObject interactor)
        {
            // Nothing to do on the tap itself. The lever follows the held state below, and the
            // door is locked; this exists so the interaction system has something to call and so
            // the prompt appears at all.
        }

        // ---- build ---------------------------------------------------------------------------

        private void Start()
        {
            Build();
        }

        private void Build()
        {
            if (_lever != null || !buildLever)
                return;

            // The door's own material, so the handle is the same wood. Reading it beats authoring
            // a second material that then has to be kept in step with the door's.
            Material material = null;
            var renderer = GetComponent<Renderer>();
            if (renderer != null)
                material = renderer.sharedMaterial;

            Vector3 normal = handleLocalNormal.sqrMagnitude < 0.0001f
                ? Vector3.back : handleLocalNormal.normalized;

            // A pivot on the door face. The lever turns about the door's normal, which is what a
            // spindle through the door does.
            var pivot = new GameObject("DoorHandle_Pivot");
            pivot.transform.SetParent(transform, false);
            pivot.transform.localPosition = handleLocalPosition;
            pivot.transform.localRotation = Quaternion.identity;
            // The door is a stretched cube, so its local scale is not uniform and a handle
            // parented straight to it would be stretched with it. Undo that here, once.
            pivot.transform.localScale = InverseScale(transform.lossyScale);

            AddPart(pivot.transform, PrimitiveType.Cylinder, "Rosette", material,
                    normal * (rosetteDepth * 0.5f),
                    Quaternion.FromToRotation(Vector3.up, normal),
                    new Vector3(rosetteRadius * 2f, rosetteDepth * 0.5f, rosetteRadius * 2f));

            var lever = new GameObject("DoorHandle_Lever");
            _lever = lever.transform;
            _lever.SetParent(pivot.transform, false);
            _lever.localPosition = normal * rosetteDepth;
            _lever.localRotation = Quaternion.identity;
            _leverRest = _lever.localRotation;

            Vector3 arm = leverLocalDirection.sqrMagnitude < 0.0001f
                ? Vector3.right : leverLocalDirection.normalized;

            AddPart(_lever, PrimitiveType.Cylinder, "Spindle", material,
                    normal * (spindleLength * 0.5f),
                    Quaternion.FromToRotation(Vector3.up, normal),
                    new Vector3(leverThickness, spindleLength * 0.5f, leverThickness));

            AddPart(_lever, PrimitiveType.Cube, "Arm", material,
                    normal * spindleLength + arm * (leverLength * 0.5f),
                    Quaternion.LookRotation(arm, normal),
                    new Vector3(leverThickness, leverThickness, leverLength));

            // Turning about the door's normal swings the arm down the face of the door.
            _hingeLocalAxis = normal;
        }

        private static Vector3 InverseScale(Vector3 s) => new Vector3(
            Mathf.Approximately(s.x, 0f) ? 1f : 1f / s.x,
            Mathf.Approximately(s.y, 0f) ? 1f : 1f / s.y,
            Mathf.Approximately(s.z, 0f) ? 1f : 1f / s.z);

        private static void AddPart(Transform parent, PrimitiveType type, string name,
                                    Material material, Vector3 localPosition,
                                    Quaternion localRotation, Vector3 localScale)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = localRotation;
            part.transform.localScale = localScale;

            // The door already has a collider and it is the door the player aims at. Colliders on
            // the handle would only shadow it.
            var collider = part.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            if (material != null)
                part.GetComponent<Renderer>().sharedMaterial = material;
        }

        // ---- per frame -----------------------------------------------------------------------

        private void Update()
        {
            if (_lever == null)
                return;

            // From the local player, not from a scene sweep.
            //
            // This was FindAnyObjectByType, guarded on null - which reads as "resolved once"
            // and is not: the doors exist before the player spawns, so every handle in the
            // house walked every object in the scene, every frame, until somebody arrived. In
            // a scene with no InteractionController at all it never stopped.
            //
            // The controller is added to the player root by PlayerRigBuilder, so this is a
            // lookup among that root's own components rather than the whole house.
            if (_interaction == null)
                _interaction = Core.LocalPlayerService.GetPlayerComponent<InteractionController>();
            if (_input == null)
                _input = MobileInputController.Instance;

            bool held = _input != null &&
                        (_input.InteractHeld || _input.InteractPressed) &&
                        _interaction != null &&
                        ReferenceEquals(_interaction.CurrentTarget, this);

            float target = held ? 1f : 0f;
            float time = held ? pressTime : releaseTime;
            _pressed = Mathf.MoveTowards(_pressed, target, Time.deltaTime / time);

            _lever.localRotation = _leverRest *
                                   Quaternion.AngleAxis(pressedDegrees * _pressed, _hingeLocalAxis);
        }
    }
}
