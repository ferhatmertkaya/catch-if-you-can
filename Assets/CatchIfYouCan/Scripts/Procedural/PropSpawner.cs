using System.Collections.Generic;
using CatchIfYouCan.Procedural.Deterministic;
using UnityEngine;

namespace CatchIfYouCan.Procedural
{
    /// <summary>
    /// STAGE B - prop instantiation.
    ///
    /// This class no longer decides anything. It receives placements that Stage A already
    /// resolved and builds the GameObjects for them.
    ///
    /// It previously gated every spawn on Physics.OverlapBox. That read the live PhysX
    /// scene, whose contents depend on frame timing, on deferred Object.Destroy, and on
    /// whether Physics.SyncTransforms had run (m_AutoSyncTransforms is 0 in this project).
    /// Worse, the RNG draws happened BEFORE the overlap test, so a client whose query
    /// disagreed kept a perfectly in-sync RNG stream while its layout silently diverged -
    /// no check short of a full layout hash could have caught it.
    ///
    /// Overlap is now resolved analytically in OccupancyGrid during Stage A. Colliders are
    /// an output of generation and never feed back into it.
    /// </summary>
    public class PropSpawner
    {
        private readonly Transform _propRoot;

        public PropSpawner(Transform propRoot)
        {
            _propRoot = propRoot;
        }

        /// <summary>Instantiates every planned placement. Returns how many were built.</summary>
        public int SpawnPlacements(
            IReadOnlyList<LayoutProp> placements,
            PropDefinition[] library,
            IReadOnlyDictionary<int, GeneratedRoomInstance> roomsById)
        {
            if (placements == null || placements.Count == 0)
                return 0;

            int spawned = 0;
            for (int i = 0; i < placements.Count; i++)
            {
                if (TrySpawn(placements[i], library, roomsById))
                    spawned++;
            }

            return spawned;
        }

        private bool TrySpawn(
            LayoutProp placement,
            PropDefinition[] library,
            IReadOnlyDictionary<int, GeneratedRoomInstance> roomsById)
        {
            var definition = ContentSnapshotFactory.FindProp(library, placement.PropDefinitionId);

            Vector3 position = new Vector3(
                Quantize.Metres(placement.PositionMm.X),
                Quantize.Metres(placement.PositionMm.Y),
                Quantize.Metres(placement.PositionMm.Z));

            Quaternion rotation = Quaternion.Euler(0f, placement.RotationIndex * 90f, 0f);

            GameObject instance;
            if (definition != null && definition.Prefab != null)
            {
                instance = Object.Instantiate(definition.Prefab, position, rotation, _propRoot);
            }
            else
            {
                Vector3 size = definition != null ? definition.BoundsSize : Vector3.one;
                string propName = definition != null ? definition.PropName : placement.PropDefinitionId;
                instance = PrimitiveRoomFactory.CreateFallbackProp(propName, size, null);
                instance.transform.SetParent(_propRoot, false);
                instance.transform.SetPositionAndRotation(position, rotation);
            }

            var category = roomsById != null && roomsById.TryGetValue(placement.RoomId, out var room)
                ? room.Category.ToString()
                : "Room";

            instance.name = $"{placement.PropDefinitionId}_{category}_{placement.RoomId}_{placement.PropInstanceId}";
            return true;
        }
    }
}
