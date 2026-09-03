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
        [Tooltip("Half-angle, in degrees, treated as \"in front of\" for a player whose camera " +
                 "this machine does not have. Wider than a real frustum on purpose: erring " +
                 "toward rejecting a spawn is the safe direction.")]
        [SerializeField, Range(20f, 90f)] private float remoteFacingAngle = 60f;

        private Camera _camera;

        private void Start()
        {
            // This manager is created before the player is spawned, so Start is too early
            // for both of these. Resolved here for the case where a player already exists,
            // and re-resolved on demand below for the case where one does not.
            _player = Core.LocalPlayerService.RootTransform;
            _camera = Core.LocalPlayerService.ResolveViewCamera();
        }

        /// <summary>
        /// A player to spawn away from, resolved late.
        ///
        /// <para>
        /// <b>This getter used to call itself.</b> Every branch tested <c>Player</c> rather
        /// than <c>_player</c>, so the first read - <c>TryFindSpawnPoint</c>, on the first
        /// ghost spawn of a mission - recursed until the stack ran out. A StackOverflowException
        /// cannot be caught in .NET and terminates the process immediately, so this was a hard
        /// crash sitting in the ghost spawn path, reached the moment anything called it with a
        /// player present.
        /// </para>
        ///
        /// <para>
        /// Resolved from the presence registry now, so it is a player rather than
        /// specifically the local one. The tag search stays as the last resort for a
        /// hand-placed player in a test scene that never registered.
        /// </para>
        /// </summary>
        private Transform TargetPlayer
        {
            get
            {
                if (_player != null)
                    return _player;

                var nearest = Player.PlayerPresence.Nearest(transform.position);
                if (nearest != null)
                {
                    _player = nearest.transform;
                    return _player;
                }

                var tagged = GameObject.FindGameObjectWithTag(playerTag);
                if (tagged != null)
                    _player = tagged.transform;

                return _player;
            }
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
            var player = TargetPlayer;
            Vector3 playerPos = player != null ? player.position : Vector3.zero;

            for (int attempt = 0; attempt < 24; attempt++)
            {
                Vector3 candidate = PickCandidate(playerPos);
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                    continue;

                candidate = hit.position;

                if (TargetPlayer != null)
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

            if (TargetPlayer != null)
            {
                Vector2 ring = Random.insideUnitCircle.normalized * Random.Range(minPlayerDistance, maxPlayerDistance);
                return playerPos + new Vector3(ring.x, 0f, ring.y);
            }

            return Random.insideUnitSphere * 10f;
        }

        /// <summary>
        /// Whether a spawn point would appear in front of somebody.
        ///
        /// <para>
        /// A ghost must not pop into existence where a player is looking, and on a host
        /// "a player" is not "the local player". This used to project into the local camera
        /// only, so with three other people in the house the ghost could appear directly in
        /// front of any of them.
        /// </para>
        ///
        /// <para>
        /// Only the local camera can be projected through - a remote player's camera does not
        /// exist on this machine - so a remote player is tested by facing instead: their root's
        /// forward and the angle to the candidate. It is a coarser test than a frustum and it
        /// is the right kind of coarse, because it errs toward rejecting a spawn.
        /// </para>
        /// </summary>
        private bool IsInPlayerFOV(Vector3 worldPos)
        {
            var players = Player.PlayerPresence.All;
            for (int i = 0; i < players.Count; i++)
            {
                var presence = players[i];
                if (presence == null)
                    continue;

                if (presence.IsLocal)
                {
                    if (_camera == null)
                        _camera = Core.LocalPlayerService.ResolveViewCamera();

                    if (_camera != null)
                    {
                        Vector3 viewport = _camera.WorldToViewportPoint(worldPos);
                        const float margin = 0.05f;
                        if (viewport.z > 0f &&
                            viewport.x >= -margin && viewport.x <= 1f + margin &&
                            viewport.y >= -margin && viewport.y <= 1f + margin)
                            return true;

                        continue;
                    }
                }

                Vector3 toCandidate = worldPos - presence.transform.position;
                if (toCandidate.sqrMagnitude < 0.0001f)
                    return true;

                if (Vector3.Angle(presence.transform.forward, toCandidate) <= remoteFacingAngle)
                    return true;
            }

            return false;
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
            var lights = FindObjectsByType<Light>();
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
