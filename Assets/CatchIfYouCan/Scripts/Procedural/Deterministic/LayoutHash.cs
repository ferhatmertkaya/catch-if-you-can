using System.Collections.Generic;
using System.Text;

namespace CatchIfYouCan.Procedural.Deterministic
{
    /// <summary>
    /// The canonical hash of a layout, split into sections so a divergence names the stage
    /// that broke instead of just saying "different".
    ///
    /// Per-section hashes are cheap and are the difference between a five-minute fix and a
    /// week of bisecting, so they are always computed, not only on failure.
    /// </summary>
    public readonly struct LayoutHash
    {
        public readonly int GenerationVersion;
        public readonly int Seed;
        public readonly string MapDefinitionId;
        public readonly ulong ContentHash;

        public readonly ulong IdentityHash;
        public readonly ulong RoomsHash;
        public readonly ulong ConnectionsHash;
        public readonly ulong DoorsHash;
        public readonly ulong FurnitureHash;
        public readonly ulong PropsHash;
        public readonly ulong GameplaySpawnHash;
        public readonly ulong FinalHash;

        public LayoutHash(int generationVersion, int seed, string mapDefinitionId, ulong contentHash,
            ulong identityHash, ulong roomsHash, ulong connectionsHash, ulong doorsHash,
            ulong furnitureHash, ulong propsHash, ulong gameplaySpawnHash, ulong finalHash)
        {
            GenerationVersion = generationVersion;
            Seed = seed;
            MapDefinitionId = mapDefinitionId;
            ContentHash = contentHash;
            IdentityHash = identityHash;
            RoomsHash = roomsHash;
            ConnectionsHash = connectionsHash;
            DoorsHash = doorsHash;
            FurnitureHash = furnitureHash;
            PropsHash = propsHash;
            GameplaySpawnHash = gameplaySpawnHash;
            FinalHash = finalHash;
        }

        public string Final => Fnv1a64.ToHex(FinalHash);

        /// <summary>Human-readable diagnostic block. This is what a mismatch report carries.</summary>
        public string ToReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Generation Version: {GenerationVersion}");
            sb.AppendLine($"Seed:               {Seed}");
            sb.AppendLine($"Map:                {MapDefinitionId}");
            sb.AppendLine($"Content:            {Fnv1a64.ToHex(ContentHash)}");
            sb.AppendLine();
            sb.AppendLine($"Identity:           {Fnv1a64.ToHex(IdentityHash)}");
            sb.AppendLine($"Rooms:              {Fnv1a64.ToHex(RoomsHash)}");
            sb.AppendLine($"Connections:        {Fnv1a64.ToHex(ConnectionsHash)}");
            sb.AppendLine($"Doors:              {Fnv1a64.ToHex(DoorsHash)}");
            sb.AppendLine($"Furniture:          {Fnv1a64.ToHex(FurnitureHash)}");
            sb.AppendLine($"Props:              {Fnv1a64.ToHex(PropsHash)}");
            sb.AppendLine($"GameplaySpawns:     {Fnv1a64.ToHex(GameplaySpawnHash)}");
            sb.AppendLine();
            sb.AppendLine($"FINAL:              {Fnv1a64.ToHex(FinalHash)}");
            return sb.ToString();
        }

        /// <summary>Names the first section that differs, for a fast mismatch triage.</summary>
        public string DescribeDifference(in LayoutHash other)
        {
            if (GenerationVersion != other.GenerationVersion)
                return $"Generation version differs: {GenerationVersion} vs {other.GenerationVersion}. The clients are running different builds.";
            if (!string.Equals(MapDefinitionId, other.MapDefinitionId, System.StringComparison.Ordinal))
                return $"Map differs: {MapDefinitionId} vs {other.MapDefinitionId}.";
            if (ContentHash != other.ContentHash)
                return $"Content differs: {Fnv1a64.ToHex(ContentHash)} vs {Fnv1a64.ToHex(other.ContentHash)}. The clients are running different content revisions.";
            if (Seed != other.Seed)
                return $"Seed differs: {Seed} vs {other.Seed}.";
            if (RoomsHash != other.RoomsHash)
                return "Rooms section differs.";
            if (ConnectionsHash != other.ConnectionsHash)
                return "Connections section differs.";
            if (DoorsHash != other.DoorsHash)
                return "Doors section differs.";
            if (FurnitureHash != other.FurnitureHash)
                return "Furniture section differs.";
            if (PropsHash != other.PropsHash)
                return "Props section differs.";
            if (GameplaySpawnHash != other.GameplaySpawnHash)
                return "Gameplay spawn section differs.";
            if (FinalHash != other.FinalHash)
                return "Final hash differs but no section does - the hash composition itself is inconsistent.";
            return "No difference.";
        }
    }

    public static class LayoutHasher
    {
        /// <summary>
        /// Hashes a layout in a canonical order fixed by THIS code, not by whatever order
        /// the builder happened to produce.
        ///
        /// Every collection is re-sorted here into a local buffer before it is written, so
        /// the hash is independent of insertion order even if a future change to the builder
        /// (or a hand-built layout, or a deserialiser) hands over a differently ordered list.
        /// Trusting the caller's ordering would make the hash silently sensitive to a
        /// refactor that nobody would think to re-test.
        /// </summary>
        public static LayoutHash Compute(HouseLayout layout)
        {
            ulong identity = HashIdentity(layout);
            ulong rooms = HashRooms(layout);
            ulong connections = HashConnections(layout);
            ulong doors = HashDoors(layout);
            ulong furniture = HashProps(layout.Furniture);
            ulong props = HashProps(layout.Props);
            ulong spawns = HashGameplaySpawns(layout);

            var final = Fnv1a64.Create();
            final.WriteHash(identity);
            final.WriteHash(rooms);
            final.WriteHash(connections);
            final.WriteHash(doors);
            final.WriteHash(furniture);
            final.WriteHash(props);
            final.WriteHash(spawns);

            return new LayoutHash(
                layout.GenerationVersion, layout.Seed, layout.MapDefinitionId, layout.ContentHash,
                identity, rooms, connections, doors, furniture, props, spawns, final.Value);
        }

        private static ulong HashIdentity(HouseLayout layout)
        {
            var h = Fnv1a64.Create();
            h.WriteString(GenerationVersion.AlgorithmId);
            h.WriteInt32(layout.GenerationVersion);
            h.WriteInt32(layout.Seed);
            h.WriteString(layout.MapDefinitionId);
            h.WriteUInt64(layout.ContentHash);
            return h.Value;
        }

        private static ulong HashRooms(HouseLayout layout)
        {
            var ordered = new List<LayoutRoom>(layout.Rooms);
            ordered.Sort((a, b) => a.RoomId.CompareTo(b.RoomId));

            var h = Fnv1a64.Create();
            h.WriteInt32(ordered.Count);
            for (int i = 0; i < ordered.Count; i++)
            {
                var r = ordered[i];
                h.WriteInt32(r.RoomId);
                h.WriteString(r.ArchetypeId);
                h.WriteInt32((int)r.Category);
                h.WriteGridCell(r.Cell);
                h.WriteInt32(r.RotationIndex);
                h.WriteVec3i(r.PositionMm);
                h.WriteVec3i(r.SizeMm);
                h.WriteInt32(r.VariantIndex);
                h.WriteInt32(r.DoorMask);
                h.WriteInt32(r.OpenMask);
            }

            return h.Value;
        }

        private static ulong HashConnections(HouseLayout layout)
        {
            var ordered = new List<LayoutConnection>(layout.Connections);
            ordered.Sort(CompareConnections);

            var h = Fnv1a64.Create();
            h.WriteInt32(ordered.Count);
            for (int i = 0; i < ordered.Count; i++)
            {
                var c = ordered[i];
                h.WriteInt32(c.ConnectionId);
                h.WriteInt32(c.RoomAId);
                h.WriteInt32(c.RoomBId);
                h.WriteInt32((int)c.DirectionFromA);
            }

            return h.Value;
        }

        private static ulong HashDoors(HouseLayout layout)
        {
            var ordered = new List<LayoutDoor>(layout.Doors);
            ordered.Sort(CompareDoors);

            var h = Fnv1a64.Create();
            h.WriteInt32(ordered.Count);
            for (int i = 0; i < ordered.Count; i++)
            {
                var d = ordered[i];
                h.WriteInt32(d.DoorId);
                h.WriteInt32(d.RoomAId);
                h.WriteInt32(d.RoomBId);
                h.WriteInt32((int)d.SocketASlot);
                h.WriteInt32((int)d.SocketBSlot);
                h.WriteVec3i(d.PositionMm);
                h.WriteInt32(d.RotationIndex);
            }

            return h.Value;
        }

        private static ulong HashProps(IReadOnlyList<LayoutProp> placements)
        {
            var ordered = new List<LayoutProp>(placements);
            ordered.Sort(CompareProps);

            var h = Fnv1a64.Create();
            h.WriteInt32(ordered.Count);
            for (int i = 0; i < ordered.Count; i++)
            {
                var p = ordered[i];
                h.WriteInt32(p.PropInstanceId);
                h.WriteString(p.PropDefinitionId);
                h.WriteInt32((int)p.Kind);
                h.WriteInt32(p.RoomId);
                h.WriteInt32((int)p.Slot);
                h.WriteVec3i(p.PositionMm);
                h.WriteInt32(p.RotationIndex);
            }

            return h.Value;
        }

        private static ulong HashGameplaySpawns(HouseLayout layout)
        {
            var h = Fnv1a64.Create();

            h.WriteInt32(layout.EntranceRoomId);
            h.WriteInt32(layout.GhostRoomId);
            h.WriteInt32(layout.WeatherIndex);

            WriteAnchors(ref h, layout.HideSpots);
            WriteAnchors(ref h, layout.EquipmentSpawns);
            WriteAnchors(ref h, layout.EvidencePoints);

            var candidates = new List<LayoutGhostCandidate>(layout.GhostRoomCandidates);
            // Rank order re-derived here, so it does not depend on how the builder emitted it.
            candidates.Sort((a, b) =>
            {
                int c = b.ScoreFixed.CompareTo(a.ScoreFixed);
                if (c != 0) return c;
                return a.RoomId.CompareTo(b.RoomId);
            });

            h.WriteInt32(candidates.Count);
            for (int i = 0; i < candidates.Count; i++)
            {
                h.WriteInt32(candidates[i].RoomId);
                h.WriteInt32(candidates[i].ScoreFixed);
            }

            return h.Value;
        }

        private static void WriteAnchors(ref Fnv1a64 h, IReadOnlyList<LayoutAnchor> anchors)
        {
            var ordered = new List<LayoutAnchor>(anchors);
            ordered.Sort(CompareAnchors);

            h.WriteInt32(ordered.Count);
            for (int i = 0; i < ordered.Count; i++)
            {
                var a = ordered[i];
                h.WriteInt32(a.AnchorId);
                h.WriteInt32(a.RoomId);
                h.WriteInt32((int)a.Slot);
                h.WriteVec3i(a.PositionMm);
            }
        }

        // ---- canonical comparators: the frozen hash order ----

        private static int CompareConnections(LayoutConnection a, LayoutConnection b)
        {
            int c = a.RoomAId.CompareTo(b.RoomAId);
            if (c != 0) return c;
            c = a.RoomBId.CompareTo(b.RoomBId);
            if (c != 0) return c;
            return ((int)a.DirectionFromA).CompareTo((int)b.DirectionFromA);
        }

        private static int CompareDoors(LayoutDoor a, LayoutDoor b)
        {
            int c = a.RoomAId.CompareTo(b.RoomAId);
            if (c != 0) return c;
            c = a.RoomBId.CompareTo(b.RoomBId);
            if (c != 0) return c;
            return ((int)a.SocketASlot).CompareTo((int)b.SocketASlot);
        }

        private static int CompareProps(LayoutProp a, LayoutProp b)
        {
            int c = a.RoomId.CompareTo(b.RoomId);
            if (c != 0) return c;
            c = ((int)a.Slot).CompareTo((int)b.Slot);
            if (c != 0) return c;
            c = string.CompareOrdinal(a.PropDefinitionId, b.PropDefinitionId);
            if (c != 0) return c;
            return a.PositionMm.CompareTo(b.PositionMm);
        }

        private static int CompareAnchors(LayoutAnchor a, LayoutAnchor b)
        {
            int c = a.RoomId.CompareTo(b.RoomId);
            if (c != 0) return c;
            c = ((int)a.Slot).CompareTo((int)b.Slot);
            if (c != 0) return c;
            return a.PositionMm.CompareTo(b.PositionMm);
        }
    }
}
