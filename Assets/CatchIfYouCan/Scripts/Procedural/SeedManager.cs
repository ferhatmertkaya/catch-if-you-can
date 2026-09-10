using System;
using CatchIfYouCan.Procedural.Deterministic;

namespace CatchIfYouCan.Procedural
{
    /// <summary>
    /// Owns the session seed and hands out deterministic RNG streams.
    ///
    /// What changed and why:
    ///
    ///  - It no longer calls UnityEngine.Random.InitState. That seeded a single
    ///    process-global stream shared with ~100 cosmetic call sites (audio, UI flicker,
    ///    footsteps) whose draw COUNT depends on frame rate and on how long a loading
    ///    screen was visible. Seeding it created the false impression that anything reading
    ///    it was reproducible.
    ///
    ///  - It no longer exposes System.Random. That algorithm is documented as
    ///    implementation-defined and changed in .NET Core 3.0; Mono and IL2CPP agree today
    ///    by accident of a shared corlib, not by contract.
    ///
    /// The seed itself is not generated here any more either - see <see cref="SessionSeedSource"/>.
    /// A seed only has to be AGREED between clients, not reproducible, so rolling it is a
    /// separate concern from consuming it.
    /// </summary>
    public static class SeedManager
    {
        public const int KnownGoodSeed = 424242;

        private static int _currentSeed = KnownGoodSeed;

        public static int CurrentSeed => _currentSeed;

        public static void SetSeed(int seed)
        {
            _currentSeed = seed;
        }

        public static int GetSeed() => _currentSeed;

        /// <summary>
        /// A deterministic generator for one subsystem. Always name the stream: sharing a
        /// stream between subsystems means adding a draw in one silently moves the other.
        /// </summary>
        public static CiycRandom CreateRandom(CiycStream stream) =>
            CiycRandom.ForStream(_currentSeed, stream);

        public static CiycRandom CreateRandom(int seed, CiycStream stream) =>
            CiycRandom.ForStream(seed, stream);

        public static CiycRandom CreateRandom(int seed, CiycStream stream, int attempt) =>
            CiycRandom.ForStream(seed, stream, attempt);
    }

    /// <summary>
    /// Produces a fresh session seed.
    ///
    /// This is host-authoritative work: in multiplayer the host rolls the seed once and
    /// replicates it, and clients never roll their own (Docs/NETWORKING.md §3). It uses a
    /// cryptographic source rather than UnityEngine.Random so that seed selection cannot be
    /// perturbed by - or perturb - any cosmetic system sharing that global stream.
    /// </summary>
    public static class SessionSeedSource
    {
        public static int Next()
        {
            Span<byte> bytes = stackalloc byte[4];
            System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
            int value = BitConverter.ToInt32(bytes);

            // Zero is reserved as "unset" in mission data, and int.MinValue has no positive
            // counterpart, which makes diagnostics awkward.
            if (value == 0) return KnownFallback;
            if (value == int.MinValue) return int.MaxValue;
            return value;
        }

        private const int KnownFallback = 424242;
    }
}
