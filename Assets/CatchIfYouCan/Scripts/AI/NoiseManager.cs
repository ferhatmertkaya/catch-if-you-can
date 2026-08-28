using System.Collections.Generic;
using UnityEngine;
using CatchIfYouCan.Core;
using CatchIfYouCan.Utilities;

namespace CatchIfYouCan.AI
{
    public class NoiseManager : SingletonBehaviour<NoiseManager>
    {
        [SerializeField] private float decayHalfLife = 3f;
        [SerializeField] private int maxEvents = 32;
        [SerializeField] private float minIntensity = 0.05f;

        private readonly List<NoiseEvent> _events = new List<NoiseEvent>();
        private bool _broadcasting;

        protected override void Awake()
        {
            base.Awake();
            GameEvents.OnNoiseGenerated += RegisterExternalNoise;
        }

        protected override void OnDestroy()
        {
            GameEvents.OnNoiseGenerated -= RegisterExternalNoise;
            base.OnDestroy();
        }

        private void Update()
        {
            PruneExpired();
        }

        public void RegisterNoise(Vector3 position, float intensity, string sourceTag = "")
        {
            intensity = Mathf.Clamp01(intensity);
            if (intensity < minIntensity) return;

            _events.Add(new NoiseEvent(position, intensity, Time.time, sourceTag));
            _broadcasting = true;
            GameEvents.NoiseGenerated(intensity, position);
            _broadcasting = false;

            while (_events.Count > maxEvents)
                _events.RemoveAt(0);
        }

        private void RegisterExternalNoise(float intensity, Vector3 position)
        {
            if (_broadcasting) return;

            if (_events.Count > 0)
            {
                var last = _events[_events.Count - 1];
                if (Vector3.Distance(last.Position, position) < 0.25f &&
                    Mathf.Abs(last.Timestamp - Time.time) < 0.05f)
                    return;
            }

            _events.Add(new NoiseEvent(position, intensity, Time.time, "external"));
            while (_events.Count > maxEvents)
                _events.RemoveAt(0);
        }

        public bool TryGetLoudestNoise(Vector3 listenerPosition, float sensitivity, float maxRange,
            out NoiseEvent result)
        {
            result = default;
            float bestScore = 0f;
            float now = Time.time;

            for (int i = 0; i < _events.Count; i++)
            {
                var e = _events[i];
                float decayed = e.DecayedIntensity(now, decayHalfLife);
                if (decayed < minIntensity) continue;

                float dist = Vector3.Distance(listenerPosition, e.Position);
                if (dist > maxRange) continue;

                float rangeFalloff = 1f - (dist / maxRange);
                float score = decayed * rangeFalloff * sensitivity;
                if (score > bestScore)
                {
                    bestScore = score;
                    result = e;
                }
            }

            return bestScore > minIntensity;
        }

        public IReadOnlyList<NoiseEvent> GetActiveEvents() => _events;

        private void PruneExpired()
        {
            float now = Time.time;
            for (int i = _events.Count - 1; i >= 0; i--)
            {
                if (_events[i].DecayedIntensity(now, decayHalfLife) < minIntensity)
                    _events.RemoveAt(i);
            }
        }
    }
}
