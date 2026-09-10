using UnityEngine;
using CatchIfYouCan.Evidence;

namespace CatchIfYouCan.Equipment
{
    /// <summary>
    /// The EMF reader: five lights, a rising beep, and a needle that only means something if
    /// it stays up.
    ///
    /// <para>
    /// It is a held item like any other now. Before this it derived from
    /// <see cref="EquipmentBase"/> rather than <see cref="HeldEquipmentBase"/>, which meant it
    /// had no grip, no presentation, no drop physics and no lifecycle - the whole carried-item
    /// pipeline reached exactly one item, the torch.
    /// </para>
    /// </summary>
    [AddComponentMenu("Catch If You Can/EMF Detector")]
    public class EMFDetector : HeldEquipmentBase
    {
        [Header("Reading")]
        [Tooltip("Reading above which this starts reporting an EMF observation. Whether that " +
                 "becomes evidence is the validator's call, not this one's.")]
        [SerializeField, Range(0f, 1f)] private float evidenceThreshold = 0.65f;

        [Tooltip("Seconds between samples. The field does not change fast enough to be worth " +
                 "reading every frame, and reading it every frame is what a scene sweep per " +
                 "frame used to cost.")]
        [SerializeField, Min(0.02f)] private float sampleInterval = 0.1f;

        [Tooltip("How quickly the needle follows the field. A detector that snaps to a value " +
                 "reads as a number rather than as an instrument.")]
        [SerializeField, Min(0.1f)] private float needleSpeed = 6f;

        [Header("Interference")]
        [Tooltip("How much other electronics running nearby muddy the reading. This is the " +
                 "reason a player learns to switch things off before trusting the needle.")]
        [SerializeField, Range(0f, 1f)] private float interferenceInfluence = 0.35f;

        [Tooltip("How far away another device still disturbs this one, in metres.")]
        [SerializeField, Min(0f)] private float interferenceRange = 4f;

        [Header("Display")]
        [SerializeField] private Renderer[] ledRenderers;
        [SerializeField] private Color ledOffColor = Color.black;
        [SerializeField] private Color[] levelColors =
        {
            new Color(0.2f, 0.8f, 0.2f),
            new Color(0.4f, 0.9f, 0.2f),
            new Color(0.9f, 0.9f, 0.2f),
            new Color(0.9f, 0.5f, 0.1f),
            new Color(0.9f, 0.1f, 0.1f)
        };

        [Header("Audio")]
        [SerializeField] private AudioClip beepClip;
        [SerializeField] private float minBeepInterval = 0.08f;
        [SerializeField] private float maxBeepInterval = 1.2f;

        private float _reading;
        private float _rawReading;
        private int _level;
        private float _beepTimer;
        private float _sampleTimer;
        private MaterialPropertyBlock _ledBlock;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        /// <summary>Current needle position, 0 to 1.</summary>
        public float CurrentReading => _reading;

        /// <summary>Current lamp, 0 to 5. What a player actually reads off it.</summary>
        public int CurrentLevel => _level;

        /// <summary>The field before interference was mixed in, for a lab readout.</summary>
        public float RawReading => _rawReading;

        /// <summary>The needle, as a number. It is the only thing this device says.</summary>
        public override string HudReadout => "LEVEL " + CurrentLevel;

        protected override float GetInterferenceMultiplier() => 0.55f;

        /// <summary>Switching a detector on and off does not wear it out.</summary>
        protected override float DurabilityLossPerUse => 0f;

        protected override void OnUse() => SetDeviceActive(!DeviceActive);

        /// <summary>
        /// The switch survives being stowed.
        ///
        /// <para>
        /// This used to force itself on in OnEquipped while Use toggled it, so re-selecting the
        /// slot switched it back on whatever the player had chosen - and it drained battery from
        /// the moment it was picked up. Being held is not the same as being switched on.
        /// </para>
        /// </summary>
        protected override void OnUnequipped()
        {
            SetLedLevel(0);
        }

        protected override void OnLifecycleStateChanged(EquipmentLifecycleState from,
                                                        EquipmentLifecycleState to)
        {
            // Stowed or lying in the room, it stops reading. Nothing to display and nothing
            // worth sampling for.
            if (to != EquipmentLifecycleState.Equipped)
            {
                _reading = 0f;
                _rawReading = 0f;
                SetLedLevel(0);
            }
        }

        protected override void TickEquipped(float deltaTime)
        {
            if (!DeviceActive)
            {
                if (_level != 0)
                {
                    _reading = 0f;
                    _rawReading = 0f;
                    SetLedLevel(0);
                }

                return;
            }

            // Sampled on a timer, not every frame. The needle keeps moving between samples so
            // it still looks continuous.
            _sampleTimer -= deltaTime;
            if (_sampleTimer <= 0f)
            {
                _sampleTimer = sampleInterval;
                _rawReading = SampleField();
            }

            float target = Mathf.Clamp01(_rawReading + SampleInterference());
            _reading = Mathf.MoveTowards(_reading, target, needleSpeed * deltaTime);

            int level = ReadingToLevel(_reading);
            if (level != _level)
            {
                _level = level;
                SetLedLevel(level);
            }

            UpdateBeep(deltaTime);

            if (_reading >= evidenceThreshold)
            {
                // The reading, not a verdict. The validator requires it to hold for a dwell
                // period and requires the ghost to actually exhibit EMF, so one spike on one
                // frame proves nothing - which is precisely what it used to prove.
                Observe(EvidenceType.EMFSurge, _reading);
            }
        }

        /// <summary>
        /// The strongest EMF source in range, from the registry rather than a scene sweep.
        /// </summary>
        private float SampleField()
        {
            Transform probe = CarriedRoot != null ? CarriedRoot : transform;
            return Mathf.Clamp01(EMFSpot.StrongestAt(probe.position));
        }

        /// <summary>
        /// How much other running electronics are muddying this. Reads the interference every
        /// device already publishes, so nothing new has to be tracked - a torch that is on, a
        /// UV lamp that is on, and this reader's own housing all contribute.
        /// </summary>
        private float SampleInterference()
        {
            if (interferenceInfluence <= 0f || interferenceRange <= 0f)
                return 0f;

            Transform probe = CarriedRoot != null ? CarriedRoot : transform;
            float total = Electronics.ElectronicDeviceRegistry.InterferenceAt(
                probe.position, interferenceRange, this);

            return Mathf.Clamp01(total) * interferenceInfluence;
        }

        private static int ReadingToLevel(float reading)
        {
            if (reading <= 0.05f) return 0;
            if (reading <= 0.2f) return 1;
            if (reading <= 0.4f) return 2;
            if (reading <= 0.65f) return 3;
            if (reading <= 0.85f) return 4;
            return 5;
        }

        /// <summary>
        /// Lights the lamps.
        ///
        /// <para>
        /// Through a property block rather than <c>Renderer.material</c>. Touching
        /// <c>.material</c> instantiates a new material every access, and this used to run
        /// every frame the detector was switched on - one leaked material per lamp per frame.
        /// </para>
        /// </summary>
        private void SetLedLevel(int level)
        {
            if (ledRenderers == null || ledRenderers.Length == 0)
                return;

            _ledBlock ??= new MaterialPropertyBlock();

            for (int i = 0; i < ledRenderers.Length; i++)
            {
                var renderer = ledRenderers[i];
                if (renderer == null)
                    continue;

                bool on = i < level;
                var color = on && levelColors != null && levelColors.Length > 0
                    ? levelColors[Mathf.Clamp(level - 1, 0, levelColors.Length - 1)]
                    : ledOffColor;

                renderer.GetPropertyBlock(_ledBlock);
                _ledBlock.SetColor(BaseColorId, color);
                _ledBlock.SetColor(ColorId, color);
                renderer.SetPropertyBlock(_ledBlock);
            }
        }

        /// <summary>Faster as the needle climbs, which is most of what makes it readable.</summary>
        private void UpdateBeep(float deltaTime)
        {
            if (_level <= 0 || beepClip == null)
                return;

            _beepTimer -= deltaTime;
            if (_beepTimer > 0f)
                return;

            _beepTimer = Mathf.Lerp(maxBeepInterval, minBeepInterval, _reading);
            PlayClip(beepClip);
        }
    }
}
