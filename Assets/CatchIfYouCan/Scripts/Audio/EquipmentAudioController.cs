using CatchIfYouCan.Core;
using CatchIfYouCan.Equipment;
using UnityEngine;

namespace CatchIfYouCan.Audio
{
    public class EquipmentAudioController : MonoBehaviour
    {
        public static EquipmentAudioController Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            GameEvents.OnEquipmentChanged += HandleEquipmentChanged;
        }

        private void OnDisable()
        {
            GameEvents.OnEquipmentChanged -= HandleEquipmentChanged;
        }

        public void RouteEquipEvent(string eventId, Vector3? pos = null, float scale = 1f)
        {
            AudioManager.Instance?.PlayEvent(eventId, pos, scale);
        }

        public void AttachFeedbackToEquipment(EquipmentBase equipment)
        {
            if (equipment == null) return;
            AttachHelper<EmfAudioFeedback>(equipment);
            AttachHelper<EvpDeviceAudio>(equipment);
            AttachHelper<CameraDeviceAudio>(equipment);
            AttachHelper<UvDeviceAudio>(equipment);
            AttachHelper<ThermometerDeviceAudio>(equipment);
            AttachHelper<ParabolicAudioProcessor>(equipment);
            AttachHelper<SpectralGridAudio>(equipment);
            AttachHelper<RelicAudio>(equipment);
            AttachHelper<SaltAudio>(equipment);
        }

        private static void AttachHelper<T>(EquipmentBase equipment) where T : Component
        {
            if (equipment.GetComponent<T>() != null) return;
            if (!ShouldAttach<T>(equipment)) return;
            equipment.gameObject.AddComponent<T>();
        }

        private static bool ShouldAttach<T>(EquipmentBase equipment)
        {
            if (equipment is EMFDetector && typeof(T) == typeof(EmfAudioFeedback)) return true;
            if (equipment is EVPRecorder && typeof(T) == typeof(EvpDeviceAudio)) return true;
            if (equipment is PhotoCameraEquipment && typeof(T) == typeof(CameraDeviceAudio)) return true;
            if (equipment is VideoCameraEquipment && typeof(T) == typeof(CameraDeviceAudio)) return true;
            if (equipment is UVLight && typeof(T) == typeof(UvDeviceAudio)) return true;
            if (equipment is ThermometerEquipment && typeof(T) == typeof(ThermometerDeviceAudio)) return true;
            if (equipment is ParabolicMicrophone && typeof(T) == typeof(ParabolicAudioProcessor)) return true;
            if (equipment is SpectralGridProjector && typeof(T) == typeof(SpectralGridAudio)) return true;
            if (equipment is WardingRelic && typeof(T) == typeof(RelicAudio)) return true;
            if (equipment is SaltEquipment && typeof(T) == typeof(SaltAudio)) return true;
            return false;
        }

        private void HandleEquipmentChanged()
        {
            WireAllEquipment();
        }

        public void WireAllEquipment()
        {
            var all = FindObjectsByType<EquipmentBase>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
                AttachFeedbackToEquipment(all[i]);
        }
    }

    public class EmfAudioFeedback : MonoBehaviour
    {
        private EMFDetector _emf;
        private int _lastLevel;
        private float _beepTimer;

        private void Awake() => _emf = GetComponent<EMFDetector>();

        private void Update()
        {
            if (_emf == null || !_emf.IsActive) return;
            int level = _emf.CurrentLevel;
            TickTempo(level);
            if (level != _lastLevel)
            {
                _lastLevel = level;
                if (level > 0)
                    PlayLevelBeep(level);
            }
        }

        private void TickTempo(int level)
        {
            if (level <= 0) return;
            _beepTimer -= Time.deltaTime;
            float interval = Mathf.Lerp(1.1f, 0.08f, level / 5f);
            if (_beepTimer > 0f) return;
            _beepTimer = interval;
            PlayLevelBeep(level);
        }

        private void PlayLevelBeep(int level)
        {
            string id = $"Equip.EMF.Beep.L{Mathf.Clamp(level, 1, 5)}";
            EquipmentAudioController.Instance?.RouteEquipEvent(id, transform.position, 0.35f + level * 0.08f);
        }
    }

    public class EvpDeviceAudio : MonoBehaviour
    {
        private EVPRecorder _evp;

        private void Awake() => _evp = GetComponent<EVPRecorder>();

        public void PlayQuestion() => EquipmentAudioController.Instance?.RouteEquipEvent("Equip.EVP.Question", transform.position, 0.5f);
        public void PlayResponse() => EquipmentAudioController.Instance?.RouteEquipEvent("Equip.EVP.Response", transform.position, 0.45f);
        public void PlayStaticBurst() => EquipmentAudioController.Instance?.RouteEquipEvent("Equip.EVP.Static", transform.position, 0.4f);
    }

    public class CameraDeviceAudio : MonoBehaviour
    {
        public void PlayShutter() => EquipmentAudioController.Instance?.RouteEquipEvent("Equip.Camera.Shutter", transform.position, 0.65f);
        public void PlayFocus() => EquipmentAudioController.Instance?.RouteEquipEvent("Equip.Camera.Focus", transform.position, 0.35f);
        public void PlayNightVisionHum() => EquipmentAudioController.Instance?.RouteEquipEvent("Equip.Camera.NV.Hum", transform.position, 0.25f);
    }

    public class UvDeviceAudio : MonoBehaviour
    {
        private UVLight _uv;
        private bool _wasActive;

        private void Awake() => _uv = GetComponent<UVLight>();

        private void Update()
        {
            if (_uv == null) return;
            bool active = _uv.IsActive;
            if (active && !_wasActive)
                EquipmentAudioController.Instance?.RouteEquipEvent("Equip.UV.Activate", transform.position, 0.4f);
            _wasActive = active;
        }
    }

    public class ThermometerDeviceAudio : MonoBehaviour
    {
        private float _timer;

        private void Update()
        {
            var therm = GetComponent<ThermometerEquipment>();
            if (therm == null || !therm.IsActive) return;
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = 1.2f;
            EquipmentAudioController.Instance?.RouteEquipEvent("Equip.Thermo.Beep", transform.position, 0.3f);
        }
    }

    public class ParabolicAudioProcessor : MonoBehaviour
    {
        [SerializeField] private float coneAngle = 35f;
        [SerializeField] private float emphasisDb = 4f;

        private ParabolicMicrophone _mic;
        private AudioLowPassFilter _listenerFilter;

        private void Awake()
        {
            _mic = GetComponent<ParabolicMicrophone>();
            var cam = Camera.main;
            if (cam != null)
                _listenerFilter = cam.GetComponent<AudioLowPassFilter>();
        }

        private void LateUpdate()
        {
            if (_mic == null || !_mic.IsActive || _listenerFilter == null) return;
            Vector3 forward = transform.forward;
            Vector3 toSource = (transform.position - Camera.main.transform.position).normalized;
            float angle = Vector3.Angle(Camera.main.transform.forward, forward);
            bool inCone = angle <= coneAngle;
            _listenerFilter.cutoffFrequency = inCone ? 18000f : 6000f;
        }
    }

    public class SpectralGridAudio : MonoBehaviour
    {
        private SpectralGridProjector _grid;
        private float _pulse;

        private void Awake() => _grid = GetComponent<SpectralGridProjector>();

        private void Update()
        {
            if (_grid == null || !_grid.IsActive) return;
            _pulse -= Time.deltaTime;
            if (_pulse > 0f) return;
            _pulse = 0.6f;
            EquipmentAudioController.Instance?.RouteEquipEvent("Equip.SpectralGrid.Pulse", transform.position, 0.35f);
        }
    }

    public class RelicAudio : MonoBehaviour
    {
        private WardingRelic _relic;

        private void Awake() => _relic = GetComponent<WardingRelic>();

        private void Update()
        {
            if (_relic == null || !_relic.IsActive) return;
            if (Random.value < 0.002f)
                EquipmentAudioController.Instance?.RouteEquipEvent("Equip.Relic.Resonate", transform.position, 0.4f);
        }
    }

    public class SaltAudio : MonoBehaviour
    {
        public void PlayPour() => EquipmentAudioController.Instance?.RouteEquipEvent("Equip.Salt.Pour", transform.position, 0.55f);
        public void PlayCrackle() => EquipmentAudioController.Instance?.RouteEquipEvent("Equip.Salt.Crackle", transform.position, 0.45f);
    }
}
