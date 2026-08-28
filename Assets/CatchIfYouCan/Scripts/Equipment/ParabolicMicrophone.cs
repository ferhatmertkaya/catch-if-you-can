using UnityEngine;
using CatchIfYouCan.Core;
using CatchIfYouCan.Evidence;

namespace CatchIfYouCan.Equipment
{
    public class ParabolicMicrophone : EquipmentBase
    {
        [SerializeField] private float maxRange = 18f;
        [SerializeField] private float coneAngle = 22f;
        [SerializeField] private float signalDecaySpeed = 2f;
        [SerializeField] private AudioSource loopSource;

        private float _signalStrength;
        private Vector3 _lastNoisePosition;

        public float SignalStrength => _signalStrength;

        protected override float GetInterferenceMultiplier() => 0.3f;

        protected override void Awake()
        {
            base.Awake();
            GameEvents.OnNoiseGenerated += HandleNoise;
        }

        private void OnDestroy()
        {
            GameEvents.OnNoiseGenerated -= HandleNoise;
        }

        protected override void OnEquipped()
        {
            SetDeviceActive(true);
        }

        protected override void TickEquipped(float deltaTime)
        {
            if (!DeviceActive)
            {
                _signalStrength = 0f;
                return;
            }

            _signalStrength = Mathf.MoveTowards(_signalStrength, 0f, signalDecaySpeed * deltaTime);

            if (loopSource != null)
            {
                loopSource.volume = _signalStrength;
                if (_signalStrength > 0.05f && !loopSource.isPlaying)
                    loopSource.Play();
                else if (_signalStrength <= 0.05f && loopSource.isPlaying)
                    loopSource.Stop();
            }

            if (_signalStrength >= 0.55f && Core.ServiceLocator.TryGet<EvidenceManager>(out var manager))
                manager.RegisterEvidence(EvidenceType.ParabolicAnomaly);
        }

        private void HandleNoise(float intensity, Vector3 position)
        {
            if (!DeviceActive || HandAnchor == null)
                return;

            _lastNoisePosition = position;
            var origin = HandAnchor.position;
            var toNoise = (position - origin).normalized;
            float distance = Vector3.Distance(origin, position);
            if (distance > maxRange)
                return;

            float angle = Vector3.Angle(HandAnchor.forward, toNoise);
            if (angle > coneAngle)
                return;

            float distanceFactor = 1f - (distance / maxRange);
            float angleFactor = 1f - (angle / coneAngle);
            float sample = intensity * distanceFactor * angleFactor;
            _signalStrength = Mathf.Clamp01(Mathf.Max(_signalStrength, sample));
        }
    }
}
