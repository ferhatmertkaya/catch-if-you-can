using CatchIfYouCan.Weather;
using UnityEngine;

namespace CatchIfYouCan.Audio
{
    public enum WeatherAudioType
    {
        Clear,
        Rain,
        HeavyRain,
        Fog,
        Thunderstorm
    }

    public class WeatherAudioController : MonoBehaviour
    {
        [SerializeField] private Transform listener;
        [SerializeField] private string interiorRainId = "Weather.Rain.Interior";
        [SerializeField] private string exteriorRainId = "Weather.Rain.Exterior";
        [SerializeField] private string heavyRainId = "Weather.Rain.Heavy";
        [SerializeField] private string fogId = "Weather.Fog.Ambient";
        [SerializeField] private string thunderId = "Weather.Thunder.Strike";
        [SerializeField] private string clearId = "Weather.Clear.Ambient";

        private AudioSource _bed;
        private WeatherAudioType _current = WeatherAudioType.Clear;
        private WeatherType _lastSystemWeather = WeatherType.Clear;
        private float _thunderTimer;
        private bool _indoor;

        private void Awake()
        {
            _bed = gameObject.AddComponent<AudioSource>();
            _bed.loop = true;
            _bed.spatialBlend = 0f;
            _bed.volume = 0f;
        }

        private void Start()
        {
            if (listener == null)
            {
                var cam = Camera.main;
                listener = cam != null ? cam.transform : transform;
            }
            if (WeatherSystem.Instance != null)
                ApplyFromSystem(WeatherSystem.Instance.CurrentWeather);
        }

        private void Update()
        {
            if (WeatherSystem.Instance != null &&
                WeatherSystem.Instance.CurrentWeather != _lastSystemWeather)
            {
                _lastSystemWeather = WeatherSystem.Instance.CurrentWeather;
                ApplyFromSystem(_lastSystemWeather);
            }
            EvaluateIndoor();
            UpdateBedVolume();
            TickThunder();
        }

        public void SetWeather(WeatherAudioType type)
        {
            _current = type;
            string id = ResolveBedId();
            var clip = AudioEventResolve.ResolveClip(id);
            if (clip == null) return;
            if (_bed.clip != clip)
            {
                _bed.clip = clip;
                _bed.Play();
            }
            _thunderTimer = Random.Range(8f, 24f);
        }

        public void ApplyFromSystem(WeatherType systemWeather)
        {
            SetWeather(MapSystemWeather(systemWeather));
        }

        private WeatherAudioType MapSystemWeather(WeatherType type)
        {
            return type switch
            {
                WeatherType.Rain => Random.value > 0.5f ? WeatherAudioType.HeavyRain : WeatherAudioType.Rain,
                WeatherType.Fog => WeatherAudioType.Fog,
                _ => WeatherAudioType.Clear
            };
        }

        private string ResolveBedId()
        {
            if (_current == WeatherAudioType.Rain || _current == WeatherAudioType.HeavyRain)
                return _indoor ? interiorRainId : (_current == WeatherAudioType.HeavyRain ? heavyRainId : exteriorRainId);
            return _current switch
            {
                WeatherAudioType.Fog => fogId,
                WeatherAudioType.Thunderstorm => exteriorRainId,
                _ => clearId
            };
        }

        private void EvaluateIndoor()
        {
            if (listener == null) return;
            var zones = FindObjectsByType<RoomAudioZone>();
            _indoor = false;
            for (int i = 0; i < zones.Length; i++)
            {
                if (zones[i] != null && zones[i].ContainsPoint(listener.position))
                {
                    _indoor = true;
                    break;
                }
            }
        }

        private void UpdateBedVolume()
        {
            float target = _current switch
            {
                WeatherAudioType.Clear => 0.08f,
                WeatherAudioType.Fog => 0.18f,
                WeatherAudioType.Rain => _indoor ? 0.22f : 0.45f,
                WeatherAudioType.HeavyRain => _indoor ? 0.32f : 0.65f,
                WeatherAudioType.Thunderstorm => _indoor ? 0.35f : 0.7f,
                _ => 0.1f
            };
            _bed.volume = Mathf.Lerp(_bed.volume, target, Time.deltaTime * 1.5f);
        }

        private void TickThunder()
        {
            if (_current != WeatherAudioType.Thunderstorm && _current != WeatherAudioType.HeavyRain) return;
            _thunderTimer -= Time.deltaTime;
            if (_thunderTimer > 0f) return;
            _thunderTimer = Random.Range(10f, 35f);
            AudioManager.Instance?.PlayEvent(thunderId, listener != null ? listener.position : Vector3.zero, _indoor ? 0.45f : 0.85f);
        }
    }
}
