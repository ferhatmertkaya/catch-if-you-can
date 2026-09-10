using System.Collections.Generic;
using CatchIfYouCan.Core;
using CatchIfYouCan.Procedural.Deterministic;

namespace CatchIfYouCan.Procedural
{
    public class HouseValidationResult
    {
        public bool IsValid;
        public List<string> Errors = new List<string>();
    }

    /// <summary>
    /// Scene-level validation of an instantiated house.
    ///
    /// Authoritative validation - reachability, room overlap, required categories, ghost
    /// room legality - now runs in Stage A on pure data (<see cref="LayoutValidator"/>),
    /// before anything is instantiated. That is what lets a failed attempt be retried for
    /// free instead of building and destroying GameObjects.
    ///
    /// What remains here is a build-integrity check: did Stage B actually produce the scene
    /// the layout described? A failure here is an instantiation bug, not a generation one,
    /// and cannot desync a session because it does not affect the layout hash.
    /// </summary>
    public static class HouseValidator
    {
        public static HouseValidationResult Validate(GeneratedHouse house)
        {
            var result = new HouseValidationResult { IsValid = true };

            if (house == null)
            {
                result.Errors.Add("Generated house is null.");
                result.IsValid = false;
                return result;
            }

            if (house.Layout == null)
            {
                result.Errors.Add("Generated house has no authoritative layout.");
                result.IsValid = false;
                return result;
            }

            // Re-run the authoritative checks so a caller holding only a GeneratedHouse
            // still sees them, then add the scene-integrity checks on top.
            var layoutValidation = LayoutValidator.Validate(
                house.Layout, MapDefinition.ById(house.Layout.MapDefinitionId));
            if (!layoutValidation.IsValid)
                result.Errors.AddRange(layoutValidation.Errors);

            ValidateRoomsInstantiated(house, result);
            ValidateEntranceAndGhostRoom(house, result);
            ValidateDoorsInstantiated(house, result);

            result.IsValid = result.Errors.Count == 0;
            return result;
        }

        private static void ValidateRoomsInstantiated(GeneratedHouse house, HouseValidationResult result)
        {
            if (house.Rooms.Count != house.Layout.Rooms.Count)
            {
                result.Errors.Add(
                    $"Instantiated {house.Rooms.Count} rooms but the layout describes {house.Layout.Rooms.Count}.");
            }

            for (int i = 0; i < house.Rooms.Count; i++)
            {
                var room = house.Rooms[i];
                if (room?.Root == null)
                {
                    result.Errors.Add($"Room {room?.NodeId} has no instantiated root.");
                    continue;
                }

                if (room.Module == null)
                    result.Errors.Add($"Room {room.NodeId} ({room.Category}) has no RoomModule.");
            }
        }

        private static void ValidateEntranceAndGhostRoom(GeneratedHouse house, HouseValidationResult result)
        {
            if (house.Entrance == null)
                result.Errors.Add("Missing entrance room instance.");

            if (house.GhostRoom == null)
            {
                result.Errors.Add("Missing ghost room instance.");
                return;
            }

            if (house.GhostRoom.NodeId != house.Layout.GhostRoomId)
            {
                result.Errors.Add(
                    $"Ghost room instance is room {house.GhostRoom.NodeId} but the layout says " +
                    $"{house.Layout.GhostRoomId}. Stage B must not choose the ghost room.");
            }
        }

        private static void ValidateDoorsInstantiated(GeneratedHouse house, HouseValidationResult result)
        {
            if (house.Doors.Count != house.Layout.Doors.Count)
            {
                result.Errors.Add(
                    $"Instantiated {house.Doors.Count} doors but the layout describes {house.Layout.Doors.Count}.");
            }
        }

        public static void LogValidation(HouseValidationResult result)
        {
            if (result.IsValid)
            {
                CIYCLog.Info("House validation passed.");
                return;
            }

            for (int i = 0; i < result.Errors.Count; i++)
                CIYCLog.Warn($"House validation: {result.Errors[i]}");
        }
    }
}
