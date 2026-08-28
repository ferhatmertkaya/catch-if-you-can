using UnityEngine;
using CatchIfYouCan.Evidence;
using CatchIfYouCan.Core;
using CatchIfYouCan.Equipment;

namespace CatchIfYouCan.Ghost
{
    [RequireComponent(typeof(GhostController))]
    public class GhostEvidenceManager : MonoBehaviour
    {
        [SerializeField] private GameObject emfSpotPrefab;
        [SerializeField] private GameObject uvMarkPrefab;
        [SerializeField] private GameObject orbPrefab;
        [SerializeField] private float spawnRadius = 6f;
        [SerializeField] private float temperatureDropAmount = 8f;
        [SerializeField] private float evidenceRefreshInterval = 45f;

        private GhostController _ghost;
        private float _nextSpawnTime;
        private float _localTemperatureOffset;

        public float TemperatureOffset => _localTemperatureOffset;

        public float GetTemperatureOffset() => _localTemperatureOffset;

        private void Awake()
        {
            _ghost = GetComponent<GhostController>();
        }

        private void Update()
        {
            if (_ghost?.Definition == null) return;
            if (Time.time < _nextSpawnTime) return;

            SpawnEvidenceForDefinition();
            _nextSpawnTime = Time.time + evidenceRefreshInterval * Random.Range(0.8f, 1.2f);
        }

        public void SpawnEvidenceForDefinition()
        {
            var def = _ghost.Definition;
            TrySpawnEvidenceType(def.Evidence1);
            TrySpawnEvidenceType(def.Evidence2);
            TrySpawnEvidenceType(def.Evidence3);
        }

        private void TrySpawnEvidenceType(EvidenceType type)
        {
            Vector3 origin = _ghost.transform.position;
            Vector3 offset = Random.insideUnitSphere * spawnRadius;
            offset.y = Random.Range(-0.5f, 0.5f);
            Vector3 pos = origin + offset;

            switch (type)
            {
                case EvidenceType.EMFSurge:
                    SpawnEmfSpot(pos);
                    GameEvents.EvidenceDetected(EvidenceType.EMFSurge);
                    break;

                case EvidenceType.UVTraces:
                    SpawnPrefab(uvMarkPrefab, pos);
                    GameEvents.EvidenceDetected(EvidenceType.UVTraces);
                    break;

                case EvidenceType.GhostOrb:
                    SpawnOrb(pos);
                    break;

                case EvidenceType.FreezingTemperature:
                    ApplyTemperatureInfluence();
                    GameEvents.EvidenceDetected(EvidenceType.FreezingTemperature);
                    break;

                case EvidenceType.PhysicalDisturbance:
                    SpawnPrefab(uvMarkPrefab, pos);
                    GameEvents.EvidenceDetected(EvidenceType.PhysicalDisturbance);
                    break;

                case EvidenceType.EVPResponse:
                case EvidenceType.SpectralGrid:
                case EvidenceType.ParabolicAnomaly:
                case EvidenceType.ElectronicDistortion:
                    GameEvents.EvidenceDetected(type);
                    break;
            }
        }

        private void SpawnEmfSpot(Vector3 position)
        {
            if (emfSpotPrefab != null)
            {
                Instantiate(emfSpotPrefab, position, Quaternion.identity, transform);
                return;
            }

            var spotGo = new GameObject("EMFSpot");
            spotGo.transform.SetParent(transform);
            spotGo.transform.position = position;
            var spot = spotGo.AddComponent<EMFSpot>();
            spot.Initialize(0.85f, 10f, 0.12f, 4f);
        }

        private void SpawnPrefab(GameObject prefab, Vector3 position)
        {
            if (prefab == null) return;
            Instantiate(prefab, position, Quaternion.identity, transform);
        }

        private void SpawnOrb(Vector3 position)
        {
            GameObject prefab = orbPrefab;
            if (prefab == null)
            {
                var orbGo = new GameObject("GhostOrb");
                orbGo.transform.position = position;
                var orb = orbGo.AddComponent<GhostOrb>();
                orb.Configure(EvidenceType.GhostOrb, Random.Range(0.4f, 0.9f), true);
                GameEvents.EvidenceDetected(EvidenceType.GhostOrb);
                return;
            }

            var instance = Instantiate(prefab, position, Quaternion.identity);
            var orbComp = instance.GetComponent<GhostOrb>();
            if (orbComp != null)
                orbComp.Configure(EvidenceType.GhostOrb, Random.Range(0.4f, 0.9f), true);

            GameEvents.EvidenceDetected(EvidenceType.GhostOrb);
        }

        private void ApplyTemperatureInfluence()
        {
            _localTemperatureOffset = -temperatureDropAmount * Random.Range(0.6f, 1f);
            CancelInvoke(nameof(DecayTemperature));
            Invoke(nameof(DecayTemperature), 20f);
        }

        private void DecayTemperature()
        {
            _localTemperatureOffset = Mathf.MoveTowards(_localTemperatureOffset, 0f, Time.deltaTime * 2f);
            if (Mathf.Abs(_localTemperatureOffset) > 0.01f)
                Invoke(nameof(DecayTemperature), 1f);
            else
                _localTemperatureOffset = 0f;
        }
    }
}
