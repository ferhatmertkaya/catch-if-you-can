using System.Collections;
using UnityEngine;

namespace CatchIfYouCan.UI
{
    /// <summary>
    /// Single runtime owner of the main-menu doorway atmosphere.
    /// <para>
    /// Everything this component touches is authored in the scene and serialized there; the
    /// component only scales the authored values by the multipliers below and drives optional
    /// timed events. It deliberately does not create particle systems or lights: if it did,
    /// the scene and the code would each hold a half-truth about how the doorway looks, which
    /// is what previously made Play Mode and device builds disagree.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public class MainMenuAtmosphereController : MonoBehaviour
    {
        [Header("Fog layers (authored in the scene)")]
        [SerializeField] private ParticleSystem[] fogLayers = new ParticleSystem[0];

        [Tooltip("Scales the authored emission rate of every fog layer. 1 = as authored.")]
        [SerializeField, Range(0f, 2f)] private float fogIntensity = 1f;

        [Tooltip("Multiplied into the authored start colour of every fog layer.")]
        [SerializeField] private Color fogTint = Color.white;

        [Header("Doorway lights (authored in the scene)")]
        [SerializeField] private Light[] doorLights = new Light[0];

        [Tooltip("Scales the authored intensity of every doorway light. 1 = as authored.")]
        [SerializeField, Range(0f, 3f)] private float doorLightIntensity = 1f;

        private float[] _authoredEmission;
        private Color[] _authoredStartColor;
        private float[] _authoredSimulationSpeed;
        private float[] _authoredLightIntensity;
        private Coroutine _pulse;

        // Set by a horror event while one is running; 1/white means "behave normally". These are
        // assigned, never accumulated, so an event that is interrupted cannot leave the fog
        // permanently agitated or tinted.
        private float _eventEmissionScale = 1f;
        private float _eventTurbulence = 1f;
        private Color _eventTint = Color.white;

        private void Awake()
        {
            CacheAuthoredValues();
        }

        private void OnEnable()
        {
            ApplyAuthoredValues();
        }

        private void CacheAuthoredValues()
        {
            _authoredEmission = new float[fogLayers.Length];
            _authoredStartColor = new Color[fogLayers.Length];
            _authoredSimulationSpeed = new float[fogLayers.Length];
            for (int i = 0; i < fogLayers.Length; i++)
            {
                if (fogLayers[i] == null)
                    continue;

                _authoredEmission[i] = fogLayers[i].emission.rateOverTime.constant;
                _authoredStartColor[i] = fogLayers[i].main.startColor.color;
                _authoredSimulationSpeed[i] = fogLayers[i].main.simulationSpeed;
            }

            _authoredLightIntensity = new float[doorLights.Length];
            for (int i = 0; i < doorLights.Length; i++)
            {
                if (doorLights[i] != null)
                    _authoredLightIntensity[i] = doorLights[i].intensity;
            }
        }

        /// <summary>
        /// Re-applies the authored baseline scaled by the current multipliers. Safe to call at
        /// any time; it always starts from the cached authored values rather than compounding.
        /// </summary>
        public void ApplyAuthoredValues()
        {
            ApplyFogValues();
            ApplyDoorLightValues();
        }

        /// <summary>
        /// Fog only. Kept separate from the door lights on purpose: those same two lights are
        /// also in the horror event's dimmed set, so a fog update that re-applied light
        /// intensities would snap the corridor back to full brightness in the middle of an
        /// event. Nothing that runs during an event may touch <see cref="doorLights"/>.
        /// </summary>
        private void ApplyFogValues()
        {
            for (int i = 0; i < fogLayers.Length; i++)
            {
                var system = fogLayers[i];
                if (system == null)
                    continue;

                var emission = system.emission;
                emission.rateOverTime = _authoredEmission[i] * fogIntensity * _eventEmissionScale;

                var main = system.main;
                main.startColor = _authoredStartColor[i] * fogTint * _eventTint;
                main.simulationSpeed = _authoredSimulationSpeed[i] * _eventTurbulence;
            }
        }

        private void ApplyDoorLightValues()
        {
            for (int i = 0; i < doorLights.Length; i++)
            {
                if (doorLights[i] != null)
                    doorLights[i].intensity = _authoredLightIntensity[i] * doorLightIntensity;
            }
        }

        /// <summary>
        /// Lets a horror event unsettle the fog without a second script writing particle
        /// properties behind this one's back.
        /// <paramref name="emissionScale"/> thickens or thins the mist,
        /// <paramref name="turbulence"/> speeds the whole simulation up so the strands churn,
        /// and <paramref name="tint"/> multiplies the authored colour.
        ///
        /// <para>
        /// The tint is meant to be a nudge, not a repaint: the fog is lit by the scene, so the
        /// red event lights already colour it. Pushing it to pure red here would flatten the
        /// green and cyan the corridor is built on.
        /// </para>
        ///
        /// <para>All three are set, never accumulated.</para>
        /// </summary>
        public void ApplyEventAtmosphere(float emissionScale, float turbulence, Color tint)
        {
            _eventEmissionScale = Mathf.Clamp(emissionScale, 0f, 4f);
            _eventTurbulence = Mathf.Clamp(turbulence, 0.05f, 4f);
            _eventTint = tint;
            ApplyFogValues();
        }

        /// <summary>Returns the fog to its authored behaviour. Safe to call when idle.</summary>
        public void ClearEventAtmosphere()
        {
            _eventEmissionScale = 1f;
            _eventTurbulence = 1f;
            _eventTint = Color.white;
            ApplyFogValues();
        }

        public void SetFogIntensity(float value)
        {
            fogIntensity = Mathf.Clamp(value, 0f, 2f);
            ApplyAuthoredValues();
        }

        public void SetDoorLightIntensity(float value)
        {
            doorLightIntensity = Mathf.Clamp(value, 0f, 3f);
            ApplyAuthoredValues();
        }

        /// <summary>
        /// Briefly swells the doorway lights and settles back to the authored baseline.
        /// Provided as the hook for later horror beats; nothing schedules it automatically.
        /// </summary>
        public void PulseDoorLights(float peakMultiplier = 1.6f, float duration = 0.9f)
        {
            if (!isActiveAndEnabled || doorLights.Length == 0)
                return;

            if (_pulse != null)
                StopCoroutine(_pulse);

            _pulse = StartCoroutine(PulseRoutine(peakMultiplier, Mathf.Max(0.05f, duration)));
        }

        private IEnumerator PulseRoutine(float peakMultiplier, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                // Rise and fall once across the whole duration.
                float curve = Mathf.Sin(Mathf.PI * Mathf.Clamp01(elapsed / duration));
                float scale = Mathf.Lerp(1f, peakMultiplier, curve) * doorLightIntensity;

                for (int i = 0; i < doorLights.Length; i++)
                {
                    if (doorLights[i] != null)
                        doorLights[i].intensity = _authoredLightIntensity[i] * scale;
                }

                yield return null;
            }

            ApplyAuthoredValues();
            _pulse = null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying || _authoredEmission == null)
                return;

            ApplyAuthoredValues();
        }
#endif
    }
}
