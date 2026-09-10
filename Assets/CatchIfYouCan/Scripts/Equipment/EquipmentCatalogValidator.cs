using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace CatchIfYouCan.Equipment
{
    /// <summary>One thing that is wrong with the equipment content, and how badly.</summary>
    public readonly struct EquipmentValidationIssue
    {
        public readonly bool IsError;
        public readonly string EquipmentId;
        public readonly string Message;

        public EquipmentValidationIssue(bool isError, string equipmentId, string message)
        {
            IsError = isError;
            EquipmentId = equipmentId;
            Message = message;
        }

        public override string ToString() =>
            (IsError ? "ERROR " : "warn  ") +
            (string.IsNullOrEmpty(EquipmentId) ? "" : "[" + EquipmentId + "] ") + Message;
    }

    /// <summary>
    /// Checks the equipment content against the eleven canonical ids, and says what is broken.
    ///
    /// <para>
    /// This exists because every content failure this project has had was silent. A definition
    /// with a duplicated id, a catalogue missing an entry, an id with no runtime implementation
    /// - each of them produced a game that started, ran, and quietly did the wrong thing. The
    /// runtime factory's unknown-id branch handing back a working flashlight was the extreme
    /// case: eleven items, one of which was implemented, and no error anywhere.
    /// </para>
    ///
    /// <para>
    /// It runs from the editor validator and from the equipment lab, and its findings are what
    /// <c>Scripts/check_equipment_catalog.sh</c> enforces in CI from the text side.
    /// </para>
    /// </summary>
    public static class EquipmentCatalogValidator
    {
        /// <summary>
        /// Validates whatever the game would actually use: the authored catalog if there is
        /// one, the code-built definitions if there is not.
        /// </summary>
        public static List<EquipmentValidationIssue> Validate()
        {
            var issues = new List<EquipmentValidationIssue>();
            var definitions = EquipmentDefinitionFactory.All();

            if (definitions == null || definitions.Length == 0)
            {
                issues.Add(new EquipmentValidationIssue(
                    true, null, "No equipment definitions at all: neither an authored catalog " +
                                "nor the code fallback produced anything."));
                return issues;
            }

            ValidateIds(definitions, issues);
            ValidateContent(definitions, issues);
            ValidateRuntimePaths(definitions, issues);
            return issues;
        }

        /// <summary>Every canonical id present exactly once, and nothing extra.</summary>
        private static void ValidateIds(EquipmentDefinition[] definitions,
                                        List<EquipmentValidationIssue> issues)
        {
            var seen = new Dictionary<string, int>();

            for (int i = 0; i < definitions.Length; i++)
            {
                var definition = definitions[i];
                if (definition == null)
                {
                    issues.Add(new EquipmentValidationIssue(
                        true, null, "Definition slot " + i + " is null."));
                    continue;
                }

                if (string.IsNullOrEmpty(definition.Id))
                {
                    issues.Add(new EquipmentValidationIssue(
                        true, definition.name, "Definition has no Id."));
                    continue;
                }

                seen[definition.Id] = seen.TryGetValue(definition.Id, out int count) ? count + 1 : 1;

                if (!EquipmentIds.IsCanonical(definition.Id))
                {
                    issues.Add(new EquipmentValidationIssue(
                        true, definition.Id,
                        "Not one of the eleven canonical ids. Add it to EquipmentIds, or fix " +
                        "the typo - an id nothing recognises resolves to nothing."));
                }
            }

            foreach (var pair in seen)
            {
                if (pair.Value > 1)
                {
                    issues.Add(new EquipmentValidationIssue(
                        true, pair.Key, "Appears " + pair.Value + " times. Ids are the data " +
                                        "identity; two definitions answering to one id means " +
                                        "whichever is found first wins."));
                }
            }

            for (int i = 0; i < EquipmentIds.All.Length; i++)
            {
                if (!seen.ContainsKey(EquipmentIds.All[i]))
                {
                    issues.Add(new EquipmentValidationIssue(
                        true, EquipmentIds.All[i], "Canonical id has no definition."));
                }
            }
        }

        /// <summary>The references an item needs before it can be more than a name.</summary>
        private static void ValidateContent(EquipmentDefinition[] definitions,
                                            List<EquipmentValidationIssue> issues)
        {
            for (int i = 0; i < definitions.Length; i++)
            {
                var definition = definitions[i];
                if (definition == null || string.IsNullOrEmpty(definition.Id))
                    continue;

                if (string.IsNullOrEmpty(definition.DisplayName))
                {
                    issues.Add(new EquipmentValidationIssue(
                        false, definition.Id, "No DisplayName; the HUD and shop will show an id."));
                }

                if (definition.Icon == null)
                {
                    issues.Add(new EquipmentValidationIssue(
                        false, definition.Id, "No Icon; its inventory slot will be blank."));
                }

                if (definition.MaxBattery <= 0f && definition.BatteryUsagePerSecond > 0f)
                {
                    issues.Add(new EquipmentValidationIssue(
                        true, definition.Id,
                        "Drains " + definition.BatteryUsagePerSecond + "/s from a battery of " +
                        "zero, so it is flat the moment it is switched on."));
                }

                if (!definition.CanUse && !definition.CanPlace)
                {
                    issues.Add(new EquipmentValidationIssue(
                        false, definition.Id, "Can neither be used nor placed; it is decoration."));
                }
            }
        }

        /// <summary>
        /// Whether the id can actually become an object. This is the check that would have
        /// caught four items silently becoming DEV placeholders.
        /// </summary>
        private static void ValidateRuntimePaths(EquipmentDefinition[] definitions,
                                                 List<EquipmentValidationIssue> issues)
        {
            for (int i = 0; i < definitions.Length; i++)
            {
                var definition = definitions[i];
                if (definition == null || string.IsNullOrEmpty(definition.Id))
                    continue;

                if (definition.Prefab != null)
                    continue;

                if (!EquipmentRuntimeFactory.HasRuntimePath(definition.Id))
                {
                    issues.Add(new EquipmentValidationIssue(
                        true, definition.Id,
                        "No authored prefab and no runtime path, so it can only ever be a " +
                        "DEV_PLACEHOLDER. Give it a prefab on the definition or a case in " +
                        "EquipmentRuntimeFactory."));
                }
            }
        }

        /// <summary>Runs the check and writes it to the console. Returns true when clean.</summary>
        public static bool ValidateAndLog()
        {
            var issues = Validate();
            int errors = 0;

            var report = new StringBuilder("[CIYC] Equipment catalog validation\n");
            for (int i = 0; i < issues.Count; i++)
            {
                report.Append("  ").Append(issues[i]).Append('\n');
                if (issues[i].IsError)
                    errors++;
            }

            if (issues.Count == 0)
                report.Append("  all clear: ").Append(EquipmentIds.All.Length).Append(" ids, no issues.");

            if (errors > 0)
                Debug.LogError(report.ToString());
            else if (issues.Count > 0)
                Debug.LogWarning(report.ToString());
            else
                Debug.Log(report.ToString());

            return errors == 0;
        }
    }
}
