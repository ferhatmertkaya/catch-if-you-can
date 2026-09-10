using System.Collections.Generic;

namespace CatchIfYouCan.Procedural.Deterministic
{
    /// <summary>
    /// The single source of truth for where a room's sockets are.
    ///
    /// Stage A needs socket positions to plan prop placement BEFORE anything is
    /// instantiated, so socket geometry cannot live in the instantiation code any more.
    /// PrimitiveRoomFactory now reads these positions instead of hard-coding its own,
    /// which keeps the logical layout and the built scene in agreement by construction.
    ///
    /// All offsets are integer millimetres derived from the room size by integer division,
    /// mirroring the fractions the factory previously used (0.2, 0.25, 0.15, 0.1 of the
    /// room extent) exactly.
    /// </summary>
    public static class RoomSocketLayout
    {
        /// <summary>Door sockets sit at the wall centre, at handle height.</summary>
        public const int DoorHeightMm = 1100;

        public static Vec3i LocalSocketOffset(SocketSlot slot, Vec3i sizeMm)
        {
            int halfX = sizeMm.X / 2;
            int halfZ = sizeMm.Z / 2;

            switch (slot)
            {
                case SocketSlot.Light:
                    return new Vec3i(0, sizeMm.Y - 250, 0);

                case SocketSlot.DoorNorth: return new Vec3i(0, DoorHeightMm, halfZ);
                case SocketSlot.DoorSouth: return new Vec3i(0, DoorHeightMm, -halfZ);
                case SocketSlot.DoorEast: return new Vec3i(halfX, DoorHeightMm, 0);
                case SocketSlot.DoorWest: return new Vec3i(-halfX, DoorHeightMm, 0);

                // size.z * 0.2
                case SocketSlot.PropA: return new Vec3i(0, 0, sizeMm.Z / 5);
                // (0.8, 0, -size.z * 0.25)
                case SocketSlot.PropB: return new Vec3i(800, 0, -(sizeMm.Z / 4));
                // (size.x * 0.15, 1.0, 0.4)
                case SocketSlot.Evidence: return new Vec3i(sizeMm.X * 3 / 20, 1000, 400);
                // (-size.x * 0.1, 0, 0)
                case SocketSlot.GhostInteract: return new Vec3i(-(sizeMm.X / 10), 0, 0);
                case SocketSlot.Hide: return new Vec3i(-1500, 0, -1500);
                case SocketSlot.EquipmentDrop: return new Vec3i(1500, 0, 1500);

                default: return Vec3i.Zero;
            }
        }

        /// <summary>
        /// Prop-carrying slots, in frozen canonical order.
        ///
        /// Furniture and small props take one slot each rather than competing for both:
        /// letting furniture fill every slot first starves the prop pass entirely, since a
        /// placed piece always occupies the space the next candidate wants. Occupancy still
        /// guards both passes - a large furniture footprint can reach into the other slot,
        /// and door approach zones reject either.
        /// </summary>
        public static readonly SocketSlot[] AllPropSlots = { SocketSlot.PropA, SocketSlot.PropB };

        public static readonly SocketSlot[] FurnitureSlots = { SocketSlot.PropA };

        public static readonly SocketSlot[] SmallPropSlots = { SocketSlot.PropB };

        public static SocketSlot[] SlotsFor(PropKind kind) =>
            kind == PropKind.Furniture ? FurnitureSlots : SmallPropSlots;

        /// <summary>
        /// Whether a room of this category gets a hide spot. Mirrors the previous
        /// PrimitiveRoomFactory.ShouldHaveHideSpot so hiding behaviour is unchanged.
        /// </summary>
        public static bool HasHideSpot(RoomCategory category)
        {
            switch (category)
            {
                case RoomCategory.Bedroom:
                case RoomCategory.KidsRoom:
                case RoomCategory.Office:
                case RoomCategory.Storage:
                case RoomCategory.Garage:
                case RoomCategory.Basement:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Every socket a room owns, in canonical slot order. Door slots are included only
        /// for directions that actually carry a door.
        /// </summary>
        public static void CollectSlots(RoomCategory category, int doorMask, List<SocketSlot> buffer)
        {
            buffer.Clear();
            buffer.Add(SocketSlot.Light);

            for (int i = 0; i < Directions.Cardinal.Length; i++)
            {
                var dir = Directions.Cardinal[i];
                if ((doorMask & LayoutRoom.DirectionMask(dir)) != 0)
                    buffer.Add(SocketSlots.DoorSlot(dir));
            }

            buffer.Add(SocketSlot.PropA);
            buffer.Add(SocketSlot.PropB);
            buffer.Add(SocketSlot.Evidence);
            buffer.Add(SocketSlot.GhostInteract);

            if (HasHideSpot(category))
                buffer.Add(SocketSlot.Hide);
        }
    }
}
