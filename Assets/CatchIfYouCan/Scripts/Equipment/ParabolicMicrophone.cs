using CatchIfYouCan.Core;
using CatchIfYouCan.Evidence;
using UnityEngine;

namespace CatchIfYouCan.Equipment
{
    /// <summary>
    /// The parabolic microphone: a dish that hears what it is pointed at, and only that.
    ///
    /// <para>
    /// It had no runtime path at all. There was no case for it in the runtime factory, so the
    /// id fell through to the unknown-id branch and the item a player would have been handed
    /// was a DEV_PLACEHOLDER box - the class existed and worked, and nothing could ever build
    /// it.
    /// </para>
    ///
    /// <para>
    /// It listens rather than scans: noise events come to it, so there is no sweep and nothing
    /// to throttle. What it adds is direction, distance and whether there is a wall in the way.
    /// </para>
    /// </summary>
    [AddComponentMenu("Catch If You Can/Parabolic Microphone")]
    public class ParabolicMicrophone : HeldEquipmentBase
    {
        [Header("Dish")]
        [Tooltip("How far it can hear, in metres.")]
        [SerializeField, Min(1f)] private float maxRange = 18f;

        [Tooltip("Half-angle of the dish, in degrees. Narrow is the whole point: a microphone " +
                 "that hears everything tells you nothing about where anything is.")]
        [SerializeField, Range(5f, 60f)] private float coneAngle = 22f;

        [Tooltip("How fast the signal falls away once a sound stops, per second.")]
        [SerializeField, Min(0.1f)] private float signalDecaySpeed = 2f;

        [Header("Occlusion")]
        [Tooltip("What blocks sound. A dish that hears straight through walls is a device for " +
                 "finding the ghost rather than for listening.")]
        [SerializeField] private LayerMask occluderMask = ~0;

        [Tooltip("How much of the signal survives a wall, 0 to 1. Not zero: a muffled sound " +
                 "through a wall is information, it is just not a position.")]
        [SerializeField, Range(0f, 1f)] private float occludedSignal = 0.35f;

        [Header("Evidence")]
        [Tooltip("Signal above which this reports an anomaly.")]
        [SerializeField, Range(0f, 1f)] private float evidenceThreshold = 0.55f;

        private AudioSource _loopSource;
        private float _signalStrength;
        private bool _lastWasOccluded;

        /// <summary>Current signal, 0 to 1. What the needle and the volume both follow.</summary>
        public float SignalStrength => _signalStrength;

        /// <summary>Whether the last thing it heard came through a wall, for a lab readout.</summary>
        public bool LastSignalOccluded => _lastWasOccluded;

        protected override float GetInterferenceMultiplier() => 0.3f;

        /// <summary>Switching it on does not wear it out.</summary>
        protected override float DurabilityLossPerUse => 0f;

        protected override void Awake()
        {
            base.Awake();
            GameEvents.OnNoiseGenerated += HandleNoise;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            GameEvents.OnNoiseGenerated -= HandleNoise;
        }

        /// <summary>
        /// The dish's own monitor output. Built here because a mesh cannot be an audio source,
        /// and because the serialized field this used to rely on was never set by anything - so
        /// the microphone has never made a sound.
        /// </summary>
        protected override void BuildCarried()
        {
            if (CarriedRoot != null)
                return;

            base.BuildCarried();

            var go = new GameObject("DishMonitor");
            go.transform.SetParent(CarriedRoot, false);

            _loopSource = go.AddComponent<AudioSource>();
            _loopSource.loop = true;
            _loopSource.playOnAwake = false;
            // Local rather than positional: this is what the headphones hear, not a sound in
            // the room.
            _loopSource.spatialBlend = 0f;
            _loopSource.volume = 0f;
        }

        protected override void OnUse() => SetDeviceActive(!DeviceActive);

        protected override void OnLifecycleStateChanged(EquipmentLifecycleState from,
                                                        EquipmentLifecycleState to)
        {
            if (to == EquipmentLifecycleState.Equipped)
                return;

            // Stowed, dropped or placed, it stops listening and stops making noise in the
            // player's ears.
            SetDeviceActive(false);
            _signalStrength = 0f;
            ApplyMonitor();
        }

        protected override void TickEquipped(float deltaTime)
        {
            if (!DeviceActive)
            {
                if (_signalStrength > 0f)
                {
                    _signalStrength = 0f;
                    ApplyMonitor();
                }

                return;
            }

            _signalStrength = Mathf.MoveTowards(_signalStrength, 0f, signalDecaySpeed * deltaTime);
            ApplyMonitor();

            if (_signalStrength >= evidenceThreshold)
            {
                // The validator holds this to a two second dwell and to the ghost's profile, so
                // one loud noise in the right direction is not an anomaly.
                Observe(EvidenceType.ParabolicAnomaly, _signalStrength);
            }
        }

        private void ApplyMonitor()
        {
            if (_loopSource == null)
                return;

            _loopSource.volume = _signalStrength;

            bool audible = _signalStrength > 0.05f && _loopSource.clip != null;
            if (audible && !_loopSource.isPlaying)
                _loopSource.Play();
            else if (!audible && _loopSource.isPlaying)
                _loopSource.Stop();
        }

        /// <summary>
        /// A noise happened somewhere. Whether this dish heard it depends on where it was
        /// pointed, how far away it was, and what is between.
        ///
        /// <para>
        /// Event-driven, so there is no scan and nothing to throttle - the cost is one raycast
        /// per noise, and only when the noise was already in the cone.
        /// </para>
        /// </summary>
        private void HandleNoise(float intensity, Vector3 position)
        {
            if (!DeviceActive || LifecycleState != EquipmentLifecycleState.Equipped)
                return;

            Transform dish = CarriedRoot != null ? CarriedRoot : transform;
            Vector3 origin = dish.position;
            // The carried transform's +Y is its length, so that is where the dish faces.
            Vector3 aim = dish.up;

            Vector3 toNoise = position - origin;
            float distance = toNoise.magnitude;
            if (distance > maxRange || distance < 0.0001f)
                return;

            float angle = Vector3.Angle(aim, toNoise / distance);
            if (angle > coneAngle)
                return;

            float distanceFactor = 1f - distance / maxRange;
            float angleFactor = 1f - angle / coneAngle;
            float sample = intensity * distanceFactor * angleFactor;

            // Through a wall it is muffled, not silenced. That keeps it a listening device
            // rather than a way to read the ghost's exact position through geometry.
            _lastWasOccluded = Physics.Linecast(origin, position, occluderMask.value);
            if (_lastWasOccluded)
                sample *= occludedSignal;

            _signalStrength = Mathf.Clamp01(Mathf.Max(_signalStrength, sample));
        }
    }
}
