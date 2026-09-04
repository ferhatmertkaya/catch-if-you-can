using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CatchIfYouCan.Content;
using CatchIfYouCan.Procedural;
using UnityEditor;
using UnityEngine;

namespace CatchIfYouCan.EditorTools
{
    /// <summary>
    /// Turns an imported modular interior pack into a ModularInteriorCatalog.
    ///
    /// <para>
    /// Nothing here names a publisher, a folder or a mesh. The pack is found by being told
    /// where it is, and its pieces are sorted into structural roles by what their filenames
    /// say. That is a guess, and it is presented as one: the window shows the classification
    /// BEFORE anything is written, with the count per role and the names that fell through,
    /// so a wrong guess is corrected by editing the catalog rather than discovered later as a
    /// room with no ceiling.
    /// </para>
    ///
    /// <para>
    /// The previous integration tool did the opposite - it scanned a hard-coded Kenney folder
    /// and wrote 130 assets on one click. That is how a pipeline gets recreated by accident
    /// after being removed. This one writes exactly one asset, and only when asked.
    /// </para>
    /// </summary>
    public class ModularInteriorTools : EditorWindow
    {
        private const string CatalogFolder = "Assets/CatchIfYouCan/ScriptableObjects/Content";
        private const string CatalogPath = CatalogFolder + "/ModularInteriorCatalog.asset";

        private string _packFolder = "Assets/HQ Modular House Interior";
        private string _report = "Paket-Ordner eintragen und 'Paket pruefen' druecken.";
        private Vector2 _scroll;
        private Classification _classified;

        [MenuItem("Catch If You Can/Modular Interior/Audit Pack")]
        public static void OpenAudit()
        {
            var w = GetWindow<ModularInteriorTools>(true, "Modularer Innenausbau");
            w.minSize = new Vector2(620f, 520f);
        }

        [MenuItem("Catch If You Can/Modular Interior/Validate Environment")]
        public static void OpenValidate()
        {
            var w = GetWindow<ModularInteriorTools>(true, "Modularer Innenausbau");
            w.minSize = new Vector2(620f, 520f);
            w._report = ValidateEnvironment();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "1. Paket pruefen  - zaehlt und klassifiziert, schreibt nichts.\n" +
                "2. Katalog bauen  - schreibt GENAU EIN Asset: den Modular-Katalog.\n" +
                "3. Umgebung pruefen - sagt, ob damit ein Haus gebaut werden kann.",
                MessageType.Info);

            _packFolder = EditorGUILayout.TextField("Paket-Ordner", _packFolder);

            if (GUILayout.Button("1. Paket pruefen"))
            {
                _classified = Classify(_packFolder);
                _report = Describe(_classified);
            }

            if (GUILayout.Button("2. Katalog bauen (schreibt " + CatalogPath + ")"))
                _report = BuildCatalog(_classified);

            if (GUILayout.Button("3. Umgebung pruefen"))
                _report = ValidateEnvironment();

            EditorGUILayout.Space();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.TextArea(_report);
            EditorGUILayout.EndScrollView();
        }

        // ------------------------------------------------------------- Klassifikation

        private class Classification
        {
            public string Folder;
            public readonly Dictionary<ModuleRole, List<string>> ByRole =
                new Dictionary<ModuleRole, List<string>>();
            public readonly List<string> Unclassified = new List<string>();
            public int Prefabs;
            public int Models;
            public int Materials;
            public int Textures;
            public int Scenes;
            public int LodGroups;
            public int NonUrpMaterials;
            public int Total;
        }

        /// <summary>
        /// Filename synonyms per structural role, longest match first so "wall-door" is read
        /// as a doorway rather than as a wall. This is the only vendor-shaped knowledge in the
        /// tool, and it is a list of ordinary English words, not a list of one pack's assets.
        /// </summary>
        private static readonly (ModuleRole Role, string[] Words)[] RoleWords =
        {
            (ModuleRole.WallWithDoorway, new[] { "walldoor", "wall_door", "wall-door", "doorway",
                                                 "wall_opening", "wall-opening", "wallopening",
                                                 "archway", "portal_wall" }),
            (ModuleRole.WallWithWindow,  new[] { "wallwindow", "wall_window", "wall-window",
                                                 "windowwall", "window_wall" }),
            (ModuleRole.Stairs,          new[] { "stair", "staircase", "steps", "ladder" }),
            (ModuleRole.Baseboard,       new[] { "baseboard", "skirting", "plinth", "molding",
                                                 "moulding", "trim" }),
            (ModuleRole.CornerTrim,      new[] { "corner", "pillar", "column" }),
            (ModuleRole.Ceiling,         new[] { "ceiling", "roof" }),
            (ModuleRole.Floor,           new[] { "floor", "ground", "parquet", "tilefloor" }),
            (ModuleRole.WallSolid,       new[] { "wall", "partition" }),
        };

        private static Classification Classify(string folder)
        {
            var c = new Classification { Folder = folder };

            if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder))
                return c;

            string[] search = { folder };
            var prefabGuids = AssetDatabase.FindAssets("t:Prefab", search);
            c.Prefabs = prefabGuids.Length;
            c.Models = AssetDatabase.FindAssets("t:Model", search).Length;
            c.Textures = AssetDatabase.FindAssets("t:Texture", search).Length;
            c.Scenes = AssetDatabase.FindAssets("t:Scene", search).Length;

            var materialGuids = AssetDatabase.FindAssets("t:Material", search);
            c.Materials = materialGuids.Length;
            for (int i = 0; i < materialGuids.Length; i++)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(
                    AssetDatabase.GUIDToAssetPath(materialGuids[i]));
                if (mat == null || mat.shader == null)
                    continue;

                // A shader outside the URP family draws magenta under Forward+. That count is
                // the conversion workload, stated as a number rather than found in the scene.
                if (!mat.shader.name.StartsWith("Universal Render Pipeline") &&
                    !mat.shader.name.StartsWith("Shader Graphs") &&
                    !mat.shader.name.StartsWith("CIYC"))
                {
                    c.NonUrpMaterials++;
                }
            }

            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null && go.GetComponentsInChildren<LODGroup>(true).Length > 0)
                    c.LodGroups++;

                c.Total++;
                string name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();

                ModuleRole? role = RoleOf(name);
                if (role.HasValue)
                {
                    if (!c.ByRole.TryGetValue(role.Value, out var list))
                    {
                        list = new List<string>();
                        c.ByRole[role.Value] = list;
                    }

                    list.Add(path);
                }
                else
                {
                    c.Unclassified.Add(path);
                }
            }

            foreach (var list in c.ByRole.Values)
                list.Sort(StringComparer.Ordinal);
            c.Unclassified.Sort(StringComparer.Ordinal);

            return c;
        }

        private static ModuleRole? RoleOf(string lowerName)
        {
            for (int i = 0; i < RoleWords.Length; i++)
            {
                var words = RoleWords[i].Words;
                for (int w = 0; w < words.Length; w++)
                {
                    if (lowerName.Contains(words[w]))
                        return RoleWords[i].Role;
                }
            }

            return null;
        }

        private static string Describe(Classification c)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== PAKET-AUDIT ===");
            sb.AppendLine("Ordner: " + c.Folder);

            if (c.Total == 0)
            {
                sb.AppendLine();
                sb.AppendLine("Dort liegt nichts. Entweder ist der Pfad falsch, oder das Paket");
                sb.AppendLine("ist noch nicht importiert. Es wurde NICHTS geschrieben.");
                return sb.ToString();
            }

            sb.AppendLine();
            sb.AppendLine("Prefabs   : " + c.Prefabs);
            sb.AppendLine("Modelle   : " + c.Models);
            sb.AppendLine("Materialien: " + c.Materials + "  (davon nicht-URP: " + c.NonUrpMaterials + ")");
            sb.AppendLine("Texturen  : " + c.Textures);
            sb.AppendLine("Szenen    : " + c.Scenes + "  (Demo-Szenen gehoeren NICHT in die Build-Liste)");
            sb.AppendLine("LODGroups : " + c.LodGroups + " von " + c.Prefabs + " Prefabs");
            sb.AppendLine();

            sb.AppendLine("--- STRUKTURELLE ROLLEN (aus Dateinamen geraten) ---");
            foreach (ModuleRole role in Enum.GetValues(typeof(ModuleRole)))
            {
                c.ByRole.TryGetValue(role, out var list);
                int count = list == null ? 0 : list.Count;
                bool required = Array.IndexOf(ModularInteriorCatalog.RequiredStructuralRoles, role) >= 0;
                sb.AppendLine(string.Format("{0,-18} {1,4}{2}", role, count,
                    count == 0 && required ? "   <-- FEHLT, ohne das kein Haus" : ""));
            }

            sb.AppendLine();
            int structural = c.ByRole.Values.Sum(l => l.Count);
            sb.AppendLine("--- KLASSIFIKATION ---");
            if (structural == 0)
                sb.AppendLine("A. FERTIGE RAEUME oder reine Moebel - kein strukturelles Kit erkannt.");
            else if (structural > c.Unclassified.Count)
                sb.AppendLine("B. MODULARES KIT - die Mehrheit der Prefabs sind Bauteile.");
            else
                sb.AppendLine("C. GEMISCHT - Bauteile und Einrichtung im selben Ordner.");

            sb.AppendLine("Nicht zugeordnet: " + c.Unclassified.Count +
                          " (Moebel, Deko, Requisiten - das ist normal)");

            if (c.Unclassified.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("--- ERSTE 40 NICHT ZUGEORDNETE ---");
                for (int i = 0; i < Math.Min(40, c.Unclassified.Count); i++)
                    sb.AppendLine("  " + Path.GetFileNameWithoutExtension(c.Unclassified[i]));
            }

            sb.AppendLine();
            sb.AppendLine("Es wurde NICHTS geschrieben. Schritt 2 schreibt genau ein Asset.");
            return sb.ToString();
        }

        // -------------------------------------------------------------- Katalog bauen

        private static string BuildCatalog(Classification c)
        {
            if (c == null || c.Total == 0)
                return "Erst 'Paket pruefen' - ohne Klassifikation wird nichts geschrieben.";

            var sb = new StringBuilder();
            sb.AppendLine("=== KATALOG BAUEN ===");

            if (!AssetDatabase.IsValidFolder(CatalogFolder))
            {
                sb.AppendLine("FEHLER: " + CatalogFolder + " gibt es nicht.");
                return sb.ToString();
            }

            var catalog = AssetDatabase.LoadAssetAtPath<ModularInteriorCatalog>(CatalogPath);
            bool created = catalog == null;
            if (created)
            {
                catalog = ScriptableObject.CreateInstance<ModularInteriorCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.PackRootFolder = c.Folder;
            catalog.PackDisplayName = Path.GetFileName(c.Folder.TrimEnd('/'));

            var sets = new List<ModuleSet>();
            foreach (ModuleRole role in Enum.GetValues(typeof(ModuleRole)))
            {
                if (!c.ByRole.TryGetValue(role, out var paths) || paths.Count == 0)
                    continue;

                var variants = new List<GameObject>(paths.Count);
                for (int i = 0; i < paths.Count; i++)
                {
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
                    if (go != null)
                        variants.Add(go);
                }

                if (variants.Count == 0)
                    continue;

                sets.Add(new ModuleSet
                {
                    Role = role,
                    Categories = new RoomCategory[0],
                    Variants = variants.ToArray(),
                    // Measured from the first variant's own space. A world AABB here would be
                    // wrong for the same reason it was wrong in the equipment factory.
                    ModuleSize = MeasureLocalSize(variants[0]),
                });

                sb.AppendLine(string.Format("{0,-18} {1,4} Varianten, Modulmass {2}",
                    role, variants.Count, MeasureLocalSize(variants[0]).ToString("F2")));
            }

            catalog.Modules = sets.ToArray();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            sb.AppendLine();
            sb.AppendLine((created ? "Angelegt: " : "Aktualisiert: ") + CatalogPath);
            sb.AppendLine();
            sb.AppendLine("NAECHSTER SCHRITT: den Katalog in InvestigationContentCatalog.asset");
            sb.AppendLine("in das Feld 'Modular Interior' ziehen. Danach Schritt 3.");
            sb.AppendLine();
            sb.AppendLine("Die Zuordnung ist geraten. Wo sie falsch ist, im Katalog-Asset");
            sb.AppendLine("korrigieren - er ist eine ganz normale Liste.");
            return sb.ToString();
        }

        /// <summary>
        /// The size of a prefab in ITS OWN space: every mesh corner pushed through the
        /// renderer's world matrix and back into the root's, never Renderer.bounds, which is
        /// a world AABB and only right while every ancestor has scale 1.
        /// </summary>
        private static Vector3 MeasureLocalSize(GameObject prefab)
        {
            if (prefab == null)
                return Vector3.zero;

            var filters = prefab.GetComponentsInChildren<MeshFilter>(true);
            if (filters.Length == 0)
                return Vector3.zero;

            var toRoot = prefab.transform.worldToLocalMatrix;
            bool any = false;
            Vector3 min = Vector3.zero;
            Vector3 max = Vector3.zero;

            for (int i = 0; i < filters.Length; i++)
            {
                var mesh = filters[i].sharedMesh;
                if (mesh == null)
                    continue;

                var localToRoot = toRoot * filters[i].transform.localToWorldMatrix;
                var b = mesh.bounds;

                for (int corner = 0; corner < 8; corner++)
                {
                    var p = new Vector3(
                        (corner & 1) == 0 ? b.min.x : b.max.x,
                        (corner & 2) == 0 ? b.min.y : b.max.y,
                        (corner & 4) == 0 ? b.min.z : b.max.z);

                    p = localToRoot.MultiplyPoint3x4(p);

                    if (!any)
                    {
                        min = max = p;
                        any = true;
                        continue;
                    }

                    min = Vector3.Min(min, p);
                    max = Vector3.Max(max, p);
                }
            }

            return any ? max - min : Vector3.zero;
        }

        // ------------------------------------------------------------ Umgebung pruefen

        private static string ValidateEnvironment()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== UMGEBUNG PRUEFEN ===");

            var catalog = AssetDatabase.LoadAssetAtPath<ModularInteriorCatalog>(CatalogPath);
            if (catalog == null)
            {
                sb.AppendLine("FEHLER: es gibt keinen " + CatalogPath);
                sb.AppendLine("Ohne ihn kann der Generator keine Hausgeometrie bauen - und er");
                sb.AppendLine("weicht auf NICHTS aus: im Editor eine Primitiv-Box mit Warnung,");
                sb.AppendLine("im Player-Build gar nichts, mit Fehler.");
                return sb.ToString();
            }

            sb.AppendLine("Katalog   : " + CatalogPath);
            sb.AppendLine("Paket     : " + catalog.PackDisplayName + "  (" + catalog.PackRootFolder + ")");
            sb.AppendLine("Modulsaetze: " + catalog.Modules.Length);
            sb.AppendLine();

            if (catalog.TryValidate(out string error))
                sb.AppendLine("OK: alle tragenden Rollen sind besetzt, keine leere Referenz.");
            else
                sb.AppendLine("FEHLER: " + error);

            sb.AppendLine();
            sb.AppendLine("--- PRO RAUMKATEGORIE ---");
            foreach (RoomCategory category in Enum.GetValues(typeof(RoomCategory)))
            {
                var missing = new List<string>();
                for (int r = 0; r < ModularInteriorCatalog.RequiredStructuralRoles.Length; r++)
                {
                    var role = ModularInteriorCatalog.RequiredStructuralRoles[r];
                    if (catalog.FindVariants(role, category).Length == 0)
                        missing.Add(role.ToString());
                }

                sb.AppendLine(string.Format("{0,-14} {1}", category,
                    missing.Count == 0 ? "vollstaendig" : "FEHLT: " + string.Join(", ", missing.ToArray())));
            }

            var content = AssetDatabase.LoadAssetAtPath<InvestigationContentCatalog>(
                "Assets/CatchIfYouCan/ScriptableObjects/Content/InvestigationContentCatalog.asset");
            sb.AppendLine();
            if (content == null)
                sb.AppendLine("WARNUNG: InvestigationContentCatalog.asset nicht gefunden.");
            else if (content.ModularInterior == null)
                sb.AppendLine("WARNUNG: der Content-Katalog zeigt noch nicht auf den Modular-Katalog.");
            else if (content.ModularInterior != catalog)
                sb.AppendLine("WARNUNG: der Content-Katalog zeigt auf einen ANDEREN Modular-Katalog.");
            else
                sb.AppendLine("OK: der Content-Katalog zeigt auf diesen Modular-Katalog.");

            return sb.ToString();
        }
    }
}
