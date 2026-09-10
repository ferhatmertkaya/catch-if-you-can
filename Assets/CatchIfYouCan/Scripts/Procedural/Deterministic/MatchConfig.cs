using System;

namespace CatchIfYouCan.Procedural.Deterministic
{
    /// <summary>
    /// The authoritative match configuration a host broadcasts as <c>MissionStart</c>
    /// (Docs/NETWORKING.md §3) and every client must agree with before it generates anything.
    ///
    /// This is the whole payload the house costs to replicate: four fields plus a version
    /// pair, instead of thirty rooms and several hundred props. That trade is the entire
    /// justification for the determinism work (NETWORKING.md §2).
    ///
    /// Deliberately excluded, per DETERMINISM.md §2.1b: ghost type, traits and tier, the
    /// objective set, and which evidence the ghost yields. Those are host state revealed
    /// through play. The seed is public to every client at join time, so anything derived
    /// from it is knowable by a client before the round starts — putting the round's answer
    /// in here would hand it out.
    ///
    /// Engine-free by construction, so the standalone determinism harness can execute the
    /// join handshake without Unity.
    /// </summary>
    public readonly struct MatchConfig : IEquatable<MatchConfig>
    {
        /// <summary>Protocol revision of the sender (<see cref="MultiplayerProtocol.Version"/>).</summary>
        public readonly int ProtocolVersion;

        /// <summary>Generation algorithm identity (<see cref="GenerationVersion.Current"/>).</summary>
        public readonly int GenerationVersion;

        /// <summary>Host-rolled session seed. Never rolled by a client (NETWORKING.md §3).</summary>
        public readonly int Seed;

        /// <summary>Which map definition the house is generated from.</summary>
        public readonly string MapDefinitionId;

        /// <summary>Hash of the baked content set both peers must share.</summary>
        public readonly ulong ContentHash;

        public MatchConfig(int protocolVersion, int generationVersion, int seed,
            string mapDefinitionId, ulong contentHash)
        {
            ProtocolVersion = protocolVersion;
            GenerationVersion = generationVersion;
            Seed = seed;
            MapDefinitionId = mapDefinitionId;
            ContentHash = contentHash;
        }

        /// <summary>
        /// Builds the config a host broadcasts. The seed must already have been drawn from
        /// <c>SessionSeedSource</c> by the authority — this type does not roll one, so that no
        /// client-side call site can accidentally mint an authoritative seed by constructing a
        /// config.
        /// </summary>
        public static MatchConfig CreateAuthoritative(int seed, MapDefinition map, ContentSnapshot content)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (content == null) throw new ArgumentNullException(nameof(content));

            return new MatchConfig(
                MultiplayerProtocol.Version,
                Deterministic.GenerationVersion.Current,
                seed,
                map.MapDefinitionId,
                content.ContentHash);
        }

        /// <summary>
        /// Zero is reserved as "unset" by <c>SessionSeedSource</c> and by mission data, so a
        /// zero seed here means the config was never populated by an authority.
        /// </summary>
        public bool HasSeed => Seed != 0;

        public bool IsWellFormed =>
            HasSeed &&
            ProtocolVersion > 0 &&
            GenerationVersion > 0 &&
            !string.IsNullOrEmpty(MapDefinitionId);

        /// <summary>
        /// Stable digest of the whole config, for cheap logging and equality in diagnostics.
        /// Uses the project's canonical FNV-1a rather than <c>GetHashCode</c>, whose string
        /// hashing is randomised per process and therefore useless across peers.
        /// </summary>
        public ulong ConfigHash()
        {
            var h = Fnv1a64.Create();
            h.WriteInt32(ProtocolVersion);
            h.WriteInt32(GenerationVersion);
            h.WriteInt32(Seed);
            h.WriteString(MapDefinitionId ?? string.Empty);
            h.WriteUInt64(ContentHash);
            return h.Value;
        }

        public bool Equals(MatchConfig other) =>
            ProtocolVersion == other.ProtocolVersion &&
            GenerationVersion == other.GenerationVersion &&
            Seed == other.Seed &&
            ContentHash == other.ContentHash &&
            string.Equals(MapDefinitionId, other.MapDefinitionId, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is MatchConfig other && Equals(other);

        public override int GetHashCode() => unchecked((int)ConfigHash());

        public override string ToString() =>
            $"MatchConfig(protocol={ProtocolVersion} gen={GenerationVersion} seed={Seed} " +
            $"map={MapDefinitionId} content={Fnv1a64.ToHex(ContentHash)})";
    }
}
