using System.Collections.Generic;

namespace CatchIfYouCan.Procedural.Deterministic
{
    /// <summary>
    /// Deterministic axis-aligned occupancy for one room, in integer room-local millimetres.
    ///
    /// This replaces Physics.OverlapBox in prop placement. A physics query reads the live
    /// PhysX scene, whose contents depend on frame timing, deferred Object.Destroy and
    /// whether Physics.SyncTransforms has run - none of which are generation inputs.
    /// Colliders are an OUTPUT of generation and can never feed back into it.
    ///
    /// Everything here is integer arithmetic on values the layout already owns, so the
    /// same seed yields the same accept/reject decisions on every platform.
    /// </summary>
    public sealed class OccupancyGrid
    {
        /// <summary>Wall thickness kept clear at the room edge.</summary>
        public const int WallMarginMm = 150;

        /// <summary>Half-width of the walkable approach kept clear in front of each door.</summary>
        public const int DoorClearHalfWidthMm = 700;

        /// <summary>How far into the room a door's approach zone reaches.</summary>
        public const int DoorClearDepthMm = 1200;

        private readonly struct Rect
        {
            public readonly int MinX, MinZ, MaxX, MaxZ;

            public Rect(int minX, int minZ, int maxX, int maxZ)
            {
                MinX = minX;
                MinZ = minZ;
                MaxX = maxX;
                MaxZ = maxZ;
            }

            public bool Intersects(in Rect other) =>
                MinX < other.MaxX && MaxX > other.MinX &&
                MinZ < other.MaxZ && MaxZ > other.MinZ;

            public bool Contains(in Rect other) =>
                other.MinX >= MinX && other.MaxX <= MaxX &&
                other.MinZ >= MinZ && other.MaxZ <= MaxZ;
        }

        private readonly List<Rect> _occupied = new List<Rect>(8);
        private Rect _interior;

        /// <summary>Resets for a new room. Reuses the internal buffer - no per-room allocation.</summary>
        public void Reset(Vec3i roomSizeMm, int doorMask)
        {
            _occupied.Clear();

            int halfX = roomSizeMm.X / 2;
            int halfZ = roomSizeMm.Z / 2;
            _interior = new Rect(
                -halfX + WallMarginMm,
                -halfZ + WallMarginMm,
                halfX - WallMarginMm,
                halfZ - WallMarginMm);

            // Reserve the approach in front of every door so props never block a doorway.
            for (int i = 0; i < Directions.Cardinal.Length; i++)
            {
                var dir = Directions.Cardinal[i];
                if ((doorMask & LayoutRoom.DirectionMask(dir)) == 0)
                    continue;

                _occupied.Add(DoorZone(dir, halfX, halfZ));
            }
        }

        private static Rect DoorZone(SocketDirection dir, int halfX, int halfZ)
        {
            switch (dir)
            {
                case SocketDirection.North:
                    return new Rect(-DoorClearHalfWidthMm, halfZ - DoorClearDepthMm, DoorClearHalfWidthMm, halfZ);
                case SocketDirection.South:
                    return new Rect(-DoorClearHalfWidthMm, -halfZ, DoorClearHalfWidthMm, -halfZ + DoorClearDepthMm);
                case SocketDirection.East:
                    return new Rect(halfX - DoorClearDepthMm, -DoorClearHalfWidthMm, halfX, DoorClearHalfWidthMm);
                default:
                    return new Rect(-halfX, -DoorClearHalfWidthMm, -halfX + DoorClearDepthMm, DoorClearHalfWidthMm);
            }
        }

        /// <summary>
        /// Tests a footprint centred on a local position and, if it fits, marks it occupied.
        /// Returns false when the prop would leave the room interior or overlap something
        /// already placed.
        /// </summary>
        public bool TryOccupy(Vec3i localCentreMm, Vec3i footprintMm)
        {
            int halfW = footprintMm.X / 2;
            int halfD = footprintMm.Z / 2;

            var candidate = new Rect(
                localCentreMm.X - halfW,
                localCentreMm.Z - halfD,
                localCentreMm.X + halfW,
                localCentreMm.Z + halfD);

            if (!_interior.Contains(candidate))
                return false;

            for (int i = 0; i < _occupied.Count; i++)
            {
                if (_occupied[i].Intersects(candidate))
                    return false;
            }

            _occupied.Add(candidate);
            return true;
        }
    }
}
