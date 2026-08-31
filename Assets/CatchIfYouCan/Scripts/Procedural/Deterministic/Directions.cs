namespace CatchIfYouCan.Procedural.Deterministic
{
    /// <summary>
    /// Pure direction helpers for Stage A. Mirrors the helpers on RoomSocket, which stay
    /// as the Unity-side entry points and delegate here so there is one implementation.
    /// </summary>
    public static class Directions
    {
        /// <summary>
        /// The four horizontal directions in canonical order. Generation iterates this
        /// FROZEN order; it must never be reordered without a generation version bump.
        /// </summary>
        public static readonly SocketDirection[] Cardinal =
        {
            SocketDirection.North,
            SocketDirection.East,
            SocketDirection.South,
            SocketDirection.West
        };

        public static SocketDirection Opposite(SocketDirection dir)
        {
            switch (dir)
            {
                case SocketDirection.North: return SocketDirection.South;
                case SocketDirection.South: return SocketDirection.North;
                case SocketDirection.East: return SocketDirection.West;
                case SocketDirection.West: return SocketDirection.East;
                case SocketDirection.Up: return SocketDirection.Down;
                case SocketDirection.Down: return SocketDirection.Up;
                default: return SocketDirection.South;
            }
        }

        public static GridCell ToGridOffset(SocketDirection dir)
        {
            switch (dir)
            {
                case SocketDirection.North: return new GridCell(0, 0, 1);
                case SocketDirection.South: return new GridCell(0, 0, -1);
                case SocketDirection.East: return new GridCell(1, 0, 0);
                case SocketDirection.West: return new GridCell(-1, 0, 0);
                case SocketDirection.Up: return new GridCell(0, 1, 0);
                case SocketDirection.Down: return new GridCell(0, -1, 0);
                default: return GridCell.Origin;
            }
        }

        /// <summary>Cardinal rotation index used for canonical rotation storage (0=N, 1=E, 2=S, 3=W).</summary>
        public static int ToRotationIndex(SocketDirection dir)
        {
            switch (dir)
            {
                case SocketDirection.North: return 0;
                case SocketDirection.East: return 1;
                case SocketDirection.South: return 2;
                case SocketDirection.West: return 3;
                default: return 0;
            }
        }

        public static SocketDirection Between(GridCell from, GridCell to)
        {
            int dz = to.Z - from.Z;
            int dx = to.X - from.X;
            int dy = to.Y - from.Y;
            if (dy > 0) return SocketDirection.Up;
            if (dy < 0) return SocketDirection.Down;
            if (dz > 0) return SocketDirection.North;
            if (dz < 0) return SocketDirection.South;
            if (dx > 0) return SocketDirection.East;
            return SocketDirection.West;
        }
    }
}
