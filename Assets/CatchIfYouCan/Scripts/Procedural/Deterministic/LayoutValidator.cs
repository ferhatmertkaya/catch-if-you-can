using System.Collections.Generic;

namespace CatchIfYouCan.Procedural.Deterministic
{
    public sealed class LayoutValidationResult
    {
        public bool IsValid;
        public readonly List<string> Errors = new List<string>();

        public void Fail(string error)
        {
            Errors.Add(error);
            IsValid = false;
        }

        public override string ToString() =>
            IsValid ? "valid" : string.Join("; ", Errors);
    }

    /// <summary>
    /// Validates a logical layout. Operates purely on layout data - no scene, no physics,
    /// no NavMesh - so a failed attempt costs nothing and leaves nothing behind.
    /// </summary>
    public static class LayoutValidator
    {
        public static LayoutValidationResult Validate(HouseLayout layout, MapDefinition map)
        {
            var result = new LayoutValidationResult { IsValid = true };

            if (layout == null)
            {
                result.Fail("Layout is null.");
                return result;
            }

            if (layout.Rooms.Count == 0)
            {
                result.Fail("Layout has no rooms.");
                return result;
            }

            if (layout.Rooms.Count < map.MinRooms)
                result.Fail($"Room count {layout.Rooms.Count} is below the map minimum {map.MinRooms}.");

            ValidateEntrance(layout, result);
            ValidateGhostRoom(layout, result);
            ValidateUniqueCells(layout, result);
            ValidateReachability(layout, result);
            ValidateRequiredCategories(layout, result);
            ValidateHideSpots(layout, result);

            return result;
        }

        private static void ValidateEntrance(HouseLayout layout, LayoutValidationResult result)
        {
            if (!layout.TryGetRoom(layout.EntranceRoomId, out var entrance))
            {
                result.Fail("Missing entrance room.");
                return;
            }

            if (entrance.Category != RoomCategory.Entrance)
                result.Fail($"Entrance room {entrance.RoomId} has category {entrance.Category}.");
        }

        private static void ValidateGhostRoom(HouseLayout layout, LayoutValidationResult result)
        {
            if (layout.GhostRoomId < 0 || !layout.TryGetRoom(layout.GhostRoomId, out var ghostRoom))
            {
                result.Fail("Missing ghost room.");
                return;
            }

            if (ghostRoom.Category == RoomCategory.Entrance)
                result.Fail("Ghost room cannot be the entrance.");
        }

        private static void ValidateUniqueCells(HouseLayout layout, LayoutValidationResult result)
        {
            // O(n^2) over at most a few dozen rooms, and it avoids a hash container whose
            // iteration order could otherwise leak into an error message.
            for (int i = 0; i < layout.Rooms.Count; i++)
            {
                for (int j = i + 1; j < layout.Rooms.Count; j++)
                {
                    if (layout.Rooms[i].Cell.Equals(layout.Rooms[j].Cell))
                    {
                        result.Fail($"Rooms {layout.Rooms[i].RoomId} and {layout.Rooms[j].RoomId} " +
                                    $"occupy the same cell {layout.Rooms[i].Cell}.");
                    }
                }
            }
        }

        private static void ValidateReachability(HouseLayout layout, LayoutValidationResult result)
        {
            int count = layout.Rooms.Count;
            var visited = new bool[count];
            var queue = new Queue<int>();

            int entranceIndex = IndexOfRoom(layout, layout.EntranceRoomId);
            if (entranceIndex < 0)
                return;

            visited[entranceIndex] = true;
            queue.Enqueue(layout.EntranceRoomId);

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                for (int i = 0; i < layout.Connections.Count; i++)
                {
                    var c = layout.Connections[i];
                    int other = c.RoomAId == current ? c.RoomBId
                              : c.RoomBId == current ? c.RoomAId
                              : -1;
                    if (other < 0)
                        continue;

                    int index = IndexOfRoom(layout, other);
                    if (index < 0 || visited[index])
                        continue;

                    visited[index] = true;
                    queue.Enqueue(other);
                }
            }

            for (int i = 0; i < count; i++)
            {
                if (!visited[i])
                    result.Fail($"Room {layout.Rooms[i].RoomId} ({layout.Rooms[i].Category}) is unreachable from the entrance.");
            }
        }

        private static void ValidateRequiredCategories(HouseLayout layout, LayoutValidationResult result)
        {
            RequireCategory(layout, RoomCategory.LivingRoom, result);
            RequireCategory(layout, RoomCategory.Bedroom, result);
            RequireCategory(layout, RoomCategory.Bathroom, result);
        }

        private static void RequireCategory(HouseLayout layout, RoomCategory category, LayoutValidationResult result)
        {
            for (int i = 0; i < layout.Rooms.Count; i++)
            {
                if (layout.Rooms[i].Category == category)
                    return;
            }

            result.Fail($"Required room category {category} is missing.");
        }

        private static void ValidateHideSpots(HouseLayout layout, LayoutValidationResult result)
        {
            if (layout.HideSpots.Count == 0)
                result.Fail("Layout has no hide spots.");
        }

        private static int IndexOfRoom(HouseLayout layout, int roomId)
        {
            for (int i = 0; i < layout.Rooms.Count; i++)
            {
                if (layout.Rooms[i].RoomId == roomId)
                    return i;
            }

            return -1;
        }
    }
}
