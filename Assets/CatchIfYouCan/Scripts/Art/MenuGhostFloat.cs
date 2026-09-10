using UnityEngine;

namespace CatchIfYouCan.Art
{
    /// <summary>
    /// Keeps the menu ghost barely suspended in the air.
    ///
    /// <para>
    /// This is ambient presentation, not a horror event: it runs the whole time the menu is up
    /// and nothing switches it on or off. The target is a couple of centimetres of slow vertical
    /// drift — enough that the figure never reads as a static prop, not so much that it bobs.
    /// </para>
    ///
    /// <para>
    /// The rest position is captured once and every frame is written as
    /// <c>base + offset</c>, never <c>current + offset</c>. Accumulating onto the current
    /// transform is how a hover like this slowly walks the character out of frame over a long
    /// session; sampling an absolute sine against a stored baseline cannot drift no matter how
    /// long the menu is left running.
    /// </para>
    ///
    /// <para>
    /// Vertical, lateral and roll each run at their own frequency, deliberately not multiples of
    /// one another, so the motion never settles into an obvious repeating loop.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Catch If You Can/Menu Ghost Float")]
    public sealed class MenuGhostFloat : MonoBehaviour
    {
        [Header("Ghost Ambient Float")]
        [SerializeField] private bool enableGhostFloat = true;

        [Tooltip("The ghost's transform. Falls back to this GameObject's own transform.")]
        [SerializeField] private Transform ghostTransform;

        [Tooltip("Peak vertical movement in metres. 0.025 is about two and a half centimetres.")]
        [SerializeField, Range(0f, 0.2f)] private float ghostFloatAmplitude = 0.025f;

        [Tooltip("Cycles per second. Slow: this should be barely perceptible.")]
        [SerializeField, Range(0.01f, 2f)] private float ghostFloatFrequency = 0.35f;

        [Tooltip("Peak lateral drift in metres. Smaller than the vertical.")]
        [SerializeField, Range(0f, 0.1f)] private float ghostFloatHorizontalAmplitude = 0.008f;

        [Tooltip("Peak roll in degrees. A fraction of a degree is enough.")]
        [SerializeField, Range(0f, 5f)] private float ghostFloatRotationAmount = 0.25f;

        private Transform _target;
        private Vector3 _baseGhostLocalPosition;
        private Quaternion _baseGhostLocalRotation;

        // Set by a horror event that needs the ghost somewhere else for a while; zero means
        // "stand where you were authored". It is assigned, never accumulated, so an event that
        // is interrupted cannot leave the ghost permanently displaced, and running the same
        // event any number of times always lands back on the same base position.
        private Vector3 _eventOffset;
        private float _phaseY;
        private float _phaseX;
        private float _phaseRoll;
        private bool _ready;

        /// <summary>The transform this component drives. Null until Awake has run.</summary>
        public Transform GhostTransform => _target;

        /// <summary>
        /// Where the ghost stands with no event running, in world space. Events measure from
        /// this rather than from the live transform, which is always a centimetre or two off
        /// while the float is running.
        /// </summary>
        public Vector3 BaseWorldPosition
        {
            get
            {
                if (_target == null)
                    return Vector3.zero;
                var parent = _target.parent;
                return parent != null
                    ? parent.TransformPoint(_baseGhostLocalPosition)
                    : _baseGhostLocalPosition;
            }
        }

        /// <summary>
        /// Displaces the ghost by a world-space offset until it is cleared. The float keeps
        /// running on top of it, so the ghost still breathes while it is standing somewhere
        /// else — the two are added, never fought over.
        ///
        /// <para>
        /// This component stays the single writer of the ghost's transform. An event that moved
        /// the transform itself would be overwritten by the next float frame, and the two would
        /// take turns clobbering each other.
        /// </para>
        /// </summary>
        public void SetEventWorldOffset(Vector3 worldOffset)
        {
            if (_target == null)
                return;

            // Converted once, here, because this component is the one that knows which space
            // the transform is written in.
            var parent = _target.parent;
            _eventOffset = parent != null
                ? parent.InverseTransformVector(worldOffset)
                : worldOffset;
        }

        /// <summary>Returns the ghost to its authored position. Safe to call when idle.</summary>
        public void ClearEventOffset() => _eventOffset = Vector3.zero;

        private void Awake()
        {
            _target = ghostTransform != null ? ghostTransform : transform;

            _baseGhostLocalPosition = _target.localPosition;
            _baseGhostLocalRotation = _target.localRotation;

            // Random phases so the ghost is not caught at the same point of its cycle every
            // time the menu loads.
            _phaseY = Random.value * Mathf.PI * 2f;
            _phaseX = Random.value * Mathf.PI * 2f;
            _phaseRoll = Random.value * Mathf.PI * 2f;

            _ready = true;
        }

        private void OnDisable()
        {
            // Leave the ghost exactly where it was authored rather than frozen mid-drift, and
            // drop any event displacement: a component disabled part way through Ghost Closer
            // must not leave the ghost standing closer to the camera than it was authored.
            ClearEventOffset();
            if (_ready && _target != null)
            {
                _target.localPosition = _baseGhostLocalPosition;
                _target.localRotation = _baseGhostLocalRotation;
            }
        }

        private void Update()
        {
            if (!_ready || _target == null)
                return;

            // Nothing to do at all: keeps the original early-out when the float is switched off
            // and no event is displacing the ghost.
            if (!enableGhostFloat && _eventOffset == Vector3.zero)
                return;

            float t = Time.time;

            // Absolute offsets from the stored baseline: no accumulation, so no drift.
            float y = Mathf.Sin(_phaseY + t * ghostFloatFrequency * Mathf.PI * 2f)
                      * ghostFloatAmplitude;

            // 0.61 and 0.37 are deliberately not simple ratios of 1, so the three channels
            // drift in and out of phase instead of tracing the same path every cycle.
            float x = Mathf.Sin(_phaseX + t * ghostFloatFrequency * 0.61f * Mathf.PI * 2f)
                      * ghostFloatHorizontalAmplitude;
            float roll = Mathf.Sin(_phaseRoll + t * ghostFloatFrequency * 0.37f * Mathf.PI * 2f)
                         * ghostFloatRotationAmount;

            // base + event offset + float offset. The event moves the ghost; the float keeps
            // breathing on top of wherever the event put it.
            if (!enableGhostFloat)
            {
                x = 0f;
                y = 0f;
                roll = 0f;
            }

            _target.localPosition = _baseGhostLocalPosition + _eventOffset + new Vector3(x, y, 0f);
            _target.localRotation = _baseGhostLocalRotation * Quaternion.Euler(0f, 0f, roll);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Re-seat the baseline when the reference is swapped in the Inspector at runtime.
            if (Application.isPlaying && _ready && ghostTransform != null && ghostTransform != _target)
            {
                _target = ghostTransform;
                _baseGhostLocalPosition = _target.localPosition;
                _baseGhostLocalRotation = _target.localRotation;
            }
        }
#endif
    }
}
