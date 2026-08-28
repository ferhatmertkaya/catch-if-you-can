using UnityEngine;
using CatchIfYouCan.Evidence;

namespace CatchIfYouCan.Equipment
{
    public class UVLight : EquipmentBase
    {
        [SerializeField] private Light uvLight;
        [SerializeField] private float revealRange = 5f;
        [SerializeField] private LayerMask revealMask = ~0;
        [SerializeField] private float revealTickInterval = 0.2f;

        private float _revealTimer;
        private bool _lightOn;

        protected override float GetInterferenceMultiplier() => 0.25f;

        protected override void OnEquipped()
        {
            SetLight(false);
        }

        protected override void OnUnequipped()
        {
            SetLight(false);
        }

        protected override void OnUse()
        {
            SetLight(!_lightOn);
            SetDeviceActive(_lightOn);
        }

        protected override void TickEquipped(float deltaTime)
        {
            if (!_lightOn || HandAnchor == null)
                return;

            _revealTimer -= deltaTime;
            if (_revealTimer > 0f)
                return;

            _revealTimer = revealTickInterval;
            RevealInCone();
        }

        private void SetLight(bool on)
        {
            _lightOn = on;
            if (uvLight != null)
                uvLight.enabled = on;
        }

        private void RevealInCone()
        {
            var origin = HandAnchor.position;
            var hits = Physics.OverlapSphere(origin, revealRange, revealMask, QueryTriggerInteraction.Collide);
            bool found = false;

            foreach (var hit in hits)
            {
                if (hit == null)
                    continue;

                var reveal = hit.GetComponentInParent<EvidenceReveal>();
                if (reveal == null || reveal.IsRevealed)
                    continue;

                var toTarget = (hit.transform.position - origin).normalized;
                if (Vector3.Dot(HandAnchor.forward, toTarget) < 0.35f)
                    continue;

                reveal.Reveal();
                found = true;
            }

            if (found && Core.ServiceLocator.TryGet<EvidenceManager>(out var manager))
                manager.RegisterEvidence(EvidenceType.UVTraces);
        }
    }
}
