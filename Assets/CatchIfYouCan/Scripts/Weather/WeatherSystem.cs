using CatchIfYouCan.Procedural;
using CatchIfYouCan.Procedural.Deterministic;
using CatchIfYouCan.Utilities;
using UnityEngine;

namespace CatchIfYouCan.Weather
{
    public enum WeatherType
    {
        Clear,
        Rain,
        Fog
    }

    public class WeatherSystem : SingletonBehaviour<WeatherSystem>
    {
        [SerializeField] private WeatherType currentWeather = WeatherType.Clear;
        [SerializeField] private ParticleSystem rainParticles;
        [SerializeField] private ParticleSystem outdoorMistParticles;
        [SerializeField] private float clearFogDensity = 0.01f;
        [SerializeField] private float rainFogDensity = 0.025f;
        [SerializeField] private float fogFogDensity = 0.08f;
        [SerializeField] private float transitionSpeed = 1.5f;

        private float _targetFogDensity;
        private float _currentFogDensity;

        public WeatherType CurrentWeather => currentWeather;

        protected override void Awake()
        {
            base.Awake();
            ApplyWeatherImmediate(currentWeather);
        }

        private void Update()
        {
            if (!RenderSettings.fog)
                RenderSettings.fog = true;

            _currentFogDensity = Mathf.Lerp(_currentFogDensity, _targetFogDensity, transitionSpeed * Time.deltaTime);
            RenderSettings.fogDensity = _currentFogDensity;
        }

        public void SetWeather(WeatherType weather)
        {
            currentWeather = weather;
            ApplyWeatherImmediate(weather);
        }

        /// <summary>
        /// Applies the weather the LAYOUT chose.
        ///
        /// Weather is gameplay-affecting - it changes visibility and audio masking - so it
        /// cannot come from UnityEngine.Random, which is a process-global stream shared with
        /// roughly a hundred cosmetic call sites whose draw count depends on frame rate.
        /// Two clients on the same seed would have disagreed about the weather. Stage A now
        /// picks it from the dedicated Weather stream and it is covered by the layout hash.
        /// </summary>
        public void ApplyLayoutWeather(int weatherIndex)
        {
            var values = (WeatherType[])System.Enum.GetValues(typeof(WeatherType));
            if (values.Length == 0)
                return;

            int index = weatherIndex % values.Length;
            if (index < 0)
                index += values.Length;

            SetWeather(values[index]);
        }

        /// <summary>
        /// Derives weather from the current session seed. Use only where no layout is
        /// available (menus, standalone scenes); gameplay should call
        /// <see cref="ApplyLayoutWeather"/> with the generated layout's value.
        /// </summary>
        public void SetSeededWeather()
        {
            var rng = SeedManager.CreateRandom(CiycStream.Weather);
            var values = (WeatherType[])System.Enum.GetValues(typeof(WeatherType));
            SetWeather(values[rng.NextInt(0, values.Length)]);
        }

        private void ApplyWeatherImmediate(WeatherType weather)
        {
            switch (weather)
            {
                case WeatherType.Rain:
                    _targetFogDensity = rainFogDensity;
                    SetParticleActive(rainParticles, true);
                    SetParticleActive(outdoorMistParticles, false);
                    break;
                case WeatherType.Fog:
                    _targetFogDensity = fogFogDensity;
                    SetParticleActive(rainParticles, false);
                    SetParticleActive(outdoorMistParticles, true);
                    break;
                default:
                    _targetFogDensity = clearFogDensity;
                    SetParticleActive(rainParticles, false);
                    SetParticleActive(outdoorMistParticles, false);
                    break;
            }
        }

        private static void SetParticleActive(ParticleSystem system, bool active)
        {
            if (system == null)
                return;

            if (active)
            {
                if (!system.isPlaying)
                    system.Play();
            }
            else if (system.isPlaying)
            {
                system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            system.gameObject.SetActive(active);
        }

        public void EnsureOutdoorParticles(Vector3 position)
        {
            if (rainParticles == null)
                rainParticles = CreateRainSystem(position);
            if (outdoorMistParticles == null)
                outdoorMistParticles = CreateMistSystem(position);
        }

        private static ParticleSystem CreateRainSystem(Vector3 position)
        {
            var go = new GameObject("RainParticles");
            go.transform.position = position + Vector3.up * 8f;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startSpeed = 12f;
            main.startLifetime = 1.2f;
            main.startSize = 0.05f;
            main.maxParticles = 800;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 500f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(30f, 0.1f, 30f);

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            // Particles/Standard Unlit is built-in and always resolves, so the URP particle
            // shader after it was never reached and the weather drew magenta.
            var particleShader = Art.CiycShaders.Find(Art.CiycShaders.ParticlesUnlit);
            if (particleShader != null)
                renderer.material = new Material(particleShader);
            go.SetActive(false);
            return ps;
        }

        private static ParticleSystem CreateMistSystem(Vector3 position)
        {
            var go = new GameObject("OutdoorMistParticles");
            go.transform.position = position;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startSpeed = 0.4f;
            main.startLifetime = 4f;
            main.startSize = 2.5f;
            main.maxParticles = 120;
            main.startColor = new Color(0.85f, 0.88f, 0.92f, 0.25f);

            var emission = ps.emission;
            emission.rateOverTime = 18f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(40f, 4f, 40f);

            go.SetActive(false);
            return ps;
        }
    }
}
