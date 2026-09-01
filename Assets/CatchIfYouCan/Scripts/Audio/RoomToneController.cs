using UnityEngine;

namespace CatchIfYouCan.Audio
{
    public class RoomToneController : MonoBehaviour
    {
        [SerializeField] private Transform listener;
        [SerializeField] private float crossfadeDuration = 2.5f;
        [SerializeField] private float checkInterval = 0.35f;

        private RoomAudioZone _currentZone;
        private string _currentToneId;
        private float _crossfade;
        private float _checkTimer;
        private AudioSource _toneA;
        private AudioSource _toneB;
        private bool _useA = true;

        public string CurrentToneId => _currentToneId ?? "—";

        private void Awake()
        {
            _toneA = CreateToneSource("RoomToneA");
            _toneB = CreateToneSource("RoomToneB");
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
            _checkTimer -= Time.deltaTime;
            if (_checkTimer > 0f) return;
            _checkTimer = checkInterval;
            EvaluateZone();
            UpdateCrossfade();
        }

        public void ForceZone(RoomAudioZone zone)
        {
            if (zone == null || zone == _currentZone) return;
            BeginCrossfade(zone.RoomToneEventId, zone);
        }

        private void EvaluateZone()
        {
            if (listener == null) return;
            var zones = FindObjectsByType<RoomAudioZone>();
            RoomAudioZone best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < zones.Length; i++)
            {
                var z = zones[i];
                if (z == null || !z.ContainsPoint(listener.position)) continue;
                float d = Vector3.Distance(listener.position, z.transform.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = z;
                }
            }

            if (best == null || best == _currentZone) return;
            BeginCrossfade(best.RoomToneEventId, best);
            var reverb = FindAnyObjectByType<ReverbZoneController>();
            reverb?.EvaluateFromRoom(best);
        }

        private void BeginCrossfade(string toneId, RoomAudioZone zone)
        {
            if (string.IsNullOrEmpty(toneId)) return;
            _currentZone = zone;
            _currentToneId = toneId;
            _crossfade = 0f;

            var incoming = _useA ? _toneB : _toneA;
            var clip = AudioEventResolve.ResolveClip(toneId);
            if (clip == null) return;

            incoming.clip = clip;
            incoming.volume = 0f;
            incoming.loop = true;
            incoming.Play();
            _useA = !_useA;
        }

        private void UpdateCrossfade()
        {
            if (_crossfade >= 1f && _toneA.isPlaying == _toneB.isPlaying) return;
            _crossfade = Mathf.Clamp01(_crossfade + Time.deltaTime / crossfadeDuration);
            var active = _useA ? _toneA : _toneB;
            var fading = _useA ? _toneB : _toneA;
            active.volume = _crossfade * 0.35f;
            fading.volume = (1f - _crossfade) * 0.35f;
            if (_crossfade >= 1f && fading.isPlaying)
                fading.Stop();
        }

        public void DropRoomTone(float scale = 0.4f)
        {
            var active = _useA ? _toneA : _toneB;
            active.volume *= scale;
        }

        private static AudioSource CreateToneSource(string name)
        {
            var go = new GameObject(name);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = true;
            src.spatialBlend = 0f;
            src.volume = 0f;
            return src;
        }
    }
}
