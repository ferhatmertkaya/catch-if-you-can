using System.Collections.Generic;
using System.Text;
using CatchIfYouCan.Content;
using CatchIfYouCan.Procedural;
using CatchIfYouCan.Procedural.Deterministic;
using UnityEditor;
using UnityEngine;

namespace CatchIfYouCan.EditorTools
{
    /// <summary>
    /// Builds ONE 6 x 3 x 6 room from the modular catalog, and reports what it actually made.
    ///
    /// <para>
    /// One room, on purpose. Processing the whole pack is what made the machine unusable, and
    /// converting the whole house before a single room has been looked at is how a mistake gets
    /// made forty times. This scans nothing, imports nothing and opens no vendor scene: it reads
    /// the catalog asset - which holds direct object references, not a folder to walk - and calls
    /// the same <see cref="ModularRoomBuilder"/> the house generator calls.
    /// </para>
    /// <para>
    /// The room it builds is the production path, not a mock-up: CIYC generates the structure at
    /// exact size with UVs in metres, the pack supplies the surface materials and the pieces that
    /// genuinely fit. That is the adapter fit Docs/HQ_MODULAR_MIGRATION.md measured and settled.
    /// </para>
    /// </summary>
    public static class HQTestRoomTool
    {
        private const string RoomName = "CIYC_HQ_TestRoom";

        /// <summary>
        /// 6 x 3 x 6, the logical cell. Not negotiable: SizeMm is in the deterministic assembly
        /// and written into the layout hash, so a test room at any other size is testing
        /// something the game will never build.
        /// </summary>
        private static readonly Vec3i CellMm = new Vec3i(6000, 3000, 6000);

        [MenuItem("Catch If You Can/9. ENTWICKLER - DEBUG/Test Room/Testraum bauen [AENDERT SZENE]", false, 930)]
        public static void BuildTestRoom()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ModularInteriorCatalog>(
                "Assets/CatchIfYouCan/ScriptableObjects/Content/ModularInteriorCatalog.asset");

            if (catalog == null)
            {
                EditorUtility.DisplayDialog(
                    "Kein Katalog",
                    "Es gibt noch keinen ModularInteriorCatalog.\n\n" +
                    "Catch If You Can > Modular Interior > Audit Pack, dann '1. Paket pruefen' " +
                    "und '2. Katalog bauen'. Der Testraum wird ohne Katalog trotzdem gebaut - " +
                    "dann aber in den neutralen Grautoenen statt mit den Paket-Materialien.",
                    "Trotzdem bauen", "Abbrechen");
            }

            Clear();

            // North carries the door; the other three are open to the outside, which is what
            // lets a window appear. Category Bedroom rather than Basement or Storage for the
            // same reason - those two never get windows.
            var room = new LayoutRoom(
                roomId: 0,
                archetypeId: "HQ_TEST_ROOM",
                category: RoomCategory.Bedroom,
                cell: new GridCell(0, 0),
                rotationIndex: 0,
                positionMm: new Vec3i(0, 0, 0),
                sizeMm: CellMm,
                variantIndex: 0,
                doorMask: LayoutRoom.DirectionMask(SocketDirection.North),
                openMask: LayoutRoom.DirectionMask(SocketDirection.East) |
                          LayoutRoom.DirectionMask(SocketDirection.South) |
                          LayoutRoom.DirectionMask(SocketDirection.West));

            GameObject built = ModularRoomBuilder.Build(room, Vector3.zero, null, catalog,
                                                        out string error);

            if (built == null)
            {
                Debug.LogError("[CIYC][TestRoom] Der Raum konnte nicht gebaut werden: " + error);
                return;
            }

            built.name = RoomName;
            AddInspectionLight(built.transform);

            string report = Report(built, room, catalog, error);
            Debug.Log(report);

            Selection.activeGameObject = built;
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        [MenuItem("Catch If You Can/9. ENTWICKLER - DEBUG/Test Room/Testraum entfernen [UNDO]", false, 931)]
        public static void Clear()
        {
            // GameObject.Find SKIPS INACTIVE OBJECTS, and a test room switched off to look past
            // it is exactly that: not found, not removed - and the next Build puts a second one
            // beside it. The scene is walked instead, inactive included, which is the only kind
            // this has to catch.
            int removed = 0;
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();

            for (int i = 0; i < roots.Length; i++)
            {
                var all = roots[i].GetComponentsInChildren<Transform>(true);
                for (int c = 0; c < all.Length; c++)
                {
                    if (all[c] == null || all[c].name != RoomName)
                        continue;

                    // Undo rather than DestroyImmediate: removing the wrong room by accident is
                    // a lost afternoon otherwise, and there is no reason this cannot be undone.
                    Undo.DestroyObjectImmediate(all[c].gameObject);
                    removed++;
                    break;      // its children went with it; this root's list is now stale
                }
            }

            Debug.Log(removed == 0
                ? "[CIYC] Kein Testraum '" + RoomName + "' in der Szene - auch kein " +
                  "ausgeschalteter."
                : "[CIYC] " + removed + " Testraum/-raeume entfernt (rueckgaengig machbar).");
        }

        /// <summary>
        /// Enough light to read a material by, and no more. The horror lighting is a different
        /// job with a different owner; a test room lit for atmosphere cannot be inspected, and
        /// one lit by six realtime lights is measuring the lights.
        /// </summary>
        private static void AddInspectionLight(Transform parent)
        {
            var go = new GameObject("TestRoom_InspectionLight");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 2.60f, 0f);

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 12f;
            light.intensity = 2.0f;
            light.color = new Color(1f, 0.96f, 0.88f);
            light.shadows = LightShadows.None;
        }

        // ------------------------------------------------------------------------- report

        private static string Report(GameObject root, LayoutRoom room,
            ModularInteriorCatalog catalog, string builderError)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== CIYC HQ TEST ROOM ===");
            sb.AppendLine("Zelle      : 6.00 x 3.00 x 6.00 m (SizeMm " + room.SizeMm + ")");
            sb.AppendLine("Katalog    : " + (catalog != null
                ? catalog.name + " (Paket '" + catalog.PackDisplayName + "')"
                : "<keiner - neutrale Ersatzmaterialien>"));

            if (!string.IsNullOrEmpty(builderError))
                sb.AppendLine("Bauhinweis : " + builderError);

            var renderers = new List<Renderer>();
            root.GetComponentsInChildren(true, renderers);

            var colliders = new List<Collider>();
            root.GetComponentsInChildren(true, colliders);

            var filters = new List<MeshFilter>();
            root.GetComponentsInChildren(true, filters);

            int triangles = 0;
            for (int i = 0; i < filters.Count; i++)
            {
                Mesh mesh = filters[i].sharedMesh;
                if (mesh != null)
                    triangles += mesh.triangles.Length / 3;
            }

            int activeColliders = 0;
            int meshColliders = 0;
            for (int i = 0; i < colliders.Count; i++)
            {
                if (colliders[i].enabled)
                    activeColliders++;
                if (colliders[i] is MeshCollider)
                    meshColliders++;
            }

            var materials = new List<Material>();
            for (int i = 0; i < renderers.Count; i++)
            {
                Material m = renderers[i].sharedMaterial;
                if (m != null && !materials.Contains(m))
                    materials.Add(m);
            }

            sb.AppendLine();
            sb.AppendLine("Renderer   : " + renderers.Count);
            sb.AppendLine("Dreiecke   : " + triangles);
            sb.AppendLine("Collider   : " + colliders.Count + " (" + activeColliders +
                          " aktiv, " + meshColliders + " MeshCollider)");
            sb.AppendLine("Materialien: " + materials.Count);

            if (meshColliders > 0)
                sb.AppendLine("WARNUNG    : ein MeshCollider auf dekorativer Paket-Geometrie ist " +
                              "der teure Weg zur selben falschen Antwort.");

            sb.AppendLine();
            sb.AppendLine("--- Materialien ---");
            for (int i = 0; i < materials.Count; i++)
                sb.AppendLine(DescribeMaterial(materials[i]));

            sb.AppendLine();
            sb.AppendLine("--- Hierarchie ---");
            Describe(root.transform, 0, sb);

            return sb.ToString();
        }

        private static string DescribeMaterial(Material m)
        {
            Shader shader = m.shader;
            string shaderName = shader != null ? shader.name : "<null>";
            var sb = new StringBuilder();
            sb.Append("  ").Append(m.name).Append("  shader=").Append(shaderName);

            if (shader != null)
                sb.Append(" supported=").Append(shader.isSupported);

            if (m.HasProperty("_BaseMap"))
            {
                Texture t = m.GetTexture("_BaseMap");
                sb.Append(" baseMap=").Append(t != null
                    ? t.name + " " + t.width + "x" + t.height
                    : "<none>");
                sb.Append(" tiling=").Append(m.GetTextureScale("_BaseMap").ToString("F4"));
            }

            if (m.HasProperty("_BumpMap"))
            {
                Texture n = m.GetTexture("_BumpMap");
                sb.Append(" normal=").Append(n != null ? n.name : "<none>");
            }

            return sb.ToString();
        }

        private static void Describe(Transform t, int depth, StringBuilder sb)
        {
            sb.Append("  ");
            for (int i = 0; i < depth; i++)
                sb.Append("    ");

            sb.Append(t.name);

            var renderer = t.GetComponent<Renderer>();
            if (renderer != null && renderer.sharedMaterial != null)
                sb.Append("  [").Append(renderer.sharedMaterial.name).Append("]");

            var colliders = t.GetComponents<Collider>();
            if (colliders.Length > 0)
                sb.Append("  {").Append(colliders.Length).Append(" collider}");

            sb.AppendLine();

            for (int i = 0; i < t.childCount; i++)
                Describe(t.GetChild(i), depth + 1, sb);
        }
    }
}
