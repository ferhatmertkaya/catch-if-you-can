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
    /// bulb, not as a flame, because consecutive frames are uncorrelated.
    /// </para>
    ///
    /// <para>
    /// <b>Intensity is the only thing this component writes.</b> Range, colour, position and
    /// the enabled flag all stay exactly as authored in the scene. An earlier version fitted
    /// the range to the spread of the flames at Awake; because the holder is a child of a
    /// prefab instance scaled 2.6, the flames are about 1.6 cm apart in world space, so the
    /// fit replaced an authored range of 0.5 with 0.078 and the candle went dark the moment
    /// Play began. The scene is the source of truth for range.
    /// </para>
    ///
    /// <para>
    /// The baseline is captured once in Awake and every frame multiplies <em>that</em>, never
    /// the current value, so the intensity cannot drift or decay over time.
    /// </para>
    ///
    /// <para>
    /// Frame cost is two <c>Mathf.PerlinNoise</c> calls and one property write. No
    /// allocations, no lookups in Update.
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
        [SerializeField, Range(0f, 0.2f)] private float slowAmount = 0.06f;
        [SerializeField, Range(0.05f, 4f)] private float slowSpeed = 0.7f;

        [Header("Fast tremor")]
        [Tooltip("Peak deviation of the fast layer. Keep well below the slow layer.")]
        [SerializeField, Range(0f, 0.1f)] private float fastAmount = 0.025f;
        [SerializeField, Range(1f, 12f)] private float fastSpeed = 3.6f;

        [Header("Bounds (fractions of the authored intensity)")]
        [Tooltip("The candle must stay visibly lit at all times.")]
        [SerializeField, Range(0.5f, 1f)] private float minimumFactor = 0.88f;
        [SerializeField, Range(1f, 1.5f)] private float maximumFactor = 1.08f;

        [Tooltip("Change per candle so several flames do not flicker in sync.")]
        [SerializeField] private int seed;

        private float _authoredIntensity;
        private float _slowOffset;
        private float _fastOffset;
        private bool _ready;

        // Set by a horror event while one is running; 1 means "behave normally". These are
        // assigned, never accumulated, so an event that is interrupted cannot leave the
        // candle permanently dimmed.
        private float _eventIntensityScale = 1f;
        private float _eventTurbulenceScale = 1f;

        /// <summary>
        /// Current intensity as a fraction of the authored value. Exposed so flame visuals can
        /// borrow the same curve instead of running a second, unrelated noise source.
        /// </summary>
        public float CurrentFactor { get; private set; } = 1f;

        /// <summary>The authored intensity this component modulates around.</summary>
        public float AuthoredIntensity => _authoredIntensity;

        /// <summary>
        /// Lets a horror event disturb the candle without a second script writing
        /// Light.intensity behind this one's back. Two writers on one property is how the
        /// last regression happened, so events go through here instead.
        /// <paramref name="intensityScale"/> multiplies the result; <paramref name="turbulenceScale"/>
        /// widens the flicker envelope. Both are set, never accumulated.
        /// </summary>
        public void ApplyEventModulation(float intensityScale, float turbulenceScale)
        {
            _eventIntensityScale = Mathf.Clamp(intensityScale, 0.05f, 2f);
            _eventTurbulenceScale = Mathf.Clamp(turbulenceScale, 0f, 6f);
        }

        /// <summary>Returns the candle to its authored behaviour.</summary>
        public void ClearEventModulation()
        {
            _eventIntensityScale = 1f;
            _eventTurbulenceScale = 1f;
        }

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
            ClearEventModulation();
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
                           + (slow - 0.5f) * 2f * slowAmount * _eventTurbulenceScale
                           + (fast - 0.5f) * 2f * fastAmount * _eventTurbulenceScale;

            // The normal envelope is deliberately narrow. A horror event needs room to swing,
            // so the bounds open up in step with the turbulence it asked for; at turbulence 1
            // the clamp is exactly the authored one.
            float minF = minimumFactor;
            float maxF = maximumFactor;
            if (_eventTurbulenceScale > 1f)
            {
                float k = Mathf.InverseLerp(1f, 4f, _eventTurbulenceScale);
                minF = Mathf.Lerp(minimumFactor, 0.35f, k);
                maxF = Mathf.Lerp(maximumFactor, 1.35f, k);
            }

            factor = Mathf.Clamp(factor, minF, maxF);

            CurrentFactor = factor;

            // Always the authored baseline times the factor, never the running value, so the
            // intensity cannot compound downwards frame after frame. The event scale is a
            // separate multiplier for the same reason.
            targetLight.intensity = _authoredIntensity * factor * _eventIntensityScale;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (maximumFactor < minimumFactor)
                maximumFactor = minimumFactor;

            if (Application.isPlaying && _ready)
                ApplySeed();
        }
#endif
    }
}
