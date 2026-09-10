using CatchIfYouCan.Core;
using CatchIfYouCan.Evidence;
using CatchIfYouCan.Ghost;
using CatchIfYouCan.Utilities;
using UnityEngine;

namespace CatchIfYouCan.Equipment
{
    /// <summary>
    /// The thermometer: a number in Celsius that lags reality, because a real one does.
    ///
    /// <para>
    /// A held item like any other now. Its readings are also a measurement rather than a die
    /// roll: the ambient used to be re-randomised every frame, so the value the needle was
    /// chasing jumped several degrees sixty times a second and the only reason it looked stable
    /// was the smoothing on top of it.
    /// </para>
    /// </summary>
    [AddComponentMenu("Catch If You Can/Thermometer")]
    public class ThermometerEquipment : HeldEquipmentBase
    {
        [Header("Ambient")]
        [SerializeField] private float normalMin = 15f;
        [SerializeField] private float normalMax = 22f;

        [Tooltip("How much colder the house gets per door left open, in Celsius.")]
        [SerializeField] private float openDoorPenalty = 1.25f;

        [Tooltip("Coldest the house itself can get from open doors, in Celsius. Without a floor " +
                 "the penalty is unbounded, and twenty open doors put the ambient at -25 - " +
                 "which reads as a ghost that is not there.")]
        [SerializeField] private float ambientFloor = 8f;

        [Header("Ghost")]
        [SerializeField] private float coldMin = 5f;
        [SerializeField] private float coldMax = 10f;
        [SerializeField] private float freezingMax = 0f;

        [Tooltip("Within this, the reading goes to freezing.")]
        [SerializeField, Min(0.5f)] private float freezingDistance = 3f;

        [Tooltip("Within this, the reading goes cold.")]
        [SerializeField, Min(1f)] private float coldDistance = 8f;

        [Header("Sensor")]
        [Tooltip("Seconds between readings. A thermometer that resolves faster than this is " +
                 "not a thermometer.")]
        [SerializeField, Min(0.1f)] private float sampleInterval = 0.5f;

        [Tooltip("How fast the display chases the reading. This is the sensor lag.")]
        [SerializeField, Min(0.1f)] private float lerpSpeed = 1.5f;

        [Tooltip("How much the reading wanders between samples, in Celsius. Sensor noise, not " +
                 "a fresh random temperature.")]
        [SerializeField, Min(0f)] private float sensorNoise = 0.35f;

        [Header("Evidence")]
        [SerializeField] private float evidenceFreezingThreshold = 1f;

        private float _displayTemperature = 20f;
        private float _targetTemperature = 20f;
        private float _ambient = 20f;
        private float _sampleTimer;
        private int _openDoorCount;

        /// <summary>What the display currently reads, in Celsius.</summary>
        public float DisplayTemperature => _displayTemperature;

        /// <summary>What it is heading towards, for a lab readout.</summary>
        public float TargetTemperature => _targetTemperature;

        /// <summary>The reading, in Celsius, to one decimal - which is what a display shows.</summary>
        public override string HudReadout => _displayTemperature.ToString("F1") + "\u00B0C";

        protected override float GetInterferenceMultiplier() => 0.2f;

        /// <summary>Switching it on does not wear it out.</summary>
        protected override float DurabilityLossPerUse => 0f;

        protected override void Awake()
        {
            base.Awake();

            // The house's own temperature, decided once. It is a property of the house, not a
            // fresh number every frame.
            _ambient = Random.Range(normalMin, normalMax);
            _displayTemperature = _ambient;
            _targetTemperature = _ambient;

            GameEvents.OnDoorOpened += HandleDoorOpened;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            GameEvents.OnDoorOpened -= HandleDoorOpened;
        }

        protected override void OnUse() => SetDeviceActive(!DeviceActive);

        protected override void TickEquipped(float deltaTime)
        {
            if (!DeviceActive)
                return;

            _sampleTimer -= deltaTime;
            if (_sampleTimer <= 0f)
            {
                _sampleTimer = sampleInterval;
                _targetTemperature = Sample();
            }

            // The lag is the whole character of the instrument: it is why walking into a cold
            // spot shows you the number falling rather than the number having fallen.
            _displayTemperature = MathUtil.SmoothApproach(
                _displayTemperature, _targetTemperature, lerpSpeed, deltaTime);

            if (_displayTemperature <= evidenceFreezingThreshold)
            {
                // How far below freezing, not merely that it is. The validator holds this to a
                // three second dwell and to the ghost's own profile, so one cold sample proves
                // nothing - which is what one used to prove.
                float depth = Mathf.InverseLerp(evidenceFreezingThreshold,
                                                evidenceFreezingThreshold - 5f,
                                                _displayTemperature);
                Observe(EvidenceType.FreezingTemperature, Mathf.Max(0.3f, depth));
            }
        }

        private void HandleDoorOpened() => _openDoorCount++;

        /// <summary>
        /// One reading. The ghost comes from the registry rather than a scene sweep - this used
        /// to call FindAnyObjectByType every frame the thermometer was held.
        /// </summary>
        private float Sample()
        {
            Vector3 probe = CarriedRoot != null ? CarriedRoot.position : transform.position;

            float ambient = Mathf.Max(ambientFloor, _ambient - _openDoorCount * openDoorPenalty);
            float reading = ambient + Random.Range(-sensorNoise, sensorNoise);

            var ghost = GhostController.Active;
            if (ghost == null)
                return reading;

            var evidence = ghost.GetComponent<GhostEvidenceManager>();
            if (evidence != null)
                reading += evidence.TemperatureOffset;

            float distance = Vector3.Distance(probe, ghost.transform.position);
            if (distance <= freezingDistance)
                return Mathf.Min(reading, Random.Range(freezingMax - 3f, freezingMax));
            if (distance <= coldDistance)
                return Mathf.Min(reading, Random.Range(coldMin, coldMax));

            return reading;
        }
    }
}
