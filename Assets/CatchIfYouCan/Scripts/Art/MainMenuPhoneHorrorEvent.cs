using System.Collections;
using UnityEngine;

namespace CatchIfYouCan.Art
{
    /// <summary>
    /// The telephone beat: the corridor's green light grows unsteady, the candles gutter, the
    /// rotary phone rings three times, and everything settles back.
    ///
    /// <para>
    /// This event no longer touches the red lights. Red is its own event
    /// (<see cref="MainMenuRedRoomEvent"/>), chosen independently by
    /// <see cref="MainMenuHorrorEventDirector"/>, so a phone call and a red takeover are two
    /// different things that happen to the menu rather than one fused sequence.
    /// </para>
    ///
    /// <para>
    /// The fog stays lit and visible for the whole event. The previous version ended on a
    /// blackout that pulled every light down to 4%; because the fog particles are lit, that
    /// read as the fog itself vanishing and popping back. There is no blackout here, and the
    /// mist only ever gets thicker, never thinner.
    /// </para>
    ///
    /// <para>
    /// Every value it touches is captured once in <c>Awake</c> and driven from that capture:
    /// <c>baseline * factor</c>, never <c>current * factor</c>, so an interrupted event cannot
    /// leave the corridor permanently dim. Restoration also runs from <c>OnDisable</c>.
    /// </para>
    ///
    /// <para>
    /// The candle is not written directly. <see cref="CandleFlicker"/> is the single writer of
    /// its light's intensity, and this asks it for a temporary modulation instead. The fog goes
    /// through the atmosphere owner's API for the same reason.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Catch If You Can/Main Menu Phone Horror Event")]
    public sealed class MainMenuPhoneHorrorEvent : MonoBehaviour, IMainMenuHorrorEvent
    {
        [Header("Scene lights (authored values are captured, never overwritten)")]
        [Tooltip("Existing scene lights destabilised during the event — the green door lights " +
                 "and the corridor's ambient/directional light. Intensity is scaled and restored.")]
        [SerializeField] private Light[] dimmedLights = new Light[0];

        [Tooltip("The candle light's flicker component. Modulated through its own API.")]
        [SerializeField] private CandleFlicker candleFlicker;

        [Tooltip("The visible flames, dimmed alongside the light they cast. Without these the " +
                 "light drops but the flame stays burning, which reads as the candle being lit " +
                 "by something else.")]
        [SerializeField] private CandleFlameFlicker[] candleFlames = new CandleFlameFlicker[0];

        [Tooltip("The doorway atmosphere owner. The fog is unsettled through its API rather " +
                 "than by writing particle systems directly. Optional.")]
        [SerializeField] private CatchIfYouCan.UI.MainMenuAtmosphereController atmosphere;

        [Header("Phone")]
        [Tooltip("The phone's AudioSource. Used as a fallback when no ring player is assigned.")]
        [SerializeField] private AudioSource phoneAudio;

        [Tooltip("The component that actually plays a ring, with its own pitch and volume " +
                 "variation. Assigning it keeps all ring audio settings in one place.")]
        [SerializeField] private RotaryPhoneRandomRing ringPlayer;

        [Header("Phone sequence")]
        [Tooltip("How many times the phone rings. The brief calls for three.")]
        [SerializeField, Min(1)] private int phoneRingCount = 3;

        [Tooltip("Silence between the end of one ring and the start of the next. The clip's own " +
                 "length is added on top, so a ring is never cut off by the one after it.")]
        [SerializeField, Min(0f)] private float phoneRingInterval = 0.8f;

        [Tooltip("Quiet beat before the first ring, while the candles begin to gutter.")]
        [SerializeField, Min(0f)] private float phoneEventLeadIn = 0.6f;

        [Tooltip("Silence held after the last ring, before anything settles.")]
        [SerializeField, Min(0f)] private float phoneEventHold = 1.2f;

        [Tooltip("How long the lights, candle and fog take to return to their authored state.")]
        [SerializeField, Min(0f)] private float phoneEventFadeOut = 0.9f;

        [Header("Green light destabilisation")]
        [Tooltip("How long the green lights take to fall to their event level.")]
        [SerializeField] private Vector2 destabiliseDuration = new Vector2(0.40f, 1.00f);

        [Tooltip("How far the green light falls during the event, as a fraction of authored.")]
        [SerializeField] private Vector2 dimEventFactor = new Vector2(0.35f, 0.60f);

        [Header("Candle")]
        [Tooltip("Candle intensity scale during the event.")]
        [SerializeField] private Vector2 candleEventIntensity = new Vector2(0.30f, 0.60f);

        [Tooltip("How much wider the candle's flicker swings during the event.")]
        [SerializeField, Range(1f, 6f)] private float candleTurbulence = 3f;

        [Header("Fog (multipliers on the authored atmosphere)")]
        [Tooltip("How much thicker the mist gets during the event. Never below 1: the fog must " +
                 "stay visible for the whole sequence.")]
        [SerializeField] private Vector2 fogEventEmission = new Vector2(1.5f, 2.1f);

        [Tooltip("How much faster the fog churns during the event.")]
        [SerializeField] private Vector2 fogEventTurbulence = new Vector2(1.4f, 1.9f);

        [Tooltip("Multiplied into the fog's authored colour at the peak. A nudge, not a repaint.")]
        [SerializeField] private Color fogEventTint = new Color(1f, 0.88f, 0.86f, 1f);

        [Header("Irregularity")]
        [Tooltip("Average seconds between sharp dips. Randomised around this.")]
        [SerializeField, Range(0.1f, 2f)] private float impulseInterval = 0.45f;
        [SerializeField, Range(0.5f, 6f)] private float noiseSpeed = 2.3f;

        [Header("Debug")]
        [Tooltip("One line at event start and one at restore. No per-frame logging.")]
        [SerializeField] private bool logEvents;

        // ---- captured once, never written back ------------------------------------------
        private float[] _dimBaseline;

        private Coroutine _routine;
        private float _noiseOffsetDim;
        private bool _ready;

        public bool IsPlaying => _routine != null;

        /// <summary>Name shown in the director's log line.</summary>
        public string EventName => "Phone";

        /// <summary>No cooldown of its own; available whenever it is not already running.</summary>
        public bool IsAvailable => _ready && isActiveAndEnabled && !IsPlaying;

        private void Awake()
        {
            _dimBaseline = new float[dimmedLights.Length];
            for (int i = 0; i < dimmedLights.Length; i++)
                if (dimmedLights[i] != null)
                    _dimBaseline[i] = dimmedLights[i].intensity;

            if (ringPlayer == null)
                ringPlayer = GetComponentInChildren<RotaryPhoneRandomRing>();

            _noiseOffsetDim = Random.value * 128f + 0.31f;
            _ready = true;
        }

        private void OnDisable()
        {
            // Covers the object being disabled, the scene unloading, and play mode ending
            // part-way through an event.
            CancelAndRestore();
        }

        /// <summary>
        /// Starts the event unless one is already running. The director decides which event
        /// happens and when, so there is no probability roll here any more.
        /// </summary>
        public bool TryBegin()
        {
            if (!_ready || !isActiveAndEnabled || IsPlaying)
                return false;

            _routine = StartCoroutine(RunEvent());
            return true;
        }

        /// <summary>Starts the event regardless. For testing from the Inspector.</summary>
        [ContextMenu("Force Phone Event")]
        public void ForceBegin() => TryBegin();

        public void CancelAndRestore()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            StopRinging();
            RestoreBaselines();
        }

        private void StopRinging()
        {
            if (ringPlayer != null)
                ringPlayer.StopRinging();
            else if (phoneAudio != null)
                phoneAudio.Stop();
        }

        private void RestoreBaselines()
        {
            if (!_ready)
                return;

            for (int i = 0; i < dimmedLights.Length; i++)
                if (dimmedLights[i] != null)
                    dimmedLights[i].intensity = _dimBaseline[i];

            if (candleFlicker != null)
                candleFlicker.ClearEventModulation();

            for (int i = 0; i < candleFlames.Length; i++)
                if (candleFlames[i] != null)
                    candleFlames[i].ClearEventModulation();

            // Puts emission, churn and tint back to the authored atmosphere. The fog is never
            // disabled or emptied by this event, only returned to its normal thickness.
            if (atmosphere != null)
                atmosphere.ClearEventAtmosphere();
        }

        // ---- the event ------------------------------------------------------------------

        private IEnumerator RunEvent()
        {
            if (logEvents)
                Debug.Log("[CIYC] Phone event: begin", this);

            float dimFloor = Random.Range(dimEventFactor.x, dimEventFactor.y);
            float candleFloor = Random.Range(candleEventIntensity.x, candleEventIntensity.y);
            float fogEmission = Mathf.Max(1f, Random.Range(fogEventEmission.x, fogEventEmission.y));
            float fogChurn = Random.Range(fogEventTurbulence.x, fogEventTurbulence.y);

            // 1. The candles begin to gutter and the green falters before the first ring.
            float d = Mathf.Max(0.01f, Random.Range(destabiliseDuration.x, destabiliseDuration.y));
            for (float e = 0f; e < d; e += Time.deltaTime)
            {
                float k = e / d;
                ApplyDimmed(Mathf.Lerp(1f, dimFloor, k), k);
                ApplyCandle(Mathf.Lerp(1f, candleFloor, k), Mathf.Lerp(1f, candleTurbulence, k));
                ApplyFog(k, fogEmission, fogChurn);
                yield return null;
            }

            yield return HoldSteady(phoneEventLeadIn, dimFloor, candleFloor, fogEmission, fogChurn);

            // 2. Exactly phoneRingCount rings, spaced so one never cuts off the last.
            float clip = ringPlayer != null ? ringPlayer.ClipLength
                       : phoneAudio != null && phoneAudio.clip != null ? phoneAudio.clip.length
                       : 0f;
            // The gap is measured from the end of the clip, so the next ring can never cut the
            // last one off however long the clip is.
            float ringGap = clip > 0f ? clip + phoneRingInterval : Mathf.Max(0.1f, phoneRingInterval);

            for (int i = 0; i < phoneRingCount; i++)
            {
                PlayOneRing();

                // No gap after the final ring; the hold below covers that.
                float wait = i == phoneRingCount - 1 ? clip : ringGap;
                yield return HoldSteady(wait, dimFloor, candleFloor, fogEmission, fogChurn);
            }

            // 3. The silence after the last ring is the point.
            yield return HoldSteady(phoneEventHold, dimFloor, candleFloor, fogEmission, fogChurn);

            // 4. Everything eases back to the authored state. Nothing is cut to black.
            d = Mathf.Max(0.01f, phoneEventFadeOut);
            for (float e = 0f; e < d; e += Time.deltaTime)
            {
                float k = e / d;
                float eased = k * k * (3f - 2f * k);
                ApplyDimmed(Mathf.Lerp(dimFloor, 1f, eased), 1f - eased);
                ApplyCandle(Mathf.Lerp(candleFloor, 1f, eased),
                            Mathf.Lerp(candleTurbulence, 1f, eased));
                ApplyFog(1f - eased, fogEmission, fogChurn);
                yield return null;
            }

            // 5. Exactly the authored values again — not approximately.
            RestoreBaselines();
            _routine = null;
            if (logEvents)
                Debug.Log("[CIYC] Phone event: restored", this);
        }

        private void PlayOneRing()
        {
            if (ringPlayer != null)
                ringPlayer.PlayRing();
            else if (phoneAudio != null && phoneAudio.clip != null)
                phoneAudio.Play();
        }

        /// <summary>
        /// Holds the event at its current level for a while, still wobbling. Used between rings
        /// so the corridor keeps breathing instead of freezing between them.
        /// </summary>
        private IEnumerator HoldSteady(float seconds, float dimFloor, float candleFloor,
                                       float fogEmission, float fogChurn)
        {
            float nextImpulse = Random.Range(impulseInterval * 0.4f, impulseInterval * 1.6f);
            float impulseDecay = 0f;

            for (float e = 0f; e < seconds; e += Time.deltaTime)
            {
                if (e >= nextImpulse)
                {
                    impulseDecay = 1f;
                    nextImpulse = e + Random.Range(impulseInterval * 0.4f, impulseInterval * 1.6f);
                }
                impulseDecay = Mathf.Max(0f, impulseDecay - Time.deltaTime * 4.5f);

                float dip = 1f - impulseDecay * 0.55f;
                ApplyDimmed(dimFloor * dip, 1f);
                ApplyCandle(candleFloor * Mathf.Lerp(1f, 0.55f, impulseDecay), candleTurbulence);
                ApplyFog(1f + impulseDecay * 0.2f, fogEmission, fogChurn);
                yield return null;
            }
        }

        // ---- helpers --------------------------------------------------------------------

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

        private void ApplyCandle(float intensityScale, float turbulence)
        {
            if (candleFlicker != null)
                candleFlicker.ApplyEventModulation(intensityScale, turbulence);

            for (int i = 0; i < candleFlames.Length; i++)
                if (candleFlames[i] != null)
                    candleFlames[i].ApplyEventModulation(intensityScale, turbulence);
        }

        /// <summary>
        /// Unsettles the fog. <paramref name="k"/> is how far into the event we are, 0 at rest
        /// and 1 at the peak. Emission never drops below the authored rate, so the mist cannot
        /// thin out or disappear part way through.
        /// </summary>
        private void ApplyFog(float k, float emissionPeak, float turbulencePeak)
        {
            if (atmosphere == null)
                return;

            k = Mathf.Clamp01(k);
            atmosphere.ApplyEventAtmosphere(
                Mathf.Lerp(1f, emissionPeak, k),
                Mathf.Lerp(1f, turbulencePeak, k),
                Color.Lerp(Color.white, fogEventTint, k));
        }
    }
}
