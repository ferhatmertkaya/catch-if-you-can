using System.Collections;
using UnityEngine;

namespace CatchIfYouCan.Art
{
    /// <summary>
    /// The red room: the corridor's green supernatural light drains away, the wall behind the
    /// ghost blazes red, and the ghost is lit from behind and to one side until the green
    /// returns.
    ///
    /// <para>
    /// This is an independent event. It used to be the back half of the phone sequence; it is
    /// now its own beat that <see cref="MainMenuHorrorEventDirector"/> can choose instead of
    /// the phone. Nothing here rings the phone, and the phone never triggers this.
    /// </para>
    ///
    /// <para>
    /// The three lights are given distinct jobs rather than one shared red wash, which is what
    /// keeps the ghost's face readable while the room is saturated:
    /// <list type="bullet">
    /// <item><b>Back</b> — strong, saturated, behind the ghost. This is the one that turns the
    /// doorway block red and throws the ghost into silhouette.</item>
    /// <item><b>Key</b> — softer, off to one side and above, so the face is modelled rather
    /// than flattened.</item>
    /// <item><b>Fill</b> — very low and close to neutral. A little non-red light is what stops
    /// the face collapsing into a single flat red shape.</item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// Peak intensities are serialized here rather than read from the lights at Awake. The
    /// earlier version treated each light's authored intensity as its peak, so saving the scene
    /// while an event happened to be mid-fade wrote the faded values back into the asset and
    /// quietly destroyed the peaks. The lights are owned outright by this component: off and at
    /// zero when nothing is running, driven from these numbers when it is.
    /// </para>
    ///
    /// <para>
    /// The fog is never thinned. It is lit, so dimming the room already makes it recede; pulling
    /// its emission down as well is what previously read as the fog vanishing.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Catch If You Can/Main Menu Red Room Event")]
    public sealed class MainMenuRedRoomEvent : MonoBehaviour, IMainMenuHorrorEvent
    {
        [Header("Green lights suppressed during the event")]
        [Tooltip("The existing green/ambient scene lights. Faded down so the red can take the " +
                 "room, then restored exactly.")]
        [SerializeField] private Light[] dimmedLights = new Light[0];

        [Tooltip("How far the green falls at the height of the event, as a fraction of authored.")]
        [SerializeField] private Vector2 dimEventFactor = new Vector2(0.05f, 0.15f);

        [Header("Red Horror Event")]
        [Tooltip("Behind the ghost. The bright saturated source that turns the doorway red.")]
        [SerializeField] private Light redBackLight;

        [Tooltip("Toward the ghost's face and upper torso. Softer, and off-axis so the face is " +
                 "modelled rather than flattened.")]
        [SerializeField] private Light redGhostKeyLight;

        [Tooltip("Low, near-neutral fill. Keeps facial detail from collapsing into flat red.")]
        [SerializeField] private Light redFillLight;

        [Tooltip("Peak intensity of the backlight.")]
        [SerializeField, Min(0f)] private float redBacklightIntensity = 14f;

        [Tooltip("Peak intensity of the ghost key light. Keep well under the backlight.")]
        [SerializeField, Min(0f)] private float redGhostKeyIntensity = 4.5f;

        [Tooltip("Peak intensity of the fill. Very low by design.")]
        [SerializeField, Min(0f)] private float redFillIntensity = 0.9f;

        [Tooltip("Master multiplier over all three red lights. 1 = as set above.")]
        [SerializeField, Range(0f, 4f)] private float redEventIntensity = 1f;

        [Tooltip("Kept from the original implementation; multiplies the red peak alongside " +
                 "redEventIntensity.")]
        [SerializeField, Range(0f, 4f)] private float redPeakScale = 1f;

        [Header("Red Horror Event — timing")]
        [SerializeField, Min(0f)] private float redEventFadeIn = 1.1f;
        [SerializeField, Min(0f)] private float redEventHoldDuration = 3.2f;
        [SerializeField, Min(0f)] private float redEventFadeOut = 1.4f;

        [Header("Red Horror Event — flicker")]
        [Tooltip("Mostly stable, with occasional irregular dips. Not a strobe.")]
        [SerializeField, Range(0f, 1f)] private float redFlickerMin = 0.72f;
        [SerializeField, Range(1f, 2f)] private float redFlickerMax = 1.10f;
        [SerializeField, Range(0.1f, 6f)] private float redFlickerSpeed = 1.9f;

        [Header("Candle")]
        [Tooltip("The candle light's flicker component. The same strong-flicker API the phone " +
                 "event uses; there is deliberately only one candle implementation.")]
        [SerializeField] private CandleFlicker candleFlicker;

        [SerializeField] private Vector2 candleEventIntensity = new Vector2(0.25f, 0.55f);
        [SerializeField, Range(1f, 6f)] private float candleTurbulence = 4f;

        [Header("Fog (multipliers on the authored atmosphere)")]
        [Tooltip("The doorway atmosphere owner. Fog is unsettled through its API and never " +
                 "thinned below the authored rate, so it stays visible for the whole event.")]
        [SerializeField] private CatchIfYouCan.UI.MainMenuAtmosphereController atmosphere;

        [SerializeField] private Vector2 fogEventEmission = new Vector2(1.7f, 2.4f);
        [SerializeField] private Vector2 fogEventTurbulence = new Vector2(1.8f, 2.6f);

        [Tooltip("Multiplied into the fog's authored colour at the peak. The fog is lit, so the " +
                 "red lights already colour it; this is a nudge, not a repaint.")]
        [SerializeField] private Color fogEventTint = new Color(0.85f, 0.62f, 0.60f, 1f);

        [Header("Debug")]
        [SerializeField] private bool logEvents;

        // ---- captured once, never written back ------------------------------------------
        private float[] _dimBaseline;
        private Coroutine _routine;
        private float _flickerOffset;
        private bool _ready;

        public bool IsPlaying => _routine != null;

        /// <summary>Name shown in the director's log line.</summary>
        public string EventName => "RedRoom";

        private void Awake()
        {
            _dimBaseline = new float[dimmedLights.Length];
            for (int i = 0; i < dimmedLights.Length; i++)
                if (dimmedLights[i] != null)
                    _dimBaseline[i] = dimmedLights[i].intensity;

            _flickerOffset = Random.value * 128f + 0.53f;
            _ready = true;

            // The red lights belong to this component alone; their resting state is off.
            SetRedResting();
        }

        private void OnDisable() => CancelAndRestore();

        public bool TryBegin()
        {
            if (!_ready || !isActiveAndEnabled || IsPlaying)
                return false;

            _routine = StartCoroutine(RunEvent());
            return true;
        }

        /// <summary>Starts the event regardless. For testing from the Inspector.</summary>
        [ContextMenu("Force Red Event")]
        public void ForceBegin() => TryBegin();

        public void CancelAndRestore()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            RestoreBaselines();
        }

        private void SetRedResting()
        {
            ForEachRedLight((light, peak) =>
            {
                light.intensity = 0f;
                light.enabled = false;
            });
        }

        private void RestoreBaselines()
        {
            if (!_ready)
                return;

            for (int i = 0; i < dimmedLights.Length; i++)
                if (dimmedLights[i] != null)
                    dimmedLights[i].intensity = _dimBaseline[i];

            SetRedResting();

            if (candleFlicker != null)
                candleFlicker.ClearEventModulation();

            if (atmosphere != null)
                atmosphere.ClearEventAtmosphere();
        }

        // ---- the event ------------------------------------------------------------------

        private IEnumerator RunEvent()
        {
            if (logEvents)
                Debug.Log("[CIYC] Red room event: begin", this);

            float dimFloor = Random.Range(dimEventFactor.x, dimEventFactor.y);
            float candleFloor = Random.Range(candleEventIntensity.x, candleEventIntensity.y);
            float fogEmission = Mathf.Max(1f, Random.Range(fogEventEmission.x, fogEventEmission.y));
            float fogChurn = Random.Range(fogEventTurbulence.x, fogEventTurbulence.y);

            SetRedEnabled(true);

            // 1. Green drains away as red rises. Both move together so the room is never
            //    momentarily unlit.
            float d = Mathf.Max(0.01f, redEventFadeIn);
            for (float e = 0f; e < d; e += Time.deltaTime)
            {
                float k = e / d;
                float eased = k * k * (3f - 2f * k);
                ApplyDimmed(Mathf.Lerp(1f, dimFloor, eased));
                ApplyRed(eased, false);
                ApplyCandle(Mathf.Lerp(1f, candleFloor, eased),
                            Mathf.Lerp(1f, candleTurbulence, eased));
                ApplyFog(eased, fogEmission, fogChurn);
                yield return null;
            }

            // 2. The room belongs to the red. Mostly steady, with irregular dips.
            for (float e = 0f; e < redEventHoldDuration; e += Time.deltaTime)
            {
                ApplyDimmed(dimFloor);
                ApplyRed(1f, true);
                ApplyCandle(candleFloor, candleTurbulence);
                ApplyFog(1f, fogEmission, fogChurn);
                yield return null;
            }

            // 3. Red drains, green returns.
            d = Mathf.Max(0.01f, redEventFadeOut);
            for (float e = 0f; e < d; e += Time.deltaTime)
            {
                float k = e / d;
                float eased = k * k * (3f - 2f * k);
                ApplyDimmed(Mathf.Lerp(dimFloor, 1f, eased));
                ApplyRed(1f - eased, false);
                ApplyCandle(Mathf.Lerp(candleFloor, 1f, eased),
                            Mathf.Lerp(candleTurbulence, 1f, eased));
                ApplyFog(1f - eased, fogEmission, fogChurn);
                yield return null;
            }

            // 4. Exactly the authored values again.
            RestoreBaselines();
            _routine = null;
            if (logEvents)
                Debug.Log("[CIYC] Red room event: restored", this);
        }

        // ---- helpers --------------------------------------------------------------------

        private void ForEachRedLight(System.Action<Light, float> action)
        {
            if (redBackLight != null) action(redBackLight, redBacklightIntensity);
            if (redGhostKeyLight != null) action(redGhostKeyLight, redGhostKeyIntensity);
            if (redFillLight != null) action(redFillLight, redFillIntensity);
        }

        private void SetRedEnabled(bool on)
        {
            ForEachRedLight((light, peak) => light.enabled = on);
        }

        /// <summary>
        /// Drives the three red lights from their serialized peaks.
        /// <paramref name="flicker"/> is only applied while the event is holding, so the fades
        /// in and out stay smooth instead of stuttering.
        /// </summary>
        private void ApplyRed(float factor, bool flicker)
        {
            float wobble = 1f;
            if (flicker)
            {
                // Perlin, not white noise: a flame-like unsteadiness rather than a strobe.
                float n = Mathf.PerlinNoise(_flickerOffset + Time.time * redFlickerSpeed, 0.5f);
                wobble = Mathf.Lerp(redFlickerMin, Mathf.Max(redFlickerMin, redFlickerMax), n);
            }

            float f = Mathf.Max(0f, factor * wobble * redEventIntensity * redPeakScale);
            ForEachRedLight((light, peak) => light.intensity = peak * f);
        }

        private void ApplyDimmed(float factor)
        {
            float f = Mathf.Max(0f, factor);
            for (int i = 0; i < dimmedLights.Length; i++)
                if (dimmedLights[i] != null)
                    dimmedLights[i].intensity = _dimBaseline[i] * f;
        }

        private void ApplyCandle(float intensityScale, float turbulence)
        {
            if (candleFlicker != null)
                candleFlicker.ApplyEventModulation(intensityScale, turbulence);
        }

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

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (redFlickerMax < redFlickerMin)
                redFlickerMax = redFlickerMin;
        }
#endif
    }
}
