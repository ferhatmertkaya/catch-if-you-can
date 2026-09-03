using CatchIfYouCan.Evidence;
using UnityEngine;

namespace CatchIfYouCan.Equipment
{
    /// <summary>
    /// The UV lamp: a narrow violet cone that brings traces out of surfaces if you hold it on
    /// them.
    ///
    /// <para>
    /// A held item like any other now, rather than an <see cref="EquipmentBase"/> with no grip,
    /// no presentation and no drop physics. Its lamp is built here for the same reason the
    /// torch's beam is: a mesh cannot be a light.
    /// </para>
    /// </summary>
    [AddComponentMenu("Catch If You Can/UV Light")]
    public class UVLight : HeldEquipmentBase
    {
        [Header("Lamp")]
        [Tooltip("How far the lamp throws, in metres. Deliberately short: a long-range light " +
                 "on a mobile forward+ renderer is a cost with no gameplay behind it, and a UV " +
                 "lamp you have to walk up to is the point.")]
        [SerializeField, Range(1f, 8f)] private float lightRange = 5f;

        [SerializeField, Range(10f, 90f)] private float lightSpotAngle = 45f;
        [SerializeField] private Color lightColor = new Color(0.45f, 0.2f, 1f);
        [SerializeField, Min(0f)] private float lightIntensity = 2.4f;

        [Header("Reveal")]
        [Tooltip("How far a trace can be and still be brought out, in metres. Never longer " +
                 "than the lamp throws.")]
        [SerializeField, Min(0.5f)] private float revealRange = 5f;

        [Tooltip("Half-angle of the useful cone, in degrees. A trace outside it is lit but not " +
                 "being pointed at.")]
        [SerializeField, Range(5f, 80f)] private float revealHalfAngle = 32f;

        [Tooltip("What can carry a trace. Not everything: this is swept several times a second.")]
        [SerializeField] private LayerMask revealMask = ~0;

        [Tooltip("Seconds between sweeps. Traces come out over about a second, so checking " +
                 "sixty times a second buys nothing.")]
        [SerializeField, Min(0.05f)] private float revealTickInterval = 0.2f;

        [Tooltip("Most traces that can be brought out at once. A bound, so a room full of " +
                 "handprints cannot turn one sweep into an unbounded loop.")]
        [SerializeField, Min(1)] private int maxTargetsPerSweep = 12;

        private Light _lamp;
        private bool _lightOn;
        private float _revealTimer;
        private float _sinceLastSweep;
        private int _revealedThisSweep;

        private Collider[] _sweepBuffer;

        /// <summary>Whether the lamp is on. Survives being stowed, like the torch's switch.</summary>
        public bool LightOn => _lightOn;

        /// <summary>How many traces the last sweep was bringing out, for a lab readout.</summary>
        public int RevealingCount => _revealedThisSweep;

        protected override float GetInterferenceMultiplier() => 0.25f;

        /// <summary>A switch does not wear out.</summary>
        protected override float DurabilityLossPerUse => 0f;

        /// <summary>
        /// The visual comes from content; the lamp does not, for the same reason the torch's
        /// beam does not. A mesh cannot be a light.
        /// </summary>
        protected override void BuildCarried()
        {
            if (CarriedRoot != null)
                return;

            base.BuildCarried();
            BuildLamp(CarriedLength);
        }

        private void BuildLamp(float length)
        {
            var go = new GameObject("UVLamp");
            go.transform.SetParent(CarriedRoot, false);
            go.transform.localPosition = new Vector3(0f, length, 0f);
            // Local +Y is the item's length by the carried-transform convention, so the lamp
            // is turned to face along it.
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            _lamp = go.AddComponent<Light>();
            _lamp.type = LightType.Spot;
            _lamp.range = lightRange;
            _lamp.spotAngle = lightSpotAngle;
            _lamp.innerSpotAngle = lightSpotAngle * 0.5f;
            _lamp.color = lightColor;
            _lamp.intensity = lightIntensity;
            // Additional-light shadows are off in the URP asset; asking for them costs the
            // sort and gives nothing back.
            _lamp.shadows = LightShadows.None;
            _lamp.enabled = false;
        }

        protected override void OnUse() => SetLamp(!_lightOn);

        protected override void OnBatteryDepleted()
        {
            base.OnBatteryDepleted();
            SetLamp(false);
        }

        protected override void OnLifecycleStateChanged(EquipmentLifecycleState from,
                                                        EquipmentLifecycleState to)
        {
            // Lit only in the hand. Stowed or on the floor a UV lamp is not being aimed at
            // anything, and leaving a spot light burning in a bag is a cost for nothing.
            ApplyLamp();
        }

        private void SetLamp(bool on)
        {
            _lightOn = on && (IsPowered || definition == null || definition.MaxBattery <= 0f);
            ApplyLamp();
        }

        private void ApplyLamp()
        {
            bool burning = _lightOn && LifecycleState == EquipmentLifecycleState.Equipped;

            if (_lamp != null)
                _lamp.enabled = burning;

            SetDeviceActive(burning);
        }

        protected override void TickEquipped(float deltaTime)
        {
            if (!_lightOn || _lamp == null)
                return;

            _sinceLastSweep += deltaTime;
            _revealTimer -= deltaTime;
            if (_revealTimer > 0f)
                return;

            float sweepDelta = _sinceLastSweep;
            _revealTimer = revealTickInterval;
            _sinceLastSweep = 0f;

            Sweep(sweepDelta);
        }

        /// <summary>
        /// Brings out whatever is inside the cone. Exposure is fed the time since the last
        /// sweep, not the frame, so the reveal rate does not depend on the frame rate.
        /// </summary>
        private void Sweep(float sweepDelta)
        {
            Transform head = CarriedRoot != null ? CarriedRoot : transform;
            Vector3 origin = head.position;
            Vector3 aim = head.up;   // the carried transform's +Y is its length

            _sweepBuffer ??= new Collider[Mathf.Max(1, maxTargetsPerSweep)];

            int count = Physics.OverlapSphereNonAlloc(
                origin, Mathf.Min(revealRange, lightRange), _sweepBuffer,
                revealMask, QueryTriggerInteraction.Collide);

            float cosineLimit = Mathf.Cos(revealHalfAngle * Mathf.Deg2Rad);
            int revealing = 0;
            float strongest = 0f;

            for (int i = 0; i < count; i++)
            {
                var hit = _sweepBuffer[i];
                if (hit == null)
                    continue;

                var reveal = hit.GetComponentInParent<EvidenceReveal>();
                if (reveal == null)
                    continue;

                Vector3 toTarget = hit.transform.position - origin;
                if (toTarget.sqrMagnitude < 1e-6f)
                    continue;

                if (Vector3.Dot(aim, toTarget.normalized) < cosineLimit)
                    continue;

                reveal.Expose(sweepDelta);
                revealing++;
                strongest = Mathf.Max(strongest, reveal.Exposure);
            }

            _revealedThisSweep = revealing;

            if (revealing == 0)
                return;

            // Reported every sweep while something is coming out, with how far out it is as
            // the strength. The validator holds it to a dwell, so brushing the beam across a
            // handprint is not proof - which is what a single tick used to be.
            Observe(EvidenceType.UVTraces, strongest);
        }
    }
}
