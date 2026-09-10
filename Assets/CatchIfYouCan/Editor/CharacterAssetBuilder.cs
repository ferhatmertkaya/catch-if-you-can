using System.IO;
using CatchIfYouCan.Character;
using CatchIfYouCan.Content;
using UnityEditor;
using UnityEngine;

namespace CatchIfYouCan.EditorTools
{
    /// <summary>
    /// Authors Nathan's character assets: the rig profile, the definition and the catalog,
    /// and points the content registry at the catalog.
    ///
    /// <para>
    /// Nathan's numbers are copied from the constants the factory used, not re-measured.
    /// The point of this phase is to move where they live, and a foundation that changes
    /// the values while it moves them is a foundation nobody can trust the first time the
    /// pose looks slightly wrong.
    /// </para>
    /// </summary>
    public static class CharacterAssetBuilder
    {
        private const string Folder = "Assets/CatchIfYouCan/Definitions/Characters";
        private const string RigProfilePath = Folder + "/RigProfile_Nathan.asset";
        private const string DefinitionPath = Folder + "/Character_Nathan.asset";
        private const string CatalogPath = Folder + "/CharacterCatalog.asset";
        private const string RegistryPath = "Assets/CatchIfYouCan/Resources/CIYC_ContentRegistry.asset";

        private const string NathanVisualPath =
            "Assets/CatchIfYouCan/Resources/Characters/Player_CharacterVisual.prefab";
        private const string NathanMaterialPath =
            "Assets/CatchIfYouCan/Art/Characters/Nathan/Materials/Nathan_Body.mat";

        [MenuItem("Catch If You Can/4. SPIELINHALT/Characters/Charakter-Assets [SCHREIBT ASSET]", false, 400)]
        public static void BuildCharacterAssets()
        {
            EnsureFolder(Folder);

            var rig = LoadOrCreate<CharacterRigProfile>(RigProfilePath);
            // Every field on the profile already defaults to the literal the motion code
            // used, so Nathan's asset is deliberately left at its defaults.

            var visual = AssetDatabase.LoadAssetAtPath<GameObject>(NathanVisualPath);
            if (visual == null)
            {
                Debug.LogWarning("[CIYC] " + NathanVisualPath + " not found. The definition is " +
                                 "written without a visual; run Characters > Build Nathan " +
                                 "Player Visual first, then this again.");
            }

            var definition = LoadOrCreate<CharacterDefinition>(DefinitionPath);
            Set(definition, "id", "nathan");
            Set(definition, "displayName", "Nathan");
            Set(definition, "visualPrefab", visual);
            Set(definition, "rigProfile", rig);
            Set(definition, "bodyMaterial", AssetDatabase.LoadAssetAtPath<Material>(NathanMaterialPath));
            // The four numbers PlayerFactory held as constants, copied rather than re-derived.
            Set(definition, "eyeHeight", 1.68f);
            Set(definition, "eyeForward", 0.21f);
            Set(definition, "capsuleHeight", 1.86f);
            Set(definition, "visualScale", 1.04f);
            Set(definition, "unlockedByDefault", true);
            EditorUtility.SetDirty(definition);

            var catalog = LoadOrCreate<CharacterCatalog>(CatalogPath);
            Set(catalog, "characters", new[] { definition });
            Set(catalog, "defaultCharacterId", "nathan");
            EditorUtility.SetDirty(catalog);

            var registry = AssetDatabase.LoadAssetAtPath<CiycContentRegistry>(RegistryPath);
            if (registry != null)
            {
                Set(registry, "characterCatalog", catalog);
                EditorUtility.SetDirty(registry);
            }
            else
            {
                Debug.LogWarning("[CIYC] No content registry at " + RegistryPath +
                                 "; the catalog was written but nothing points at it yet. " +
                                 "Run Content > Create Content Registry, then this again.");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string report = "Character assets written under\n" + Folder +
                            "\n\nNathan keeps eye height 1.68, eye forward 0.21, capsule " +
                            "1.86 and visual scale 1.04, and a rig profile left at the " +
                            "defaults - which are the exact bone suffixes the motion code " +
                            "used to have written into it.";
            Debug.Log("[CIYC] " + report);
            EditorUtility.DisplayDialog("Character Assets", report, "OK");
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
                return existing;

            var created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        /// <summary>
        /// Writes a serialized private field. The assets are authored data, so the fields
        /// stay private with public accessors; this is the only writer.
        /// </summary>
        private static void Set(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (field == null)
            {
                Debug.LogError("[CIYC] " + target.GetType().Name + " has no field '" + fieldName +
                               "'. The asset builder and the class have diverged.");
                return;
            }

            field.SetValue(target, value);
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
