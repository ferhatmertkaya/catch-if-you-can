using UnityEngine;
using CatchIfYouCan.Core;

namespace CatchIfYouCan.Ghost
{
    [RequireComponent(typeof(GhostController))]
    public class HuntController : MonoBehaviour
    {
        [SerializeField] private float minDuration = 20f;
        [SerializeField] private float maxDuration = 45f;
        [SerializeField] private float preWarningDuration = 3f;
        [SerializeField] private float electronicsGlitchStrength = 0.8f;
        [SerializeField] private float lightFlickerStrength = 0.7f;
        [SerializeField] private AudioSource heartbeatSource;
        [SerializeField] private AudioClip heartbeatClip;
        [SerializeField] private AudioClip huntStingClip;

        private GhostController _ghost;
        private float _huntEndTime;
        private float _preWarningEndTime;
        private bool _isHunting;
        private bool _preWarningActive;
        private int _difficulty = 1;

        public bool IsHunting => _isHunting;
        public bool PreWarningActive => _preWarningActive;
        public float RemainingTime => _isHunting ? Mathf.Max(0f, _huntEndTime - Time.time) : 0f;

        private void Awake()
        {
            _ghost = GetComponent<GhostController>();
        }

        public void SetDifficulty(int difficulty)
        {
            _difficulty = Mathf.Clamp(difficulty, 1, 3);
        }

        public void ForceStartHunt()
        {
            if (_isHunting)
                return;

            _preWarningActive = false;
            BeginHuntPhase();
        }

        public bool TryStartHunt()
        {
            if (_isHunting || _preWarningActive) return false;

            _preWarningActive = true;
            _preWarningEndTime = Time.time + preWarningDuration;
            TriggerPreWarningEffects();
            CIYCLog.Info("Hunt pre-warning started.");
            return true;
        }

        public void ForceEndHunt()
        {
            if (!_isHunting && !_preWarningActive) return;
            EndHuntImmediate();
        }

        private void Update()
        {
            if (_preWarningActive && !_isHunting)
            {
                UpdatePreWarning();
                return;
            }

            if (_isHunting && Time.time >= _huntEndTime)
                EndHuntImmediate();
        }

        private void UpdatePreWarning()
        {
            PulsePreWarningEffects();

            if (Time.time >= _preWarningEndTime)
                BeginHuntPhase();
        }

        private void BeginHuntPhase()
        {
            _preWarningActive = false;
            _isHunting = true;

            float t = Mathf.InverseLerp(1, 3, _difficulty);
            float duration = Mathf.Lerp(maxDuration, minDuration, t);
            _huntEndTime = Time.time + duration;

            if (_ghost != null)
                _ghost.OnHuntStarted();

            GameEvents.HuntStarted();
            GameEvents.FearChanged(1f);

            if (heartbeatSource != null && heartbeatClip != null)
            {
                heartbeatSource.clip = heartbeatClip;
                heartbeatSource.loop = true;
                heartbeatSource.Play();
            }

            if (huntStingClip != null)
                AudioSource.PlayClipAtPoint(huntStingClip, transform.position);

            CIYCLog.Info($"Hunt started ({duration:F0}s).");
        }

        private void EndHuntImmediate()
        {
            _isHunting = false;
            _preWarningActive = false;

            if (heartbeatSource != null && heartbeatSource.isPlaying)
                heartbeatSource.Stop();

            StopPreWarningEffects();

            if (_ghost != null)
                _ghost.OnHuntEnded();

            GameEvents.HuntEnded();
            GameEvents.FearChanged(0.4f);

            if (GhostActivitySystem.Instance != null)
                GhostActivitySystem.Instance.OnHuntEnded();

            CIYCLog.Info("Hunt ended.");
        }

        private void TriggerPreWarningEffects()
        {
            GameEvents.BreakerChanged();
            GameEvents.FearChanged(0.75f);
        }

        private void PulsePreWarningEffects()
        {
            float pulse = Mathf.PingPong(Time.time * 4f, 1f);
            if (Random.value < 0.08f * electronicsGlitchStrength)
                GameEvents.BreakerChanged();

            if (Random.value < 0.05f * lightFlickerStrength * pulse)
            {
                var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
                for (int i = 0; i < lights.Length && i < 3; i++)
                {
                    if (lights[i].type == LightType.Point || lights[i].type == LightType.Spot)
                        lights[i].intensity *= Random.Range(0.2f, 1.4f);
                }
            }
        }

        private void StopPreWarningEffects()
        {
            GameEvents.FearChanged(0.2f);
        }
    }
}
