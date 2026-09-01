using UnityEngine;

namespace CatchIfYouCan.Art
{
    /// <summary>
    /// Gives one candle flame its own quiet life: how it moves, how bright it burns and what
    /// colour it is.
    ///
    /// <para>
    /// The flame sprite is a complete flame, so it is rendered as a single long-lived particle
    /// rather than a plume. That keeps it welded to the wick, but it also means the particle
    /// system itself can no longer animate it: size- and rotation-over-lifetime map to a
    /// lifetime that never elapses. So the motion is driven here instead, on the transform.
    /// </para>
    ///
    /// <para>
    /// The same fact is why brightness and colour are driven here too, through a
    /// <see cref="MaterialPropertyBlock"/>, rather than through the particle system's start
    /// colour. The particle is emitted once, at prewarm, with a lifetime of an hour and a cap of
    /// one particle; <c>main.startColor</c> only ever applies to a particle that has yet to be
    /// emitted, so writing it after the fact changes nothing that is on screen. A property block
    /// is read at draw time, so it reaches the flame that is actually burning — and it does it
    /// without instancing the material, so all three flames still share one asset and one draw
    /// setup.
    /// </para>
    ///
    /// <para>
    /// Only scale and roll are written on the transform. Position is never touched, which is what
    /// keeps the base of the flame on the wick — the failure mode this is built to avoid is a
    /// flame that drifts, climbs or visibly re-spawns somewhere else. Two Perlin layers per
    /// channel give an irregular flutter that never repeats on a short cycle, and a per-flame
    /// <see cref="seed"/> keeps three candles on the same holder from breathing in unison.
    /// Brightness runs on its own third offset and its own speed, so a flame is not at its
    /// brightest exactly when it is at its tallest.
    /// </para>
    ///
    /// <para>
    /// <b>This component is the only writer of the flame's colour.</b> Horror events do not write
    /// the renderer or the particle system; they call
    /// <see cref="ApplyEventModulation"/> and <see cref="ApplyEventColour"/>, exactly as they
    /// already call <see cref="CandleFlicker"/> for the light rather than writing
    /// <c>Light.intensity</c>. Two writers on one property is how the last regression happened.
    /// </para>
    ///
    /// <para>
    /// Six <c>Mathf.PerlinNoise</c> calls, one scale write, one rotation write and one property
    /// block write per frame. The block is allocated once in <c>Awake</c>. No allocations and no
    /// lookups in Update, and the shared material is never instanced.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Catch If You Can/Candle Flame Flicker")]
    public sealed class CandleFlameFlicker : MonoBehaviour
    {
        [Header("Breathing (scale)")]
        [Tooltip("Peak horizontal scale deviation. A flame widens far less than it stretches.")]
        [SerializeField, Range(0f, 0.15f)] private float scaleAmountX = 0.03f;

        [Tooltip("Peak vertical scale deviation.")]
        [SerializeField, Range(0f, 0.2f)] private float scaleAmountY = 0.05f;

        [SerializeField, Range(0.1f, 6f)] private float scaleSpeed = 1.35f;

        [Header("Roll (degrees about local Z)")]
        [Tooltip("Peak roll. Keep small: a candle flame leans, it does not spin.")]
        [SerializeField, Range(0f, 6f)] private float rollDegrees = 2f;
        [SerializeField, Range(0.1f, 6f)] private float rollSpeed = 0.95f;

        [Header("Brightness")]
        [Tooltip("Peak brightness deviation, as a fraction of the material's authored colour. " +
                 "Runs on its own noise offset and speed so a flame is not brightest exactly " +
                 "when it is tallest.")]
        [SerializeField, Range(0f, 0.5f)] private float brightnessAmount = 0.16f;

        [SerializeField, Range(0.1f, 8f)] private float brightnessSpeed = 2.4f;

        [Tooltip("The flame must never go out, however far the noise swings.")]
        [SerializeField, Range(0.2f, 1f)] private float minimumBrightness = 0.72f;
        [SerializeField, Range(1f, 1.6f)] private float maximumBrightness = 1.2f;

        [Header("Identity")]
        [Tooltip("Change per flame so the candles on one holder never move together.")]
        [SerializeField] private int seed;

        [Header("References")]
        [Tooltip("The flame's renderer. Falls back to a Renderer on this GameObject.")]
        [SerializeField] private Renderer flameRenderer;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private Vector3 _baseScale;
        private Quaternion _baseRotation;
        private float _scaleOffsetX;
        private float _scaleOffsetY;
        private float _rollOffset;
        private float _brightnessOffset;
        private float _scaleSpeed2;
        private float _rollSpeed2;
        private float _brightnessSpeed2;

        private MaterialPropertyBlock _block;
        private Color _authoredColor = Color.white;
        private bool _hasRenderer;

        // Set by a horror event while one is running; the defaults mean "behave normally". These
        // are assigned, never accumulated, so an event that is interrupted cannot leave the flame
        // permanently dimmed or permanently red.
        private float _eventBrightnessScale = 1f;
        private float _eventTurbulenceScale = 1f;
        private Color _eventColor = Color.white;
        private float _eventColorBlend;

        /// <summary>Current brightness as a fraction of the material's authored colour.</summary>
        public float CurrentBrightness { get; private set; } = 1f;

        /// <summary>
        /// Lets a horror event dim the flame and widen its flutter without writing the renderer
        /// behind this component's back. <paramref name="brightnessScale"/> multiplies the result;
        /// <paramref name="turbulenceScale"/> widens the envelope. Both are set, never accumulated.
        /// Mirrors <see cref="CandleFlicker.ApplyEventModulation"/> so the two read the same way
        /// from an event's point of view.
        /// </summary>
        public void ApplyEventModulation(float brightnessScale, float turbulenceScale)
        {
            _eventBrightnessScale = Mathf.Clamp(brightnessScale, 0.05f, 2f);
            _eventTurbulenceScale = Mathf.Clamp(turbulenceScale, 0f, 6f);
        }

        /// <summary>
        /// Bleeds the flame toward an event colour. The colour wins; the flicker keeps running
        /// underneath it, so a red flame still gutters rather than sitting flat.
        /// </summary>
        public void ApplyEventColour(Color colour, float blend)
        {
            _eventColor = colour;
            _eventColorBlend = Mathf.Clamp01(blend);
        }

        /// <summary>Returns the flame to its authored behaviour.</summary>
        public void ClearEventModulation()
        {
            _eventBrightnessScale = 1f;
            _eventTurbulenceScale = 1f;
            _eventColorBlend = 0f;
        }

        private void Awake()
        {
            _baseScale = transform.localScale;
            _baseRotation = transform.localRotation;

            if (flameRenderer == null)
                flameRenderer = GetComponent<Renderer>();

            _hasRenderer = flameRenderer != null;
            if (_hasRenderer)
            {
                _block = new MaterialPropertyBlock();

                // sharedMaterial, never material: reading the instance property would clone the
                // asset per flame and undo the point of using a property block at all.
                var shared = flameRenderer.sharedMaterial;
                if (shared != null)
                {
                    if (shared.HasProperty(BaseColorId))
                        _authoredColor = shared.GetColor(BaseColorId);
                    else if (shared.HasProperty(ColorId))
                        _authoredColor = shared.GetColor(ColorId);
                }
            }

            ApplySeed();
        }

        private void OnDisable()
        {
            // Leave the flame exactly as it was authored rather than frozen mid-flutter.
            transform.localScale = _baseScale;
            transform.localRotation = _baseRotation;

            ClearEventModulation();

            if (_hasRenderer)
            {
                _block.Clear();
                flameRenderer.SetPropertyBlock(_block);
            }
        }

        private void ApplySeed()
        {
            var rng = new System.Random(seed);
            _scaleOffsetX = (float)rng.NextDouble() * 96f + 0.211f;
            _scaleOffsetY = (float)rng.NextDouble() * 96f + 0.443f;
            _rollOffset = (float)rng.NextDouble() * 96f + 0.607f;
            _brightnessOffset = (float)rng.NextDouble() * 96f + 0.829f;

            // Second layer runs at an irrational-ish multiple of the first so the pair never
            // lines up into an obvious repeating beat.
            _scaleSpeed2 = scaleSpeed * 2.37f;
            _rollSpeed2 = rollSpeed * 3.11f;
            _brightnessSpeed2 = brightnessSpeed * 2.73f;
        }

        private void Update()
        {
            float t = Time.time;

            float sx = Mathf.PerlinNoise(_scaleOffsetX + t * scaleSpeed, 0.5f) - 0.5f
                       + (Mathf.PerlinNoise(_scaleOffsetX + t * _scaleSpeed2, 2.5f) - 0.5f) * 0.4f;
            float sy = Mathf.PerlinNoise(_scaleOffsetY + t * scaleSpeed, 1.5f) - 0.5f
                       + (Mathf.PerlinNoise(_scaleOffsetY + t * _scaleSpeed2, 3.5f) - 0.5f) * 0.4f;
            float r = Mathf.PerlinNoise(0.5f, _rollOffset + t * rollSpeed) - 0.5f
                      + (Mathf.PerlinNoise(2.5f, _rollOffset + t * _rollSpeed2) - 0.5f) * 0.35f;

            // Each pair of layers sums to at most +-0.7, so normalise back to +-1.
            const float ScaleNorm = 2f / 1.4f;
            const float RollNorm = 2f / 1.35f;

            // Position is never written: that is what keeps the base welded to the wick.
            transform.localScale = new Vector3(
                _baseScale.x * (1f + sx * ScaleNorm * scaleAmountX),
                _baseScale.y * (1f + sy * ScaleNorm * scaleAmountY),
                _baseScale.z);
            transform.localRotation = _baseRotation *
                                      Quaternion.Euler(0f, 0f, r * RollNorm * rollDegrees);

            if (_hasRenderer)
                UpdateBrightness(t);
        }

        private void UpdateBrightness(float t)
        {
            float b = Mathf.PerlinNoise(3.5f, _brightnessOffset + t * brightnessSpeed) - 0.5f
                      + (Mathf.PerlinNoise(4.5f, _brightnessOffset + t * _brightnessSpeed2) - 0.5f) * 0.4f;

            const float BrightnessNorm = 2f / 1.4f;

            float factor = 1f + b * BrightnessNorm * brightnessAmount * _eventTurbulenceScale;

            // The normal envelope is deliberately narrow. An event needs room to swing, so the
            // bounds open up in step with the turbulence it asked for; at turbulence 1 the clamp
            // is exactly the authored one. Same rule as CandleFlicker, so the light and the flame
            // it belongs to widen together.
            float minB = minimumBrightness;
            float maxB = maximumBrightness;
            if (_eventTurbulenceScale > 1f)
            {
                float k = Mathf.InverseLerp(1f, 4f, _eventTurbulenceScale);
                minB = Mathf.Lerp(minimumBrightness, 0.3f, k);
                maxB = Mathf.Lerp(maximumBrightness, 1.35f, k);
            }

            factor = Mathf.Clamp(factor, minB, maxB);
            CurrentBrightness = factor;

            // The event colour wins; the flicker rides underneath it. Always the authored colour
            // times the factor, never the running value, so brightness cannot compound downwards.
            Color colour = _eventColorBlend > 0f
                ? Color.Lerp(_authoredColor, _eventColor, _eventColorBlend)
                : _authoredColor;

            colour *= factor * _eventBrightnessScale;
            colour.a = _authoredColor.a;

            _block.SetColor(BaseColorId, colour);
            _block.SetColor(ColorId, colour);
            _block.SetColor(EmissionColorId, colour);
            flameRenderer.SetPropertyBlock(_block);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (maximumBrightness < minimumBrightness)
                maximumBrightness = minimumBrightness;

            if (Application.isPlaying)
                ApplySeed();
        }
#endif
    }
}
