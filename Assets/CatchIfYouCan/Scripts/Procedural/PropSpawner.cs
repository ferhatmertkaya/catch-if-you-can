using System.Collections.Generic;
using UnityEngine;

namespace CatchIfYouCan.Procedural
{
    public class PropSpawner
    {
        private readonly LayerMask _overlapMask;
        private readonly Transform _propRoot;

        public PropSpawner(Transform propRoot, LayerMask overlapMask)
        {
            _propRoot = propRoot;
            _overlapMask = overlapMask.value == 0 ? ~0 : overlapMask;
        }

        public int SpawnProps(
            IEnumerable<GeneratedRoomInstance> rooms,
            PropDefinition[] propLibrary,
            System.Random rng,
            float spawnChancePerSocket = 0.65f)
        {
            if (rooms == null || propLibrary == null || propLibrary.Length == 0)
                return 0;

            int spawned = 0;
            foreach (var room in rooms)
            {
                if (room?.Module == null)
                    continue;

                var sockets = room.Module.GetSockets(SocketType.Prop);
                for (int i = 0; i < sockets.Count; i++)
                {
                    if (rng.NextDouble() > spawnChancePerSocket)
                        continue;

                    var candidates = FilterProps(propLibrary, room.Category);
                    if (candidates.Count == 0)
                        continue;

                    var definition = PickWeighted(candidates, rng);
                    if (TrySpawnAtSocket(definition, sockets[i], room))
                        spawned++;
                }
            }

            return spawned;
        }

        private List<PropDefinition> FilterProps(PropDefinition[] library, RoomCategory category)
        {
            var list = new List<PropDefinition>();
            for (int i = 0; i < library.Length; i++)
            {
                var def = library[i];
                if (def != null && def.MatchesRoom(category))
                    list.Add(def);
            }

            return list;
        }

        private PropDefinition PickWeighted(List<PropDefinition> candidates, System.Random rng)
        {
            float total = 0f;
            for (int i = 0; i < candidates.Count; i++)
                total += Mathf.Max(0.01f, candidates[i].Weight);

            float roll = SeedManager.NextFloat(rng, 0f, total);
            float cumulative = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                cumulative += Mathf.Max(0.01f, candidates[i].Weight);
                if (roll <= cumulative)
                    return candidates[i];
            }

            return candidates[candidates.Count - 1];
        }

        private bool TrySpawnAtSocket(PropDefinition definition, RoomSocket socket, GeneratedRoomInstance room)
        {
            if (definition == null || socket == null)
                return false;

            Vector3 position = socket.transform.position;
            Vector3 halfExtents = definition.BoundsSize * 0.5f;
            Quaternion rotation = Quaternion.LookRotation(-socket.GetWorldDirection(), Vector3.up);

            if (Physics.OverlapBox(position + Vector3.up * halfExtents.y, halfExtents * 0.9f, rotation, _overlapMask, QueryTriggerInteraction.Ignore).Length > 0)
                return false;

            GameObject instance;
            if (definition.Prefab != null)
            {
                instance = Object.Instantiate(definition.Prefab, position, rotation, _propRoot);
            }
            else
            {
                instance = PrimitiveRoomFactory.CreateFallbackProp(definition.PropName, definition.BoundsSize, null);
                instance.transform.SetParent(_propRoot, false);
                instance.transform.SetPositionAndRotation(position, rotation);
            }

            instance.name = $"{definition.PropName}_{room.Category}_{room.NodeId}";
            return true;
        }
    }
}
