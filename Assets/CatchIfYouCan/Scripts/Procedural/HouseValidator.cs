using System.Collections.Generic;
using CatchIfYouCan.Core;
using CatchIfYouCan.Interaction;
using UnityEngine;

namespace CatchIfYouCan.Procedural
{
    public class HouseValidationResult
    {
        public bool IsValid;
        public List<string> Errors = new List<string>();
    }

    public static class HouseValidator
    {
        public static HouseValidationResult Validate(GeneratedHouse house)
        {
            var result = new HouseValidationResult { IsValid = true };

            if (house == null)
            {
                result.IsValid = false;
                result.Errors.Add("Generated house is null.");
                return result;
            }

            if (house.Entrance == null)
            {
                result.IsValid = false;
                result.Errors.Add("Missing entrance room.");
            }

            if (house.GhostRoom == null)
            {
                result.IsValid = false;
                result.Errors.Add("Missing ghost room.");
            }
            else if (house.GhostRoom.Category == RoomCategory.Entrance)
            {
                result.IsValid = false;
                result.Errors.Add("Ghost room cannot be the entrance.");
            }

            ValidateOverlaps(house, result);
            ValidateReachability(house, result);
            ValidateOpenSockets(house, result);
            ValidateGhostRoom(house, result);

            result.IsValid = result.Errors.Count == 0;
            return result;
        }

        private static void ValidateOverlaps(GeneratedHouse house, HouseValidationResult result)
        {
            for (int i = 0; i < house.Rooms.Count; i++)
            {
                for (int j = i + 1; j < house.Rooms.Count; j++)
                {
                    var a = house.Rooms[i]?.Module;
                    var b = house.Rooms[j]?.Module;
                    if (a == null || b == null)
                        continue;

                    if (a.Overlaps(b))
                        result.Errors.Add($"Room overlap detected between node {a.GraphNodeId} and {b.GraphNodeId}.");
                }
            }
        }

        private static void ValidateReachability(GeneratedHouse house, HouseValidationResult result)
        {
            if (house.Entrance == null || house.LayoutGraph == null)
                return;

            var visited = new HashSet<int>();
            var queue = new Queue<int>();
            queue.Enqueue(house.Entrance.NodeId);
            visited.Add(house.Entrance.NodeId);

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                foreach (var neighbor in house.LayoutGraph.GetNeighbors(current))
                {
                    if (neighbor == null || visited.Contains(neighbor.Id))
                        continue;

                    visited.Add(neighbor.Id);
                    queue.Enqueue(neighbor.Id);
                }
            }

            for (int i = 0; i < house.Rooms.Count; i++)
            {
                if (!visited.Contains(house.Rooms[i].NodeId))
                    result.Errors.Add($"Room {house.Rooms[i].Category} (node {house.Rooms[i].NodeId}) unreachable from entrance.");
            }
        }

        private static void ValidateOpenSockets(GeneratedHouse house, HouseValidationResult result)
        {
            for (int i = 0; i < house.Rooms.Count; i++)
            {
                var module = house.Rooms[i].Module;
                if (module == null)
                    continue;

                var doorSockets = module.GetSockets(SocketType.Door);
                for (int d = 0; d < doorSockets.Count; d++)
                {
                    var socket = doorSockets[d];
                    if (socket == null || socket.IsOccupied)
                        continue;

                    if (IsSocketFacingVoid(house, house.Rooms[i], socket.Direction))
                        result.Errors.Add($"Open door socket to void in room {house.Rooms[i].Category} facing {socket.Direction}.");
                }

                var wallSockets = module.GetSockets(SocketType.Wall);
                for (int w = 0; w < wallSockets.Count; w++)
                {
                    var socket = wallSockets[w];
                    if (socket == null || socket.IsOccupied)
                        continue;

                    if (IsSocketFacingVoid(house, house.Rooms[i], socket.Direction))
                        result.Errors.Add($"Open wall socket to void in room {house.Rooms[i].Category} facing {socket.Direction}.");
                }
            }
        }

        private static bool IsSocketFacingVoid(GeneratedHouse house, GeneratedRoomInstance room, SocketDirection direction)
        {
            if (house.LayoutGraph == null || room == null)
                return true;

            var node = house.LayoutGraph.GetNode(room.NodeId);
            if (node == null)
                return true;

            var targetCell = node.GridCell + RoomSocket.DirectionToGridOffset(direction);
            return house.LayoutGraph.GetNodeAt(targetCell) == null;
        }

        private static void ValidateGhostRoom(GeneratedHouse house, HouseValidationResult result)
        {
            if (house.GhostRoom?.Module == null)
                return;

            var ghostSockets = house.GhostRoom.Module.GetSockets(SocketType.GhostInteract);
            if (ghostSockets.Count == 0 && house.GhostRoom.Category == RoomCategory.Hallway)
                result.Errors.Add("Ghost room lacks ghost interaction anchor.");
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
