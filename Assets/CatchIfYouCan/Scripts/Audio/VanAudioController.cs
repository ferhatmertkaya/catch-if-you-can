using CatchIfYouCan.Procedural;
using UnityEngine;

namespace CatchIfYouCan.Audio
{
    public class VanAudioController : MonoBehaviour
    {
        [SerializeField] private string humId = "Env.Van.ElectricalHum";
        [SerializeField] private string fanId = "Env.Van.Fan";
        [SerializeField] private string rainMetalId = "Weather.Rain.Metal";
        [SerializeField] private string doorOpenExteriorId = "Env.Van.DoorExterior";
        [SerializeField] private float interiorVolume = 0.35f;

        private AudioSource _hum;
        private AudioSource _fan;
        private AudioSource _rain;
        private Transform _exitDoor;
        private bool _doorOpen;
        private WeatherAudioController _weather;

        public void Initialize(VanBuildResult van, WeatherAudioController weather)
        {
            _weather = weather;
            if (van?.Root == null) return;
            transform.SetParent(van.Root.transform, false);
            _exitDoor = van.ExitDoor;
            SetupSources();
            PlayInteriorLoops();
        }

        private void SetupSources()
        {
            _hum = CreateLoopSource("VanHum");
            _fan = CreateLoopSource("VanFan");
            _rain = CreateLoopSource("VanRain");
            _hum.transform.SetParent(transform, false);
            _fan.transform.SetParent(transform, false);
            _rain.transform.SetParent(transform, false);
        }

        private void PlayInteriorLoops()
        {
            PlayLoop(_hum, humId, interiorVolume * 0.6f);
            PlayLoop(_fan, fanId, interiorVolume * 0.45f);
        }

        private void Update()
        {
            UpdateRainOnMetal();
            CheckDoorState();
        }

        private void UpdateRainOnMetal()
        {
            if (_weather == null) return;
            bool raining = _weather.enabled;
            float target = raining && !_doorOpen ? interiorVolume * 0.25f : 0f;
            _rain.volume = Mathf.Lerp(_rain.volume, target, Time.deltaTime * 2f);
            if (_rain.clip == null && target > 0.01f)
                PlayLoop(_rain, rainMetalId, 0f);
        }

        private void CheckDoorState()
        {
            if (_exitDoor == null) return;
            var door = _exitDoor.GetComponentInParent<CatchIfYouCan.Interaction.InteractiveDoor>();
            if (door == null) return;
            bool open = door.IsOpen;
            if (open && !_doorOpen)
                AudioManager.Instance?.PlayEvent(doorOpenExteriorId, _exitDoor.position, 0.55f);
            _doorOpen = open;
        }

        private void PlayLoop(AudioSource src, string eventId, float volume)
        {
            var clip = AudioEventResolve.ResolveClip(eventId);
            if (clip == null) return;
            src.clip = clip;
            src.volume = volume;
            src.loop = true;
            src.Play();
        }

        private static AudioSource CreateLoopSource(string name)
        {
            var go = new GameObject(name);
            var src = go.AddComponent<AudioSource>();
            src.loop = true;
            src.spatialBlend = 0f;
            src.playOnAwake = false;
            return src;
        }
    }
}
