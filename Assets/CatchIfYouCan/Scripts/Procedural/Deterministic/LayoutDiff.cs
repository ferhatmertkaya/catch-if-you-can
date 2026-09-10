using System.Collections.Generic;
using System.Text;

namespace CatchIfYouCan.Procedural.Deterministic
{
    /// <summary>
    /// Reports the FIRST authoritative difference between two layouts, in the same
    /// canonical order the hash uses.
    ///
    /// Exists so nobody has to diff two thousand-line JSON dumps by hand: when a section
    /// hash disagrees, this names the exact room, door or prop and shows expected vs actual.
    /// </summary>
    public static class LayoutDiff
    {
        public static bool TryDescribeFirstDifference(HouseLayout expected, HouseLayout actual, out string description)
        {
            if (expected == null || actual == null)
            {
                description = "One of the layouts is null.";
                return expected != actual;
            }

            if (expected.GenerationVersion != actual.GenerationVersion)
            {
                description = Block("Generation version mismatch",
                    $"expected: {expected.GenerationVersion}",
                    $"actual:   {actual.GenerationVersion}");
                return true;
            }

            if (!string.Equals(expected.MapDefinitionId, actual.MapDefinitionId, System.StringComparison.Ordinal))
            {
                description = Block("Map mismatch",
                    $"expected: {expected.MapDefinitionId}",
                    $"actual:   {actual.MapDefinitionId}");
                return true;
            }

            if (expected.Seed != actual.Seed)
            {
                description = Block("Seed mismatch",
                    $"expected: {expected.Seed}",
                    $"actual:   {actual.Seed}");
                return true;
            }

            if (expected.ContentHash != actual.ContentHash)
            {
                description = Block("Content revision mismatch",
                    $"expected: {Fnv1a64.ToHex(expected.ContentHash)}",
                    $"actual:   {Fnv1a64.ToHex(actual.ContentHash)}",
                    "The two builds do not carry the same authored content.");
                return true;
            }

            if (TryDiffRooms(expected, actual, out description)) return true;
            if (TryDiffConnections(expected, actual, out description)) return true;
            if (TryDiffDoors(expected, actual, out description)) return true;
            if (TryDiffProps("Furniture", expected.Furniture, actual.Furniture, out description)) return true;
            if (TryDiffProps("Prop", expected.Props, actual.Props, out description)) return true;
            if (TryDiffAnchors("Hide spot", expected.HideSpots, actual.HideSpots, out description)) return true;
            if (TryDiffAnchors("Equipment spawn", expected.EquipmentSpawns, actual.EquipmentSpawns, out description)) return true;
            if (TryDiffAnchors("Evidence point", expected.EvidencePoints, actual.EvidencePoints, out description)) return true;

            if (expected.GhostRoomId != actual.GhostRoomId)
            {
                description = Block("Ghost room mismatch",
                    $"expected: room {expected.GhostRoomId}",
                    $"actual:   room {actual.GhostRoomId}");
                return true;
            }

            if (expected.WeatherIndex != actual.WeatherIndex)
            {
                description = Block("Weather mismatch",
                    $"expected: {expected.WeatherIndex}",
                    $"actual:   {actual.WeatherIndex}");
                return true;
            }

            description = "No difference.";
            return false;
        }

        private static bool TryDiffRooms(HouseLayout expected, HouseLayout actual, out string description)
        {
            if (expected.Rooms.Count != actual.Rooms.Count)
            {
                description = Block("Room count mismatch",
                    $"expected: {expected.Rooms.Count}",
                    $"actual:   {actual.Rooms.Count}");
                return true;
            }

            for (int i = 0; i < expected.Rooms.Count; i++)
            {
                var e = expected.Rooms[i];
                var a = actual.Rooms[i];
                if (e.RoomId == a.RoomId && e.Category == a.Category && e.Cell.Equals(a.Cell) &&
                    string.Equals(e.ArchetypeId, a.ArchetypeId, System.StringComparison.Ordinal) &&
                    e.VariantIndex == a.VariantIndex && e.DoorMask == a.DoorMask &&
                    e.OpenMask == a.OpenMask && e.PositionMm.Equals(a.PositionMm) &&
                    e.RotationIndex == a.RotationIndex)
                    continue;

                description = Block($"Room mismatch at index {i}",
                    $"roomId = ROOM_{e.RoomId:D2}",
                    "",
                    "   expected:",
                    $"      archetype = {e.ArchetypeId}",
                    $"      category  = {e.Category}",
                    $"      grid      = {e.Cell}",
                    $"      position  = {e.PositionMm}",
                    $"      variant   = {e.VariantIndex}",
                    $"      doorMask  = {e.DoorMask}",
                    "",
                    "   actual:",
                    $"      archetype = {a.ArchetypeId}",
                    $"      category  = {a.Category}",
                    $"      grid      = {a.Cell}",
                    $"      position  = {a.PositionMm}",
                    $"      variant   = {a.VariantIndex}",
                    $"      doorMask  = {a.DoorMask}");
                return true;
            }

            description = null;
            return false;
        }

        private static bool TryDiffConnections(HouseLayout expected, HouseLayout actual, out string description)
        {
            if (expected.Connections.Count != actual.Connections.Count)
            {
                description = Block("Connection count mismatch",
                    $"expected: {expected.Connections.Count}",
                    $"actual:   {actual.Connections.Count}");
                return true;
            }

            for (int i = 0; i < expected.Connections.Count; i++)
            {
                var e = expected.Connections[i];
                var a = actual.Connections[i];
                if (e.RoomAId == a.RoomAId && e.RoomBId == a.RoomBId && e.DirectionFromA == a.DirectionFromA)
                    continue;

                description = Block($"Connection mismatch at index {i}",
                    $"expected: room {e.RoomAId} -> room {e.RoomBId} ({e.DirectionFromA})",
                    $"actual:   room {a.RoomAId} -> room {a.RoomBId} ({a.DirectionFromA})");
                return true;
            }

            description = null;
            return false;
        }

        private static bool TryDiffDoors(HouseLayout expected, HouseLayout actual, out string description)
        {
            if (expected.Doors.Count != actual.Doors.Count)
            {
                description = Block("Door count mismatch",
                    $"expected: {expected.Doors.Count}",
                    $"actual:   {actual.Doors.Count}");
                return true;
            }

            for (int i = 0; i < expected.Doors.Count; i++)
            {
                var e = expected.Doors[i];
                var a = actual.Doors[i];
                if (e.RoomAId == a.RoomAId && e.RoomBId == a.RoomBId &&
                    e.SocketASlot == a.SocketASlot && e.SocketBSlot == a.SocketBSlot &&
                    e.PositionMm.Equals(a.PositionMm) && e.RotationIndex == a.RotationIndex)
                    continue;

                description = Block($"Door mismatch at index {i}",
                    $"doorId = DOOR_{e.DoorId:D2}",
                    $"expected: rooms {e.RoomAId}/{e.RoomBId} slots {e.SocketASlot}/{e.SocketBSlot} at {e.PositionMm}",
                    $"actual:   rooms {a.RoomAId}/{a.RoomBId} slots {a.SocketASlot}/{a.SocketBSlot} at {a.PositionMm}");
                return true;
            }

            description = null;
            return false;
        }

        private static bool TryDiffProps(string label, IReadOnlyList<LayoutProp> expected,
            IReadOnlyList<LayoutProp> actual, out string description)
        {
            if (expected.Count != actual.Count)
            {
                description = Block($"{label} count mismatch",
                    $"expected: {expected.Count}",
                    $"actual:   {actual.Count}");
                return true;
            }

            for (int i = 0; i < expected.Count; i++)
            {
                var e = expected[i];
                var a = actual[i];
                if (e.RoomId == a.RoomId && e.Slot == a.Slot &&
                    string.Equals(e.PropDefinitionId, a.PropDefinitionId, System.StringComparison.Ordinal) &&
                    e.PositionMm.Equals(a.PositionMm) && e.RotationIndex == a.RotationIndex)
                    continue;

                description = Block($"{label} mismatch at index {i}",
                    $"expected: {e.PropDefinitionId} in room {e.RoomId} slot {e.Slot} at {e.PositionMm}",
                    $"actual:   {a.PropDefinitionId} in room {a.RoomId} slot {a.Slot} at {a.PositionMm}");
                return true;
            }

            description = null;
            return false;
        }

        private static bool TryDiffAnchors(string label, IReadOnlyList<LayoutAnchor> expected,
            IReadOnlyList<LayoutAnchor> actual, out string description)
        {
            if (expected.Count != actual.Count)
            {
                description = Block($"{label} count mismatch",
                    $"expected: {expected.Count}",
                    $"actual:   {actual.Count}");
                return true;
            }

            for (int i = 0; i < expected.Count; i++)
            {
                var e = expected[i];
                var a = actual[i];
                if (e.RoomId == a.RoomId && e.Slot == a.Slot && e.PositionMm.Equals(a.PositionMm))
                    continue;

                description = Block($"{label} mismatch at index {i}",
                    $"expected: room {e.RoomId} slot {e.Slot} at {e.PositionMm}",
                    $"actual:   room {a.RoomId} slot {a.Slot} at {a.PositionMm}");
                return true;
            }

            description = null;
            return false;
        }

        private static string Block(string header, params string[] lines)
        {
            var sb = new StringBuilder();
            sb.AppendLine(header + ":");
            for (int i = 0; i < lines.Length; i++)
                sb.AppendLine("   " + lines[i]);
            return sb.ToString();
        }
    }
}
