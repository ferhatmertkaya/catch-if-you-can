using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
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

        private string _packFolder = "Assets/HQ Modular House";
        private string _report = "Paket-Ordner eintragen und 'Paket pruefen' druecken.";
        private Vector2 _scroll;
        private Classification _classified;

        [MenuItem("Catch If You Can/Modular Interior/Audit Pack")]
        public static void OpenAudit()
        {
            var w = GetWindow<ModularInteriorTools>(true, "Modularer Innenausbau");
            w.minSize = new Vector2(620f, 520f);
        }

        [MenuItem("Catch If You Can/Modular Interior/Architecture Forensics - Interior")]
        public static void OpenForensicsInterior()
        {
            var w = GetWindow<ModularInteriorTools>(true, "Modularer Innenausbau");
            w.minSize = new Vector2(620f, 520f);
            w._packFolder = "Assets/HQ Modular House/interior";
            w._report = MeasureArchitecture(w._packFolder);
        }

        [MenuItem("Catch If You Can/Modular Interior/Architecture Forensics - Full HQ Package")]
        public static void OpenForensicsFullPackage()
        {
            var w = GetWindow<ModularInteriorTools>(true, "Modularer Innenausbau");
            w.minSize = new Vector2(620f, 520f);
            w._packFolder = "Assets/HQ Modular House";
            w._report = MeasureArchitecture(w._packFolder);
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
                "3. Kit vermessen  - Masse, Pivots, Ausrichtung, Collider, Raster. Schreibt nichts.\n" +
                "4. Architektur-Forensik - liest GENAU den Ordner oben, rekursiv, ohne Umleitung.\n" +
                "5. Umgebung pruefen - sagt, ob damit ein Haus gebaut werden kann.",
                MessageType.Info);

            _packFolder = EditorGUILayout.TextField("Paket-Ordner", _packFolder);

            if (GUILayout.Button("1. Paket pruefen"))
            {
                _classified = Classify(_packFolder);
                _report = Describe(_classified);
            }

            if (GUILayout.Button("2. Katalog bauen (schreibt " + CatalogPath + ")"))
                _report = BuildCatalog(_classified);

            if (GUILayout.Button("3. Kit vermessen (schreibt nichts)"))
                _report = MeasureKit(_packFolder);

            if (GUILayout.Button("4. Architektur-Forensik auf GENAU dem Ordner oben"))
                _report = MeasureArchitecture(_packFolder);

            if (GUILayout.Button("5. Umgebung pruefen"))
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

        /// <summary>
        /// Hangs the freshly built kit off the catalog the RUNTIME actually reads.
        ///
        /// <para>
        /// This used to be a sentence in the report asking the user to drag the asset into a
        /// field, and it is the step that was missed: with 'Modular Interior' empty, the
        /// generator can build no room from the pack and every room in the house falls back to
        /// an untextured primitive box. A tool that produces an asset nothing points at has
        /// done half a job - the same shape as content removed without removing what pointed
        /// at it, only pointing the other way.
        /// </para>
        /// <para>
        /// An existing reference is left alone and reported, because overwriting a hand-made
        /// wiring is not this button's business.
        /// </para>
        /// </summary>
        private static string WireIntoContentCatalog(ModularInteriorCatalog kit)
        {
            const string ContentCatalogPath =
                "Assets/CatchIfYouCan/Resources/CatchIfYouCan/InvestigationContentCatalog.asset";

            var content = AssetDatabase.LoadAssetAtPath<InvestigationContentCatalog>(ContentCatalogPath);
            if (content == null)
                return "WARNUNG: " + ContentCatalogPath + " gibt es nicht, der Katalog haengt " +
                       "also an nichts. Ohne ihn baut der Generator KEINEN Raum aus dem Paket.\n";

            if (content.ModularInterior == kit)
                return "Verdrahtet: " + ContentCatalogPath + " zeigt bereits auf diesen Katalog.\n";

            if (content.ModularInterior != null)
                return "HINWEIS: " + ContentCatalogPath + " zeigt auf '" +
                       content.ModularInterior.name + "' und wird nicht ueberschrieben. Wenn " +
                       "das falsch ist, das Feld 'Modular Interior' von Hand aendern.\n";

            content.ModularInterior = kit;
            EditorUtility.SetDirty(content);
            AssetDatabase.SaveAssets();
            return "Verdrahtet: " + ContentCatalogPath + " -> Modular Interior = " +
                   kit.name + ". Der Generator liest jetzt dieses Kit.\n";
        }

        /// <summary>
        /// Picks the wall, floor and ceiling materials, and MEASURES the density each one is
        /// authored at instead of assuming one.
        ///
        /// <para>
        /// The pack normalises its UVs per piece: every wall maps its texture 0..1 across its
        /// own width, so a tiling of 1.5 means 0.38 repeats per metre on a 3.95 m piece and 0.13
        /// on an 11.90 m one. Generated geometry writes its UVs in metres, so the two only agree
        /// if the density is restated in metres - and the only honest source for that number is
        /// the asset itself: the material's own tiling divided by the piece it is used on.
        /// </para>
        /// <para>
        /// Measured in the piece's OWN space. A world AABB would be right only while every
        /// ancestor has scale 1, which is the mistake that once made a flashlight 2 mm long and
        /// a room wall a hundred times too big.
        /// </para>
        /// <para>
        /// Nothing is scanned for this. It reads the prefabs the classification already
        /// selected, and no more.
        /// </para>
        /// </summary>
        private static string ChooseSurfaces(ModularInteriorCatalog catalog, Classification c)
        {
            var sb = new StringBuilder();
            sb.AppendLine("--- OBERFLAECHEN ---");

            var found = new Dictionary<string, SurfaceCandidate>();

            foreach (var pair in c.ByRole)
            {
                List<string> paths = pair.Value;
                if (paths == null)
                    continue;

                for (int i = 0; i < paths.Count && i < SurfaceSampleLimit; i++)
                    CollectSurfaces(paths[i], found);
            }

            if (found.Count == 0)
            {
                sb.AppendLine("Keine Materialien auf den klassifizierten Prefabs gefunden.");
                sb.AppendLine("Die Raeume bleiben in den neutralen Grautoenen.");
                return sb.ToString();
            }

            // Named preferences first, then whatever was measured most often. The names come
            // from the measured material families in Docs/HQ_MODULAR_MIGRATION.md; falling back
            // to the commonest material means a renamed pack still produces a textured room.
            catalog.WallSurface = Choose(found, sb, "Wand", "wallpaper3", "wallpaper1", "beton");
            catalog.FloorSurface = Choose(found, sb, "Boden", "tile1", "beton", "wallpaper1");
            catalog.CeilingSurface = Choose(found, sb, "Decke", "white", "beton", "wallpaper1");

            sb.AppendLine();
            sb.AppendLine("Die Dichte ist GEMESSEN (Kachelung des Materials geteilt durch die");
            sb.AppendLine("Groesse des Teils, auf dem es liegt), nicht geschaetzt. Wo sie falsch");
            sb.AppendLine("aussieht, im Katalog-Asset korrigieren - es sind zwei Zahlen.");
            return sb.ToString();
        }

        /// <summary>How many prefabs per role are opened to look for materials. Small on purpose.</summary>
        private const int SurfaceSampleLimit = 6;

        private struct SurfaceCandidate
        {
            public Material Material;
            public Vector2 RepeatsPerMetre;
            public int Seen;
        }

        private static void CollectSurfaces(string prefabPath, Dictionary<string, SurfaceCandidate> found)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
                return;

            var renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                var filter = renderers[r].GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                    continue;

                // The mesh's own bounds, in the mesh's own space.
                Vector3 local = filter.sharedMesh.bounds.size;
                float width = Mathf.Max(local.x, local.z);
                float height = local.y;
                if (width < 0.01f || height < 0.01f)
                    continue;

                var materials = renderers[r].sharedMaterials;
                for (int m = 0; m < materials.Length; m++)
                {
                    Material mat = materials[m];
                    if (mat == null || mat.shader == null || !mat.HasProperty("_BaseMap"))
                        continue;

                    if (mat.GetTexture("_BaseMap") == null)
                        continue;

                    Vector2 tiling = mat.GetTextureScale("_BaseMap");
                    if (tiling.x <= 0f || tiling.y <= 0f)
                        continue;

                    string key = mat.name;
                    if (found.TryGetValue(key, out SurfaceCandidate existing))
                    {
                        existing.Seen++;
                        found[key] = existing;
                        continue;
                    }

                    found[key] = new SurfaceCandidate
                    {
                        Material = mat,
                        RepeatsPerMetre = new Vector2(tiling.x / width, tiling.y / height),
                        Seen = 1,
                    };
                }
            }
        }

        private static SurfaceMaterial Choose(Dictionary<string, SurfaceCandidate> found,
            StringBuilder sb, string role, params string[] preferred)
        {
            for (int p = 0; p < preferred.Length; p++)
            {
                foreach (var pair in found)
                {
                    if (pair.Key.IndexOf(preferred[p], StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    return Report(sb, role, pair.Value, "Name '" + preferred[p] + "'");
                }
            }

            SurfaceCandidate best = default;
            int bestSeen = -1;
            foreach (var pair in found)
            {
                if (pair.Value.Seen > bestSeen)
                {
                    bestSeen = pair.Value.Seen;
                    best = pair.Value;
                }
            }

            return Report(sb, role, best, "haeufigstes Material");
        }

        private static SurfaceMaterial Report(StringBuilder sb, string role,
            SurfaceCandidate candidate, string why)
        {
            if (candidate.Material == null)
            {
                sb.AppendLine(string.Format("{0,-6} : <keins> - bleibt neutral grau", role));
                return default;
            }

            sb.AppendLine(string.Format("{0,-6} : {1} ({2}), {3} Wiederholungen/m, Shader {4}",
                role, candidate.Material.name, why,
                candidate.RepeatsPerMetre.ToString("F4"),
                candidate.Material.shader != null ? candidate.Material.shader.name : "<null>"));

            return new SurfaceMaterial
            {
                Material = candidate.Material,
                RepeatsPerMetre = candidate.RepeatsPerMetre,
            };
        }

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
            sb.AppendLine();
            sb.Append(ChooseSurfaces(catalog, c));

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            sb.AppendLine();
            sb.AppendLine((created ? "Angelegt: " : "Aktualisiert: ") + CatalogPath);
            sb.AppendLine();
            sb.Append(WireIntoContentCatalog(catalog));
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

        // ---------------------------------------------------------------- Kit vermessen

        /// <summary>
        /// What one modular piece actually is, measured rather than assumed.
        ///
        /// Every number here comes from the mesh in the prefab's OWN space - each corner of each
        /// submesh pushed through the renderer's matrix and back into the prefab root. A world
        /// AABB would be wrong for the same reason it was wrong for the flashlight and the room
        /// walls: it carries whatever scale the ancestors happen to have.
        /// </summary>
        private class Piece
        {
            public string Path;
            public Vector3 Size;          // metres, in the prefab's own space
            public Vector3 PivotToMin;    // where the origin sits relative to the bounds minimum
            public string PivotDescription;
            public string ThinAxis;       // the axis a wall is thin on - its facing convention
            public int Colliders;
            public string ColliderKinds;
            public int LodLevels;
            public int Materials;
            public string Shaders;
            public string SourceModel;
        }

        private static string MeasureKit(string root)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=========================================================");
            sb.AppendLine("KIT-VERMESSUNG  -  ES WIRD NICHTS GESCHRIEBEN");
            sb.AppendLine("=========================================================");
            sb.AppendLine("Ordner: " + root);
            sb.AppendLine();

            if (string.IsNullOrEmpty(root) || !AssetDatabase.IsValidFolder(root))
            {
                sb.AppendLine("Ordner nicht gefunden.");
                return sb.ToString();
            }

            var pieces = new List<Piece>();
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { root });

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null)
                    continue;

                var piece = new Piece { Path = path };
                MeasureLocal(go, out Vector3 min, out Vector3 max);
                piece.Size = max - min;
                piece.PivotToMin = -min;
                piece.PivotDescription = DescribePivot(min, max);
                piece.ThinAxis = ThinnestAxis(piece.Size);

                var colliders = go.GetComponentsInChildren<Collider>(true);
                piece.Colliders = colliders.Length;
                var kinds = new SortedDictionary<string, int>();
                for (int c = 0; c < colliders.Length; c++)
                {
                    string k = colliders[c].GetType().Name;
                    kinds.TryGetValue(k, out int n);
                    kinds[k] = n + 1;
                }
                var kindText = new List<string>();
                foreach (var pair in kinds)
                    kindText.Add(pair.Key + "x" + pair.Value);
                piece.ColliderKinds = kindText.Count == 0 ? "-" : string.Join(",", kindText.ToArray());

                var lods = go.GetComponentsInChildren<LODGroup>(true);
                piece.LodLevels = lods.Length == 0 ? 0 : lods[0].lodCount;

                var renderers = go.GetComponentsInChildren<Renderer>(true);
                var shaders = new SortedDictionary<string, int>();
                int materialCount = 0;
                for (int r = 0; r < renderers.Length; r++)
                {
                    var mats = renderers[r].sharedMaterials;
                    for (int m = 0; m < mats.Length; m++)
                    {
                        if (mats[m] == null)
                            continue;

                        materialCount++;
                        string sh = mats[m].shader != null ? mats[m].shader.name : "(kein Shader)";
                        shaders.TryGetValue(sh, out int n);
                        shaders[sh] = n + 1;
                    }
                }
                piece.Materials = materialCount;
                var shaderText = new List<string>();
                foreach (var pair in shaders)
                    shaderText.Add(pair.Key);
                piece.Shaders = shaderText.Count == 0 ? "-" : string.Join(" | ", shaderText.ToArray());

                var filters = go.GetComponentsInChildren<MeshFilter>(true);
                piece.SourceModel = filters.Length > 0 && filters[0].sharedMesh != null
                    ? Path.GetFileName(AssetDatabase.GetAssetPath(filters[0].sharedMesh))
                    : "-";

                pieces.Add(piece);
            }

            sb.AppendLine("Prefabs vermessen: " + pieces.Count);
            sb.AppendLine();

            // ---- Massverteilung: daraus faellt das Raster
            var sizeGroups = new SortedDictionary<string, int>();
            for (int i = 0; i < pieces.Count; i++)
            {
                string key = Round(pieces[i].Size.x) + " x " + Round(pieces[i].Size.y) +
                             " x " + Round(pieces[i].Size.z);
                sizeGroups.TryGetValue(key, out int n);
                sizeGroups[key] = n + 1;
            }

            sb.AppendLine("--- MASSE, GERUNDET AUF 5 cm (die haeufigsten zuerst) ---");
            sb.AppendLine("  Ein modulares Kit hat wenige, oft wiederholte Masse. Viele einzelne");
            sb.AppendLine("  Masse heissen: es ist kein Kit, sondern eine Moebelsammlung.");
            sb.AppendLine();
            var ordered = new List<KeyValuePair<string, int>>(sizeGroups);
            ordered.Sort((a, b) => b.Value.CompareTo(a.Value));
            for (int i = 0; i < Mathf.Min(30, ordered.Count); i++)
                sb.AppendLine(string.Format("  {0,4}x   {1}", ordered[i].Value, ordered[i].Key));
            sb.AppendLine();
            sb.AppendLine("  verschiedene Masse insgesamt: " + sizeGroups.Count + " bei " + pieces.Count + " Prefabs");
            sb.AppendLine();

            // ---- Pivot-Konvention
            var pivots = new SortedDictionary<string, int>();
            for (int i = 0; i < pieces.Count; i++)
            {
                pivots.TryGetValue(pieces[i].PivotDescription, out int n);
                pivots[pieces[i].PivotDescription] = n + 1;
            }

            sb.AppendLine("--- PIVOT-KONVENTION ---");
            sb.AppendLine("  Wo der Nullpunkt eines Teils relativ zu seinen eigenen Grenzen liegt.");
            sb.AppendLine("  Der Assembler muss das wissen, sonst steht jede Wand um ihre halbe");
            sb.AppendLine("  Dicke daneben.");
            sb.AppendLine();
            foreach (var pair in pivots)
                sb.AppendLine(string.Format("  {0,4}x   {1}", pair.Value, pair.Key));
            sb.AppendLine();

            // ---- Ausrichtung
            var axes = new SortedDictionary<string, int>();
            for (int i = 0; i < pieces.Count; i++)
            {
                axes.TryGetValue(pieces[i].ThinAxis, out int n);
                axes[pieces[i].ThinAxis] = n + 1;
            }

            sb.AppendLine("--- DUENNSTE ACHSE (die Blickrichtung einer Wand) ---");
            foreach (var pair in axes)
                sb.AppendLine(string.Format("  {0,4}x   {1}", pair.Value, pair.Key));
            sb.AppendLine();

            // ---- Collider und LOD
            int noCollider = 0, meshCollider = 0, withLod = 0;
            for (int i = 0; i < pieces.Count; i++)
            {
                if (pieces[i].Colliders == 0) noCollider++;
                if (pieces[i].ColliderKinds.Contains("MeshCollider")) meshCollider++;
                if (pieces[i].LodLevels > 0) withLod++;
            }

            sb.AppendLine("--- COLLIDER UND LOD ---");
            sb.AppendLine("  ohne jeden Collider : " + noCollider + " von " + pieces.Count +
                          "   <- die brauchen einen, sonst laeuft der Spieler hindurch");
            sb.AppendLine("  mit MeshCollider    : " + meshCollider + " von " + pieces.Count);
            sb.AppendLine("  mit LODGroup        : " + withLod + " von " + pieces.Count);
            sb.AppendLine();

            // ---- Einzelaufstellung
            sb.AppendLine("--- ALLE TEILE ---");
            sb.AppendLine(string.Format("  {0,-22} {1,-6} {2,-14} {3,-8} {4,-4} {5}",
                "GROESSE (m)", "DUENN", "PIVOT", "COLLIDER", "LOD", "PREFAB"));
            pieces.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));
            for (int i = 0; i < pieces.Count; i++)
            {
                var pc = pieces[i];
                sb.AppendLine(string.Format("  {0,-22} {1,-6} {2,-14} {3,-8} {4,-4} {5}",
                    Round(pc.Size.x) + " x " + Round(pc.Size.y) + " x " + Round(pc.Size.z),
                    pc.ThinAxis, pc.PivotDescription, pc.ColliderKinds, pc.LodLevels,
                    Path.GetFileNameWithoutExtension(pc.Path)));
            }
            sb.AppendLine();

            sb.Append(MeasureDemoScenes(root));

            sb.AppendLine();
            sb.AppendLine("--- WAS DER ASSEMBLER TREFFEN MUSS ---");
            sb.AppendLine("  Der logische Raum ist " + PrimitiveRoomFactory.DefaultRoomSize.x + " x " +
                          PrimitiveRoomFactory.DefaultRoomSize.y + " x " +
                          PrimitiveRoomFactory.DefaultRoomSize.z + " m (PrimitiveRoomFactory.DefaultRoomSize),");
            sb.AppendLine("  die Tueroeffnung 1,2 x 2,2 m in der Wandmitte, der Tuer-Socket auf 1,1 m.");
            sb.AppendLine("  Passen die Kit-Masse ganzzahlig in 6 m Breite und 3 m Hoehe, kachelt der");
            sb.AppendLine("  Assembler sauber. Passen sie nicht, ist das die eine Stelle, an der die");
            sb.AppendLine("  logische Raumgroesse zur Diskussion steht - und die aendert den");
            sb.AppendLine("  Layout-Hash, also nicht nebenbei.");
            return sb.ToString();
        }

        /// <summary>
        /// Reads a scene's YAML and derives the spacing the author actually used.
        ///
        /// The scene file is parsed as text rather than opened. Opening it would disturb whatever
        /// the user has loaded, and the only thing needed here is where pieces were put: the
        /// distinct positions along each axis, and the gaps between them. A modular kit shows one
        /// gap far more often than any other, and that gap is the grid.
        /// </summary>
        private static string MeasureDemoScenes(string root)
        {
            var sb = new StringBuilder();
            sb.AppendLine("--- DEMO-SZENEN: WELCHES RASTER HAT DER AUTOR BENUTZT ---");

            var sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { root });
            if (sceneGuids.Length == 0)
            {
                sb.AppendLine("  Keine Szene im Paket. Das Raster muss aus den Massen oben folgen.");
                return sb.ToString();
            }

            for (int s = 0; s < sceneGuids.Length; s++)
            {
                string path = AssetDatabase.GUIDToAssetPath(sceneGuids[s]);
                sb.AppendLine();
                sb.AppendLine("  " + path);

                string text;
                try { text = File.ReadAllText(path); }
                catch (Exception e) { sb.AppendLine("    nicht lesbar: " + e.Message); continue; }

                var xs = new List<float>();
                var zs = new List<float>();

                foreach (Match m in Regex.Matches(text,
                             @"m_LocalPosition: \{x: (-?[\d.eE+-]+), y: (-?[\d.eE+-]+), z: (-?[\d.eE+-]+)\}"))
                {
                    if (float.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float x))
                        xs.Add(x);
                    if (float.TryParse(m.Groups[3].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
                        zs.Add(z);
                }

                foreach (Match m in Regex.Matches(text,
                             @"propertyPath: m_LocalPosition\.([xz])\s*\n\s*value: (-?[\d.eE+-]+)"))
                {
                    if (!float.TryParse(m.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
                        continue;

                    if (m.Groups[1].Value == "x") xs.Add(v); else zs.Add(v);
                }

                sb.AppendLine("    Positionen gefunden: " + xs.Count + " in X, " + zs.Count + " in Z");
                sb.AppendLine("    " + Spacing("X", xs));
                sb.AppendLine("    " + Spacing("Z", zs));
            }

            sb.AppendLine();
            sb.AppendLine("  Der haeufigste Abstand ist das Raster. Kommt derselbe Wert in X und Z");
            sb.AppendLine("  heraus, ist das Kit quadratisch gerastert - dann muss nur noch gelten,");
            sb.AppendLine("  dass 6 m ein ganzes Vielfaches davon sind.");
            return sb.ToString();
        }

        private static string Spacing(string axis, List<float> values)
        {
            if (values.Count < 2)
                return axis + ": zu wenige Werte";

            var distinct = new List<float>();
            values.Sort();
            for (int i = 0; i < values.Count; i++)
            {
                if (distinct.Count == 0 || Mathf.Abs(values[i] - distinct[distinct.Count - 1]) > 0.01f)
                    distinct.Add(values[i]);
            }

            if (distinct.Count < 2)
                return axis + ": alles auf einer Linie";

            var gaps = new SortedDictionary<string, int>();
            for (int i = 1; i < distinct.Count; i++)
            {
                float gap = distinct[i] - distinct[i - 1];
                if (gap < 0.05f || gap > 50f)
                    continue;

                string key = Round(gap);
                gaps.TryGetValue(key, out int n);
                gaps[key] = n + 1;
            }

            if (gaps.Count == 0)
                return axis + ": keine brauchbaren Abstaende";

            var ordered = new List<KeyValuePair<string, int>>(gaps);
            ordered.Sort((a, b) => b.Value.CompareTo(a.Value));

            var top = new List<string>();
            for (int i = 0; i < Mathf.Min(4, ordered.Count); i++)
                top.Add(ordered[i].Key + " m (" + ordered[i].Value + "x)");

            return axis + ": " + distinct.Count + " verschiedene Positionen, haeufigste Abstaende " +
                   string.Join(", ", top.ToArray());
        }

        private static string Round(float v) => (Mathf.Round(v * 20f) / 20f).ToString("0.00", CultureInfo.InvariantCulture);

        private static string DescribePivot(Vector3 min, Vector3 max)
        {
            return Axis("X", min.x, max.x) + Axis("Y", min.y, max.y) + Axis("Z", min.z, max.z);
        }

        private static string Axis(string name, float min, float max)
        {
            float size = max - min;
            if (size < 0.001f)
                return name + "0 ";

            if (Mathf.Abs(min) < size * 0.05f) return name + "min ";
            if (Mathf.Abs(max) < size * 0.05f) return name + "max ";
            if (Mathf.Abs(min + max) < size * 0.05f) return name + "mid ";
            return name + "? ";
        }

        private static string ThinnestAxis(Vector3 size)
        {
            if (size.x <= size.y && size.x <= size.z) return "X";
            if (size.z <= size.x && size.z <= size.y) return "Z";
            return "Y";
        }

        /// <summary>
        /// The bounds of a prefab in its OWN space: every mesh corner pushed through the
        /// renderer's world matrix and back into the prefab root. Renderer.bounds is a world
        /// AABB and would carry the ancestors' scale - the mistake that produced a 2 mm
        /// flashlight and hundredfold walls.
        ///
        /// <para>
        /// CAREFUL, and this is what made a floor lamp measure 36 x 57.55 x 36 metres: a prefab
        /// asset root has no parent, so its world matrix IS its local matrix, and multiplying by
        /// worldToLocalMatrix cancels the ROOT'S OWN localScale. What comes back is the size
        /// BEFORE that scale is applied. A vendor who imports a centimetre-authored FBX at scale
        /// factor 1 and compensates with a root scale of 0.01 therefore measures exactly 100x too
        /// large here. Callers must multiply by the root's localScale to get the size the object
        /// really has in a scene - see the effective size in the forensic report.
        /// </para>
        /// </summary>
        private static void MeasureLocal(GameObject prefab, out Vector3 min, out Vector3 max)
        {
            min = max = Vector3.zero;
            var filters = prefab.GetComponentsInChildren<MeshFilter>(true);
            var toRoot = prefab.transform.worldToLocalMatrix;
            bool any = false;

            for (int i = 0; i < filters.Length; i++)
            {
                var mesh = filters[i].sharedMesh;
                if (mesh == null)
                    continue;

                var m = toRoot * filters[i].transform.localToWorldMatrix;
                var b = mesh.bounds;

                for (int corner = 0; corner < 8; corner++)
                {
                    var p = m.MultiplyPoint3x4(new Vector3(
                        (corner & 1) == 0 ? b.min.x : b.max.x,
                        (corner & 2) == 0 ? b.min.y : b.max.y,
                        (corner & 4) == 0 ? b.min.z : b.max.z));

                    if (!any) { min = max = p; any = true; continue; }
                    min = Vector3.Min(min, p);
                    max = Vector3.Max(max, p);
                }
            }
        }

        // ------------------------------------------------------- Architektur-Forensik

        private class Child
        {
            public string HierarchyPath;
            public string Name;
            public string Model;
            public Vector3 LocalPosition;
            public Vector3 LocalEuler;
            public Vector3 LocalScale;
            public Vector3 MeshSize;        // the mesh's own bounds, untouched
            public Vector3 RootSize;        // the same mesh expressed in the prefab root's space
            public string Pivot;
            public string Materials;
            public string Collider;
            public bool HasRenderer;
        }

        private class Structure
        {
            public string Path;
            public Role Role;
            public Opening Opening;
            public Vector3 RootScale;
            public Vector3 OwnSize;         // before the root's own scale
            public Vector3 EffectiveSize;   // what it really is in a scene
            public int Depth;
            public readonly List<Child> Children = new List<Child>();
        }

        /// <summary>
        /// Architecture only, and measured to the child rather than to the root.
        ///
        /// A root-level bounding box says nothing useful about a prefab that contains a whole
        /// room: it reports the room, not the wall. So every mesh-carrying child is measured on
        /// its own, with its place in the hierarchy, and the report groups what repeats. A piece
        /// that appears many times at the same size, in several prefabs, is a module; a piece
        /// that appears once is set dressing wearing a module's dimensions.
        /// </summary>
        private static string MeasureArchitecture(string packRoot)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=========================================================");
            sb.AppendLine("ARCHITEKTUR-FORENSIK  -  ES WIRD NICHTS GESCHRIEBEN");
            sb.AppendLine("=========================================================");

            // Genau der Ordner, der uebergeben wurde. Kein Anhaengen von "/interior", kein
            // Zurueckfallen auf einen anderen Pfad: ein Bericht, der etwas anderes gelesen hat
            // als sein Kopf behauptet, ist schlimmer als gar keiner - er sieht richtig aus.
            string scope = packRoot.TrimEnd('/');

            if (!AssetDatabase.IsValidFolder(scope))
            {
                sb.AppendLine("Bereich: " + scope);
                sb.AppendLine();
                sb.AppendLine("ORDNER GIBT ES NICHT. Es wurde nichts gelesen und nichts ersetzt.");
                return sb.ToString();
            }

            sb.AppendLine("Bereich: " + scope + "   (genau dieser Pfad, rekursiv)");
            sb.AppendLine();

            var structures = new List<Structure>();
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { scope });

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null)
                    continue;

                var st = new Structure { Path = path, RootScale = go.transform.localScale };
                MeasureLocal(go, out Vector3 min, out Vector3 max);
                st.OwnSize = max - min;
                MeasureWorld(go, out Vector3 wMin, out Vector3 wMax);
                st.EffectiveSize = wMax - wMin;

                var filters = go.GetComponentsInChildren<MeshFilter>(true);

                for (int f = 0; f < filters.Length; f++)
                {
                    var mesh = filters[f].sharedMesh;
                    if (mesh == null)
                        continue;

                    var t = filters[f].transform;
                    var child = new Child
                    {
                        HierarchyPath = HierarchyPath(go.transform, t),
                        Name = t.name,
                        Model = Path.GetFileName(AssetDatabase.GetAssetPath(mesh)),
                        LocalPosition = t.localPosition,
                        LocalEuler = t.localEulerAngles,
                        LocalScale = t.localScale,
                        MeshSize = mesh.bounds.size,
                        HasRenderer = filters[f].GetComponent<Renderer>() != null,
                    };

                    // localToWorldMatrix DIREKT, ohne worldToLocalMatrix der Wurzel davor.
                    // Genau diese Multiplikation war der Fehler: fuer ein Mesh AUF der Wurzel
                    // ergibt sie die Einheitsmatrix und kuerzt damit Rotation UND Skalierung der
                    // Wurzel heraus. Die Wandmodule tragen eine 270-Grad-Drehung um X - die
                    // Z-hoch-zu-Y-hoch-Konvertierung aus dem Autorenwerkzeug -, und ohne sie
                    // erscheint jede Wand liegend: 0,40 m hoch und 4,10 m tief statt 4,10 m hoch
                    // und 0,40 m dick.
                    CornerBounds(t.localToWorldMatrix, mesh.bounds, out Vector3 cMin, out Vector3 cMax);
                    child.RootSize = cMax - cMin;
                    child.Pivot = DescribePivot(mesh.bounds.min, mesh.bounds.max);

                    var renderer = filters[f].GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        var names = new List<string>();
                        var mats = renderer.sharedMaterials;
                        for (int k = 0; k < mats.Length; k++)
                            names.Add(mats[k] == null ? "(null)" : mats[k].name);
                        child.Materials = string.Join(",", names.ToArray());
                    }
                    else
                    {
                        child.Materials = "-";
                    }

                    var col = filters[f].GetComponent<Collider>();
                    child.Collider = col == null ? "-" : col.GetType().Name;

                    st.Children.Add(child);
                    st.Depth = Mathf.Max(st.Depth, child.HierarchyPath.Split('/').Length);
                }

                st.Opening = FindOpening(go);
                st.Role = ClassifyPiece(path, st.EffectiveSize, st.Children.Count, st.Opening);
                structures.Add(st);
            }

            structures.Sort((a, b) => b.Children.Count.CompareTo(a.Children.Count));

            sb.AppendLine("Prefabs im Bereich: " + structures.Count);
            sb.AppendLine();

            var scenes = AssetDatabase.FindAssets("t:Scene", new[] { scope });
            sb.AppendLine("Szenen im Bereich: " + scenes.Length);
            for (int i = 0; i < scenes.Length; i++)
                sb.AppendLine("   " + AssetDatabase.GUIDToAssetPath(scenes[i]));
            sb.AppendLine();

            // ---- Semantische Rollen
            var byRole = new SortedDictionary<string, int>();
            for (int i = 0; i < structures.Count; i++)
                Bump(byRole, structures[i].Role.ToString());

            sb.AppendLine("--- SEMANTISCHE ROLLEN ---");
            sb.AppendLine("  Aus Form, Ordner und Inhalt, nicht aus dem Dateinamen allein.");
            sb.AppendLine("  Was hier NON_STRUCTURAL oder ROOM_ASSEMBLY heisst, darf das");
            sb.AppendLine("  Architekturraster NICHT mitbestimmen.");
            sb.AppendLine();
            foreach (var pair in byRole)
                sb.AppendLine(string.Format("  {0,-22} {1,4}", pair.Key, pair.Value));
            sb.AppendLine();

            // ================================================== 1. KANONISCHE WANDFAMILIE
            sb.AppendLine("--- 1. KANONISCHE WANDFAMILIE (nur interior/moduls/) ---");
            sb.AppendLine("  Der PIVOT-VERSATZ ist die entscheidende Zahl. Liegt der Nullpunkt am");
            sb.AppendLine("  linken Rand des Meshes, ist das Snap-Mass gleich der sichtbaren");
            sb.AppendLine("  Breite. Liegt er davor oder dahinter, ist das Snap-Mass groesser -");
            sb.AppendLine("  und DAS ist das Raster, nicht die Rendererbreite.");
            sb.AppendLine();
            sb.AppendLine(string.Format("  {0,-22} {1,-9} {2,-9} {3,-11} {4,-11} {5,-9} {6}",
                "EFFEKTIV BxHxT", "BREITE", "DICKE", "PIVOT->LINKS", "PIVOT->RECHTS",
                "SNAP?", "PREFAB"));

            var wallFamily = new List<Structure>();
            for (int i = 0; i < structures.Count; i++)
            {
                var st = structures[i];
                if (!IsModuleSource(st.Path))
                    continue;
                if (st.Role != Role.WALL && st.Role != Role.WALL_WITH_DOOR && st.Role != Role.WALL_WITH_WINDOW)
                    continue;

                wallFamily.Add(st);

                var go = AssetDatabase.LoadAssetAtPath<GameObject>(st.Path);
                if (go == null)
                    continue;

                MeasureWorld(go, out Vector3 wMin, out Vector3 wMax);
                Vector3 size = wMax - wMin;
                int uAxis = size.x >= size.z ? 0 : 2;
                float width = size[uAxis];
                float thickness = Mathf.Min(size.x, size.z);

                // Where the pivot (origin) sits relative to the mesh along the width axis.
                float toLeft = -wMin[uAxis];
                float toRight = wMax[uAxis];

                // If the pivot is at one edge the snap is the visible width; if it sits outside
                // the mesh the snap is larger, and the difference is the joint overlap.
                float snap = Mathf.Max(Mathf.Abs(toLeft), Mathf.Abs(toRight)) * 2f;
                string snapNote = Mathf.Abs(toLeft) < 0.1f || Mathf.Abs(toRight) < 0.1f
                    ? "Rand"
                    : (Mathf.Abs(toLeft + toRight) < 0.1f ? "mittig " + Round(snap) : "?");

                sb.AppendLine(string.Format("  {0,-22} {1,-9} {2,-9} {3,-11} {4,-11} {5,-9} {6}",
                    V(size), Round(width), Round(thickness), Round(toLeft), Round(toRight),
                    snapNote, Path.GetFileNameWithoutExtension(st.Path)));
            }

            if (wallFamily.Count == 0)
                sb.AppendLine("  KEINE. Unter interior/moduls/ wurde keine Wand erkannt.");
            sb.AppendLine();

            // ================================================ 2. TUER- UND FENSTERWAENDE
            sb.AppendLine("--- 2. TUER- UND FENSTERWAENDE, MIT ABLEHNUNGSGRUND ---");
            sb.AppendLine("  \"0 Tueren\" heisst nicht \"das Kit hat keine\". Hier steht bei JEDEM");
            sb.AppendLine("  Wandkandidaten, was das groesste leere Rechteck war und warum es");
            sb.AppendLine("  verworfen wurde. Ein Kind namens door ist das TUERBLATT und wird");
            sb.AppendLine("  getrennt gelistet - es sagt nichts ueber das Loch in der Wand.");
            sb.AppendLine();
            sb.AppendLine(string.Format("  {0,-14} {1,-16} {2,-9} {3,-9} {4}",
                "ERGEBNIS", "GROESSTE LUECKE", "UNTEN", "STURZ", "PREFAB / GRUND"));

            int doors = 0, windows = 0;
            for (int i = 0; i < structures.Count; i++)
            {
                var st = structures[i];
                if (TierOf(st.Path) != SourceTier.Architecture)
                    continue;
                if (!IsWallCandidate(st.EffectiveSize, out _, out _, out _))
                    continue;

                var op = st.Opening;
                if (op.Found)
                {
                    if (op.Kind == "FENSTER") windows++; else doors++;
                    sb.AppendLine(string.Format("  {0,-14} {1,-16} {2,-9} {3,-9} {4}",
                        op.Kind, Round(op.Width) + " x " + Round(op.Height),
                        Round(op.BottomV), Round(op.Lintel),
                        Path.GetFileNameWithoutExtension(st.Path)));
                }
                else
                {
                    sb.AppendLine(string.Format("  {0,-14} {1,-16} {2,-9} {3,-9} {4}",
                        "verworfen",
                        op.RawWidth > 0f ? Round(op.RawWidth) + " x " + Round(op.RawHeight) : "-",
                        op.RawWidth > 0f ? Round(op.RawBottom) : "-",
                        op.RawWidth > 0f ? Round(op.RawLintel) : "-",
                        Path.GetFileNameWithoutExtension(st.Path) + "   " + op.Reject));
                }
            }

            sb.AppendLine();
            sb.AppendLine("  Tueroeffnungen: " + doors + "   Fensteroeffnungen: " + windows);
            sb.AppendLine();
            sb.AppendLine("  TUERBLAETTER als eigene Kinder (nicht die Oeffnung!):");
            for (int i = 0; i < structures.Count; i++)
            {
                var st = structures[i];
                if (TierOf(st.Path) != SourceTier.Architecture)
                    continue;

                for (int c = 0; c < st.Children.Count; c++)
                {
                    string n = st.Children[c].Name.ToLowerInvariant();
                    if (!n.Contains("door") && !n.Contains("window") && !n.Contains("frame"))
                        continue;

                    sb.AppendLine("    " + V(st.Children[c].RootSize) + "   " +
                                  st.Children[c].Name + "   in " +
                                  Path.GetFileNameWithoutExtension(st.Path));
                }
            }
            sb.AppendLine();

            // =============================================== 3. BOEDEN UND DECKEN
            sb.AppendLine("--- 3. BODEN- UND DECKENQUELLEN ---");
            sb.AppendLine("  Getrennt nach Herkunft. Ein Teppich unter props/ ist kein Boden, und");
            sb.AppendLine("  eine Unity-Standardflaeche aus der Demo ist kein Modul.");
            sb.AppendLine();

            int planes = 0, realFloors = 0;
            for (int i = 0; i < structures.Count; i++)
            {
                var st = structures[i];
                var tier = TierOf(st.Path);
                if (tier == SourceTier.Prop)
                    continue;

                var go = AssetDatabase.LoadAssetAtPath<GameObject>(st.Path);
                if (go == null)
                    continue;

                var filters = go.GetComponentsInChildren<MeshFilter>(true);
                for (int f = 0; f < filters.Length; f++)
                {
                    var mesh = filters[f].sharedMesh;
                    if (mesh == null)
                        continue;

                    // Unity's built-in Plane: 10x10 units, 11x11 vertices, 200 triangles. That
                    // signature is exact and is the only safe way to tell the primitive apart
                    // from an authored floor slab that happens to be flat.
                    bool builtInPlane = mesh.name == "Plane" && mesh.vertexCount == 121;
                    if (builtInPlane)
                    {
                        planes++;
                        if (planes <= 6)
                        {
                            var scale = filters[f].transform.lossyScale;
                            sb.AppendLine("    UNITY-STANDARDFLAECHE  Skalierung " + V(scale) +
                                          "  -> " + Round(10f * scale.x) + " x " + Round(10f * scale.z) +
                                          " m   in " + Path.GetFileNameWithoutExtension(st.Path));
                        }
                    }
                }

                if (tier == SourceTier.Architecture && st.Role == Role.FLOOR_OR_CEILING)
                {
                    realFloors++;
                    sb.AppendLine("    ECHTES BAUTEIL         " + V(st.EffectiveSize) + " m   " +
                                  st.Path.Substring(scope.Length).TrimStart('/'));
                }
            }

            sb.AppendLine();
            sb.AppendLine("  Unity-Standardflaechen gefunden: " + planes +
                          (planes > 6 ? "   (nur die ersten 6 gelistet)" : ""));
            sb.AppendLine("  Echte Boden-/Deckenbauteile unter interior/: " + realFloors);
            if (realFloors == 0)
                sb.AppendLine("  -> KEIN Bodenmodul im Kit. CIYC muss Boden und Decke selbst erzeugen.");
            sb.AppendLine();

            // ================================================ 4. MATERIAL UND UV
            sb.AppendLine("--- 4. MATERIAL UND UV DER WANDFAMILIE ---");
            sb.AppendLine("  Entscheidet, ob eine CIYC-Fuellwand mit demselben Material ueberzeugt.");
            sb.AppendLine("  UV/METER nahe eins heisst: die Textur laeuft im Weltmass, ein");
            sb.AppendLine("  erzeugtes Stueck kachelt ohne Verzerrung mit. Stark abweichende");
            sb.AppendLine("  Werte heissen, die UV haengt am Modell und muss nachgerechnet werden.");
            sb.AppendLine();

            for (int i = 0; i < wallFamily.Count; i++)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(wallFamily[i].Path);
                if (go == null)
                    continue;

                sb.AppendLine("    " + Path.GetFileNameWithoutExtension(wallFamily[i].Path));

                var renderers = go.GetComponentsInChildren<Renderer>(true);
                for (int r = 0; r < renderers.Length && r < 4; r++)
                {
                    var mats = renderers[r].sharedMaterials;
                    for (int m = 0; m < mats.Length; m++)
                    {
                        var mat = mats[m];
                        if (mat == null)
                        {
                            sb.AppendLine("      (null-Material)");
                            continue;
                        }

                        sb.AppendLine("      Material " + mat.name +
                                      "   Shader " + (mat.shader != null ? mat.shader.name : "-"));
                        sb.AppendLine("        Tiling " + mat.mainTextureScale.ToString("0.00") +
                                      "   Offset " + mat.mainTextureOffset.ToString("0.00"));

                        var names = mat.GetTexturePropertyNames();
                        for (int n = 0; n < names.Length; n++)
                        {
                            var tex = mat.GetTexture(names[n]);
                            if (tex != null)
                                sb.AppendLine("        " + names[n] + " = " + tex.name);
                        }
                    }

                    var filter = renderers[r].GetComponent<MeshFilter>();
                    if (filter != null && filter.sharedMesh != null)
                    {
                        var uv = filter.sharedMesh.uv;
                        if (uv != null && uv.Length > 0)
                        {
                            float uMin = uv[0].x, uMax = uv[0].x, vMin = uv[0].y, vMax = uv[0].y;
                            for (int k = 1; k < uv.Length; k++)
                            {
                                uMin = Mathf.Min(uMin, uv[k].x); uMax = Mathf.Max(uMax, uv[k].x);
                                vMin = Mathf.Min(vMin, uv[k].y); vMax = Mathf.Max(vMax, uv[k].y);
                            }

                            var size = wallFamily[i].EffectiveSize;
                            float width = Mathf.Max(size.x, size.z);
                            sb.AppendLine("        UV-Spanne " + Round(uMax - uMin) + " x " + Round(vMax - vMin) +
                                          "   -> " + Round(width > 0.01f ? (uMax - uMin) / width : 0f) +
                                          " U/m,  " + Round(size.y > 0.01f ? (vMax - vMin) / size.y : 0f) + " V/m");
                        }
                    }
                }

                sb.AppendLine();
            }

            if (wallFamily.Count == 0)
                sb.AppendLine("    keine Wandfamilie erkannt - nichts zu berichten.");
            sb.AppendLine();

            // ========================================== 5. DEMO-BAUGRUPPEN, NUR STRUKTUR
            sb.AppendLine("--- 5. DEMO-BAUGRUPPEN: WIE WURDEN DIE MODULE BENUTZT ---");
            sb.AppendLine("  room1/2/3 sind Referenz, keine Module. Gelistet werden nur ihre");
            sb.AppendLine("  Kinder, deren Modell aus interior/ stammt - Moebel bleiben draussen.");
            sb.AppendLine();

            for (int i = 0; i < structures.Count; i++)
            {
                var st = structures[i];
                if (TierOf(st.Path) != SourceTier.DemoAssembly)
                    continue;

                sb.AppendLine("    === " + Path.GetFileNameWithoutExtension(st.Path) +
                              "   " + st.Children.Count + " Mesh-Kinder, effektiv " +
                              V(st.EffectiveSize) + " m");

                int shown = 0;
                for (int c = 0; c < st.Children.Count && shown < 25; c++)
                {
                    var ch = st.Children[c];
                    string model = ch.Model.ToLowerInvariant();
                    bool structural = model.Length > 0 && model != "-" &&
                                      !model.Contains("carpet") && !model.Contains("towel");

                    if (!structural)
                        continue;

                    sb.AppendLine("      " + V(ch.RootSize) + "   Pos " + V(ch.LocalPosition) +
                                  "   Rot " + V(ch.LocalEuler) + "   " + ch.Model + "   " + ch.Name);
                    shown++;
                }

                if (st.Children.Count > shown)
                    sb.AppendLine("      ... " + (st.Children.Count - shown) + " weitere nicht gelistet");
                sb.AppendLine();
            }

            sb.Append(StructuralSpacing(packRoot));
            return sb.ToString();
        }

        /// <summary>
        /// Spacing between repeated instances OF THE SAME prefab in the demo scene.
        ///
        /// A histogram over every transform in a scene measures furniture, trim and hand-placed
        /// decoration, and its most common gap is whatever the author nudged things by. Grouping
        /// by source prefab first removes all of that: the distance between two instances of the
        /// same wall is evidence about walls, and nothing else.
        /// </summary>
        private static string StructuralSpacing(string packRoot)
        {
            var sb = new StringBuilder();
            sb.AppendLine("--- DEMO-SZENE: ABSTAENDE JE MODUL, NICHT UEBER ALLES ---");

            var sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { packRoot });
            if (sceneGuids.Length == 0)
            {
                sb.AppendLine("  Keine Szene im Paket.");
                return sb.ToString();
            }

            for (int s = 0; s < sceneGuids.Length; s++)
            {
                string scenePath = AssetDatabase.GUIDToAssetPath(sceneGuids[s]);
                sb.AppendLine();
                sb.AppendLine("  " + scenePath);

                string text;
                try { text = File.ReadAllText(scenePath); }
                catch (Exception e) { sb.AppendLine("    nicht lesbar: " + e.Message); continue; }

                // Each PrefabInstance document carries its own position modifications and the
                // guid of the prefab it instantiates. Splitting on the document marker keeps
                // one instance's numbers from being read as another's.
                var byPrefab = new Dictionary<string, List<Vector2>>();
                string[] docs = text.Split(new[] { "--- !u!1001 " }, StringSplitOptions.None);

                for (int d = 1; d < docs.Length; d++)
                {
                    var srcMatch = Regex.Match(docs[d], @"m_SourcePrefab: \{fileID: \d+, guid: ([0-9a-f]{32})");
                    if (!srcMatch.Success)
                        continue;

                    float x = 0f, z = 0f;
                    bool hasX = false, hasZ = false;

                    foreach (Match m in Regex.Matches(docs[d],
                                 @"propertyPath: m_LocalPosition\.([xz])\s*\n\s*value: (-?[\d.eE+-]+)"))
                    {
                        if (!float.TryParse(m.Groups[2].Value, NumberStyles.Float,
                                            CultureInfo.InvariantCulture, out float v))
                            continue;

                        if (m.Groups[1].Value == "x") { x = v; hasX = true; }
                        else { z = v; hasZ = true; }
                    }

                    if (!hasX && !hasZ)
                        continue;

                    string guid = srcMatch.Groups[1].Value;
                    if (!byPrefab.TryGetValue(guid, out var list))
                    {
                        list = new List<Vector2>();
                        byPrefab[guid] = list;
                    }
                    list.Add(new Vector2(x, z));
                }

                // Ein entpacktes Objekt hat keine PrefabInstance. Es steht als GameObject mit
                // einem MeshFilter und einem Transform in der Szene, und die drei kennen sich
                // nur ueber fileIDs. Also wird der Graph gegangen: MeshFilter nennt sein
                // GameObject und sein Mesh, Transform nennt sein GameObject und seine Position -
                // ueber die GameObject-fileID zusammengefuehrt ergibt das Mesh plus Ort.
                var meshOfGameObject = new Dictionary<string, string>();
                foreach (Match m in Regex.Matches(text,
                             @"--- !u!33 &\d+\s*\nMeshFilter:(?:(?!--- !u!).)*?m_GameObject: \{fileID: (\d+)\}(?:(?!--- !u!).)*?m_Mesh: \{fileID: -?\d+, guid: ([0-9a-f]{32})",
                             RegexOptions.Singleline))
                {
                    meshOfGameObject[m.Groups[1].Value] = m.Groups[2].Value;
                }

                int unpacked = 0;
                foreach (Match m in Regex.Matches(text,
                             @"--- !u!4 &\d+\s*\nTransform:(?:(?!--- !u!).)*?m_GameObject: \{fileID: (\d+)\}(?:(?!--- !u!).)*?m_LocalPosition: \{x: (-?[\d.eE+-]+), y: (-?[\d.eE+-]+), z: (-?[\d.eE+-]+)\}",
                             RegexOptions.Singleline))
                {
                    if (!meshOfGameObject.TryGetValue(m.Groups[1].Value, out string meshGuid))
                        continue;

                    if (!float.TryParse(m.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float ux) ||
                        !float.TryParse(m.Groups[4].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float uz))
                        continue;

                    string key = "mesh:" + meshGuid;
                    if (!byPrefab.TryGetValue(key, out var ulist))
                    {
                        ulist = new List<Vector2>();
                        byPrefab[key] = ulist;
                    }
                    ulist.Add(new Vector2(ux, uz));
                    unpacked++;
                }

                sb.AppendLine("    Prefab-Instanzen mit Position: " +
                              (CountAll(byPrefab) - unpacked));
                sb.AppendLine("    entpackte Objekte, ueber ihr Mesh gruppiert: " + unpacked);
                sb.AppendLine("    Gruppen insgesamt: " + byPrefab.Count);
                sb.AppendLine();

                var ordered = new List<KeyValuePair<string, List<Vector2>>>(byPrefab);
                ordered.Sort((a, b) => b.Value.Count.CompareTo(a.Value.Count));

                sb.AppendLine(string.Format("    {0,-6} {1,-34} {2}", "ANZAHL", "PREFAB", "ABSTAENDE ZWISCHEN GLEICHEN INSTANZEN"));
                for (int i = 0; i < Mathf.Min(25, ordered.Count); i++)
                {
                    var entry = ordered[i];
                    if (entry.Value.Count < 2)
                        continue;

                    string guid = entry.Key.StartsWith("mesh:", StringComparison.Ordinal)
                        ? entry.Key.Substring(5) : entry.Key;
                    string name = Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid));
                    if (string.IsNullOrEmpty(name))
                        name = guid.Substring(0, 8);
                    if (entry.Key.StartsWith("mesh:", StringComparison.Ordinal))
                        name = "[mesh] " + name;

                    var xs = new List<float>();
                    var zs = new List<float>();
                    for (int v = 0; v < entry.Value.Count; v++)
                    {
                        xs.Add(entry.Value[v].x);
                        zs.Add(entry.Value[v].y);
                    }

                    sb.AppendLine(string.Format("    {0,-6} {1,-34} {2}",
                        entry.Value.Count, name, Spacing("X", xs)));
                    sb.AppendLine(string.Format("    {0,-6} {1,-34} {2}", "", "", Spacing("Z", zs)));
                }
            }

            sb.AppendLine();
            sb.AppendLine("  Nur die Abstaende innerhalb EINER Zeile sind Beweise. Taucht dieselbe");
            sb.AppendLine("  Wand bei 0, 3, 6, 9 auf, ist das Modul 3 m - unabhaengig davon, wie oft");
            sb.AppendLine("  irgendwo im Raum 0,05 m vorkommt.");
            return sb.ToString();
        }

        private static int CountAll(Dictionary<string, List<Vector2>> map)
        {
            int n = 0;
            foreach (var pair in map)
                n += pair.Value.Count;
            return n;
        }

        private static string HierarchyPath(Transform root, Transform t)
        {
            var parts = new List<string>();
            var cursor = t;
            while (cursor != null && cursor != root)
            {
                parts.Insert(0, cursor.name);
                cursor = cursor.parent;
            }

            return parts.Count == 0 ? "(root)" : string.Join("/", parts.ToArray());
        }

        private static void CornerBounds(Matrix4x4 m, Bounds b, out Vector3 min, out Vector3 max)
        {
            min = max = m.MultiplyPoint3x4(b.min);
            for (int corner = 1; corner < 8; corner++)
            {
                var p = m.MultiplyPoint3x4(new Vector3(
                    (corner & 1) == 0 ? b.min.x : b.max.x,
                    (corner & 2) == 0 ? b.min.y : b.max.y,
                    (corner & 4) == 0 ? b.min.z : b.max.z));
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
        }

        /// <summary>
        /// The prefab's bounds in ITS OWN world space - root rotation and scale included.
        ///
        /// This is the opposite of MeasureLocal and the two must not be confused. MeasureLocal
        /// answers "how big is this before I scale it", which is what the catalog builder needs
        /// when it is about to set a scale. This answers "how big is this when I drop it in a
        /// scene", which is the only thing a forensic audit is about.
        /// </summary>
        private static void MeasureWorld(GameObject prefab, out Vector3 min, out Vector3 max)
        {
            min = max = Vector3.zero;
            var filters = prefab.GetComponentsInChildren<MeshFilter>(true);
            bool any = false;

            for (int i = 0; i < filters.Length; i++)
            {
                var mesh = filters[i].sharedMesh;
                if (mesh == null)
                    continue;

                CornerBounds(filters[i].transform.localToWorldMatrix, mesh.bounds,
                    out Vector3 cMin, out Vector3 cMax);

                if (!any) { min = cMin; max = cMax; any = true; continue; }
                min = Vector3.Min(min, cMin);
                max = Vector3.Max(max, cMax);
            }
        }

        // ------------------------------------------------------- Oeffnungen, geometrisch

        private class Opening
        {
            public bool Found;
            public string Reject = "-";
            public float RawWidth;      // what the rectangle search found, before the tests
            public float RawHeight;
            public float RawBottom;
            public float RawLintel;
            public float Width;
            public float Height;
            public float CentreU;      // relative to the wall's left edge
            public float BottomV;      // relative to the wall's base
            public float LeftSolid;
            public float RightSolid;
            public float Lintel;       // solid above the opening
            public string Kind;        // TUER / FENSTER / (keine)
        }

        /// <summary>
        /// Where a piece comes from, which decides whether it may be architecture at all.
        ///
        /// <para>
        /// Shape is evidence about a shape, not about a role. A mirror 1.20 x 2.00 x 0.10 has a
        /// door's proportions; a carpet 7.85 x 0.05 x 7.70 has a floor's; a cupboard has a
        /// wall's. Classifying those three from their dimensions produced exactly those three
        /// wrong answers. So the folder decides first and the shape only refines within what the
        /// folder allows: nothing under props/ can become structure, whatever it measures.
        /// </para>
        /// </summary>
        private enum SourceTier
        {
            Architecture,   // interior/ - the authoritative structural source
            DemoAssembly,   // demo scenes/, room1-3 - reference for HOW pieces were used
            Prop,           // props/ - never structure
            Unknown,        // anything else - treated as a prop until proven otherwise
        }

        private static SourceTier TierOf(string assetPath)
        {
            string lower = assetPath.ToLowerInvariant();

            if (lower.Contains("/props/"))
                return SourceTier.Prop;

            if (lower.Contains("/demo scenes/") || lower.Contains("/demo/"))
                return SourceTier.DemoAssembly;

            // room1/2/3 are demo assemblies wherever they live.
            string file = Path.GetFileNameWithoutExtension(lower);
            if (file.StartsWith("room", StringComparison.Ordinal) && file.Length <= 6)
                return SourceTier.DemoAssembly;

            if (lower.Contains("/interior/"))
                return SourceTier.Architecture;

            return SourceTier.Unknown;
        }

        /// <summary>The authoritative structural folder, where the wall family lives.</summary>
        private static bool IsModuleSource(string assetPath) =>
            assetPath.ToLowerInvariant().Contains("/interior/moduls/");

        /// <summary>What a piece is, decided from its shape, its folder and its contents.</summary>
        private enum Role
        {
            FLOOR_OR_CEILING,
            WALL,
            WALL_WITH_DOOR,
            WALL_WITH_WINDOW,
            DOOR_LEAF,
            WINDOW_LEAF,
            CORNER_OR_COLUMN,
            TRIM,
            STAIRS_CANDIDATE,
            RAILING_CANDIDATE,
            ROOM_ASSEMBLY,
            HOUSE_SECTION,
            DECAL_OR_OVERLAY,
            NON_STRUCTURAL,
            EMPTY,
        }

        /// <summary>
        /// Whether this piece is credible as a WALL, which is the only thing an opening detector
        /// may run on.
        ///
        /// <para>
        /// The previous pass ran it on everything and duly reported a fireplace, a column and a
        /// set of wallpaper patches as doorways. Every one of those is a shape with a gap in it;
        /// none of them is a wall. A wall is thin, tall, wide, and thin by a wide margin - a
        /// fireplace 3.25 wide and 1.65 deep has a width-to-thickness ratio of 2, a wall has 10
        /// or more.
        /// </para>
        /// </summary>
        private static bool IsWallCandidate(Vector3 size, out float width, out float height, out float thickness)
        {
            // Thickness is the thinnest axis; height is Y; width is whichever horizontal axis is left.
            thickness = Mathf.Min(size.x, size.z);
            width = Mathf.Max(size.x, size.z);
            height = size.y;

            if (size.y <= Mathf.Min(size.x, size.z))
                return false;                                  // lying down - a floor, not a wall

            if (thickness < 0.02f || thickness > 0.80f) return false;
            if (height < 2.0f || height > 5.5f) return false;
            if (width < 1.0f) return false;
            if (width / Mathf.Max(0.01f, thickness) < 4f) return false;
            if (height / Mathf.Max(0.01f, thickness) < 4f) return false;

            return true;
        }

        private static void Bump<T>(SortedDictionary<T, int> map, T key)
        {
            map.TryGetValue(key, out int n);
            map[key] = n + 1;
        }

        private static bool ContainsAny(string haystack, string[] needles)
        {
            for (int i = 0; i < needles.Length; i++)
            {
                if (haystack.Contains(needles[i]))
                    return true;
            }

            return false;
        }

        private static Role ClassifyPiece(string path, Vector3 size, int meshChildren, Opening opening)
        {
            if (meshChildren == 0)
                return Role.EMPTY;

            var tier = TierOf(path);

            // The rule that fixes the mirror, the carpet and the cupboard. A prop is a prop
            // whatever it measures, and nothing but an explicit structural reference could
            // change that - which no prop in this pack has.
            if (tier == SourceTier.Prop || tier == SourceTier.Unknown)
                return Role.NON_STRUCTURAL;

            // A demo assembly is a reference for HOW pieces were used, never a module itself.
            if (tier == SourceTier.DemoAssembly)
                return meshChildren >= 25 ? Role.HOUSE_SECTION : Role.ROOM_ASSEMBLY;

            string lower = path.ToLowerInvariant();
            float minAxis = Mathf.Min(size.x, Mathf.Min(size.y, size.z));

            // A big multi-mesh thing is an assembly whatever its bounds look like.
            if (meshChildren >= 25 || (size.x > 12f && size.z > 12f))
                return Role.HOUSE_SECTION;
            if (meshChildren >= 8)
                return Role.ROOM_ASSEMBLY;

            // Zero thickness is a plane: wallpaper, a poster, a painted-on detail.
            if (minAxis < 0.02f)
                return Role.DECAL_OR_OVERLAY;

            if (ContainsAny(lower, new[] { "stair", "steps", "staircase" }) && size.y > 0.8f)
                return Role.STAIRS_CANDIDATE;
            if (ContainsAny(lower, new[] { "railing", "banister", "handrail", "balustrade" }))
                return Role.RAILING_CANDIDATE;

            // Flat and wide: a floor or a ceiling. Which of the two cannot be told from the mesh -
            // the same slab serves both - so it is reported as the pair it is.
            if (size.y < 0.45f && size.x > 1.5f && size.z > 1.5f)
                return Role.FLOOR_OR_CEILING;

            // Low and long against a wall: skirting, cornice, picture rail.
            if (size.y < 0.5f && Mathf.Max(size.x, size.z) > 1.5f && minAxis < 0.3f)
                return Role.TRIM;

            if (IsWallCandidate(size, out _, out _, out _))
            {
                if (opening != null && opening.Found)
                    return opening.Kind == "FENSTER" ? Role.WALL_WITH_WINDOW : Role.WALL_WITH_DOOR;
                return Role.WALL;
            }

            // Tall and square-ish in plan: a column, a pillar, an inside corner.
            if (size.y > 2f && Mathf.Max(size.x, size.z) < 2.5f)
                return Role.CORNER_OR_COLUMN;

            // A door leaf: person-sized, thin, and small overall.
            if (size.y > 1.7f && size.y < 3.2f && Mathf.Max(size.x, size.z) < 1.8f && minAxis < 0.35f)
                return Role.DOOR_LEAF;

            return Role.NON_STRUCTURAL;
        }

        /// <summary>
        /// Finds the empty rectangle in a wall by looking at the geometry, not at a child's name.
        ///
        /// <para>
        /// A child called "door" is a door LEAF - the panel that swings. It says nothing about
        /// the hole in the wall, and on this pack it measures 1.35 x 2.60 while the wall's actual
        /// opening could be anything. So: every triangle of the wall is projected onto the wall
        /// plane, rasterised into an occupancy grid, and the largest all-empty axis-aligned
        /// rectangle in that grid is the opening. That is a real measurement of real geometry.
        /// </para>
        ///
        /// <para>
        /// A gap sitting on the floor is a door; one with solid geometry beneath it is a window,
        /// and the solid beneath is the sill height. Below the thresholds it is a modelling gap,
        /// not an opening, and is reported as none.
        /// </para>
        /// </summary>
        private static Opening FindOpening(GameObject prefab, int resolution = 128)
        {
            var result = new Opening { Kind = "(keine)" };

            // A prop is never a wall, so its geometry is never searched for a doorway.
            if (TierOf(AssetDatabase.GetAssetPath(prefab)) != SourceTier.Architecture)
            {
                result.Reject = "keine Architekturquelle";
                return result;
            }

            MeasureWorld(prefab, out Vector3 min, out Vector3 max);
            Vector3 size = max - min;

            // The gate. Without it this finds the gap between two wallpaper patches, the recess
            // in a fireplace and the space beside a column, and calls all three a doorway.
            if (!IsWallCandidate(size, out _, out _, out _))
            {
                result.Reject = "kein Wandkandidat (Dicke/Hoehe/Breite/Verhaeltnis)";
                return result;
            }

            int thin = size.x <= size.z ? 0 : 2;
            int uAxis = thin == 0 ? 2 : 0;
            int vAxis = 1;

            float uSize = size[uAxis];
            float vSize = size[vAxis];

            int cols = resolution;
            int rows = Mathf.Max(8, Mathf.RoundToInt(resolution * vSize / uSize));
            var solid = new bool[cols * rows];

            var filters = prefab.GetComponentsInChildren<MeshFilter>(true);
            for (int f = 0; f < filters.Length; f++)
            {
                var mesh = filters[f].sharedMesh;
                if (mesh == null)
                    continue;

                // What FILLS an opening is not what CLOSES it. A glazed window has no empty
                // rectangle at all - the glass occupies it - and a wall whose door leaf is
                // modelled in place has none either. Both were reported as "no opening", which
                // is the opposite of the truth: those are precisely the walls that have one.
                // So glass and leaves are left out of the occupancy grid and the hole appears.
                var renderer = filters[f].GetComponent<Renderer>();
                var mats = renderer != null ? renderer.sharedMaterials : null;
                string childName = filters[f].name.ToLowerInvariant();
                bool isLeaf = childName.Contains("door") || childName.Contains("okno") ||
                              childName.Contains("window");

                var m = filters[f].transform.localToWorldMatrix;
                var verts = mesh.vertices;
                if (verts == null)
                    continue;

                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    var mat = mats != null && sub < mats.Length ? mats[sub] : null;
                    if (isLeaf || IsFillMaterial(mat))
                        continue;

                    var tris = mesh.GetTriangles(sub);
                    if (tris == null)
                        continue;

                    for (int t = 0; t + 2 < tris.Length; t += 3)
                    {
                        var a = m.MultiplyPoint3x4(verts[tris[t]]);
                        var b = m.MultiplyPoint3x4(verts[tris[t + 1]]);
                        var c = m.MultiplyPoint3x4(verts[tris[t + 2]]);

                        var p0 = new Vector2((a[uAxis] - min[uAxis]) / uSize, (a[vAxis] - min[vAxis]) / vSize);
                        var p1 = new Vector2((b[uAxis] - min[uAxis]) / uSize, (b[vAxis] - min[vAxis]) / vSize);
                        var p2 = new Vector2((c[uAxis] - min[uAxis]) / uSize, (c[vAxis] - min[vAxis]) / vSize);

                        RasteriseTriangle(solid, cols, rows, p0, p1, p2);
                    }
                }
            }

            if (!LargestEmptyRectangle(solid, cols, rows, out int rx, out int ry, out int rw, out int rh))
                return result;

            float w = rw * uSize / cols;
            float h = rh * vSize / rows;
            float left = rx * uSize / cols;
            float bottom = ry * vSize / rows;

            float rightSolid = uSize - left - w;
            float lintel = vSize - bottom - h;

            // Kept whatever happens: "0 doors" must be distinguishable from "no gap anywhere".
            result.RawWidth = w;
            result.RawHeight = h;
            result.RawBottom = bottom;
            result.RawLintel = lintel;

            // An opening is ENCLOSED. Wall to its left, wall to its right, and - the test that
            // rejects the wallpaper patches - wall above it. A gap running the full height of a
            // piece is the space between two pieces, not a hole in one.
            if (left < 0.15f || rightSolid < 0.15f || lintel < 0.15f)
            {
                result.Reject = "nicht eingefasst (links " + Round(left) + " rechts " + Round(rightSolid) + " Sturz " + Round(lintel) + ")";
                return result;
            }

            // And it is a hole in a wall, not most of the wall. Past this the piece is two
            // fragments that happen to share a prefab.
            if (w * h > uSize * vSize * 0.6f)
            {
                result.Reject = "Loch waere ueber 60% der Wandflaeche";
                return result;
            }

            bool door = bottom < 0.20f;
            if (door)
            {
                // A doorway and an archway are the same thing at different sizes, and this pack
                // has both: prefab 2 measures 2.95 x 3.05 and its material is called "arch big",
                // prefab 3 measures 1.55 x 3.25 and its is "arch small". Rejecting those as
                // implausible doors threw away the only opening walls in the kit.
                if (w < 0.6f || w > 3.6f || h < 1.6f || h > 3.8f)
                {
                    result.Reject = "Durchgangsmass unplausibel " + Round(w) + " x " + Round(h);
                    return result;
                }
            }
            else
            {
                if (bottom < 0.30f)
                {
                    result.Reject = "weder am Boden noch mit Bruestung (" + Round(bottom) + ")";
                    return result;
                }

                if (w < 0.4f || w > 3.0f || h < 0.4f || h > 2.5f)
                {
                    result.Reject = "Fenstermass unplausibel " + Round(w) + " x " + Round(h);
                    return result;
                }
            }

            result.Found = true;
            result.Width = w;
            result.Height = h;
            result.LeftSolid = left;
            result.RightSolid = rightSolid;
            result.BottomV = bottom;
            result.CentreU = left + w * 0.5f;
            result.Lintel = lintel;
            result.Kind = door ? (w > 2.0f || h > 2.8f ? "BOGEN" : "TUER") : "FENSTER";
            return result;
        }

        /// <summary>
        /// Whether this material FILLS an opening rather than closing it: glass, and anything
        /// else the pack renders transparent. Judged from the material, not from its name -
        /// "Steklo" is only glass if you happen to read Russian.
        /// </summary>
        private static bool IsFillMaterial(Material mat)
        {
            if (mat == null)
                return false;

            if (mat.renderQueue >= 2450)
                return true;
            if (mat.HasProperty("_Surface") && mat.GetFloat("_Surface") > 0.5f)
                return true;
            if (mat.HasProperty("_AlphaClip") && mat.GetFloat("_AlphaClip") > 0.5f)
                return true;

            return false;
        }

        private static void RasteriseTriangle(bool[] grid, int cols, int rows,
            Vector2 a, Vector2 b, Vector2 c)
        {
            float minX = Mathf.Min(a.x, Mathf.Min(b.x, c.x)) * cols;
            float maxX = Mathf.Max(a.x, Mathf.Max(b.x, c.x)) * cols;
            float minY = Mathf.Min(a.y, Mathf.Min(b.y, c.y)) * rows;
            float maxY = Mathf.Max(a.y, Mathf.Max(b.y, c.y)) * rows;

            int x0 = Mathf.Clamp(Mathf.FloorToInt(minX), 0, cols - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt(maxX), 0, cols - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(minY), 0, rows - 1);
            int y1 = Mathf.Clamp(Mathf.CeilToInt(maxY), 0, rows - 1);

            var pa = new Vector2(a.x * cols, a.y * rows);
            var pb = new Vector2(b.x * cols, b.y * rows);
            var pc = new Vector2(c.x * cols, c.y * rows);

            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    if (InTriangle(p, pa, pb, pc))
                        grid[y * cols + x] = true;
                }
            }
        }

        private static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Sign(p, a, b);
            float d2 = Sign(p, b, c);
            float d3 = Sign(p, c, a);
            bool neg = d1 < 0f || d2 < 0f || d3 < 0f;
            bool pos = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(neg && pos);
        }

        private static float Sign(Vector2 p, Vector2 a, Vector2 b) =>
            (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);

        /// <summary>
        /// The largest all-empty axis-aligned rectangle in a binary grid, by the standard
        /// per-row histogram scan: O(cols x rows), which is what makes running this over a
        /// whole pack practical.
        /// </summary>
        private static bool LargestEmptyRectangle(bool[] solid, int cols, int rows,
            out int bestX, out int bestY, out int bestW, out int bestH)
        {
            bestX = bestY = bestW = bestH = 0;
            int bestArea = 0;

            var heights = new int[cols];
            var stack = new int[cols + 1];

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                    heights[x] = solid[y * cols + x] ? 0 : heights[x] + 1;

                int top = 0;
                for (int x = 0; x <= cols; x++)
                {
                    int h = x == cols ? 0 : heights[x];
                    int start = x;

                    while (top > 0 && heights[stack[top - 1]] >= h)
                    {
                        int idx = stack[--top];
                        int height = heights[idx];
                        int width = x - idx;
                        int area = height * width;

                        if (area > bestArea)
                        {
                            bestArea = area;
                            bestX = idx;
                            bestY = y - height + 1;
                            bestW = width;
                            bestH = height;
                        }

                        start = idx;
                    }

                    if (x < cols)
                        stack[top++] = start;
                }
            }

            return bestArea > 0;
        }

        private static string V(Vector3 v) =>
            Round(v.x) + " x " + Round(v.y) + " x " + Round(v.z);

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
