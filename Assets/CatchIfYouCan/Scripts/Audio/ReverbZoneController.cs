using System.Collections.Generic;
using UnityEngine;

namespace CatchIfYouCan.Audio
{
    [System.Serializable]
    public class ReverbProfile
    {
        public string Id;
        public float Room;
        public float RoomHF;
        public float DecayTime;
        public float Diffusion;
        public float Density;
    }

    public class ReverbZoneController : MonoBehaviour
    {
        [SerializeField] private Transform listener;
        [SerializeField] private float morphSpeed = 3f;

        private AudioReverbFilter _filter;
        private readonly Dictionary<string, ReverbProfile> _profiles = new Dictionary<string, ReverbProfile>();
        private ReverbProfile _current;
        private ReverbProfile _target;

        public string CurrentProfileId => _current?.Id ?? _target?.Id ?? "—";

        private void Awake()
        {
            BuildProfiles();
            EnsureFilter();
            _current = _profiles["Hallway"];
            _target = _current;
            ApplyImmediate(_current);
        }

        private void Start()
        {
            if (listener == null)
            {
                var cam = Camera.main;
                listener = cam != null ? cam.transform : transform;
            }
        }

        private void Update()
        {
            if (_target == null || _current == null) return;
            if (_current.Id == _target.Id) return;
            _current = LerpProfiles(_current, _target, morphSpeed * Time.deltaTime);
            ApplyImmediate(_current);
            if (ProfilesNearEqual(_current, _target))
                _current = _target;
        }

        public void SetProfile(string profileId)
        {
            if (string.IsNullOrEmpty(profileId)) return;
            if (!_profiles.TryGetValue(profileId, out var profile))
                profile = _profiles["Hallway"];
            _target = profile;
        }

        public void EvaluateFromRoom(RoomAudioZone zone)
        {
            if (zone == null) return;
            SetProfile(zone.ReverbProfileId);
        }

        private void EnsureFilter()
        {
            _filter = GetComponent<AudioReverbFilter>();
            if (_filter != null) return;
            var listenerObj = GetComponent<AudioListener>();
            if (listenerObj == null)
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    listenerObj = cam.GetComponent<AudioListener>();
                    if (listenerObj == null)
                        listenerObj = cam.gameObject.AddComponent<AudioListener>();
                    transform.SetParent(cam.transform, false);
                }
            }
            _filter = gameObject.AddComponent<AudioReverbFilter>();
            _filter.reverbPreset = AudioReverbPreset.Off;
        }

        private void ApplyImmediate(ReverbProfile p)
        {
            if (_filter == null || p == null) return;
            _filter.room = p.Room;
            _filter.roomHF = p.RoomHF;
            _filter.decayTime = p.DecayTime;
            _filter.diffusion = p.Diffusion;
            _filter.density = p.Density;
        }

        private void BuildProfiles()
        {
            Add("SmallBedroom", -800f, -1200f, 0.45f, 0.7f, 0.55f);
            Add("Hallway", -1200f, -800f, 0.65f, 0.75f, 0.65f);
            Add("Bathroom", -600f, -2000f, 0.35f, 0.55f, 0.5f);
            Add("Basement", -1500f, -600f, 1.1f, 0.8f, 0.75f);
            Add("Van", -400f, -2500f, 0.25f, 0.4f, 0.45f);
            Add("Exterior", -2000f, -500f, 0.2f, 0.3f, 0.35f);
        }

        private void Add(string id, float room, float hf, float decay, float diff, float dens)
        {
            _profiles[id] = new ReverbProfile
            {
                Id = id,
                Room = room,
                RoomHF = hf,
                DecayTime = decay,
                Diffusion = diff,
                Density = dens
            };
        }

        private static bool ProfilesNearEqual(ReverbProfile a, ReverbProfile b)
        {
            return Mathf.Abs(a.Room - b.Room) < 1f && Mathf.Abs(a.DecayTime - b.DecayTime) < 0.01f;
        }

        public static ReverbProfile LerpProfiles(ReverbProfile a, ReverbProfile b, float t)
        {
            t = Mathf.Clamp01(t);
            return new ReverbProfile
            {
                Id = b.Id,
                Room = Mathf.Lerp(a.Room, b.Room, t),
                RoomHF = Mathf.Lerp(a.RoomHF, b.RoomHF, t),
                DecayTime = Mathf.Lerp(a.DecayTime, b.DecayTime, t),
                Diffusion = Mathf.Lerp(a.Diffusion, b.Diffusion, t),
                Density = Mathf.Lerp(a.Density, b.Density, t)
            };
        }
    }
}
