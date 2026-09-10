using System.IO;
using CatchIfYouCan.Content;
using CatchIfYouCan.Player;
using UnityEditor;
using UnityEngine;

namespace CatchIfYouCan.EditorTools
{
    /// <summary>
    /// Bakes PF_Player from the same code that builds the player at runtime, and points the
    /// content registry at it.
    ///
    /// <para>
    /// Generated rather than authored by hand on purpose. A player prefab drawn in the
    /// inspector and a factory that builds one in code are two descriptions of the same
    /// hierarchy, and two descriptions drift: the eye height moves in one and not the other,
    /// and the bug shows up as "the camera is in the neck again" months later. Baking the
    /// prefab from <see cref="PlayerRigBuilder"/> makes the code the single description and
    /// the prefab a build product of it.
    /// </para>
    ///
    /// <para>
    /// What is baked is only the character-independent half. VisualRoot stays empty, because
    /// which character hangs there is chosen at runtime, and a Nathan baked into the prefab
    /// would make it a Nathan prefab.
    /// </para>
    /// </summary>
    public static class PlayerPrefabBuilder
    {
        private const string PrefabFolder = "Assets/CatchIfYouCan/Prefabs/Player";
        private const string PrefabPath = PrefabFolder + "/PF_Player.prefab";
        private const string RegistryFolder = "Assets/CatchIfYouCan/Resources";
        private const string RegistryPath = RegistryFolder + "/CIYC_ContentRegistry.asset";

        [MenuItem("Catch If You Can/4. SPIELINHALT/Content/Player-Prefab [SCHREIBT ASSET]", false, 431)]
        public static void BuildPlayerPrefab()
        {
            var prefab = Build();
            if (prefab == null)
                return;

            var registry = EnsureRegistry();
            if (registry != null)
            {
                var field = typeof(CiycContentRegistry).GetField(
                    "playerPrefab", System.Reflection.BindingFlags.NonPublic |
                                    System.Reflection.BindingFlags.Instance);
                field?.SetValue(registry, prefab);
                EditorUtility.SetDirty(registry);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string report =
                "PF_Player rebuilt at\n" + PrefabPath + "\n\n" +
                "VisualRoot is empty by design - the character is chosen at runtime.\n\n" +
                "The content registry now points at it, so PlayerFactory will instantiate " +
                "the prefab instead of building the player in code.";
            Debug.Log("[CIYC] " + report);
            EditorUtility.DisplayDialog("Player Prefab", report, "OK");
        }

        [MenuItem("Catch If You Can/4. SPIELINHALT/Content/Content-Registry [SCHREIBT ASSET]", false, 430)]
        public static void CreateContentRegistry()
        {
            var registry = EnsureRegistry();
            if (registry == null)
                return;

            AssetDatabase.SaveAssets();
            Selection.activeObject = registry;
            EditorGUIUtility.PingObject(registry);
        }

        private static GameObject Build()
        {
            EnsureFolder(PrefabFolder);

            // Built in the open scene and destroyed immediately after saving. The rig has no
            // Awake side effects worth avoiding, and building it for real is the only way to
            // guarantee the prefab matches what the factory would have produced.
            var rig = PlayerRigBuilder.Build();
            if (rig == null)
            {
                Debug.LogError("[CIYC] PlayerRigBuilder produced nothing; prefab not written.");
                return null;
            }

            if (!rig.IsComplete)
            {
                Debug.LogError("[CIYC] The freshly built rig is missing " + rig.DescribeMissing() +
                               ", so the prefab would be broken. Nothing was written.");
                Object.DestroyImmediate(rig.gameObject);
                return null;
            }

            var saved = PrefabUtility.SaveAsPrefabAsset(rig.gameObject, PrefabPath, out bool ok);
            Object.DestroyImmediate(rig.gameObject);

            if (!ok || saved == null)
            {
                Debug.LogError("[CIYC] Failed to write " + PrefabPath + ".");
                return null;
            }

            return saved;
        }

        private static CiycContentRegistry EnsureRegistry()
        {
            var existing = AssetDatabase.LoadAssetAtPath<CiycContentRegistry>(RegistryPath);
            if (existing != null)
                return existing;

            EnsureFolder(RegistryFolder);

            var registry = ScriptableObject.CreateInstance<CiycContentRegistry>();
            AssetDatabase.CreateAsset(registry, RegistryPath);
            Debug.Log("[CIYC] Created the content registry at " + RegistryPath + ".");
            return registry;
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
