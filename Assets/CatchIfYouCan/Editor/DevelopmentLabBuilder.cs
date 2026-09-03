using System;
using System.IO;
using CatchIfYouCan.Core.SceneSetup;
using CatchIfYouCan.Development;
using CatchIfYouCan.Development.Labs;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CatchIfYouCan.EditorTools
{
    /// <summary>
    /// Creates the development lab scenes.
    ///
    /// <para>
    /// The scenes exist as assets but hold almost nothing: a bootstrapper and the lab's
    /// installer, which builds the room in code on entry. That is deliberate. A lab whose
    /// fixtures live in the scene file becomes a second source of truth for room geometry,
    /// conflicts on every merge, and drifts from the code that is supposed to be under
    /// test. A lab whose fixtures are built in code can be regenerated, reviewed as a diff,
    /// and never disagrees with itself.
    /// </para>
    ///
    /// <para>
    /// It is also why this is a tool rather than nine committed scene files: a scene is
    /// serialized Unity state, and writing that by hand outside the editor is how
    /// references get silently dropped.
    /// </para>
    /// </summary>
    public static class DevelopmentLabBuilder
    {
        [MenuItem("Catch If You Can/Development/Create Missing Lab Scenes", false, 100)]
        public static void CreateMissingLabScenes() => Create(overwriteExisting: false);

        [MenuItem("Catch If You Can/Development/Rebuild All Lab Scenes", false, 101)]
        public static void RebuildAllLabScenes()
        {
            bool ok = EditorUtility.DisplayDialog(
                "Rebuild All Lab Scenes",
                "This replaces every DEV_ lab scene with a freshly generated one.\n\n" +
                "Anything added to a lab scene by hand is lost. Fixtures built by the " +
                "installers are not affected - they are code.",
                "Rebuild", "Cancel");

            if (ok)
                Create(overwriteExisting: true);
        }

        private static void Create(bool overwriteExisting)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EnsureFolder(DevelopmentScenes.Folder);

            int created = 0, skipped = 0;
            string reopen = EditorSceneManager.GetActiveScene().path;

            foreach (var lab in DevelopmentScenes.All)
            {
                string path = DevelopmentScenes.PathOf(lab);

                if (File.Exists(path) && !overwriteExisting)
                {
                    skipped++;
                    continue;
                }

                BuildOne(lab, path);
                created++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!string.IsNullOrEmpty(reopen) && File.Exists(reopen))
                EditorSceneManager.OpenScene(reopen, OpenSceneMode.Single);

            string report = $"Created {created} lab scene(s), skipped {skipped} existing.\n\n" +
                            "These are NOT added to Build Settings, and the build tooling " +
                            "refuses to ship them.";
            Debug.Log("[CIYC] " + report);
            EditorUtility.DisplayDialog("Development Labs", report, "OK");
        }

        private static void BuildOne(DevelopmentLab lab, string path)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Built rather than copied from a template, so a lab is exactly the four things
            // it needs and nothing that happened to be in a template.
            var setup = new GameObject("SCENE_BOOTSTRAP");
            var installer = (DevelopmentLabInstaller)setup.AddComponent(InstallerTypeFor(lab));
            var bootstrapper = setup.AddComponent<SceneBootstrapper>();

            var installerField = typeof(SceneBootstrapper).GetField(
                "installer", System.Reflection.BindingFlags.NonPublic |
                             System.Reflection.BindingFlags.Instance);
            installerField?.SetValue(bootstrapper, installer);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path);
        }

        private static Type InstallerTypeFor(DevelopmentLab lab)
        {
            switch (lab)
            {
                case DevelopmentLab.Equipment: return typeof(EquipmentLabInstaller);
                case DevelopmentLab.Character: return typeof(CharacterLabInstaller);
                case DevelopmentLab.Interaction: return typeof(InteractionLabInstaller);
                case DevelopmentLab.Ghost: return typeof(GhostLabInstaller);
                case DevelopmentLab.Audio: return typeof(AudioLabInstaller);
                case DevelopmentLab.Lighting: return typeof(LightingLabInstaller);
                case DevelopmentLab.Environment: return typeof(EnvironmentLabInstaller);
                case DevelopmentLab.UIInput: return typeof(UIInputLabInstaller);
                case DevelopmentLab.Network: return typeof(NetworkLabInstaller);
                default: throw new ArgumentOutOfRangeException(nameof(lab), lab, null);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            var parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
