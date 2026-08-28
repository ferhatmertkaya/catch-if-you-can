using UnityEngine;
using CatchIfYouCan.Evidence;

namespace CatchIfYouCan.Equipment
{
    public class FlashlightEquipment : EquipmentBase
    {
        [SerializeField] private Light spotlight;
        [SerializeField] private float flickerThreshold = 0.12f;
        [SerializeField] private float flickerSpeed = 12f;

        private bool _isOn;

        protected override float GetInterferenceMultiplier() => 0.2f;

        protected override void OnEquipped()
        {
            SetFlashlight(false);
        }

        protected override void OnUnequipped()
        {
            SetFlashlight(false);
        }

        protected override void OnUse()
        {
            SetFlashlight(!_isOn);
        }

        protected override void OnBatteryDepleted()
        {
            base.OnBatteryDepleted();
            SetFlashlight(false);
        }

        protected override void TickEquipped(float deltaTime)
        {
            if (!_isOn || spotlight == null)
                return;

            if (BatteryPercent <= flickerThreshold)
            {
                spotlight.intensity = 1.2f + Mathf.PerlinNoise(Time.time * flickerSpeed, 0f) * 0.8f;
            }
            else
            {
                spotlight.intensity = 2f;
            }

            if (BatteryPercent <= flickerThreshold * 0.5f
                && Core.ServiceLocator.TryGet<EvidenceManager>(out var manager))
            {
                manager.RegisterEvidence(EvidenceType.ElectronicDistortion);
            }
        }

        private void SetFlashlight(bool on)
        {
            _isOn = on && IsPowered;
            SetDeviceActive(_isOn);
            if (spotlight != null)
                spotlight.enabled = _isOn;
        }
    }
}
