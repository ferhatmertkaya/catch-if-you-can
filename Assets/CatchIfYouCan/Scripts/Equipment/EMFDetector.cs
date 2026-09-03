using UnityEngine;
using CatchIfYouCan.Core;
using CatchIfYouCan.Evidence;

namespace CatchIfYouCan.Equipment
{
    public class EMFDetector : EquipmentBase
    {
        [SerializeField] private float evidenceThreshold = 0.65f;
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
        [SerializeField] private AudioClip beepClip;
        [SerializeField] private float minBeepInterval = 0.08f;
        [SerializeField] private float maxBeepInterval = 1.2f;

        private float _reading;
        private int _level;
        private float _beepTimer;

        protected override float GetInterferenceMultiplier() => 0.55f;

        protected override void OnEquipped()
        {
            SetDeviceActive(true);
        }

        protected override void OnUnequipped()
        {
            SetDeviceActive(false);
            SetLedLevel(0);
        }

        protected override void OnUse()
        {
            SetDeviceActive(!DeviceActive);
        }

        protected override void TickEquipped(float deltaTime)
        {
            if (!DeviceActive)
            {
                _reading = 0f;
                SetLedLevel(0);
                return;
            }

            _reading = SampleEmfReading();
            _level = ReadingToLevel(_reading);
            SetLedLevel(_level);
            UpdateBeep(deltaTime);

            if (_reading >= evidenceThreshold)
                ReportReading();
        }

        private float SampleEmfReading()
        {
            if (HandAnchor == null)
                return 0f;

            var spots = FindObjectsByType<EMFSpot>();
            float max = 0f;
            var probe = HandAnchor.position;

            foreach (var spot in spots)
            {
                if (spot == null)
                    continue;

                max = Mathf.Max(max, spot.GetStrengthAtPoint(probe));
            }

            return Mathf.Clamp01(max);
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

        private void SetLedLevel(int level)
        {
            if (ledRenderers == null)
                return;

            for (int i = 0; i < ledRenderers.Length; i++)
            {
                if (ledRenderers[i] == null)
                    continue;

                bool on = i < level;
                var color = on && levelColors != null && levelColors.Length > 0
                    ? levelColors[Mathf.Clamp(level - 1, 0, levelColors.Length - 1)]
                    : ledOffColor;

                if (ledRenderers[i].material != null)
                    ledRenderers[i].material.color = color;
            }
        }

        private void UpdateBeep(float deltaTime)
        {
            if (_level <= 0 || beepClip == null)
                return;

            _beepTimer -= deltaTime;
            if (_beepTimer > 0f)
                return;

            float interval = Mathf.Lerp(maxBeepInterval, minBeepInterval, _reading);
            _beepTimer = interval;
            PlayClip(beepClip);
        }

        public int CurrentLevel => _level;
        public float CurrentReading => _reading;

        private void ReportReading()
        {
            // The reading, not a verdict. Strength is the reading itself, so a needle barely
            // over the threshold and one pinned to the stop are different observations.
            Observe(EvidenceType.EMFSurge, Mathf.Clamp01(_reading));
        }
    }
}
