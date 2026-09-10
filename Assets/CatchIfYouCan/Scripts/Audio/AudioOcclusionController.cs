using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace CatchIfYouCan.Audio
{
    public class AudioOcclusionController : MonoBehaviour
    {
        [SerializeField] private Transform listener;
        [SerializeField] private int sourcesPerFrame = 6;
        [SerializeField] private float wallAttenuationDb = -14f;
        [SerializeField] private float updateInterval = 0.08f;

        private readonly List<AudioSource> _tracked = new List<AudioSource>();
        private readonly Dictionary<AudioSource, AudioLowPassFilter> _filters = new Dictionary<AudioSource, AudioLowPassFilter>();
        private int _scanIndex;
        private float _timer;
        private RoomAudioZone _listenerZone;

        public int TrackedSourceCount => _tracked.Count;
        public string ListenerZoneName => _listenerZone != null ? _listenerZone.name : null;

        private void Start()
        {
            RefreshListener();
        }

        private bool _listenerAutoResolved;

        /// <summary>
        /// Finds the ear to measure from, and keeps looking until it finds the real one.
        ///
        /// This used to latch Camera.main once in Start. These controllers are installed
        /// before the player is spawned, so that latch bound the audio to whatever camera
        /// the scene carried at boot - in the lobby, the menu camera - and never let go.
        /// A listener assigned by hand in the inspector is left alone.
        /// </summary>
        private void RefreshListener()
        {
            if (listener != null && !_listenerAutoResolved)
                return;

            var resolved = Core.LocalPlayerService.ResolveListenerTransform();
            if (resolved != null)
            {
                listener = resolved;
                _listenerAutoResolved = true;
                return;
            }

            if (listener == null)
            {
                listener = transform;
                _listenerAutoResolved = true;
            }
        }

        private void Update()
        {
            // Keeps looking until a player registers a real listener; after that
            // ResolveListenerTransform returns it and this settles.
            if (_listenerAutoResolved)
                RefreshListener();

            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = updateInterval;
            RefreshListenerZone();
            StaggeredOcclusionPass();
        }

        public void RegisterSource(AudioSource source)
        {
            if (source == null || _tracked.Contains(source)) return;
            _tracked.Add(source);
        }

        public void UnregisterSource(AudioSource source)
        {
            _tracked.Remove(source);
            if (_filters.TryGetValue(source, out var filter) && filter != null)
                Destroy(filter);
            _filters.Remove(source);
        }

        private void RefreshListenerZone()
        {
            _listenerZone = null;
            if (listener == null) return;
            var zones = FindObjectsByType<RoomAudioZone>();
            for (int i = 0; i < zones.Length; i++)
            {
                if (zones[i] != null && zones[i].ContainsPoint(listener.position))
                {
                    _listenerZone = zones[i];
                    break;
                }
            }
        }

        private void StaggeredOcclusionPass()
        {
            if (_tracked.Count == 0) return;
            int count = Mathf.Min(sourcesPerFrame, _tracked.Count);
            for (int i = 0; i < count; i++)
            {
                _scanIndex = (_scanIndex + 1) % _tracked.Count;
                var src = _tracked[_scanIndex];
                if (src == null)
                {
                    _tracked.RemoveAt(_scanIndex);
                    continue;
                }
                ApplyOcclusion(src);
            }
        }

        private void ApplyOcclusion(AudioSource source)
        {
            float attenDb = 0f;
            float lpf = 22000f;

            if (_listenerZone == null)
            {
                attenDb = wallAttenuationDb;
                lpf = 2500f;
            }
            else
            {
                var sourceZone = FindZoneForPoint(source.transform.position);
                if (sourceZone == null || sourceZone == _listenerZone)
                {
                    attenDb = 0f;
                    lpf = 22000f;
                }
                else
                {
                    var portalAtten = FindPortalAttenuation(_listenerZone, sourceZone);
                    if (portalAtten.HasValue)
                    {
                        attenDb = portalAtten.Value;
                        lpf = FindPortalCutoff(_listenerZone, sourceZone);
                    }
                    else
                    {
                        attenDb = wallAttenuationDb;
                        lpf = 1800f;
                    }
                }
            }

            float linear = DbToLinear(attenDb);
            source.volume = Mathf.Clamp01(source.volume); // preserve designer volume; apply via filter only
            var lpfFilter = GetOrCreateFilter(source);
            lpfFilter.cutoffFrequency = lpf;
            source.spatialBlend = 1f;
            source.SetCustomCurve(AudioSourceCurveType.CustomRolloff,
                AnimationCurve.Linear(0f, linear, source.maxDistance, linear * 0.5f));
        }

        private float? FindPortalAttenuation(RoomAudioZone listenerZone, RoomAudioZone sourceZone)
        {
            var portals = FindObjectsByType<AudioPortal>();
            for (int i = 0; i < portals.Length; i++)
            {
                var p = portals[i];
                if (p == null) continue;
                bool listenerInA = p.RoomA == listenerZone;
                bool listenerInB = p.RoomB == listenerZone;
                bool sourceInA = p.RoomA == sourceZone;
                bool sourceInB = p.RoomB == sourceZone;
                if ((listenerInA || listenerInB) && (sourceInA || sourceInB))
                    return p.GetOcclusionAttenuationDb(listenerInA, sourceInA);
            }
            return null;
        }

        private float FindPortalCutoff(RoomAudioZone listenerZone, RoomAudioZone sourceZone)
        {
            var portals = FindObjectsByType<AudioPortal>();
            for (int i = 0; i < portals.Length; i++)
            {
                var p = portals[i];
                if (p == null) continue;
                if ((p.RoomA == listenerZone || p.RoomB == listenerZone) &&
                    (p.RoomA == sourceZone || p.RoomB == sourceZone))
                    return p.GetLowPassCutoff();
            }
            return 1800f;
        }

        private RoomAudioZone FindZoneForPoint(Vector3 point)
        {
            var zones = FindObjectsByType<RoomAudioZone>();
            for (int i = 0; i < zones.Length; i++)
            {
                if (zones[i] != null && zones[i].ContainsPoint(point))
                    return zones[i];
            }
            return null;
        }

        private AudioLowPassFilter GetOrCreateFilter(AudioSource source)
        {
            if (_filters.TryGetValue(source, out var filter) && filter != null)
                return filter;
            filter = source.gameObject.GetComponent<AudioLowPassFilter>();
            if (filter == null)
                filter = source.gameObject.AddComponent<AudioLowPassFilter>();
            _filters[source] = filter;
            return filter;
        }

        private static float DbToLinear(float db)
        {
            if (db <= -80f) return 0f;
            return Mathf.Pow(10f, db / 20f);
        }
    }
}
