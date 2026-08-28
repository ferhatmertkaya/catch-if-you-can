using UnityEngine;
using CatchIfYouCan.Core;
using CatchIfYouCan.Ghost;

namespace CatchIfYouCan.Equipment
{
    public class WardingRelic : EquipmentBase
    {
        [SerializeField] private float wardRadius = 5f;
        [SerializeField] private int maxCharges = 3;
        [SerializeField] private GameObject crystalIntact;
        [SerializeField] private GameObject crystalBreakVfxPrefab;
        [SerializeField] private AudioClip breakClip;

        private int _charges;
        private bool _huntActive;

        public int RemainingCharges => _charges;

        protected override void Awake()
        {
            base.Awake();
            _charges = maxCharges;
            GameEvents.OnHuntStarted += HandleHuntStarted;
            GameEvents.OnHuntEnded += HandleHuntEnded;
        }

        private void OnDestroy()
        {
            GameEvents.OnHuntStarted -= HandleHuntStarted;
            GameEvents.OnHuntEnded -= HandleHuntEnded;
        }

        protected override void OnEquipped()
        {
            SetDeviceActive(true);
        }

        protected override void OnPlaced()
        {
            SetDeviceActive(true);
        }

        protected override void TickEquipped(float deltaTime)
        {
            if (!_huntActive || _charges <= 0)
                return;

            if (!IsEquipped && !IsPlaced)
                return;

            if (IsHuntOriginWithinRadius())
                InterruptHunt();
        }

        private void HandleHuntStarted() => _huntActive = true;
        private void HandleHuntEnded() => _huntActive = false;

        private bool IsHuntOriginWithinRadius()
        {
            var huntOrigin = GameObject.Find("Ghost");
            if (huntOrigin == null)
                huntOrigin = GameObject.Find("GhostEntity");

            if (huntOrigin == null)
                return IsEquipped || IsPlaced;

            return Vector3.Distance(transform.position, huntOrigin.transform.position) <= wardRadius;
        }

        private void InterruptHunt()
        {
            _charges--;
            var hunt = FindFirstObjectByType<HuntController>();
            if (hunt != null)
                hunt.ForceEndHunt();
            else
                GameEvents.HuntEnded();

            _huntActive = false;
            PlayBreakVfx();

            if (_charges <= 0)
            {
                if (crystalIntact != null)
                    crystalIntact.SetActive(false);
                SetDeviceActive(false);
            }
        }

        private void PlayBreakVfx()
        {
            if (breakClip != null)
                PlayClip(breakClip);

            if (crystalBreakVfxPrefab != null)
                Instantiate(crystalBreakVfxPrefab, transform.position, Quaternion.identity);
        }
    }
}
