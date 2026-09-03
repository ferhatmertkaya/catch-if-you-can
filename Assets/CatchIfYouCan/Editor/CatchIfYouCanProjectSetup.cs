using System.Collections.Generic;
using System.IO;
using System.Text;
using CatchIfYouCan.Equipment;
using CatchIfYouCan.Ghost;
using CatchIfYouCan.Missions;
using UnityEditor;
using UnityEngine;

namespace CatchIfYouCan.EditorTools
{
    public static class CatchIfYouCanProjectSetup
    {
        private const string Root = "Assets/CatchIfYouCan";
        private const string ScriptableObjectsPath = Root + "/ScriptableObjects";
        private const string InputPath = Root + "/Input/CIYCInputActions.inputactions";

        private static readonly string[] RequiredLayers =
        {
            "Player", "Ghost", "Interactable", "Evidence", "Environment", "HideSpot", "Equipment", "PostProcessing"
        };

        private static readonly string[] RequiredTags =
        {
            "Interactable", "Ghost", "HideSpot", "Evidence", "Equipment", "Door", "Van", "Breaker", "Player"
        };

        private static readonly string[] RequiredFolders =
        {
            Root + "/Editor",
            Root + "/Scripts",
            Root + "/ScriptableObjects",
            Root + "/Prefabs",
            Root + "/Scenes",
            Root + "/Shaders",
            Root + "/Input",
            Root + "/Materials",
            "Builds/Android",
            "Builds/iOS"
        };

        // Derived, never retyped. This list used to be four hard-coded paths, and it is
        // assigned straight onto EditorBuildSettings.scenes below - so a scene added by
        // hand and forgotten here was silently deleted from the build the next time anyone
        // ran Setup Project.
        private static string[] RequiredScenes => Core.CiycScenes.ProductionPaths();

        [MenuItem("Catch If You Can/Setup Project")]
        public static void SetupProject()
        {
            string report = RunSetup();
            EditorUtility.DisplayDialog("Catch If You Can", report, "OK");
        }

        /// <summary>Batch/CI friendly setup without dialogs.</summary>
        public static void SetupProjectSilent()
        {
            RunSetup();
        }

        private static string RunSetup()
        {
            var report = new StringBuilder();
            report.AppendLine("=== CATCH IF YOU CAN — Project Setup ===");

            EnsureFolders(report);
            EnsureAudioSetup(report);
            EnsureLayers(report);
            EnsureTags(report);
            EnsureScriptableObjects(report);
            if (AssetDatabase.IsValidFolder("Assets/External/Kenney/FurnitureKit/Models"))
            {
                ExternalAssetDownloader.EnsureBundledAssetsPresent();
                try
                {
                    report.AppendLine(ExternalAssetIntegrator.RunIntegration());
                }
                catch (System.Exception ex)
                {
                    report.AppendLine($"External asset integration failed: {ex.Message}");
                }
            }
            else
            {
                report.AppendLine("External assets not found — skipped Integrate External Assets.");
            }

            EnsureBuildScenes(report);
            ValidateUrp(report);
            EnsureInputActions(report);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            report.AppendLine("Setup complete.");
            Debug.Log(report.ToString());
            return report.ToString();
        }

        private static void EnsureFolders(StringBuilder report)
        {
            int created = 0;
            for (int i = 0; i < RequiredFolders.Length; i++)
            {
                if (EnsureFolderPath(RequiredFolders[i]))
                    created++;
            }

            report.AppendLine($"Directories: {created} created/verified.");
        }

        private static void EnsureAudioSetup(StringBuilder report)
        {
            CatchIfYouCanAudioMixerBuilder.EnsureAudioFolders();
            var config = CatchIfYouCanAudioMixerBuilder.EnsureMixerConfig();
            CatchIfYouCanAudioMixerBuilder.WriteMixerReadme(config);
            var gen = CatchIfYouCanAudioMixerBuilder.GenerateDefaultAudioEventsInternal();
            report.AppendLine($"Audio: folders ensured; {gen.EventsCreated} events, {gen.ClipsCreated} procedural clips created.");
        }

        private static bool EnsureFolderPath(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return false;

            var parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }

            return true;
        }

        private static void EnsureLayers(StringBuilder report)
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            int added = 0;

            for (int i = 0; i < RequiredLayers.Length; i++)
            {
                if (LayerExists(layers, RequiredLayers[i]))
                    continue;

                for (int slot = 8; slot < layers.arraySize; slot++)
                {
                    var prop = layers.GetArrayElementAtIndex(slot);
                    if (!string.IsNullOrEmpty(prop.stringValue))
                        continue;

                    prop.stringValue = RequiredLayers[i];
                    added++;
                    break;
                }
            }

            tagManager.ApplyModifiedProperties();
            report.AppendLine($"Layers: {added} added.");
        }

        private static void EnsureTags(StringBuilder report)
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var tags = tagManager.FindProperty("tags");
            int added = 0;

            for (int i = 0; i < RequiredTags.Length; i++)
            {
                if (TagExists(tags, RequiredTags[i]))
                    continue;

                tags.InsertArrayElementAtIndex(tags.arraySize);
                tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = RequiredTags[i];
                added++;
            }

            tagManager.ApplyModifiedProperties();
            report.AppendLine($"Tags: {added} added.");
        }

        private static void EnsureScriptableObjects(StringBuilder report)
        {
            int ghosts = SaveDefinitions(GhostDefinitionFactory.CreateAllDefaultGhosts(), ScriptableObjectsPath);
            int equipment = SaveDefinitions(EquipmentDefinitionFactory.CreateAllDefaultDefinitions(), ScriptableObjectsPath);
            int missions = SaveMissionDefinitions(MissionDefinitionFactory.CreateAllDefaultMissions(), ScriptableObjectsPath);
            report.AppendLine($"ScriptableObjects: {ghosts} ghosts, {equipment} equipment, {missions} missions created.");
        }

        private static int SaveDefinitions<T>(T[] definitions, string folder) where T : ScriptableObject
        {
            if (definitions == null)
                return 0;

            int created = 0;
            for (int i = 0; i < definitions.Length; i++)
            {
                var def = definitions[i];
                if (def == null)
                    continue;

                string fileName = GetAssetFileName(def);
                string path = $"{folder}/{fileName}.asset";
                if (AssetDatabase.LoadAssetAtPath<T>(path) != null)
                    continue;

                AssetDatabase.CreateAsset(def, path);
                created++;
            }

            return created;
        }

        private static int SaveMissionDefinitions(MissionDefinition[] missions, string folder)
        {
            if (missions == null)
                return 0;

            int created = 0;
            for (int i = 0; i < missions.Length; i++)
            {
                var mission = missions[i];
                if (mission == null)
                    continue;

                string path = $"{folder}/mission_{mission.Theme.ToString().ToLowerInvariant()}.asset";
                if (AssetDatabase.LoadAssetAtPath<MissionDefinition>(path) != null)
                    continue;

                if (mission.Difficulty != null)
                {
                    string diffPath = $"{folder}/difficulty_{mission.Difficulty.Tier.ToString().ToLowerInvariant()}.asset";
                    if (AssetDatabase.LoadAssetAtPath<DifficultyDefinition>(diffPath) == null)
                        AssetDatabase.CreateAsset(mission.Difficulty, diffPath);
                    else
                        mission.Difficulty = AssetDatabase.LoadAssetAtPath<DifficultyDefinition>(diffPath);
                }

                AssetDatabase.CreateAsset(mission, path);
                created++;
            }

            return created;
        }

        private static string GetAssetFileName(ScriptableObject def)
        {
            switch (def)
            {
                case GhostDefinition ghost when !string.IsNullOrEmpty(ghost.Id):
                    return ghost.Id;
                case EquipmentDefinition equipment when !string.IsNullOrEmpty(equipment.Id):
                    return equipment.Id;
                default:
                    return def.name;
            }
        }

        private static void EnsureBuildScenes(StringBuilder report)
        {
            var scenes = new List<EditorBuildSettingsScene>();
            var required = RequiredScenes;
            int missing = 0;

            // Production scenes first and in order, because index 0 is the scene the player
            // starts in.
            for (int i = 0; i < required.Length; i++)
            {
                bool exists = File.Exists(required[i]);
                if (!exists)
                    missing++;

                scenes.Add(new EditorBuildSettingsScene(required[i], exists));
            }

            // Anything else already registered is kept, disabled or not. This used to be a
            // wholesale replacement, which quietly removed every scene the production list
            // did not know about - including, after the lobby split, the lobby itself if
            // this file had not been updated in the same change.
            int kept = 0;
            var existing = EditorBuildSettings.scenes;
            if (existing != null)
            {
                for (int i = 0; i < existing.Length; i++)
                {
                    if (existing[i] == null || string.IsNullOrEmpty(existing[i].path))
                        continue;
                    if (System.Array.IndexOf(required, existing[i].path) >= 0)
                        continue;

                    scenes.Add(existing[i]);
                    kept++;
                }
            }

            EditorBuildSettings.scenes = scenes.ToArray();

            report.AppendLine(missing == 0
                ? $"Build Settings: all {required.Length} production scenes registered."
                : $"Build Settings: {missing} of {required.Length} production scene(s) missing on disk.");
            if (kept > 0)
                report.AppendLine($"Build Settings: kept {kept} additional non-production scene(s).");
        }

        private static void ValidateUrp(StringBuilder report)
        {
            bool urpPresent = false;
            string[] guids = AssetDatabase.FindAssets("t:Script UniversalRenderPipelineAsset");
            if (guids.Length > 0)
                urpPresent = true;

            if (!urpPresent)
            {
                var manifest = File.ReadAllText("Packages/manifest.json");
                urpPresent = manifest.Contains("com.unity.render-pipelines.universal");
            }

            report.AppendLine(urpPresent
                ? "URP: package present."
                : "URP: WARNING — com.unity.render-pipelines.universal not detected.");
        }

        private static void EnsureInputActions(StringBuilder report)
        {
            if (File.Exists(InputPath))
            {
                report.AppendLine("Input: CIYCInputActions.inputactions exists.");
                return;
            }

            EnsureFolderPath(Root + "/Input");
            File.WriteAllText(InputPath, GetInputActionsStub());
            AssetDatabase.ImportAsset(InputPath);
            report.AppendLine("Input: created CIYCInputActions.inputactions stub.");
            report.AppendLine("Input System: assign asset under Project Settings > Player > Active Input Handling.");
        }

        private static bool LayerExists(SerializedProperty layers, string name)
        {
            for (int i = 0; i < layers.arraySize; i++)
            {
                if (layers.GetArrayElementAtIndex(i).stringValue == name)
                    return true;
            }

            return false;
        }

        private static bool TagExists(SerializedProperty tags, string name)
        {
            for (int i = 0; i < tags.arraySize; i++)
            {
                if (tags.GetArrayElementAtIndex(i).stringValue == name)
                    return true;
            }

            return false;
        }

        private static string GetInputActionsStub()
        {
            return @"{
  ""name"": ""CIYCInputActions"",
  ""maps"": [
    {
      ""name"": ""Player"",
      ""id"": ""a1b2c3d4-e5f6-7890-abcd-ef1234567890"",
      ""actions"": [
        { ""name"": ""Move"", ""type"": ""Value"", ""id"": ""11111111-1111-1111-1111-111111111111"" },
        { ""name"": ""Look"", ""type"": ""Value"", ""id"": ""22222222-2222-2222-2222-222222222222"" },
        { ""name"": ""Interact"", ""type"": ""Button"", ""id"": ""33333333-3333-3333-3333-333333333333"" },
        { ""name"": ""Sprint"", ""type"": ""Button"", ""id"": ""44444444-4444-4444-4444-444444444444"" }
      ],
      ""bindings"": []
    }
  ],
  ""controlSchemes"": []
}";
        }
    }
}
