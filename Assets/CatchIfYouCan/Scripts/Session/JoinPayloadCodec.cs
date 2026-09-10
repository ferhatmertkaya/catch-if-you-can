using System;
using System.Text;
using CatchIfYouCan.Procedural.Deterministic;

namespace CatchIfYouCan.Session
{
    /// <summary>
    /// Turns a <see cref="MatchConfig"/> into bytes and back, in a fixed, bounded layout.
    ///
    /// <para>
    /// This is the first thing an untrusted peer sends and the last thing anyone should be
    /// casual about. It is a connection payload from a machine that has not been admitted yet,
    /// so it is decoded before any check has passed - which makes every unbounded read in it a
    /// way to make the host allocate or throw on demand.
    /// </para>
    ///
    /// <para>
    /// Hence: a fixed layout with one variable-length field, a hard cap on that field's length,
    /// a total size cap, and a decoder that returns false rather than throwing on anything
    /// unexpected. Big-endian, so the bytes mean the same thing on every architecture rather
    /// than depending on the sender's.
    /// </para>
    ///
    /// <para>
    /// Deliberately not JSON. A text payload here is a parser on the untrusted edge, an
    /// allocation per field, and a size that depends on how long somebody named a map.
    /// </para>
    /// </summary>
    public static class JoinPayloadCodec
    {
        /// <summary>
        /// A magic number, so a payload from something that is not this game is rejected as
        /// garbage rather than parsed as a very strange config. "CIYC" in ASCII.
        /// </summary>
        private const uint Magic = 0x43495943;

        /// <summary>
        /// Longest map id accepted, in UTF-8 bytes. Ids are short constants in this project;
        /// this is far above any real one and far below anything worth allocating for.
        /// </summary>
        public const int MaxMapIdBytes = 64;

        /// <summary>4 magic + 4 protocol + 4 generation + 4 seed + 8 content + 2 length.</summary>
        private const int HeaderBytes = 26;

        /// <summary>The largest payload this will ever produce or accept.</summary>
        public const int MaxPayloadBytes = HeaderBytes + MaxMapIdBytes;

        /// <summary>
        /// Encodes a config. Returns null when the config cannot be represented - a map id
        /// longer than the cap, which is a content bug rather than a network one.
        /// </summary>
        public static byte[] Encode(in MatchConfig config)
        {
            byte[] mapId = Encoding.UTF8.GetBytes(config.MapDefinitionId ?? string.Empty);
            if (mapId.Length > MaxMapIdBytes)
                return null;

            var buffer = new byte[HeaderBytes + mapId.Length];
            int at = 0;

            WriteUInt32(buffer, ref at, Magic);
            WriteUInt32(buffer, ref at, unchecked((uint)config.ProtocolVersion));
            WriteUInt32(buffer, ref at, unchecked((uint)config.GenerationVersion));
            WriteUInt32(buffer, ref at, unchecked((uint)config.Seed));
            WriteUInt64(buffer, ref at, config.ContentHash);
            WriteUInt16(buffer, ref at, (ushort)mapId.Length);

            Buffer.BlockCopy(mapId, 0, buffer, at, mapId.Length);
            return buffer;
        }

        /// <summary>
        /// Decodes a payload. False on anything at all unexpected, and never throws.
        ///
        /// <para>
        /// The caller treats false as <see cref="JoinVerdict.MalformedConfig"/>. It must not
        /// treat it as a reason to look closer: a peer that sends a payload this cannot read
        /// has already failed the only test that matters at this stage.
        /// </para>
        /// </summary>
        public static bool TryDecode(byte[] payload, out MatchConfig config)
        {
            config = default;

            if (payload == null || payload.Length < HeaderBytes || payload.Length > MaxPayloadBytes)
                return false;

            int at = 0;

            if (ReadUInt32(payload, ref at) != Magic)
                return false;

            int protocolVersion = unchecked((int)ReadUInt32(payload, ref at));
            int generationVersion = unchecked((int)ReadUInt32(payload, ref at));
            int seed = unchecked((int)ReadUInt32(payload, ref at));
            ulong contentHash = ReadUInt64(payload, ref at);
            int mapIdLength = ReadUInt16(payload, ref at);

            // The declared length must match what is actually there. A payload claiming more
            // than it carries is the classic way to read past the end of a buffer.
            if (mapIdLength > MaxMapIdBytes || at + mapIdLength != payload.Length)
                return false;

            string mapId;
            try
            {
                mapId = Encoding.UTF8.GetString(payload, at, mapIdLength);
            }
            catch (ArgumentException)
            {
                // Invalid UTF-8. Rejected rather than repaired.
                return false;
            }

            config = new MatchConfig(protocolVersion, generationVersion, seed, mapId, contentHash);
            return true;
        }

        private static void WriteUInt16(byte[] b, ref int at, ushort value)
        {
            b[at++] = (byte)(value >> 8);
            b[at++] = (byte)value;
        }

        private static void WriteUInt32(byte[] b, ref int at, uint value)
        {
            b[at++] = (byte)(value >> 24);
            b[at++] = (byte)(value >> 16);
            b[at++] = (byte)(value >> 8);
            b[at++] = (byte)value;
        }

        private static void WriteUInt64(byte[] b, ref int at, ulong value)
        {
            for (int shift = 56; shift >= 0; shift -= 8)
                b[at++] = (byte)(value >> shift);
        }

        private static ushort ReadUInt16(byte[] b, ref int at)
        {
            ushort value = (ushort)((b[at] << 8) | b[at + 1]);
            at += 2;
            return value;
        }

        private static uint ReadUInt32(byte[] b, ref int at)
        {
            uint value = ((uint)b[at] << 24) | ((uint)b[at + 1] << 16) |
                         ((uint)b[at + 2] << 8) | b[at + 3];
            at += 4;
            return value;
        }

        private static ulong ReadUInt64(byte[] b, ref int at)
        {
            ulong value = 0;
            for (int i = 0; i < 8; i++)
                value = (value << 8) | b[at + i];

            at += 8;
            return value;
        }
    }
}
