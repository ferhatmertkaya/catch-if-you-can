using CatchIfYouCan.Core;
using UnityEngine;

namespace CatchIfYouCan.Interaction
{
    /// <summary>
    /// A door you open by pushing it, not by asking it to open.
    ///
    /// <para>
    /// <b>What this is not.</b> <see cref="InteractiveDoor"/> plays a canned swing: press once
    /// and the leaf travels to a fixed angle on its own clock. That reads as a cutscene in a
    /// game whose whole tension is in how far you dare to open something. This one has no
    /// animation at all - the leaf goes exactly where the hand puts it and stays there.
    /// </para>
    ///
    /// <para>
    /// <b>How it is driven, and why that way.</b> Hold the pointer on the leaf and the leaf
    /// follows where you look: the aim ray is intersected with the horizontal plane through the
    /// grab point, and the hinge turns so the grabbed spot stays under the aim. Following the
    /// AIM rather than raw pointer pixels is deliberate - suppressing the look while a door is
    /// held would need a second thing that suspends player input, and in this project exactly one
    /// thing is allowed to do that (<c>MenuInputGate</c>, for menus). So the camera keeps
    /// turning, and the door comes with it. The cost is honest and small: turning while dragging
    /// moves both. The gain is that no input code is touched, mobile included.
    /// </para>
    ///
    /// <para>
    /// <b>The hinge is a hinge.</b> The leaf turns about its hinge edge, never its centre, and
    /// the angle is CLAMPED to [0, <see cref="maxAngle"/>] on the side the hinge allows. Clamped
    /// rather than accumulated: a Euler angle that is added to every frame drifts past its limit
    /// on one long drag and the leaf ends up inside the wall, which is a hole you can walk
    /// through in an object built to stop you.
    /// </para>
    ///
    /// <para>
    /// <b>Released is where you left it.</b> No spring, no auto-close, no snap - except within
    /// <see cref="settleAngle"/> of either limit, where it settles onto the limit so a door that
    /// is a degree from shut is shut.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Catch If You Can/Draggable Door")]
    public sealed class DraggableDoor : MonoBehaviour
    {
        private const string LogTag = "[CIYC][Door] ";

        [Header("Hinge")]
        [Tooltip("The transform that turns. Left empty, this object turns. Its LOCAL Y axis is " +
                 "the hinge, and its origin must sit on the hinge edge - a pivot in the middle " +
                 "of the leaf swings the door through the frame.")]
        [SerializeField] private Transform hinge;

        [Tooltip("Which way the door is allowed to swing. +1 opens towards the hinge's local " +
                 "+Y rotation, -1 the other way.")]
        [SerializeField] private int openSign = 1;

        [Tooltip("How far it opens. A real interior door stops on its own frame or a wall well " +
                 "before 180 degrees.")]
        [SerializeField, Range(20f, 120f)] private float maxAngle = 85f;

        [Tooltip("Within this many degrees of shut or fully open, the leaf settles onto the " +
                 "limit when released. Bigger than this and it stays exactly where it is.")]
        [SerializeField, Range(0f, 15f)] private float settleAngle = 3f;

        [Header("Grabbing")]
        [Tooltip("How far the player can be and still take hold of the leaf.")]
        [SerializeField, Min(0.2f)] private float grabDistance = 2.2f;

        [Tooltip("Degrees per second the leaf is allowed to travel. A cap, not a speed: it " +
                 "stops a flick of the mouse from teleporting the door through the player.")]
        [SerializeField, Min(30f)] private float maxDegreesPerSecond = 420f;

        [Header("Camera")]
        [Tooltip("Left empty, the camera that can see the player is found once at Start.")]
        [SerializeField] private Camera aimCamera;

        private Quaternion _restLocal;
        private float _angle;
        private bool _dragging;
        private float _grabOffset;
        private float _grabHeight;
        private Collider[] _leafColliders;

        /// <summary>How far open the leaf is, in degrees. Zero is shut.</summary>
        public float OpenAngle => _angle;

        /// <summary>True while somebody has hold of it.</summary>
        public bool IsHeld => _dragging;

        private void Awake()
        {
            if (hinge == null)
                hinge = transform;

            _restLocal = hinge.localRotation;
            _leafColliders = hinge.GetComponentsInChildren<Collider>(true);

            if (openSign == 0)
                openSign = 1;
            openSign = openSign > 0 ? 1 : -1;
        }

        private void Start()
        {
            if (aimCamera == null)
                aimCamera = Camera.main;

            if (_leafColliders == null || _leafColliders.Length == 0)
            {
                CIYCLog.Warn(LogTag + "'" + name + "' has no collider, so there is nothing to " +
                             "take hold of and nothing to stop the player walking through it. " +
                             "A door leaf needs a collider before it needs a drag.");
            }
        }

        private void Update()
        {
            if (aimCamera == null)
            {
                aimCamera = Camera.main;
                if (aimCamera == null)
                    return;
            }

            bool down = PointerDown(out Vector3 screenPosition);

            if (!down)
            {
                if (_dragging)
                    Release();
                return;
            }

            if (!_dragging)
            {
                if (TryGrab(screenPosition))
                    _dragging = true;
                return;
            }

            Drag(screenPosition);
        }

        // ---- input ------------------------------------------------------------------------------

        /// <summary>
        /// Whether a finger or the left mouse button is down, and where.
        ///
        /// <para>
        /// Read here rather than through the shared input controller on purpose: that controller
        /// owns movement, look, the joystick and the HUD buttons, and a door has no business
        /// adding a field to it. This asks the platform two questions and answers them for this
        /// one door.
        /// </para>
        /// </summary>
        private static bool PointerDown(out Vector3 screenPosition)
        {
            if (UnityEngine.Input.touchCount > 0)
            {
                Touch touch = UnityEngine.Input.GetTouch(0);
                screenPosition = new Vector3(touch.position.x, touch.position.y, 0f);
                return touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled;
            }

            screenPosition = UnityEngine.Input.mousePosition;
            return UnityEngine.Input.GetMouseButton(0);
        }

        // ---- the drag ---------------------------------------------------------------------------

        /// <summary>
        /// Takes hold, if the pointer is actually on this leaf and within reach.
        ///
        /// <para>
        /// The grab is recorded as an ANGLE OFFSET rather than as a point: the difference between
        /// where the aim is and where the leaf is, at the moment of grabbing. Every frame after
        /// that subtracts the same offset, so the door does not jump to meet the cursor on the
        /// first frame - it starts exactly where it was.
        /// </para>
        /// </summary>
        private bool TryGrab(Vector3 screenPosition)
        {
            Ray ray = aimCamera.ScreenPointToRay(screenPosition);

            if (!Physics.Raycast(ray, out RaycastHit hit, grabDistance, ~0,
                                 QueryTriggerInteraction.Ignore))
                return false;

            if (!IsMine(hit.collider))
                return false;

            _grabHeight = hit.point.y;

            if (!TryAimAngle(ray, out float aim))
                return false;

            _grabOffset = aim - _angle * openSign;
            return true;
        }

        private bool IsMine(Collider collider)
        {
            if (collider == null || _leafColliders == null)
                return false;

            for (int i = 0; i < _leafColliders.Length; i++)
            {
                if (_leafColliders[i] == collider)
                    return true;
            }

            return false;
        }

        private void Drag(Vector3 screenPosition)
        {
            Ray ray = aimCamera.ScreenPointToRay(screenPosition);

            if (!TryAimAngle(ray, out float aim))
                return;

            float wanted = Mathf.Clamp((aim - _grabOffset) * openSign, 0f, maxAngle);

            // Rate-limited. Without it a single frame in which the aim crosses the hinge sends
            // the leaf the whole way in one step, and a collider that moves 85 degrees between
            // two physics frames sweeps through whatever was standing next to it.
            float step = maxDegreesPerSecond * Time.deltaTime;
            _angle = Mathf.MoveTowards(_angle, wanted, step);

            Apply();
        }

        private void Release()
        {
            _dragging = false;

            // Settle onto a limit only when it is already all but there. Anywhere else the door
            // stays exactly where the hand left it, which is the whole point of dragging one.
            if (_angle <= settleAngle)
                _angle = 0f;
            else if (_angle >= maxAngle - settleAngle)
                _angle = maxAngle;

            Apply();

            // Tell the swinging door where its leaf ended up. It only writes the hinge while it
            // is animating, so it left this drag alone - and then still believed the angle it
            // last computed. Unsynced, the next press of the interact key snaps the leaf from
            // that stale angle to the target, which is a jump with no cause on screen.
            var swing = GetComponent<InteractiveDoor>();
            if (swing != null)
                swing.SyncToAngle(_angle * openSign);
        }

        private void Apply()
        {
            // Composed from the REST rotation every frame rather than added to the current one.
            // Accumulating Euler angles drifts, and a drifting door leaves its frame.
            hinge.localRotation = _restLocal * Quaternion.AngleAxis(_angle * openSign, Vector3.up);
        }

        /// <summary>
        /// Where the aim ray crosses the leaf's swing plane, as an angle about the hinge.
        ///
        /// <para>
        /// The swing plane is horizontal at the height the leaf was grabbed, because a door turns
        /// about a vertical axis and its horizontal travel is the only part that matters. An aim
        /// that is parallel to that plane - looking straight along the floor - has no crossing,
        /// and the door simply does not move that frame rather than snapping somewhere.
        /// </para>
        /// </summary>
        private bool TryAimAngle(Ray ray, out float degrees)
        {
            degrees = 0f;

            var plane = new Plane(Vector3.up, new Vector3(0f, _grabHeight, 0f));
            if (!plane.Raycast(ray, out float enter))
                return false;

            // ray.origin + direction * enter rather than Ray.GetPoint: the same point,
            // and it does not depend on a helper the offline typecheck harness lacks.
            Vector3 point = ray.origin + ray.direction * enter;
            Vector3 local = hinge.parent != null
                ? hinge.parent.InverseTransformPoint(point)
                : point;
            Vector3 pivot = hinge.localPosition;

            var flat = new Vector2(local.x - pivot.x, local.z - pivot.z);
            if (flat.sqrMagnitude < 0.0004f)
                return false;

            degrees = Mathf.Atan2(flat.x, flat.y) * Mathf.Rad2Deg;
            return true;
        }

        /// <summary>
        /// Sets the hinge and the swing direction from code, for a door built at runtime.
        ///
        /// <para>
        /// A public method rather than reflection into these fields: reflection compiles, reviews
        /// clean and fails silently on the next rename (CLAUDE.md mistake 4).
        /// </para>
        /// </summary>
        public void Configure(Transform hingeTransform, int swingSign, float limitDegrees)
        {
            hinge = hingeTransform != null ? hingeTransform : transform;
            openSign = swingSign >= 0 ? 1 : -1;
            maxAngle = Mathf.Clamp(limitDegrees, 20f, 120f);

            _restLocal = hinge.localRotation;
            _leafColliders = hinge.GetComponentsInChildren<Collider>(true);
            _angle = 0f;
            Apply();
        }
    }
}
