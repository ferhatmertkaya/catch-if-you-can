using System.Collections.Generic;

namespace CatchIfYouCan.Procedural.Deterministic
{
    /// <summary>
    /// Deterministic PRNG for authoritative procedural generation (PCG32, pcg_oneseq_32).
    ///
    /// Pure 64-bit integer arithmetic: bit-identical on Mono, IL2CPP, ARM64 and x64,
    /// independent of compiler optimisation settings. This is the ONLY random source
    /// permitted inside deterministic generation.
    ///
    /// Do not use UnityEngine.Random (process-global, shared with cosmetic systems,
    /// draw count depends on frame rate) or System.Random (algorithm is documented as
    /// implementation-defined and changed in .NET Core 3.0).
    ///
    /// The constants and the seeding sequence below are frozen and are part of
    /// <see cref="GenerationVersion"/>. Changing any of them changes every layout ever
    /// produced from a stored seed and REQUIRES a generation version bump.
    /// </summary>
    public struct CiycRandom
    {
        private const ulong Multiplier = 6364136223846793005UL;

        private ulong _state;
        private readonly ulong _increment;

        public CiycRandom(ulong seed, ulong stream)
        {
            _state = 0UL;
            _increment = (stream << 1) | 1UL; // must be odd
            NextUInt();
            unchecked { _state += seed; }
            NextUInt();
        }

        /// <summary>Creates a stream-isolated generator from a session seed.</summary>
        public static CiycRandom ForStream(int seed, CiycStream stream)
        {
            return new CiycRandom(unchecked((ulong)(uint)seed), (ulong)stream);
        }

        /// <summary>
        /// Creates a stream-isolated generator for a retry attempt. The ATTEMPT varies the
        /// seed, never the stream, so streams stay isolated across attempts. The golden-ratio
        /// constant avoids the collisions that a small linear step (seed + attempt * k)
        /// produces between nearby seeds.
        /// </summary>
        public static CiycRandom ForStream(int seed, CiycStream stream, int attempt)
        {
            ulong mixed = unchecked((ulong)(uint)seed + (ulong)(uint)attempt * 0x9E3779B97F4A7C15UL);
            return new CiycRandom(mixed, (ulong)stream);
        }

        public uint NextUInt()
        {
            unchecked
            {
                ulong old = _state;
                _state = old * Multiplier + _increment;
                uint xorshifted = (uint)(((old >> 18) ^ old) >> 27);
                int rot = (int)(old >> 59);
                return (xorshifted >> rot) | (xorshifted << ((-rot) & 31));
            }
        }

        /// <summary>
        /// Unbiased draw in [0, bound). Rejection sampling rather than modulo folding:
        /// modulo would bias the low values, and the bias would differ with bound.
        /// </summary>
        public uint NextUInt(uint bound)
        {
            if (bound == 0u)
                return 0u;

            uint threshold = (uint)((0x1_0000_0000UL - bound) % bound);
            while (true)
            {
                uint r = NextUInt();
                if (r >= threshold)
                    return r % bound;
            }
        }

        /// <summary>Draw in [minInclusive, maxExclusive). Returns minInclusive if the range is empty.</summary>
        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
                return minInclusive;

            uint span = unchecked((uint)(maxExclusive - minInclusive));
            return minInclusive + (int)NextUInt(span);
        }

        public bool NextBool() => (NextUInt() & 1u) != 0u;

        /// <summary>
        /// Draw in [0,1) with an exact 24-bit mantissa: one integer shift and one multiply
        /// by a power of two, so there is no rounding ambiguity on any platform.
        /// </summary>
        public float NextFloat() => (NextUInt() >> 8) * (1.0f / 16777216.0f);

        public float NextFloat(float min, float max) => min + (max - min) * NextFloat();

        /// <summary>Fisher-Yates. Never shuffle with a random sort key (see Docs/DETERMINISM.md R4).</summary>
        public void Shuffle<T>(IList<T> items)
        {
            if (items == null)
                return;

            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = (int)NextUInt((uint)(i + 1));
                T tmp = items[i];
                items[i] = items[j];
                items[j] = tmp;
            }
        }

        /// <summary>
        /// Weighted pick over a parallel weight array. Accumulation order is the array order,
        /// which the caller must have already canonicalised; IEEE-754 addition is exact given
        /// a fixed order, so this is reproducible.
        /// </summary>
        public int PickWeightedIndex(IReadOnlyList<float> weights)
        {
            if (weights == null || weights.Count == 0)
                return -1;

            float total = 0f;
            for (int i = 0; i < weights.Count; i++)
                total += weights[i] > 0.01f ? weights[i] : 0.01f;

            float roll = NextFloat(0f, total);
            float cumulative = 0f;
            for (int i = 0; i < weights.Count; i++)
            {
                cumulative += weights[i] > 0.01f ? weights[i] : 0.01f;
                if (roll <= cumulative)
                    return i;
            }

            return weights.Count - 1;
        }
    }
}
