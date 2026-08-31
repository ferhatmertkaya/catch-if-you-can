namespace CatchIfYouCan.Procedural.Deterministic
{
    /// <summary>
    /// Stable socket identity within a room.
    ///
    /// These are FIXED SLOTS, not creation-order indices: a room missing its north door
    /// still has DoorNorth reserved at 1, so socket ids never shift when the door set
    /// changes. Sequential ids would renumber every socket whenever a door was added and
    /// would make layout diffs unreadable.
    ///
    /// Append-only. Never renumber an existing slot.
    /// </summary>
    public enum SocketSlot
    {
        Light = 0,
        DoorNorth = 1,
        DoorEast = 2,
        DoorSouth = 3,
        DoorWest = 4,
        PropA = 5,
        PropB = 6,
        Evidence = 7,
        GhostInteract = 8,
        Hide = 9,
        EquipmentDrop = 10,
    }

    public static class SocketSlots
    {
        public static SocketSlot DoorSlot(SocketDirection direction)
        {
            switch (direction)
            {
                case SocketDirection.North: return SocketSlot.DoorNorth;
                case SocketDirection.East: return SocketSlot.DoorEast;
                case SocketDirection.South: return SocketSlot.DoorSouth;
                case SocketDirection.West: return SocketSlot.DoorWest;
                default: return SocketSlot.DoorNorth;
            }
        }

        public static SocketType TypeOf(SocketSlot slot)
        {
            switch (slot)
            {
                case SocketSlot.Light: return SocketType.Light;
                case SocketSlot.DoorNorth:
                case SocketSlot.DoorEast:
                case SocketSlot.DoorSouth:
                case SocketSlot.DoorWest: return SocketType.Door;
                case SocketSlot.PropA:
                case SocketSlot.PropB: return SocketType.Prop;
                case SocketSlot.Evidence: return SocketType.Evidence;
                case SocketSlot.GhostInteract: return SocketType.GhostInteract;
                case SocketSlot.Hide: return SocketType.Hide;
                case SocketSlot.EquipmentDrop: return SocketType.Prop;
                default: return SocketType.Prop;
            }
        }
    }
}
