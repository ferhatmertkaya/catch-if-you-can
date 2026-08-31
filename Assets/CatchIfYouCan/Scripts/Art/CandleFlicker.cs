using UnityEngine;

namespace CatchIfYouCan.Art
{
    /// <summary>
    /// Smooth candle-light flicker for a single warm point light.
    ///
    /// <para>
    /// Two layers of Perlin noise — a slow breathing wander plus a smaller, faster tremor —
    /// scale the intensity the light was authored with. Perlin is used rather than a
    /// per-frame random value on purpose: white noise reads as electrical buzz or a failing
    /// bulb, not as a flame, because consecutive frames are uncorrelated. The light is also
    /// floored well above zero, since a candle that blinks out and returns looks like a bug.
    /// </para>
    ///
    /// <para>
    /// The authored intensity is captured once in Awake and treated as the baseline, so the
    /// value set in the scene stays the artistic source of truth and this component only
    /// modulates it. Give each candle a different <see cref="seed"/> and they will drift
    /// independently rather than pulsing in unison.
    /// </para>
    ///
    /// <para>
    /// Frame cost is two <c>Mathf.PerlinNoise</c> calls and one property write. No
    /// allocations, no lookups in Update, nothing that scales with scene size.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Catch If You Can/Candle Flicker")]
    public sealed class CandleFlicker : MonoBehaviour
    {
        [Tooltip("Light to modulate. Falls back to a Light on this GameObject.")]
        [SerializeField] private Light targetLight;

        [Header("Slow breathing")]
        [Tooltip("Peak deviation of the slow layer, as a fraction of the authored intensity.")]
        [SerializeField, Range(0f, 0.6f)] private float slowAmount = 0.16f;
        [SerializeField, Range(0.05f, 4f)] private float slowSpeed = 0.85f;

        [Header("Fast tremor")]
        [Tooltip("Peak deviation of the fast layer. Keep well below the slow layer.")]
        [SerializeField, Range(0f, 0.3f)] private float fastAmount = 0.06f;
        [SerializeField, Range(1f, 12f)] private float fastSpeed = 4.5f;

        [Header("Safety")]
        [Tooltip("Lowest fraction of the authored intensity. A candle never gutters out.")]
        [SerializeField, Range(0.2f, 1f)] private float minimumFactor = 0.7f;

        [Tooltip("Change per candle so several flames do not flicker in sync.")]
        [SerializeField] private int seed;

        private float _authoredIntensity;
        private float _slowOffset;
        private float _fastOffset;
        private bool _ready;

        /// <summary>
        /// Current intensity as a fraction of the authored value. Exposed so flame visuals can
        /// borrow the same curve instead of running a second, unrelated noise source.
        /// </summary>
        public float CurrentFactor { get; private set; } = 1f;

        private void Awake()
        {
            if (targetLight == null)
                targetLight = GetComponent<Light>();

            if (targetLight == null)
            {
                Debug.LogWarning($"[CIYC] CandleFlicker on '{name}' has no Light assigned; disabling.", this);
                enabled = false;
                return;
            }

            _authoredIntensity = targetLight.intensity;
            ApplySeed();
            _ready = true;
        }

        private void OnEnable()
        {
            // Restore the authored value so toggling the component never leaves the light
            // parked at whatever the last flicker frame happened to be.
            if (_ready && targetLight != null)
                targetLight.intensity = _authoredIntensity;
        }

        private void OnDisable()
        {
            if (_ready && targetLight != null)
                targetLight.intensity = _authoredIntensity;
        }

        private void ApplySeed()
        {
            // Perlin noise returns 0.5 along integer lattice lines, so the offsets have to be
            // fractional or every candle would start from the same value.
            var rng = new System.Random(seed);
            _slowOffset = (float)rng.NextDouble() * 128f + 0.317f;
            _fastOffset = (float)rng.NextDouble() * 128f + 0.713f;
        }

        private void Update()
        {
            if (!_ready)
                return;

            float t = Time.time;

            // Sampling one axis while holding the other fixed gives two independent 1D walks.
            float slow = Mathf.PerlinNoise(_slowOffset + t * slowSpeed, 0.5f);
            float fast = Mathf.PerlinNoise(0.5f, _fastOffset + t * fastSpeed);

            // Perlin is 0..1 centred near 0.5; recentre so the mean sits on the authored value.
            float factor = 1f
                           + (slow - 0.5f) * 2f * slowAmount
                           + (fast - 0.5f) * 2f * fastAmount;

            if (factor < minimumFactor)
                factor = minimumFactor;

            CurrentFactor = factor;
            targetLight.intensity = _authoredIntensity * factor;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying && _ready)
                ApplySeed();
        }
#endif
    }
}
