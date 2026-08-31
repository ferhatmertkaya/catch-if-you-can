using System.Collections;
using UnityEngine;

namespace CatchIfYouCan.Art
{
    /// <summary>
    /// The main menu's first supernatural beat: the rotary phone rings, the corridor's green
    /// light falters, red bleeds in, the phone cuts out mid-ring, and everything settles back.
    ///
    /// <para>
    /// This component owns the <em>visuals</em> only. The phone keeps owning its own audio and
    /// its own random schedule; it just calls <see cref="TryBegin"/> when it rings and carries
    /// on regardless of the answer. That keeps the ring working on its own and stops this from
    /// growing into a menu god-object.
    /// </para>
    ///
    /// <para>
    /// Every value it touches is captured once in <c>Awake</c> and driven from that capture:
    /// <c>baseline * factor</c>, never <c>current * factor</c>, so an interrupted event cannot
    /// leave the corridor permanently dim. Restoration also runs from <c>OnDisable</c>, so
    /// disabling the object or unloading the scene mid-event still puts the lights back.
    /// </para>
    ///
    /// <para>
    /// The candle is not written directly. <see cref="CandleFlicker"/> is the single writer of
    /// its light's intensity, and this asks it for a temporary modulation instead — two scripts
    /// writing one property is how the last lighting regression happened.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Catch If You Can/Main Menu Phone Horror Event")]
    public sealed class MainMenuPhoneHorrorEvent : MonoBehaviour, IMainMenuHorrorEvent
    {
        [Header("Scene lights (authored values are captured, never overwritten)")]
        [Tooltip("Existing scene lights dimmed during the event — the green door lights and " +
                 "the corridor's ambient/directional light. Intensity is scaled and restored.")]
        [SerializeField] private Light[] dimmedLights = new Light[0];

        [Tooltip("Red event lights. Start disabled; this component owns them entirely.")]
        [SerializeField] private Light[] redLights = new Light[0];

        [Tooltip("The candle light's flicker component. Modulated through its own API.")]
        [SerializeField] private CandleFlicker candleFlicker;

        [Header("Phone")]
        [Tooltip("The phone's AudioSource. Stopped abruptly at the climax. Optional.")]
        [SerializeField] private AudioSource phoneAudio;

        [Tooltip("Chance that a ring escalates into the full event. The rest stay ordinary rings.")]
        [SerializeField, Range(0f, 1f)] private float eventProbability = 0.35f;

        [Header("Timing (seconds, min..max — rolled per event)")]
        [SerializeField] private Vector2 onsetDelay = new Vector2(0.40f, 0.80f);
        [SerializeField] private Vector2 destabiliseDuration = new Vector2(0.40f, 1.00f);
        [SerializeField] private Vector2 redEmergeDuration = new Vector2(0.35f, 0.70f);
        [SerializeField] private Vector2 mainPhaseDuration = new Vector2(1.30f, 3.50f);
        [SerializeField] private Vector2 dropoutDuration = new Vector2(0.12f, 0.30f);
        [SerializeField] private Vector2 recoveryDuration = new Vector2(0.60f, 1.20f);

        [Header("Intensity targets (fractions of the authored value)")]
        [Tooltip("How far the green light falls at the height of the event.")]
        [SerializeField] private Vector2 dimEventFactor = new Vector2(0.10f, 0.30f);

        [Tooltip("Peak multiplier applied to each red light's authored intensity.")]
        [SerializeField, Range(0f, 4f)] private float redPeakScale = 1f;

        [Tooltip("Candle intensity scale at the height of the event.")]
        [SerializeField] private Vector2 candleEventIntensity = new Vector2(0.30f, 0.60f);

        [Tooltip("How much wider the candle's flicker swings during the event.")]
        [SerializeField, Range(1f, 6f)] private float candleTurbulence = 3f;

        [Header("Irregularity")]
        [Tooltip("Average seconds between sharp dips. Randomised around this.")]
        [SerializeField, Range(0.1f, 2f)] private float impulseInterval = 0.45f;
        [SerializeField, Range(0.5f, 6f)] private float noiseSpeed = 2.3f;

        [Header("Debug")]
        [Tooltip("One line at event start and one at restore. No per-frame logging.")]
        [SerializeField] private bool logEvents;

        // ---- captured once, never written back ------------------------------------------
        private float[] _dimBaseline;
        private float[] _redPeak;

        private Coroutine _routine;
        private float _noiseOffsetDim;
        private float _noiseOffsetRed;
        private bool _ready;

        public bool IsPlaying => _routine != null;

        /// <summary>
        /// True from the start of the event until the climax, where the phone is cut off.
        /// The ring scheduler polls this so it stops issuing new rings at the right moment
        /// instead of ringing over the blackout and the recovery.
        /// </summary>
        public bool PhoneShouldKeepRinging { get; private set; }

        private void Awake()
        {
            _dimBaseline = new float[dimmedLights.Length];
            for (int i = 0; i < dimmedLights.Length; i++)
                if (dimmedLights[i] != null)
                    _dimBaseline[i] = dimmedLights[i].intensity;

            // The red lights exist only for this event, so their resting state is off and the
            // authored intensity is read as the peak to aim for, not a value to restore to.
            _redPeak = new float[redLights.Length];
            for (int i = 0; i < redLights.Length; i++)
            {
                if (redLights[i] == null)
                    continue;
                _redPeak[i] = redLights[i].intensity;
                redLights[i].intensity = 0f;
                redLights[i].enabled = false;
            }

            _noiseOffsetDim = Random.value * 128f + 0.31f;
            _noiseOffsetRed = Random.value * 128f + 0.77f;
            _ready = true;
        }

        private void OnDisable()
        {
            // Covers the object being disabled, the scene unloading, and play mode ending
            // part-way through an event.
            CancelAndRestore();
        }

        /// <summary>
        /// Rolls the probability and starts the event if it wins and nothing is already
        /// running. Returns false for an ordinary ring, which is the common case.
        /// </summary>
        public bool TryBegin()
        {
            if (!_ready || !isActiveAndEnabled || IsPlaying)
                return false;

            if (Random.value > eventProbability)
                return false;

            _routine = StartCoroutine(RunEvent());
            return true;
        }

        /// <summary>Starts the event regardless of the probability roll. For testing.</summary>
        [ContextMenu("Force Event")]
        public void ForceBegin()
        {
            if (!_ready || !isActiveAndEnabled || IsPlaying)
                return;

            _routine = StartCoroutine(RunEvent());
        }

        public void CancelAndRestore()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            RestoreBaselines();
        }

        private void RestoreBaselines()
        {
            PhoneShouldKeepRinging = false;

            if (!_ready)
                return;

            for (int i = 0; i < dimmedLights.Length; i++)
                if (dimmedLights[i] != null)
                    dimmedLights[i].intensity = _dimBaseline[i];

            for (int i = 0; i < redLights.Length; i++)
            {
                if (redLights[i] == null)
                    continue;
                redLights[i].intensity = 0f;
                redLights[i].enabled = false;
            }

            if (candleFlicker != null)
                candleFlicker.ClearEventModulation();
        }

        // ---- the event ------------------------------------------------------------------

        private IEnumerator RunEvent()
        {
            PhoneShouldKeepRinging = true;
            if (logEvents)
                Debug.Log("[CIYC] Phone horror event: begin", this);

            float dimFloor = Random.Range(dimEventFactor.x, dimEventFactor.y);
            float candleFloor = Random.Range(candleEventIntensity.x, candleEventIntensity.y);

            SetRedEnabled(true);

            // 1. The phone rings alone for a beat before anything visual happens.
            yield return Wait(Random.Range(onsetDelay.x, onsetDelay.y));

            // 2. Green falters and the candle grows unsettled.
            float d = Random.Range(destabiliseDuration.x, destabiliseDuration.y);
            for (float e = 0f; e < d; e += Time.deltaTime)
            {
                float k = e / d;
                ApplyDimmed(Mathf.Lerp(1f, dimFloor, k), k);
                ApplyCandle(Mathf.Lerp(1f, candleFloor, k), Mathf.Lerp(1f, candleTurbulence, k));
                yield return null;
            }

            // 3. Red bleeds in.
            d = Random.Range(redEmergeDuration.x, redEmergeDuration.y);
            for (float e = 0f; e < d; e += Time.deltaTime)
            {
                float k = e / d;
                ApplyDimmed(dimFloor, 1f);
                ApplyRed(k, 1f);
                ApplyCandle(candleFloor, candleTurbulence);
                yield return null;
            }

            // 4. The corridor belongs to the red. Irregular pulses on every channel; nothing
            //    is on a beat the viewer can anticipate.
            d = Random.Range(mainPhaseDuration.x, mainPhaseDuration.y);
            float nextImpulse = Random.Range(impulseInterval * 0.4f, impulseInterval * 1.6f);
            float impulseDecay = 0f;
            for (float e = 0f; e < d; e += Time.deltaTime)
            {
                if (e >= nextImpulse)
                {
                    impulseDecay = 1f;
                    nextImpulse = e + Random.Range(impulseInterval * 0.4f, impulseInterval * 1.6f);
                }
                impulseDecay = Mathf.Max(0f, impulseDecay - Time.deltaTime * 4.5f);

                float dip = 1f - impulseDecay * 0.75f;
                ApplyDimmed(dimFloor * dip, 1f);
                ApplyRed(1f, dip);
                ApplyCandle(candleFloor * Mathf.Lerp(1f, 0.45f, impulseDecay), candleTurbulence);
                yield return null;
            }

            // 5. The phone stops mid-ring. The silence is the point.
            PhoneShouldKeepRinging = false;
            if (phoneAudio != null)
                phoneAudio.Stop();

            // 6. Everything drops away for an instant.
            d = Random.Range(dropoutDuration.x, dropoutDuration.y);
            for (float e = 0f; e < d; e += Time.deltaTime)
            {
                ApplyDimmed(0.04f, 0f);
                ApplyRed(0.08f, 0f);
                ApplyCandle(0.25f, 1f);
                yield return null;
            }

            // 7. Green comes back, red drains away, the candle settles.
            d = Random.Range(recoveryDuration.x, recoveryDuration.y);
            for (float e = 0f; e < d; e += Time.deltaTime)
            {
                float k = e / d;
                float eased = k * k * (3f - 2f * k);
                ApplyDimmed(Mathf.Lerp(0.04f, 1f, eased), 1f - eased);
                ApplyRed(Mathf.Lerp(0.08f, 0f, eased), 0f);
                ApplyCandle(Mathf.Lerp(0.25f, 1f, eased), Mathf.Lerp(1f, candleTurbulence, 1f - eased));
                yield return null;
            }

            // 8. Exactly the authored values again — not approximately.
            SetRedEnabled(false);
            RestoreBaselines();
            _routine = null;
            if (logEvents)
                Debug.Log("[CIYC] Phone horror event: restored", this);
        }

        // ---- helpers --------------------------------------------------------------------

        /// <summary>Allocation-free wait; WaitForSeconds would allocate on every event.</summary>
        private IEnumerator Wait(float seconds)
        {
            for (float e = 0f; e < seconds; e += Time.deltaTime)
                yield return null;
        }

        /// <summary>
        /// Perlin plus a faster layer, so the wobble is irregular rather than a sine.
        /// Returns roughly 0..1.
        /// </summary>
        private static float Noise(float offset, float speed)
        {
            float t = Time.time;
            return Mathf.PerlinNoise(offset + t * speed, 0.5f) * 0.72f
                   + Mathf.PerlinNoise(0.5f, offset + t * speed * 3.7f) * 0.28f;
        }

        private void ApplyDimmed(float factor, float wobble)
        {
            float n = wobble > 0f ? 1f + (Noise(_noiseOffsetDim, noiseSpeed) - 0.5f) * 0.5f * wobble : 1f;
            float f = Mathf.Max(0f, factor * n);
            for (int i = 0; i < dimmedLights.Length; i++)
                if (dimmedLights[i] != null)
                    dimmedLights[i].intensity = _dimBaseline[i] * f;
        }

        private void ApplyRed(float factor, float wobble)
        {
            float n = wobble > 0f ? 1f + (Noise(_noiseOffsetRed, noiseSpeed * 1.3f) - 0.5f) * 0.6f * wobble : 1f;
            float f = Mathf.Max(0f, factor * n * redPeakScale);
            for (int i = 0; i < redLights.Length; i++)
                if (redLights[i] != null)
                    redLights[i].intensity = _redPeak[i] * f;
        }

        private void ApplyCandle(float intensityScale, float turbulence)
        {
            if (candleFlicker != null)
                candleFlicker.ApplyEventModulation(intensityScale, turbulence);
        }

        private void SetRedEnabled(bool on)
        {
            for (int i = 0; i < redLights.Length; i++)
                if (redLights[i] != null)
                    redLights[i].enabled = on;
        }
    }
}
