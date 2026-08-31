using System.Collections.Generic;

namespace CatchIfYouCan.Procedural.Deterministic
{
    /// <summary>
    /// STAGE A - pure generation.
    ///
    /// Turns (seed, generationVersion, mapDefinitionId, content) into an authoritative
    /// <see cref="HouseLayout"/>. Creates no GameObjects, reads no engine state, performs
    /// no physics queries and touches no wall clock. Retries therefore cost nothing but
    /// CPU and, crucially, cannot be contaminated by a previous attempt's scene objects.
    ///
    /// Stage B (ProceduralHouseGenerator) instantiates the result. It never makes a
    /// generation decision of its own.
    /// </summary>
    public static class HouseLayoutBuilder
    {
        public const int MaxAttempts = 6;
        private const int ExpansionSafetyLimit = 64;

        private struct WorkNode
        {
            public int Id;
            public RoomCategory Category;
            public GridCell Cell;
        }

        private struct WorkEdge
        {
            public int AId;
            public int BId;
            public SocketDirection DirectionFromA;
        }

        private static readonly RoomCategory[] RequiredRooms =
        {
            RoomCategory.LivingRoom,
            RoomCategory.Bedroom,
            RoomCategory.Bathroom
        };

        private static readonly RoomCategory[] OptionalSpecialRooms =
        {
            RoomCategory.Basement,
            RoomCategory.Garage,
            RoomCategory.Attic
        };

        private static readonly RoomCategory[] FillerCategories =
        {
            RoomCategory.Hallway,
            RoomCategory.Kitchen,
            RoomCategory.DiningRoom,
            RoomCategory.Storage,
            RoomCategory.Laundry,
            RoomCategory.Office,
            RoomCategory.KidsRoom,
            RoomCategory.UtilityRoom
        };

        /// <summary>
        /// Generates and validates, retrying on a failed validation. Every attempt is pure
        /// data, so attempt N cannot see anything attempt N-1 produced.
        /// </summary>
        public static HouseLayout Generate(int seed, MapDefinition map, ContentSnapshot content,
            out LayoutValidationResult validation)
        {
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                var layout = Build(seed, map, content, attempt);
                validation = LayoutValidator.Validate(layout, map);
                if (validation.IsValid)
                    return layout;
            }

            // Deterministic, explicit failure. The caller must surface it - never silently
            // repair a layout (Docs/DETERMINISM.md §7, Docs/NETWORKING.md §5).
            var finalLayout = Build(seed, map, content, MaxAttempts - 1);
            validation = LayoutValidator.Validate(finalLayout, map);
            return finalLayout;
        }

        public static HouseLayout Build(int seed, MapDefinition map, ContentSnapshot content, int attempt)
        {
            var nodes = new List<WorkNode>(map.MaxRooms + 8);
            var edges = new List<WorkEdge>(map.MaxRooms + 8);

            var rngLayout = CiycRandom.ForStream(seed, CiycStream.Layout, attempt);
            var rngCorridors = CiycRandom.ForStream(seed, CiycStream.Corridors, attempt);

            int targetRooms = rngLayout.NextInt(map.MinRooms, map.MaxRooms + 1);

            AddNode(nodes, RoomCategory.Entrance, GridCell.Origin);

            int requiredIndex = 0;
            int safety = 0;
            while (nodes.Count < targetRooms && safety++ < ExpansionSafetyLimit)
            {
                RoomCategory category = requiredIndex < RequiredRooms.Length
                    ? RequiredRooms[requiredIndex++]
                    : FillerCategories[rngLayout.NextInt(0, FillerCategories.Length)];

                if (!TryExpand(nodes, edges, ref rngLayout, category))
                    break;
            }

            while (requiredIndex < RequiredRooms.Length)
            {
                var category = RequiredRooms[requiredIndex++];
                if (!TryExpand(nodes, edges, ref rngLayout, category))
                    ForceRequiredRoom(nodes, edges, ref rngCorridors, category);
            }

            int specialCount = rngLayout.NextInt(0, map.MaxSpecialRooms + 1);
            for (int i = 0; i < specialCount && nodes.Count < map.MaxRooms; i++)
            {
                var special = OptionalSpecialRooms[rngLayout.NextInt(0, OptionalSpecialRooms.Length)];
                if (ContainsCategory(nodes, special))
                    continue;

                TryExpand(nodes, edges, ref rngLayout, special);
            }

            while (nodes.Count < map.MinRooms)
            {
                var filler = FillerCategories[rngLayout.NextInt(0, FillerCategories.Length)];
                if (!TryExpand(nodes, edges, ref rngLayout, filler))
                {
                    ForceRequiredRoom(nodes, edges, ref rngCorridors, RoomCategory.Hallway);
                    break;
                }
            }

            return Assemble(seed, map, content, attempt, nodes, edges);
        }

        // ---------------------------------------------------------------- graph

        private static WorkNode AddNode(List<WorkNode> nodes, RoomCategory category, GridCell cell)
        {
            var node = new WorkNode { Id = nodes.Count, Category = category, Cell = cell };
            nodes.Add(node);
            return node;
        }

        private static bool ContainsCategory(List<WorkNode> nodes, RoomCategory category)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].Category == category)
                    return true;
            }

            return false;
        }

        private static int IndexOfCell(List<WorkNode> nodes, GridCell cell)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].Cell.Equals(cell))
                    return i;
            }

            return -1;
        }

        private static bool IsDirectionUsed(List<WorkEdge> edges, int nodeId, SocketDirection direction)
        {
            for (int i = 0; i < edges.Count; i++)
            {
                var edge = edges[i];
                if (edge.AId == nodeId && edge.DirectionFromA == direction)
                    return true;
                if (edge.BId == nodeId && Directions.Opposite(edge.DirectionFromA) == direction)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Adds one room adjacent to an existing one.
        ///
        /// Candidates are built in canonical (node id, cardinal direction) order and then
        /// picked uniformly. The previous implementation shuffled the node list first with
        /// OrderBy(_ => rng.Next()) - a random sort key, which is unspecified in both
        /// invocation order and tie-breaking - but since a uniform pick follows, the
        /// shuffle never affected the distribution. Dropping it removes the violation
        /// outright rather than replacing it with a Fisher-Yates that does nothing.
        /// </summary>
        private static bool TryExpand(List<WorkNode> nodes, List<WorkEdge> edges, ref CiycRandom rng,
            RoomCategory category)
        {
            var candidateNodeIds = new List<int>(nodes.Count * 4);
            var candidateDirections = new List<SocketDirection>(nodes.Count * 4);

            for (int n = 0; n < nodes.Count; n++)
            {
                var node = nodes[n];
                for (int d = 0; d < Directions.Cardinal.Length; d++)
                {
                    var dir = Directions.Cardinal[d];
                    if (IsDirectionUsed(edges, node.Id, dir))
                        continue;

                    var cell = node.Cell + Directions.ToGridOffset(dir);
                    if (IndexOfCell(nodes, cell) >= 0)
                        continue;

                    candidateNodeIds.Add(node.Id);
                    candidateDirections.Add(dir);
                }
            }

            if (candidateNodeIds.Count == 0)
                return false;

            int pick = rng.NextInt(0, candidateNodeIds.Count);
            int parentId = candidateNodeIds[pick];
            var direction = candidateDirections[pick];
            var parentCell = nodes[parentId].Cell;
            var newCell = parentCell + Directions.ToGridOffset(direction);

            var child = AddNode(nodes, category, newCell);
            edges.Add(new WorkEdge { AId = parentId, BId = child.Id, DirectionFromA = direction });
            return true;
        }

        /// <summary>
        /// Places a required room that normal expansion could not fit, bridging with
        /// hallways. The search direction order comes from the Corridors stream so corridor
        /// shape stays independent of room-graph draws.
        /// </summary>
        private static void ForceRequiredRoom(List<WorkNode> nodes, List<WorkEdge> edges,
            ref CiycRandom rng, RoomCategory category)
        {
            if (ContainsCategory(nodes, category))
                return;

            int parentId = 0;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].Category == RoomCategory.Hallway || nodes[i].Category == RoomCategory.LivingRoom)
                {
                    parentId = nodes[i].Id;
                    break;
                }
            }

            var order = new List<SocketDirection>(Directions.Cardinal);
            rng.Shuffle(order);

            var parentCell = nodes[parentId].Cell;

            for (int step = 1; step <= 8; step++)
            {
                for (int d = 0; d < order.Count; d++)
                {
                    var offset = Directions.ToGridOffset(order[d]);
                    var targetCell = parentCell + offset * step;
                    if (IndexOfCell(nodes, targetCell) >= 0)
                        continue;

                    int bridgeId = parentId;
                    for (int s = 1; s <= step - 1; s++)
                    {
                        var hallwayCell = parentCell + offset * s;
                        int existing = IndexOfCell(nodes, hallwayCell);
                        if (existing < 0)
                        {
                            var hallway = AddNode(nodes, RoomCategory.Hallway, hallwayCell);
                            edges.Add(new WorkEdge
                            {
                                AId = bridgeId,
                                BId = hallway.Id,
                                DirectionFromA = Directions.Between(nodes[bridgeId].Cell, hallwayCell)
                            });
                            bridgeId = hallway.Id;
                        }
                        else
                        {
                            bridgeId = nodes[existing].Id;
                        }
                    }

                    var child = AddNode(nodes, category, targetCell);
                    edges.Add(new WorkEdge
                    {
                        AId = bridgeId,
                        BId = child.Id,
                        DirectionFromA = Directions.Between(nodes[bridgeId].Cell, child.Cell)
                    });
                    return;
                }
            }
        }

        // ---------------------------------------------------------------- assembly

        private static HouseLayout Assemble(int seed, MapDefinition map, ContentSnapshot content, int attempt,
            List<WorkNode> nodes, List<WorkEdge> edges)
        {
            int roomCount = nodes.Count;

            var doorMasks = new int[roomCount];
            for (int i = 0; i < edges.Count; i++)
            {
                var edge = edges[i];
                if (edge.AId >= 0 && edge.AId < roomCount)
                    doorMasks[edge.AId] |= LayoutRoom.DirectionMask(edge.DirectionFromA);
                if (edge.BId >= 0 && edge.BId < roomCount)
                    doorMasks[edge.BId] |= LayoutRoom.DirectionMask(Directions.Opposite(edge.DirectionFromA));
            }

            var openMasks = new int[roomCount];
            for (int i = 0; i < roomCount; i++)
            {
                int open = 0;
                for (int d = 0; d < Directions.Cardinal.Length; d++)
                {
                    var dir = Directions.Cardinal[d];
                    if ((doorMasks[i] & LayoutRoom.DirectionMask(dir)) == 0)
                        open |= LayoutRoom.DirectionMask(dir);
                }

                openMasks[i] = open;
            }

            var rngRooms = CiycRandom.ForStream(seed, CiycStream.Rooms, attempt);
            var rngVariants = CiycRandom.ForStream(seed, CiycStream.RoomVariants, attempt);

            var rooms = new List<LayoutRoom>(roomCount);
            var archetypeBuffer = new List<RoomArchetype>(8);
            var weightBuffer = new List<float>(8);

            for (int i = 0; i < roomCount; i++)
            {
                var node = nodes[i];
                content.CollectRoomArchetypes(node.Category, archetypeBuffer);

                string archetypeId;
                Vec3i sizeMm;
                int variantIndex = 0;

                if (archetypeBuffer.Count == 0)
                {
                    archetypeId = "ARCH_FALLBACK_" + node.Category;
                    sizeMm = map.RoomSizeMm;
                }
                else
                {
                    weightBuffer.Clear();
                    for (int a = 0; a < archetypeBuffer.Count; a++)
                        weightBuffer.Add(archetypeBuffer[a].WeightFixed / (float)Quantize.WeightScale);

                    int picked = rngRooms.PickWeightedIndex(weightBuffer);
                    var archetype = archetypeBuffer[picked < 0 ? 0 : picked];
                    archetypeId = archetype.ArchetypeId;
                    sizeMm = archetype.SizeMm;
                    variantIndex = archetype.VariantCount > 1
                        ? rngVariants.NextInt(0, archetype.VariantCount)
                        : 0;
                }

                var positionMm = new Vec3i(
                    node.Cell.X * map.RoomSpacingMm.X,
                    node.Cell.Y * map.RoomSpacingMm.Y,
                    node.Cell.Z * map.RoomSpacingMm.Z);

                rooms.Add(new LayoutRoom(
                    node.Id, archetypeId, node.Category, node.Cell,
                    0, positionMm, sizeMm, variantIndex, doorMasks[i], openMasks[i]));
            }

            rooms.Sort((a, b) => a.RoomId.CompareTo(b.RoomId));

            var connections = BuildConnections(edges);
            var doors = BuildDoors(connections, rooms);
            var furniture = new List<LayoutProp>();
            var props = new List<LayoutProp>();
            PlaceProps(seed, map, content, attempt, rooms, furniture, props);

            var hideSpots = BuildHideSpots(rooms);
            var evidencePoints = BuildEvidencePoints(rooms);
            var equipmentSpawns = BuildEquipmentSpawns(rooms);

            var ghostCandidates = BuildGhostCandidates(seed, attempt, rooms);
            int ghostRoomId = ghostCandidates.Count > 0 ? ghostCandidates[0].RoomId : -1;

            var rngWeather = CiycRandom.ForStream(seed, CiycStream.Weather, attempt);
            int weatherIndex = rngWeather.NextInt(0, 4);

            return new HouseLayout(
                GenerationVersion.Current, seed, map.MapDefinitionId, content.ContentHash, attempt,
                rooms, connections, doors, furniture, props,
                hideSpots, equipmentSpawns, evidencePoints, ghostCandidates,
                entranceRoomId: 0, ghostRoomId: ghostRoomId, weatherIndex: weatherIndex);
        }

        private static List<LayoutConnection> BuildConnections(List<WorkEdge> edges)
        {
            var connections = new List<LayoutConnection>(edges.Count);
            for (int i = 0; i < edges.Count; i++)
            {
                var edge = edges[i];
                // Canonical orientation: lower room id is always A, so the same adjacency
                // hashes identically no matter which side discovered it.
                if (edge.AId <= edge.BId)
                    connections.Add(new LayoutConnection(0, edge.AId, edge.BId, edge.DirectionFromA));
                else
                    connections.Add(new LayoutConnection(0, edge.BId, edge.AId, Directions.Opposite(edge.DirectionFromA)));
            }

            connections.Sort((a, b) =>
            {
                int c = a.RoomAId.CompareTo(b.RoomAId);
                if (c != 0) return c;
                c = a.RoomBId.CompareTo(b.RoomBId);
                if (c != 0) return c;
                return ((int)a.DirectionFromA).CompareTo((int)b.DirectionFromA);
            });

            var numbered = new List<LayoutConnection>(connections.Count);
            for (int i = 0; i < connections.Count; i++)
            {
                var c = connections[i];
                numbered.Add(new LayoutConnection(i, c.RoomAId, c.RoomBId, c.DirectionFromA));
            }

            return numbered;
        }

        private static List<LayoutDoor> BuildDoors(List<LayoutConnection> connections, List<LayoutRoom> rooms)
        {
            var doors = new List<LayoutDoor>(connections.Count);
            for (int i = 0; i < connections.Count; i++)
            {
                var c = connections[i];
                if (c.RoomAId < 0 || c.RoomAId >= rooms.Count || c.RoomBId < 0 || c.RoomBId >= rooms.Count)
                    continue;

                var roomA = rooms[c.RoomAId];
                var roomB = rooms[c.RoomBId];
                var slotA = SocketSlots.DoorSlot(c.DirectionFromA);
                var slotB = SocketSlots.DoorSlot(Directions.Opposite(c.DirectionFromA));

                var worldA = roomA.PositionMm + RoomSocketLayout.LocalSocketOffset(slotA, roomA.SizeMm);
                var worldB = roomB.PositionMm + RoomSocketLayout.LocalSocketOffset(slotB, roomB.SizeMm);

                // Integer midpoint: exact, and independent of which side is evaluated first.
                var midpoint = new Vec3i(
                    (worldA.X + worldB.X) / 2,
                    (worldA.Y + worldB.Y) / 2,
                    (worldA.Z + worldB.Z) / 2);

                doors.Add(new LayoutDoor(i, c.RoomAId, c.RoomBId, slotA, slotB, midpoint,
                    Directions.ToRotationIndex(c.DirectionFromA)));
            }

            return doors;
        }

        private static void PlaceProps(int seed, MapDefinition map, ContentSnapshot content, int attempt,
            List<LayoutRoom> rooms, List<LayoutProp> furniture, List<LayoutProp> props)
        {
            var rngFurniture = CiycRandom.ForStream(seed, CiycStream.Furniture, attempt);
            var rngProps = CiycRandom.ForStream(seed, CiycStream.Props, attempt);

            var occupancy = new OccupancyGrid();
            var candidates = new List<PropArchetype>(16);
            var weights = new List<float>(16);

            int furnitureId = 0;
            int propId = 0;

            for (int r = 0; r < rooms.Count; r++)
            {
                var room = rooms[r];
                occupancy.Reset(room.SizeMm, room.DoorMask);

                // Furniture first, then small props, so props fit around furniture. Each
                // pass draws from its own stream, so adding a prop can never move furniture.
                furnitureId = PlacePass(room, content, PropKind.Furniture, ref rngFurniture, map,
                    occupancy, candidates, weights, furniture, furnitureId);
                propId = PlacePass(room, content, PropKind.Prop, ref rngProps, map,
                    occupancy, candidates, weights, props, propId);
            }

            furniture.Sort(ComparePropPlacement);
            props.Sort(ComparePropPlacement);
        }

        private static int PlacePass(LayoutRoom room, ContentSnapshot content, PropKind kind,
            ref CiycRandom rng, MapDefinition map, OccupancyGrid occupancy,
            List<PropArchetype> candidates, List<float> weights, List<LayoutProp> output, int nextId)
        {
            content.CollectPropArchetypes(room.Category, kind, candidates);
            var slots = RoomSocketLayout.SlotsFor(kind);

            for (int s = 0; s < slots.Length; s++)
            {
                var slot = slots[s];

                // Every draw happens unconditionally and in a fixed order, BEFORE the
                // geometric test, so the RNG stream position never depends on placement
                // outcomes. (The old code had this property too - which is exactly why the
                // physics-dependent test desynced the layout without desyncing the RNG.)
                int roll = rng.NextInt(0, 1000);
                if (candidates.Count == 0)
                    continue;

                weights.Clear();
                for (int c = 0; c < candidates.Count; c++)
                    weights.Add(candidates[c].WeightFixed / (float)Quantize.WeightScale);

                int picked = rng.PickWeightedIndex(weights);
                if (roll >= map.PropSpawnPermille)
                    continue;
                if (picked < 0)
                    continue;

                var archetype = candidates[picked];
                var localOffset = RoomSocketLayout.LocalSocketOffset(slot, room.SizeMm);

                if (!occupancy.TryOccupy(localOffset, archetype.BoundsMm))
                    continue;

                output.Add(new LayoutProp(
                    nextId++,
                    archetype.PropDefinitionId,
                    kind,
                    room.RoomId,
                    slot,
                    room.PositionMm + localOffset,
                    Directions.ToRotationIndex(SlotFacing(slot))));
            }

            return nextId;
        }

        /// <summary>
        /// The direction a prop faces. Props face INTO the room, away from the wall their
        /// socket sits against, which is what the previous LookRotation(-socketDirection)
        /// produced - so the built scene looks the same as before.
        /// </summary>
        private static SocketDirection SlotFacing(SocketSlot slot)
        {
            switch (slot)
            {
                case SocketSlot.PropA: return SocketDirection.South;
                case SocketSlot.PropB: return SocketDirection.North;
                default: return SocketDirection.North;
            }
        }

        private static int ComparePropPlacement(LayoutProp a, LayoutProp b)
        {
            int c = a.RoomId.CompareTo(b.RoomId);
            if (c != 0) return c;
            c = ((int)a.Slot).CompareTo((int)b.Slot);
            if (c != 0) return c;
            return string.CompareOrdinal(a.PropDefinitionId, b.PropDefinitionId);
        }

        private static List<LayoutAnchor> BuildHideSpots(List<LayoutRoom> rooms)
        {
            var result = new List<LayoutAnchor>();
            int id = 0;
            for (int i = 0; i < rooms.Count; i++)
            {
                var room = rooms[i];
                if (!RoomSocketLayout.HasHideSpot(room.Category))
                    continue;

                var offset = RoomSocketLayout.LocalSocketOffset(SocketSlot.Hide, room.SizeMm);
                result.Add(new LayoutAnchor(id++, room.RoomId, SocketSlot.Hide, room.PositionMm + offset));
            }

            return result;
        }

        private static List<LayoutAnchor> BuildEvidencePoints(List<LayoutRoom> rooms)
        {
            var result = new List<LayoutAnchor>(rooms.Count);
            for (int i = 0; i < rooms.Count; i++)
            {
                var room = rooms[i];
                var offset = RoomSocketLayout.LocalSocketOffset(SocketSlot.Evidence, room.SizeMm);
                result.Add(new LayoutAnchor(i, room.RoomId, SocketSlot.Evidence, room.PositionMm + offset));
            }

            return result;
        }

        private static List<LayoutAnchor> BuildEquipmentSpawns(List<LayoutRoom> rooms)
        {
            var result = new List<LayoutAnchor>(1);
            for (int i = 0; i < rooms.Count; i++)
            {
                if (rooms[i].Category != RoomCategory.Entrance)
                    continue;

                var offset = RoomSocketLayout.LocalSocketOffset(SocketSlot.EquipmentDrop, rooms[i].SizeMm);
                result.Add(new LayoutAnchor(0, rooms[i].RoomId, SocketSlot.EquipmentDrop, rooms[i].PositionMm + offset));
                break;
            }

            return result;
        }

        /// <summary>
        /// Ranks ghost rooms by distance from the entrance plus a category bonus and a small
        /// jitter, mirroring the previous scoring. Distance uses an exact integer square root
        /// rather than Mathf.Sqrt, and the score is stored as fixed point so it is safe to hash.
        /// </summary>
        private static List<LayoutGhostCandidate> BuildGhostCandidates(int seed, int attempt, List<LayoutRoom> rooms)
        {
            var rng = CiycRandom.ForStream(seed, CiycStream.GhostRoomCandidates, attempt);

            Vec3i entrancePos = Vec3i.Zero;
            for (int i = 0; i < rooms.Count; i++)
            {
                if (rooms[i].Category == RoomCategory.Entrance)
                {
                    entrancePos = rooms[i].PositionMm;
                    break;
                }
            }

            var candidates = new List<LayoutGhostCandidate>(rooms.Count);
            for (int i = 0; i < rooms.Count; i++)
            {
                var room = rooms[i];

                // Draw for every room, including skipped ones, so the stream position does
                // not depend on which categories happen to be present.
                int jitter = rng.NextInt(0, 51);

                if (room.Category == RoomCategory.Entrance || room.Category == RoomCategory.Hallway)
                    continue;

                long distanceMm = IntMath.Sqrt(room.PositionMm.HorizontalDistanceSquared(entrancePos));
                int score = (int)(distanceMm / 10) + CategoryBonus(room.Category) + jitter;
                candidates.Add(new LayoutGhostCandidate(room.RoomId, score));
            }

            // Highest score first; ties broken by room id so the order is total.
            candidates.Sort((a, b) =>
            {
                int c = b.ScoreFixed.CompareTo(a.ScoreFixed);
                if (c != 0) return c;
                return a.RoomId.CompareTo(b.RoomId);
            });

            if (candidates.Count == 0)
            {
                for (int i = 0; i < rooms.Count; i++)
                {
                    if (rooms[i].Category == RoomCategory.Entrance)
                        continue;

                    candidates.Add(new LayoutGhostCandidate(rooms[i].RoomId, 0));
                    break;
                }
            }

            return candidates;
        }

        private static int CategoryBonus(RoomCategory category)
        {
            switch (category)
            {
                case RoomCategory.Basement:
                case RoomCategory.Attic:
                    return 200;
                case RoomCategory.Bedroom:
                case RoomCategory.KidsRoom:
                    return 150;
                case RoomCategory.Bathroom:
                    return 100;
                default:
                    return 0;
            }
        }
    }
}
