using UnityEngine;
using UnityEngine.AI;
using CatchIfYouCan.Core;
using CatchIfYouCan.Evidence;

namespace CatchIfYouCan.Ghost
{
    public class GhostSpawnManager : MonoBehaviour
    {
        [SerializeField] private GhostDefinition defaultDefinition;
        [SerializeField] private Transform[] roomAnchors;
        [SerializeField] private float minPlayerDistance = 6f;
        [SerializeField] private float maxPlayerDistance = 24f;
        [SerializeField] private LayerMask visibilityBlockMask = ~0;
        [SerializeField] private string playerTag = "Player";

        private Transform _player;
        private Camera _camera;

        private void Start()
        {
            var playerObj = GameObject.FindGameObjectWithTag(playerTag);
            if (playerObj != null) _player = playerObj.transform;
            _camera = Camera.main;
        }

        public GhostController SpawnGhost(GhostDefinition definition, bool forceEventSpawn = false)
        {
            definition = definition != null ? definition : defaultDefinition;
            if (definition == null)
            {
                CIYCLog.Warn("GhostSpawnManager: missing ghost definition.");
                return null;
            }

            if (!TryFindSpawnPoint(forceEventSpawn, out Vector3 spawnPos))
            {
                CIYCLog.Warn("GhostSpawnManager: failed to find valid spawn point.");
                return null;
            }

            GameObject instance;
            if (definition.Prefab != null)
            {
                instance = Instantiate(definition.Prefab, spawnPos, Quaternion.identity);
            }
            else
            {
                instance = GhostFactory.Create(definition, spawnPos);
            }

            var controller = instance.GetComponent<GhostController>();
            if (controller == null)
                controller = instance.AddComponent<GhostController>();

            controller.EnsureManifestationRenderers();
            controller.Initialize(definition);

            CIYCLog.Info($"Ghost spawned at {spawnPos} ({definition.DisplayName}).");
            return controller;
        }

        public void SetRoomAnchors(Transform[] anchors)
        {
            roomAnchors = anchors;
        }

        public bool TryFindSpawnPoint(bool allowFrontSpawn, out Vector3 position)
        {
            position = Vector3.zero;
            Vector3 playerPos = _player != null ? _player.position : Vector3.zero;

            for (int attempt = 0; attempt < 24; attempt++)
            {
                Vector3 candidate = PickCandidate(playerPos);
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                    continue;

                candidate = hit.position;

                if (_player != null)
                {
                    float dist = Vector3.Distance(candidate, playerPos);
                    if (dist < minPlayerDistance || dist > maxPlayerDistance)
                        continue;

                    if (!allowFrontSpawn && IsInPlayerFOV(candidate))
                        continue;

                    if (!IsHiddenSpawn(candidate, playerPos))
                        continue;
                }

                position = candidate;
                return true;
            }

            return false;
        }

        private Vector3 PickCandidate(Vector3 playerPos)
        {
            if (roomAnchors != null && roomAnchors.Length > 0)
            {
                var anchor = roomAnchors[Random.Range(0, roomAnchors.Length)];
                return anchor.position + Random.insideUnitSphere * 4f;
            }

            if (_player != null)
            {
                Vector2 ring = Random.insideUnitCircle.normalized * Random.Range(minPlayerDistance, maxPlayerDistance);
                return playerPos + new Vector3(ring.x, 0f, ring.y);
            }

            return Random.insideUnitSphere * 10f;
        }

        private bool IsInPlayerFOV(Vector3 worldPos)
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return false;

            Vector3 viewport = _camera.WorldToViewportPoint(worldPos);
            if (viewport.z <= 0f) return false;

            const float margin = 0.05f;
            return viewport.x >= -margin && viewport.x <= 1f + margin &&
                   viewport.y >= -margin && viewport.y <= 1f + margin;
        }

        private bool IsHiddenSpawn(Vector3 spawnPos, Vector3 playerPos)
        {
            if (IsBehindDoor(spawnPos, playerPos)) return true;
            if (IsAroundCorner(spawnPos, playerPos)) return true;
            if (IsDarkArea(spawnPos)) return true;
            return !IsInPlayerFOV(spawnPos);
        }

        private bool IsBehindDoor(Vector3 spawnPos, Vector3 playerPos)
        {
            Vector3 dir = spawnPos - playerPos;
            if (Physics.Raycast(playerPos + Vector3.up, dir.normalized, out RaycastHit hit,
                    dir.magnitude, visibilityBlockMask, QueryTriggerInteraction.Ignore))
            {
                return hit.collider.CompareTag("Door");
            }
            return false;
        }

        private bool IsAroundCorner(Vector3 spawnPos, Vector3 playerPos)
        {
            Vector3 mid = (spawnPos + playerPos) * 0.5f + Vector3.up;
            return Physics.Raycast(playerPos + Vector3.up, (mid - playerPos).normalized,
                       out _, Vector3.Distance(playerPos, mid), visibilityBlockMask,
                       QueryTriggerInteraction.Ignore) &&
                   Physics.Raycast(mid, (spawnPos - mid).normalized, out _, Vector3.Distance(mid, spawnPos),
                       visibilityBlockMask, QueryTriggerInteraction.Ignore);
        }

        private static bool IsDarkArea(Vector3 pos)
        {
            var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            float brightness = 0f;
            for (int i = 0; i < lights.Length; i++)
            {
                if (!lights[i].enabled) continue;
                float d = Vector3.Distance(pos, lights[i].transform.position);
                if (d < 0.01f) continue;
                brightness += lights[i].intensity / (d * d);
            }
            return brightness < 0.15f;
        }
    }
}
