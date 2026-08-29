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

            // Detect duplicates HERE as well as in ContentSnapshot, because only this side
            // knows the asset names. "PropDefinition 'Chair (1)' and 'Chair (2)' both resolve
            // to 'Chair'" is actionable; the id alone is not. ContentSnapshot keeps its own
            // check as the backstop for callers that bypass this factory.
            ReportDuplicateAssets(roomDefinitions, propDefinitions);

            return new ContentSnapshot(rooms, props);
        }

        /// <summary>
        /// Logs which ASSETS collide before letting the snapshot reject them, so the fix is
        /// obvious from the Console without hunting for the offending definition.
        /// </summary>
        private static void ReportDuplicateAssets(RoomDefinition[] roomDefinitions, PropDefinition[] propDefinitions)
        {
            LogCollisions("RoomDefinition", CollectIds(roomDefinitions,
                d => d == null ? null : d.ResolveStableId(),
                d => d == null ? "<null>" : d.name));

            LogCollisions("PropDefinition", CollectIds(propDefinitions,
                d => d == null ? null : d.ResolveStableId(),
                d => d == null ? "<null>" : d.name));
        }

        private static List<KeyValuePair<string, string>> CollectIds<T>(
            T[] definitions, Func<T, string> idOf, Func<T, string> nameOf)
        {
            var pairs = new List<KeyValuePair<string, string>>();
            if (definitions == null)
                return pairs;

            for (int i = 0; i < definitions.Length; i++)
            {
                string id = idOf(definitions[i]);
                if (!string.IsNullOrEmpty(id))
                    pairs.Add(new KeyValuePair<string, string>(id, nameOf(definitions[i])));
            }

            return pairs;
        }

        private static void LogCollisions(string kind, List<KeyValuePair<string, string>> pairs)
        {
            var ids = new List<string>(pairs.Count);
            for (int i = 0; i < pairs.Count; i++)
                ids.Add(pairs[i].Key);

            var duplicates = ContentSnapshot.FindDuplicateIds(ids);
            for (int d = 0; d < duplicates.Count; d++)
            {
                var owners = new List<string>();
                for (int i = 0; i < pairs.Count; i++)
                {
                    if (string.Equals(pairs[i].Key, duplicates[d], StringComparison.Ordinal))
                        owners.Add("'" + pairs[i].Value + "'");
                }

                Debug.LogError(
                    $"[Determinism] Duplicate {kind} stable id '{duplicates[d]}' shared by " +
                    $"{string.Join(", ", owners)}. A stable id is content identity - it selects " +
                    "which asset a seed produces and it feeds the content hash - so two assets " +
                    "cannot share one. Set a unique StableId on each. Generation is refused " +
                    "until they are distinct.");
            }
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
