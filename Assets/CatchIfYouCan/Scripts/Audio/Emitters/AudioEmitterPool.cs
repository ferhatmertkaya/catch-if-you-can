using System.Collections.Generic;
using CatchIfYouCan.Graphics;
using CatchIfYouCan.Utilities;
using UnityEngine;

namespace CatchIfYouCan.Audio
{
    public class AudioEmitterPool : SingletonBehaviour<AudioEmitterPool>
    {
        [SerializeField] private Transform poolRoot;
        [SerializeField] private int prewarmCount = 8;

        private readonly Stack<AudioEmitter> _available = new Stack<AudioEmitter>();
        private readonly List<AudioEmitter> _active = new List<AudioEmitter>();
        private int _maxSimultaneous = 40;

        public int ActiveCount => _active.Count;
        public int MaxSimultaneous => _maxSimultaneous;

        protected override void Awake()
        {
            persist = true;
            base.Awake();
            if (poolRoot == null)
            {
                var root = new GameObject("AudioEmitterPool");
                root.transform.SetParent(transform, false);
                poolRoot = root.transform;
            }
            RefreshBudgetFromQuality();
            Prewarm();
        }

        public void RefreshBudgetFromQuality()
        {
            var quality = AudioQualityController.Instance;
            if (quality != null)
            {
                _maxSimultaneous = quality.MaxSimultaneousSources;
                return;
            }

            var graphics = GraphicsManager.Instance;
            _maxSimultaneous = graphics != null
                ? graphics.CurrentProfile switch
                {
                    GraphicsProfile.Low => 24,
                    GraphicsProfile.High => 56,
                    _ => 40
                }
                : 40;
        }

        public AudioEmitter Get()
        {
            if (_active.Count >= _maxSimultaneous)
                EvictLowestPriority();

            AudioEmitter emitter;
            if (_available.Count > 0)
            {
                emitter = _available.Pop();
            }
            else
            {
                emitter = CreateEmitter();
            }

            emitter.gameObject.SetActive(true);
            emitter.OnReleased = HandleReleased;
            _active.Add(emitter);
            return emitter;
        }

        public void Release(AudioEmitter emitter)
        {
            if (emitter == null) return;
            emitter.Stop();
            emitter.OnReleased = null;
            _active.Remove(emitter);
            emitter.gameObject.SetActive(false);
            emitter.transform.SetParent(poolRoot, false);
            _available.Push(emitter);
        }

        public void StopAll(bool includeCritical = false)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var emitter = _active[i];
                if (emitter == null) continue;
                if (!includeCritical && emitter.Priority >= AudioPriority.Critical) continue;
                emitter.Release();
            }
        }

        public void UpdateActiveDistances()
        {
            for (int i = 0; i < _active.Count; i++)
            {
                if (_active[i] != null)
                    _active[i].UpdateListenerDistance();
            }
        }

        public bool TryEvictForPriority(AudioPriority incomingPriority)
        {
            if (_active.Count < _maxSimultaneous)
                return true;

            if (incomingPriority >= AudioPriority.Critical)
            {
                EvictLowestPriority(incomingPriority);
                return _active.Count < _maxSimultaneous;
            }

            EvictLowestPriority(incomingPriority);
            return _active.Count < _maxSimultaneous;
        }

        private void EvictLowestPriority(AudioPriority incomingPriority = AudioPriority.Low)
        {
            AudioEmitter candidate = null;
            float bestScore = float.MaxValue;

            for (int i = 0; i < _active.Count; i++)
            {
                var emitter = _active[i];
                if (emitter == null || !emitter.IsPlaying)
                    continue;

                if (emitter.Priority >= AudioPriority.Critical && incomingPriority < AudioPriority.Critical)
                    continue;

                if (!emitter.CanInterrupt && emitter.Priority >= incomingPriority)
                    continue;

                float score = (int)emitter.Priority * 1000f + emitter.DistanceToListener;
                if (score < bestScore)
                {
                    bestScore = score;
                    candidate = emitter;
                }
            }

            candidate?.Release();
        }

        private void HandleReleased(AudioEmitter emitter)
        {
            Release(emitter);
        }

        private void Prewarm()
        {
            int count = Mathf.Min(prewarmCount, _maxSimultaneous);
            for (int i = 0; i < count; i++)
            {
                var emitter = CreateEmitter();
                emitter.gameObject.SetActive(false);
                _available.Push(emitter);
            }
        }

        private AudioEmitter CreateEmitter()
        {
            var go = new GameObject("PooledAudioEmitter");
            go.transform.SetParent(poolRoot, false);
            return go.AddComponent<AudioEmitter>();
        }
    }
}
