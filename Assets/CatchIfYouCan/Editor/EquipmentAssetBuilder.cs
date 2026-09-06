using System.Collections.Generic;
using CatchIfYouCan.Content;
using CatchIfYouCan.Equipment;
using CatchIfYouCan.Interaction;
using UnityEditor;
using UnityEngine;

namespace CatchIfYouCan.EditorTools
{
    /// <summary>
    /// Builds the equipment prefabs and points the definition assets and the content registry
    /// at them.
    ///
    /// <para>
    /// The eleven <see cref="EquipmentDefinition"/> assets and the
    /// <see cref="EquipmentCatalog"/> are checked in; this is what fills in the half of them
    /// that has to be a prefab. There is one base prefab holding everything an item needs to
    /// be picked up off the floor, and one variant per item on top of it - so a change to how
    /// pickup works is one edit rather than eleven.
    /// </para>
    ///
    /// <para>
    /// Only the flashlight is a real item. The other ten are variants carrying
    /// <see cref="DevPlaceholderEquipment"/> and named DEV_PLACEHOLDER, which is the point:
    /// the runtime factory used to hand every unimplemented id a working torch, and an
    /// unimplemented item that quietly works is one nobody ever finishes.
    /// </para>
    ///
    /// <para>
    /// This is an editor tool rather than checked-in prefab YAML because a prefab is a graph
    /// of cross-referencing documents and hand-writing one is how references get silently
    /// broken. Run it once after opening the project.
    /// </para>
    /// </summary>
    public static class EquipmentAssetBuilder
    {
        private const string PrefabFolder = "Assets/CatchIfYouCan/Prefabs/Equipment";
        private const string DefinitionFolder = "Assets/CatchIfYouCan/Definitions/Equipment";
        private const string CatalogPath = DefinitionFolder + "/EquipmentCatalog.asset";
        private const string BasePath = PrefabFolder + "/PF_Equipment_Base.prefab";
        private const string RegistryFolder = "Assets/CatchIfYouCan/Resources";
        private const string RegistryPath = RegistryFolder + "/CIYC_ContentRegistry.asset";

        /// <summary>Radius of the trigger the interaction ray hits, in metres.</summary>
        private const float PickupRadius = 0.14f;

        [MenuItem("Catch If You Can/4. SPIELINHALT/Equipment/Equipment-Prefabs [SCHREIBT ASSET]", false, 410)]
        public static void BuildEquipmentPrefabs()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<EquipmentCatalog>(CatalogPath);
            if (catalog == null || catalog.Count == 0)
            {
                Debug.LogError("[CIYC] No EquipmentCatalog at " + CatalogPath +
                               ", so there is nothing to build prefabs for.");
                return;
            }

            EnsureFolder(PrefabFolder);

            var basePrefab = BuildBasePrefab();
            if (basePrefab == null)
                return;

            var built = new List<string>();
            var placeholders = new List<string>();

            foreach (var definition in catalog.Equipment)
            {
                if (definition == null || string.IsNullOrEmpty(definition.Id))
                    continue;

                bool real = definition.Id == "flashlight";
                string path = PrefabFolder + "/PF_Equipment_" +
                              (real ? PascalCase(definition.Id)
                                    : "DEV_PLACEHOLDER_" + PascalCase(definition.Id)) + ".prefab";

                var prefab = real
                    ? BuildFlashlightVariant(basePrefab, definition, path)
                    : BuildPlaceholderVariant(basePrefab, definition, path);

                if (prefab == null)
                    continue;

                definition.Prefab = prefab;
                EditorUtility.SetDirty(definition);
                (real ? built : placeholders).Add(definition.Id);
            }

            PointRegistryAtCatalog(catalog);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string report =
                "Equipment prefabs written to\n" + PrefabFolder + "\n\n" +
                "Implemented: " + string.Join(", ", built) + "\n\n" +
                "DEV_PLACEHOLDER (inert, refuse to be used): " +
                string.Join(", ", placeholders) + "\n\n" +
                "Every definition asset now points at its prefab, and the content registry " +
                "points at the catalog.";
            Debug.Log("[CIYC] " + report);
            EditorUtility.DisplayDialog("Equipment Prefabs", report, "OK");
        }

        /// <summary>
        /// Everything an item needs to be found on the floor and picked up, and nothing about
        /// what it does. The variants add that.
        /// </summary>
        private static GameObject BuildBasePrefab()
        {
            var root = new GameObject("Equipment");
            root.tag = "Equipment";

            // A trigger, not a solid: the interaction ray is cast with
            // QueryTriggerInteraction.Collide, so this is enough to be picked up, and a solid
            // collider on something held inside the player's own capsule is nothing but a
            // source of contacts to resolve. The capsule it lands on is a separate collider
            // that HeldEquipmentBase builds and keeps switched off while carried.
            var trigger = root.AddComponent<SphereCollider>();
            trigger.radius = PickupRadius;
            trigger.isTrigger = true;

            root.AddComponent<InteractivePickup>();

            var saved = PrefabUtility.SaveAsPrefabAsset(root, BasePath, out bool ok);
            Object.DestroyImmediate(root);

            if (!ok || saved == null)
            {
                Debug.LogError("[CIYC] Failed to write " + BasePath + ".");
                return null;
            }

            return saved;
        }

        private static GameObject BuildFlashlightVariant(
            GameObject basePrefab, EquipmentDefinition definition, string path)
        {
            var instance = PrefabUtility.InstantiatePrefab(basePrefab) as GameObject;
            if (instance == null)
                return null;

            instance.name = definition.DisplayName;

            var torch = instance.AddComponent<HeldFlashlight>();
            torch.BindDefinition(definition);
            instance.GetComponent<InteractivePickup>()
                ?.Configure(torch, "Pick Up " + definition.DisplayName, destroyWhenTaken: false);

            return SaveVariant(instance, path);
        }

        private static GameObject BuildPlaceholderVariant(
            GameObject basePrefab, EquipmentDefinition definition, string path)
        {
            var instance = PrefabUtility.InstantiatePrefab(basePrefab) as GameObject;
            if (instance == null)
                return null;

            instance.name = "DEV_PLACEHOLDER_" + definition.Id;

            var placeholder = instance.AddComponent<DevPlaceholderEquipment>();
            placeholder.BindDefinition(definition);
            instance.GetComponent<InteractivePickup>()
                ?.Configure(placeholder, "Pick Up " + definition.DisplayName + " (placeholder)",
                            destroyWhenTaken: false);

            // A box, so that what is in the hand is visibly a box. Anything prettier would be
            // mistaken for the finished item, which is exactly the failure this replaces.
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "DEV_PLACEHOLDER_Body";
            body.transform.SetParent(instance.transform, false);
            body.transform.localScale = new Vector3(0.12f, 0.12f, 0.12f);
            var solid = body.GetComponent<Collider>();
            if (solid != null)
                Object.DestroyImmediate(solid);

            return SaveVariant(instance, path);
        }

        private static GameObject SaveVariant(GameObject instance, string path)
        {
            var saved = PrefabUtility.SaveAsPrefabAsset(instance, path, out bool ok);
            Object.DestroyImmediate(instance);

            if (!ok || saved == null)
            {
                Debug.LogError("[CIYC] Failed to write " + path + ".");
                return null;
            }

            return saved;
        }

        private static void PointRegistryAtCatalog(EquipmentCatalog catalog)
        {
            var registry = AssetDatabase.LoadAssetAtPath<CiycContentRegistry>(RegistryPath);
            if (registry == null)
            {
                Debug.LogWarning("[CIYC] No content registry at " + RegistryPath +
                                 ". Create it with Catch If You Can > Content > Create Content " +
                                 "Registry, then run this again so the catalog is reachable at " +
                                 "runtime.");
                return;
            }

            var field = typeof(CiycContentRegistry).GetField(
                "equipmentCatalog", System.Reflection.BindingFlags.NonPublic |
                                    System.Reflection.BindingFlags.Instance);
            field?.SetValue(registry, catalog);
            EditorUtility.SetDirty(registry);
        }

        private static string PascalCase(string id)
        {
            var parts = id.Split('_');
            var builder = new System.Text.StringBuilder();
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length == 0)
                    continue;
                builder.Append(char.ToUpperInvariant(parts[i][0]));
                builder.Append(parts[i].Substring(1));
            }

            return builder.ToString();
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
