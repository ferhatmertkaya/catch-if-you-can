using System.Text;

namespace CatchIfYouCan.Procedural.Deterministic
{
    /// <summary>
    /// FNV-1a 64-bit, the project's stable hashing contract.
    ///
    /// Explicitly chosen and implemented here because string.GetHashCode() and
    /// object.GetHashCode() are NOT persistence or network contracts: .NET randomises
    /// string hashing per process by default, and the algorithm may change between
    /// runtimes. Those must never appear in a layout hash.
    ///
    /// All multi-byte values are written little-endian explicitly rather than through
    /// BitConverter, whose byte order follows the host architecture.
    /// </summary>
    public struct Fnv1a64
    {
        private const ulong OffsetBasis = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        private ulong _hash;

        public static Fnv1a64 Create() => new Fnv1a64 { _hash = OffsetBasis };

        public ulong Value => _hash;

        public void WriteByte(byte b)
        {
            unchecked
            {
                _hash ^= b;
                _hash *= Prime;
            }
        }

        public void WriteInt32(int value) => WriteUInt32(unchecked((uint)value));

        public void WriteUInt32(uint value)
        {
            WriteByte((byte)(value & 0xFF));
            WriteByte((byte)((value >> 8) & 0xFF));
            WriteByte((byte)((value >> 16) & 0xFF));
            WriteByte((byte)((value >> 24) & 0xFF));
        }

        public void WriteUInt64(ulong value)
        {
            WriteUInt32((uint)(value & 0xFFFFFFFFUL));
            WriteUInt32((uint)((value >> 32) & 0xFFFFFFFFUL));
        }

        public void WriteBool(bool value) => WriteByte(value ? (byte)1 : (byte)0);

        /// <summary>Length-prefixed UTF-8, so "ab"+"c" cannot collide with "a"+"bc".</summary>
        public void WriteString(string value)
        {
            if (value == null)
            {
                WriteInt32(-1);
                return;
            }

            byte[] bytes = Encoding.UTF8.GetBytes(value);
            WriteInt32(bytes.Length);
            for (int i = 0; i < bytes.Length; i++)
                WriteByte(bytes[i]);
        }

        public void WriteVec3i(Vec3i v)
        {
            WriteInt32(v.X);
            WriteInt32(v.Y);
            WriteInt32(v.Z);
        }

        public void WriteGridCell(GridCell c)
        {
            WriteInt32(c.X);
            WriteInt32(c.Y);
            WriteInt32(c.Z);
        }

        /// <summary>Folds a completed sub-hash into this one, for section hashes.</summary>
        public void WriteHash(ulong other) => WriteUInt64(other);

        public static string ToHex(ulong hash) => hash.ToString("X16");

        /// <summary>Short form for logs. Never use a truncated hash for a comparison.</summary>
        public static string ToShortHex(ulong hash) => hash.ToString("X16").Substring(0, 8);
    }
}
