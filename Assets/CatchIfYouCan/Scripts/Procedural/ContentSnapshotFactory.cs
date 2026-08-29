using System;
using System.Collections.Generic;
using CatchIfYouCan.Procedural.Deterministic;
using UnityEngine;

namespace CatchIfYouCan.Procedural
{
    /// <summary>
    /// Converts authored Unity assets into the engine-free <see cref="ContentSnapshot"/>
    /// that Stage A reads.
    ///
    /// This is the ONLY boundary where authored floats become generation input, and it is
    /// where they get quantized. Everything downstream is integer millimetres.
    ///
    /// The snapshot sorts by stable id, so the inspector order of the generator's
    /// definition arrays cannot influence generation - reordering them in the inspector
    /// used to change which prop a seed produced.
    /// </summary>
    public static class ContentSnapshotFactory
    {
        public static ContentSnapshot Create(RoomDefinition[] roomDefinitions, PropDefinition[] propDefinitions)
        {
            var rooms = new List<RoomArchetype>();
            var props = new List<PropArchetype>();

            if (roomDefinitions != null)
            {
                for (int i = 0; i < roomDefinitions.Length; i++)
                {
                    var def = roomDefinitions[i];
                    if (def == null)
                        continue;

                    rooms.Add(new RoomArchetype(
                        def.ResolveStableId(),
                        def.Category,
                        Vec3i.FromMetres(def.Size.x, def.Size.y, def.Size.z),
                        def.VariantCount,
                        Quantize.Weight(def.Weight)));
                }
            }

            if (propDefinitions != null)
            {
                for (int i = 0; i < propDefinitions.Length; i++)
                {
                    var def = propDefinitions[i];
                    if (def == null)
                        continue;

                    props.Add(new PropArchetype(
                        def.ResolveStableId(),
                        def.Kind,
                        Vec3i.FromMetres(def.BoundsSize.x, def.BoundsSize.y, def.BoundsSize.z),
                        Quantize.Weight(def.Weight),
                        ResolveCategories(def)));
                }
            }

            if (rooms.Count == 0 && props.Count == 0)
                return ContentSnapshot.CreateFallback();

            return new ContentSnapshot(rooms, props);
        }

        /// <summary>
        /// Resolves authored string tags to enum values once, here, so Stage A never does
        /// string comparison during generation. An unparseable tag is dropped with a warning
        /// rather than silently matching everything - a typo that widened a prop to every
        /// room would be invisible otherwise.
        /// </summary>
        private static RoomCategory[] ResolveCategories(PropDefinition def)
        {
            if (def.CategoryTags == null || def.CategoryTags.Length == 0)
                return Array.Empty<RoomCategory>();

            var result = new List<RoomCategory>(def.CategoryTags.Length);
            for (int i = 0; i < def.CategoryTags.Length; i++)
            {
                string tag = def.CategoryTags[i];
                if (string.IsNullOrEmpty(tag))
                    continue;

                if (string.Equals(tag, "Any", StringComparison.OrdinalIgnoreCase))
                    return Array.Empty<RoomCategory>();

                if (Enum.TryParse(tag, true, out RoomCategory category))
                {
                    if (!result.Contains(category))
                        result.Add(category);
                }
                else
                {
                    Debug.LogWarning($"[Determinism] PropDefinition '{def.ResolveStableId()}' has unknown " +
                                     $"room tag '{tag}'. It will not match any room.");
                }
            }

            result.Sort((a, b) => ((int)a).CompareTo((int)b));
            return result.ToArray();
        }

        /// <summary>Looks a definition up by the stable id the layout recorded.</summary>
        public static PropDefinition FindProp(PropDefinition[] definitions, string stableId)
        {
            if (definitions == null || string.IsNullOrEmpty(stableId))
                return null;

            for (int i = 0; i < definitions.Length; i++)
            {
                if (definitions[i] != null &&
                    string.Equals(definitions[i].ResolveStableId(), stableId, StringComparison.Ordinal))
                    return definitions[i];
            }

            return null;
        }

        public static RoomDefinition FindRoom(RoomDefinition[] definitions, string stableId)
        {
            if (definitions == null || string.IsNullOrEmpty(stableId))
                return null;

            for (int i = 0; i < definitions.Length; i++)
            {
                if (definitions[i] != null &&
                    string.Equals(definitions[i].ResolveStableId(), stableId, StringComparison.Ordinal))
                    return definitions[i];
            }

            return null;
        }
    }
}
