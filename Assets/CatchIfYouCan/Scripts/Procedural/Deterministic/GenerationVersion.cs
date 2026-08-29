namespace CatchIfYouCan.Procedural.Deterministic
{
    /// <summary>
    /// Identity of the generation algorithm.
    ///
    /// A layout is identified by (Current, mapDefinitionId, seed). A stored seed is
    /// meaningless without the version that produced it, so old recorded seeds keep
    /// naming the algorithm that generated them.
    ///
    /// INCREMENT <see cref="Current"/> whenever any of these change:
    ///   - CiycRandom constants or seeding sequence
    ///   - the order or count of draws in any generation stream
    ///   - stream id assignments
    ///   - the canonical hash layout in LayoutHasher
    ///   - Quantize scales
    ///   - any rule that alters which layout a seed produces
    ///
    /// Bumping it invalidates the golden seed table; regenerate it in the same commit
    /// (Tools > Catch If You Can > Determinism > Generate Golden Seeds).
    /// </summary>
    public static class GenerationVersion
    {
        /// <summary>Version 1: PCG32 streams, pure Stage A generation, AABB prop occupancy.</summary>
        public const int Current = 1;

        /// <summary>Human-readable algorithm identity, recorded in diagnostics.</summary>
        public const string AlgorithmId = "ciyc-house-gen-v1-pcg32";
    }
}
