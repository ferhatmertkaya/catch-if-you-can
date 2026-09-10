using System;

namespace CatchIfYouCan.Procedural.Deterministic
{
    /// <summary>
    /// Integer room-grid coordinate. X and Z are the horizontal axes (matching Unity's
    /// Y-up convention); Y is the floor level, so basements and attics can be expressed
    /// without changing the layout model.
    /// </summary>
    public readonly struct GridCell : IEquatable<GridCell>, IComparable<GridCell>
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Z;

        public GridCell(int x, int z) : this(x, 0, z) { }

        public GridCell(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static readonly GridCell Origin = new GridCell(0, 0, 0);

        public static GridCell operator +(GridCell a, GridCell b) => new GridCell(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static GridCell operator *(GridCell a, int s) => new GridCell(a.X * s, a.Y * s, a.Z * s);

        public bool Equals(GridCell other) => X == other.X && Y == other.Y && Z == other.Z;

        public override bool Equals(object obj) => obj is GridCell other && Equals(other);

        public override int GetHashCode() => unchecked((X * 73856093) ^ (Y * 19349663) ^ (Z * 83492791));

        public int CompareTo(GridCell other)
        {
            int c = Y.CompareTo(other.Y);
            if (c != 0) return c;
            c = X.CompareTo(other.X);
            if (c != 0) return c;
            return Z.CompareTo(other.Z);
        }

        public override string ToString() => $"({X},{Y},{Z})";
    }
}
