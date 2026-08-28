using UnityEngine;
using CatchIfYouCan.Core;
using CatchIfYouCan.Utilities;

namespace CatchIfYouCan.Ghost
{
    public class GhostActivitySystem : SingletonBehaviour<GhostActivitySystem>
    {
        [SerializeField] private float riseRate = 8f;
        [SerializeField] private float fallRate = 4f;
        [SerializeField] private float eventBump = 6f;
        [SerializeField] private float huntDrain = 25f;

        private float _activity;

        public float Activity => _activity;
        public float Normalized => _activity / 100f;

        private void Update()
        {
            ApplyNaturalDecay();
            GameEvents.GhostActivityChanged(_activity);
        }

        public void AddActivity(float amount)
        {
            if (amount <= 0f) return;
            float curve = 1f + (_activity / 100f) * 0.5f;
            _activity = Mathf.Clamp(_activity + amount * curve, 0f, 100f);
        }

        public void RegisterGhostEvent(float intensity)
        {
            AddActivity(eventBump * Mathf.Clamp01(intensity));
        }

        public void RegisterPlayerNoise(float intensity)
        {
            AddActivity(intensity * 4f);
        }

        public void OnHuntEnded()
        {
            _activity = Mathf.Max(0f, _activity - huntDrain);
        }

        public bool ShouldConsiderHunt(float threshold)
        {
            return Normalized >= threshold;
        }

        private void ApplyNaturalDecay()
        {
            if (_activity <= 0f) return;
            float fall = fallRate * (1f + _activity / 100f);
            _activity = Mathf.Max(0f, _activity - fall * Time.deltaTime);
        }

        public void SetActivity(float value)
        {
            _activity = Mathf.Clamp(value, 0f, 100f);
        }
    }
}
