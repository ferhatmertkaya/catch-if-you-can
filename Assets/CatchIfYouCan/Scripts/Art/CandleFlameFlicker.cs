using UnityEngine;

namespace CatchIfYouCan.Art
{
    /// <summary>
    /// Gives one candle flame its own quiet life.
    ///
    /// <para>
    /// The flame sprite is a complete flame, so it is rendered as a single long-lived particle
    /// rather than a plume. That keeps it welded to the wick, but it also means the particle
    /// system itself can no longer animate it: size- and rotation-over-lifetime map to a
    /// lifetime that never elapses. So the motion is driven here instead, on the transform.
    /// </para>
    ///
    /// <para>
    /// Only scale and roll are touched. Position is never written, which is what keeps the
    /// base of the flame on the wick — the failure mode this is built to avoid is a flame
    /// that drifts, climbs or visibly re-spawns somewhere else. Two Perlin layers per channel
    /// give an irregular flutter that never repeats on a short cycle, and a per-flame
    /// <see cref="seed"/> keeps three candles on the same holder from breathing in unison.
    /// </para>
    ///
    /// <para>
    /// Four <c>Mathf.PerlinNoise</c> calls, one scale write and one rotation write per frame.
    /// No allocations, no lookups in Update.
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

        [Header("Identity")]
        [Tooltip("Change per flame so the candles on one holder never move together.")]
        [SerializeField] private int seed;

        private Vector3 _baseScale;
        private Quaternion _baseRotation;
        private float _scaleOffsetX;
        private float _scaleOffsetY;
        private float _rollOffset;
        private float _scaleSpeed2;
        private float _rollSpeed2;

        private void Awake()
        {
            _baseScale = transform.localScale;
            _baseRotation = transform.localRotation;
            ApplySeed();
        }

        private void OnDisable()
        {
            // Leave the flame exactly as it was authored rather than frozen mid-flutter.
            transform.localScale = _baseScale;
            transform.localRotation = _baseRotation;
        }

        private void ApplySeed()
        {
            var rng = new System.Random(seed);
            _scaleOffsetX = (float)rng.NextDouble() * 96f + 0.211f;
            _scaleOffsetY = (float)rng.NextDouble() * 96f + 0.443f;
            _rollOffset = (float)rng.NextDouble() * 96f + 0.607f;

            // Second layer runs at an irrational-ish multiple of the first so the pair never
            // lines up into an obvious repeating beat.
            _scaleSpeed2 = scaleSpeed * 2.37f;
            _rollSpeed2 = rollSpeed * 3.11f;
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
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying)
                ApplySeed();
        }
#endif
    }
}
