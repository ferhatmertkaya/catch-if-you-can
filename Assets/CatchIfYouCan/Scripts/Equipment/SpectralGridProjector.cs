using UnityEngine;
using CatchIfYouCan.Core;
using CatchIfYouCan.Evidence;
using CatchIfYouCan.Ghost;

namespace CatchIfYouCan.Equipment
{
    public class SpectralGridProjector : EquipmentBase
    {
        [SerializeField] private ParticleSystem gridParticles;
        [SerializeField] private GameObject ghostSilhouettePrefab;
        [SerializeField] private float projectionRadius = 4f;
        [SerializeField] private float silhouetteInterval = 6f;
        [SerializeField] private float baseSilhouetteChance = 0.08f;

        private float _ghostActivity;
        private float _silhouetteTimer;
        private Transform _ghostRoomCenter;
        private bool _huntActive;
        private GhostController _ghost;

        protected override void Awake()
        {
            base.Awake();
            GameEvents.OnGhostActivityChanged += HandleGhostActivity;
            GameEvents.OnHuntStarted += HandleHuntStarted;
            GameEvents.OnHuntEnded += HandleHuntEnded;
            _ghost = FindFirstObjectByType<GhostController>();
            if (_ghost != null)
                _ghostRoomCenter = _ghost.transform;
        }

        private void OnDestroy()
        {
            GameEvents.OnGhostActivityChanged -= HandleGhostActivity;
            GameEvents.OnHuntStarted -= HandleHuntStarted;
            GameEvents.OnHuntEnded -= HandleHuntEnded;
        }

        protected override void OnPlaced()
        {
            SetDeviceActive(true);
            if (gridParticles != null)
                gridParticles.Play();
        }

        protected override void OnUnequipped()
        {
            if (gridParticles != null && !IsPlaced)
                gridParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        protected override void TickEquipped(float deltaTime)
        {
            if (!IsPlaced)
                return;

            _silhouetteTimer -= deltaTime;
            if (_silhouetteTimer > 0f)
                return;

            _silhouetteTimer = silhouetteInterval;
            TrySpawnSilhouette();
        }

        private void HandleGhostActivity(float value) => _ghostActivity = Mathf.Clamp01(value);
        private void HandleHuntStarted() => _huntActive = true;
        private void HandleHuntEnded() => _huntActive = false;

        private void TrySpawnSilhouette()
        {
            if (ghostSilhouettePrefab == null)
                return;

            if (_ghost != null)
                _ghostRoomCenter = _ghost.transform;

            float chance = baseSilhouetteChance + _ghostActivity * 0.35f;
            if (_huntActive)
                chance += 0.15f;

            if (_ghostRoomCenter != null)
            {
                float roomDistance = Vector3.Distance(transform.position, _ghostRoomCenter.position);
                if (roomDistance <= projectionRadius)
                    chance += 0.25f;
                else
                    chance *= Mathf.Clamp01(projectionRadius / Mathf.Max(roomDistance, 0.1f));
            }

            if (Random.value > chance)
                return;

            var offset = Random.insideUnitSphere * projectionRadius;
            offset.y = 0f;
            var spawnPos = transform.position + offset;
            Instantiate(ghostSilhouettePrefab, spawnPos, Quaternion.LookRotation(transform.forward, Vector3.up));

            if (Core.ServiceLocator.TryGet<EvidenceManager>(out var manager))
                manager.RegisterEvidence(EvidenceType.SpectralGrid);
        }
    }
}
