using System;
using System.Collections.Generic;

namespace CatchIfYouCan.Procedural.Deterministic
{
    public readonly struct RoomArchetype
    {
        public readonly string ArchetypeId;
        public readonly RoomCategory Category;
        public readonly Vec3i SizeMm;
        public readonly int VariantCount;
        public readonly int WeightFixed;

        public RoomArchetype(string archetypeId, RoomCategory category, Vec3i sizeMm, int variantCount, int weightFixed)
        {
            ArchetypeId = archetypeId;
            Category = category;
            SizeMm = sizeMm;
            VariantCount = variantCount < 1 ? 1 : variantCount;
            WeightFixed = weightFixed;
        }
    }

    public readonly struct PropArchetype
    {
        public readonly string PropDefinitionId;
        public readonly PropKind Kind;
        public readonly Vec3i BoundsMm;
        public readonly int WeightFixed;
        /// <summary>Room categories this prop may occupy. Empty means "any".</summary>
        public readonly RoomCategory[] AllowedCategories;

        public PropArchetype(string propDefinitionId, PropKind kind, Vec3i boundsMm, int weightFixed,
            RoomCategory[] allowedCategories)
        {
            PropDefinitionId = propDefinitionId;
            Kind = kind;
            BoundsMm = boundsMm;
            WeightFixed = weightFixed;
            AllowedCategories = allowedCategories ?? Array.Empty<RoomCategory>();
        }

        public bool MatchesRoom(RoomCategory category)
        {
            if (AllowedCategories.Length == 0)
                return true;

            for (int i = 0; i < AllowedCategories.Length; i++)
            {
                if (AllowedCategories[i] == category)
                    return true;
            }

            return false;
        }
    }

    /// <summary>
    /// The engine-free view of authored content that generation is allowed to read.
    ///
    /// Content parity is part of determinism: a seed indexes into this set, so two clients
    /// with different content produce different layouts from the same seed. The
    /// <see cref="ContentHash"/> is compared alongside the layout hash, and a mismatch
    /// there is a DIFFERENT error - it means the clients are running different builds and
    /// no amount of seed agreement will help.
    ///
    /// Entries are sorted by stable id at construction, so authoring order (for example the
    /// order of a [SerializeField] array in the inspector) can never change generation.
    /// </summary>
    public sealed class ContentSnapshot
    {
        public IReadOnlyList<RoomArchetype> RoomArchetypes { get; }
        public IReadOnlyList<PropArchetype> PropArchetypes { get; }
        public ulong ContentHash { get; }

        public ContentSnapshot(IEnumerable<RoomArchetype> roomArchetypes, IEnumerable<PropArchetype> propArchetypes)
        {
            var rooms = new List<RoomArchetype>(roomArchetypes ?? Array.Empty<RoomArchetype>());
            var props = new List<PropArchetype>(propArchetypes ?? Array.Empty<PropArchetype>());

            rooms.Sort((a, b) => string.CompareOrdinal(a.ArchetypeId, b.ArchetypeId));
            props.Sort((a, b) => string.CompareOrdinal(a.PropDefinitionId, b.PropDefinitionId));

            RoomArchetypes = rooms;
            PropArchetypes = props;
            ContentHash = ComputeContentHash(rooms, props);
        }

        private static ulong ComputeContentHash(List<RoomArchetype> rooms, List<PropArchetype> props)
        {
            var h = Fnv1a64.Create();
            h.WriteString(GenerationVersion.AlgorithmId);
            h.WriteInt32(GenerationVersion.Current);

            h.WriteInt32(rooms.Count);
            for (int i = 0; i < rooms.Count; i++)
            {
                var r = rooms[i];
                h.WriteString(r.ArchetypeId);
                h.WriteInt32((int)r.Category);
                h.WriteVec3i(r.SizeMm);
                h.WriteInt32(r.VariantCount);
                h.WriteInt32(r.WeightFixed);
            }

            h.WriteInt32(props.Count);
            for (int i = 0; i < props.Count; i++)
            {
                var p = props[i];
                h.WriteString(p.PropDefinitionId);
                h.WriteInt32((int)p.Kind);
                h.WriteVec3i(p.BoundsMm);
                h.WriteInt32(p.WeightFixed);
                h.WriteInt32(p.AllowedCategories.Length);
                for (int c = 0; c < p.AllowedCategories.Length; c++)
                    h.WriteInt32((int)p.AllowedCategories[c]);
            }

            return h.Value;
        }

        /// <summary>Room archetypes for a category, in canonical id order. Never allocates a dictionary.</summary>
        public void CollectRoomArchetypes(RoomCategory category, List<RoomArchetype> buffer)
        {
            buffer.Clear();
            for (int i = 0; i < RoomArchetypes.Count; i++)
            {
                if (RoomArchetypes[i].Category == category)
                    buffer.Add(RoomArchetypes[i]);
            }
        }

        public void CollectPropArchetypes(RoomCategory category, PropKind kind, List<PropArchetype> buffer)
        {
            buffer.Clear();
            for (int i = 0; i < PropArchetypes.Count; i++)
            {
                var p = PropArchetypes[i];
                if (p.Kind == kind && p.MatchesRoom(category))
                    buffer.Add(p);
            }
        }

        /// <summary>A deterministic fallback used when no content is authored, so tests and empty scenes still generate.</summary>
        public static ContentSnapshot CreateFallback()
        {
            var rooms = new List<RoomArchetype>();
            foreach (RoomCategory category in Enum.GetValues(typeof(RoomCategory)))
            {
                rooms.Add(new RoomArchetype(
                    "ARCH_" + category,
                    category,
                    new Vec3i(6000, 3000, 6000),
                    1,
                    Quantize.Weight(1f)));
            }

            var props = new List<PropArchetype>
            {
                new PropArchetype("PROP_CRATE", PropKind.Prop, new Vec3i(700, 700, 700), Quantize.Weight(1f), null),
                new PropArchetype("PROP_LAMP", PropKind.Prop, new Vec3i(400, 1400, 400), Quantize.Weight(0.7f), null),
                new PropArchetype("FURN_SHELF", PropKind.Furniture, new Vec3i(1600, 2000, 500), Quantize.Weight(1f), null),
                new PropArchetype("FURN_TABLE", PropKind.Furniture, new Vec3i(1400, 800, 900), Quantize.Weight(1f), null),
            };

            return new ContentSnapshot(rooms, props);
        }
    }
}
