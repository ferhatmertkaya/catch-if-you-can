using UnityEngine;
using CatchIfYouCan.Core;
using CatchIfYouCan.Evidence;
using CatchIfYouCan.Ghost;
using CatchIfYouCan.Utilities;

namespace CatchIfYouCan.Equipment
{
    public class ThermometerEquipment : EquipmentBase
    {
        [SerializeField] private float normalMin = 15f;
        [SerializeField] private float normalMax = 22f;
        [SerializeField] private float coldMin = 5f;
        [SerializeField] private float coldMax = 10f;
        [SerializeField] private float freezingMax = 0f;
        [SerializeField] private float lerpSpeed = 1.5f;
        [SerializeField] private float openDoorPenalty = 1.25f;
        [SerializeField] private float evidenceFreezingThreshold = 1f;

        private float _displayTemperature = 20f;
        private int _openDoorCount;

        public float DisplayTemperature => _displayTemperature;

        protected override void Awake()
        {
            base.Awake();
            _displayTemperature = Random.Range(normalMin, normalMax);
            GameEvents.OnDoorOpened += HandleDoorOpened;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            GameEvents.OnDoorOpened -= HandleDoorOpened;
        }

        protected override void OnEquipped()
        {
            SetDeviceActive(true);
        }

        protected override void TickEquipped(float deltaTime)
        {
            if (!DeviceActive)
                return;

            float target = ComputeTargetTemperature();
            _displayTemperature = MathUtil.SmoothApproach(_displayTemperature, target, lerpSpeed, deltaTime);

            if (_displayTemperature <= evidenceFreezingThreshold)
            {
                // How far below freezing, not merely that it is. A reading hovering on the
                // threshold and one well under it are different observations.
                float depth = Mathf.InverseLerp(evidenceFreezingThreshold,
                                                evidenceFreezingThreshold - 5f,
                                                _displayTemperature);
                Observe(EvidenceType.FreezingTemperature, Mathf.Max(0.3f, depth));
            }
        }

        private void HandleDoorOpened()
        {
            _openDoorCount++;
        }

        private float ComputeTargetTemperature()
        {
            Vector3 probe = HandAnchor != null ? HandAnchor.position : transform.position;
            float baseTemp = Random.Range(normalMin, normalMax) - _openDoorCount * openDoorPenalty;

            var ghost = FindAnyObjectByType<GhostController>();
            if (ghost == null)
                return baseTemp;

            var evidence = ghost.GetComponent<GhostEvidenceManager>();
            if (evidence != null)
                baseTemp += evidence.TemperatureOffset;

            float distance = Vector3.Distance(probe, ghost.transform.position);
            if (distance <= 3f)
                return Random.Range(freezingMax - 3f, freezingMax);
            if (distance <= 8f)
                return Random.Range(coldMin, coldMax);

            return baseTemp;
        }
    }
}
