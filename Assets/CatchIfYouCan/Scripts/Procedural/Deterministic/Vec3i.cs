using System;

namespace CatchIfYouCan.Procedural.Deterministic
{
    /// <summary>
    /// Integer millimetre position. Stage A does all position math in this type so that
    /// no generation decision ever depends on floating point rounding.
    /// </summary>
    public readonly struct Vec3i : IEquatable<Vec3i>, IComparable<Vec3i>
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Z;

        public Vec3i(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static readonly Vec3i Zero = new Vec3i(0, 0, 0);

        public static Vec3i FromMetres(float x, float y, float z) =>
            new Vec3i(Quantize.Millimetres(x), Quantize.Millimetres(y), Quantize.Millimetres(z));

        public static Vec3i operator +(Vec3i a, Vec3i b) => new Vec3i(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3i operator -(Vec3i a, Vec3i b) => new Vec3i(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3i operator *(Vec3i a, int s) => new Vec3i(a.X * s, a.Y * s, a.Z * s);

        public Vec3i HalvedXYZ() => new Vec3i(X / 2, Y / 2, Z / 2);

        /// <summary>Squared horizontal distance in mm^2. Long to avoid overflow across a large house.</summary>
        public long HorizontalDistanceSquared(Vec3i other)
        {
            long dx = X - other.X;
            long dz = Z - other.Z;
            return dx * dx + dz * dz;
        }

        public bool Equals(Vec3i other) => X == other.X && Y == other.Y && Z == other.Z;

        public override bool Equals(object obj) => obj is Vec3i other && Equals(other);

        /// <summary>
        /// Present so the type behaves correctly in local dictionaries. NEVER used as a
        /// persistence or network contract - hashing goes through Fnv1a64 (Docs/DETERMINISM.md §8).
        /// </summary>
        public override int GetHashCode() => unchecked((X * 397) ^ (Y * 31) ^ Z);

        public int CompareTo(Vec3i other)
        {
            int c = X.CompareTo(other.X);
            if (c != 0) return c;
            c = Y.CompareTo(other.Y);
            if (c != 0) return c;
            return Z.CompareTo(other.Z);
        }

        public override string ToString() => $"({X},{Y},{Z})mm";
    }
}
