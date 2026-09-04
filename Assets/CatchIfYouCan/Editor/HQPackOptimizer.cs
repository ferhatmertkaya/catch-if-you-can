using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CatchIfYouCan.EditorTools
{
    /// <summary>
    /// Audits, and only on a separate explicit action optimises, an imported Asset Store pack.
    ///
    /// <para>
    /// Nothing here is destructive. Every change it can make is a TextureImporter or
    /// ModelImporter setting - the source PNG, TGA and FBX files are never rewritten, never
    /// resized and never deleted. That is deliberate: a purchased pack must stay reinstallable,
    /// and an importer setting is reproducible from this tool while a resized PNG is not.
    /// </para>
    ///
    /// <para>
    /// The two operations are separated because they have opposite risk. AUDIT reads and writes
    /// nothing, so it is safe to run at any time. APPLY changes import settings for hundreds of
    /// assets and triggers a long reimport, so it asks first and refuses to touch a single path
    /// outside the pack root. The project's own materials and shaders are not in scope and are
    /// not reachable from here.
    /// </para>
    /// </summary>
    public class HQPackOptimizer : EditorWindow
    {
        private const string DefaultRoot = "Assets/HQ Modular House";
        private const string ReportFile = "HQ_Pack_Audit.txt";

        private string _root = DefaultRoot;
        private Audit _audit;
        private string _report = "AUDIT druecken. Es wird dabei nichts geaendert.";
        private Vector2 _scroll;

        [MenuItem("Catch If You Can/HQ Pack Optimizer")]
        public static void Open()
        {
            var w = GetWindow<HQPackOptimizer>(true, "HQ Pack Optimizer");
            w.minSize = new Vector2(760f, 600f);
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "AUDIT liest nur. Es aendert keine einzige Datei und keine Import-Einstellung.\n\n" +
                "SAFE OPTIMIERUNG aendert ausschliesslich Import-Einstellungen unterhalb des " +
                "Paket-Ordners. Quelldateien werden nie veraendert, nie verkleinert, nie " +
                "geloescht. Alpha-Kanaele werden nie angefasst.",
                MessageType.Info);

            _root = EditorGUILayout.TextField("Paket-Ordner", _root);
            EditorGUILayout.Space();

            if (GUILayout.Button("1.  AUDIT  (aendert nichts)", GUILayout.Height(30f)))
            {
                _audit = RunAudit(_root);
                _report = Describe(_audit);
                WriteReport(_report);
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("2.  SAFE OPTIMIERUNG ANWENDEN  (aendert Import-Einstellungen)",
                                 GUILayout.Height(30f)))
            {
                if (_audit == null || _audit.Textures.Count == 0)
                {
                    _report = "Erst AUDIT. Ohne Bestandsaufnahme wird nichts geaendert.";
                }
                else if (EditorUtility.DisplayDialog(
                             "Import-Einstellungen aendern?",
                             "Es werden " + _audit.Textures.Count + " Texturen und " +
                             _audit.Models.Count + " Modelle unterhalb von\n\n" + _audit.Root +
                             "\n\nneu importiert. Quelldateien bleiben unveraendert.\n\n" +
                             "Das dauert lange und laesst sich nur durch erneutes Importieren " +
                             "des Pakets vollstaendig zuruecknehmen.",
                             "Aendern", "Abbrechen"))
                {
                    _report = ApplySafeOptimization(_audit);
                    WriteReport(_report);
                }
            }

            EditorGUILayout.Space();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.TextArea(_report);
            EditorGUILayout.EndScrollView();
        }

        private static void WriteReport(string text)
        {
            try
            {
                // Written beside the project, not into Assets - a text file under Assets would
                // be imported, which is exactly the churn this tool exists to reduce.
                File.WriteAllText(ReportFile, text);
                Debug.Log("[CIYC][HQPack] Bericht geschrieben: " + Path.GetFullPath(ReportFile));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[CIYC][HQPack] Bericht konnte nicht geschrieben werden: " + e.Message);
            }
        }

        // ==================================================================== Bestandsaufnahme

        /// <summary>What a texture is for, which decides how large it is allowed to be.</summary>
        private enum Tier
        {
            Architecture,   // walls, floors, ceilings, doors, stairs - the player stands next to these
            LargeFurniture, // beds, sofas, wardrobes, kitchen units
            Prop,           // ordinary objects
            SmallDetail,    // handles, switches, cutlery
        }

        private class TexInfo
        {
            public string Path;
            public int Width;
            public int Height;
            public long FileBytes;
            public bool Mipmaps;
            public bool Readable;
            public bool IsNormalMap;
            public bool HasAlpha;
            public bool AlphaCritical;
            public string Kind;      // Albedo / Normal / Specular / Smoothness / Transparency / ?
            public Tier Tier;
            public int CurrentMax;
            public int ProposedMax;
        }

        private class MeshInfo
        {
            public string Path;
            public int Triangles;
            public int Vertices;
            public bool Readable;
            public string Compression;
            public bool HasLodGroup;
        }

        private class Audit
        {
            public string Root;
            public readonly List<TexInfo> Textures = new List<TexInfo>();
            public readonly List<MeshInfo> Models = new List<MeshInfo>();
            public readonly List<string> LegacyShaderMaterials = new List<string>();
            public readonly List<string> DuplicateCandidates = new List<string>();
            public int Materials;
            public int Prefabs;
            public int Scenes;
        }

        // Filename fragments, longest and most specific first. These are ordinary English words,
        // not one pack's asset list, so the same tool works on the next pack.
        private static readonly (Tier Tier, string[] Words)[] TierWords =
        {
            (Tier.Architecture, new[] { "wall", "floor", "ceiling", "roof", "stair", "door",
                                        "wallpaper", "plaster", "brick", "concrete", "parquet",
                                        "tile", "baseboard", "skirting", "column", "beam" }),
            (Tier.LargeFurniture, new[] { "bed", "sofa", "couch", "wardrobe", "closet", "cabinet",
                                          "kitchen", "fridge", "refrigerator", "bath", "shower",
                                          "table", "desk", "bookshelf", "shelf", "piano",
                                          "fireplace", "stove", "oven", "sink", "toilet" }),
            (Tier.SmallDetail, new[] { "handle", "knob", "switch", "socket", "screw", "nail",
                                       "cutlery", "spoon", "fork", "knife", "key", "button",
                                       "hinge", "plug", "coin", "pen", "cup", "glass_small" }),
        };

        // Alpha actually carries meaning in these. Their alpha channel is never touched and they
        // never drop below 1024 - a cutout leaf or a curtain at 512 turns into visible fringing.
        private static readonly string[] AlphaCriticalWords =
        {
            "transparen", "alpha", "glass", "window", "curtain", "foliage", "leaf", "leaves",
            "plant", "cutout", "opacity", "decal", "grid", "fence", "lace", "net",
        };

        private static Audit RunAudit(string root)
        {
            var audit = new Audit { Root = root };

            if (string.IsNullOrEmpty(root) || !AssetDatabase.IsValidFolder(root))
                return audit;

            string[] search = { root };

            var texGuids = AssetDatabase.FindAssets("t:Texture2D", search);
            var bySignature = new Dictionary<string, List<string>>();

            for (int i = 0; i < texGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(texGuids[i]);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                    continue;

                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex == null)
                    continue;

                string lower = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                string lowerPath = path.ToLowerInvariant();

                var info = new TexInfo
                {
                    Path = path,
                    Width = tex.width,
                    Height = tex.height,
                    FileBytes = FileSize(path),
                    Mipmaps = importer.mipmapEnabled,
                    Readable = importer.isReadable,
                    IsNormalMap = importer.textureType == TextureImporterType.NormalMap
                                  || lower.Contains("normal") || lower.EndsWith("_nrm")
                                  || lower.EndsWith("_n"),
                    HasAlpha = importer.DoesSourceTextureHaveAlpha(),
                    CurrentMax = importer.maxTextureSize,
                };

                info.AlphaCritical = info.HasAlpha && ContainsAny(lowerPath, AlphaCriticalWords);
                info.Kind = KindOf(lower, info.IsNormalMap, info.HasAlpha);
                info.Tier = TierOf(lowerPath);
                info.ProposedMax = ProposeMax(info);

                audit.Textures.Add(info);

                // Two files of the same pixel size AND the same byte length are worth a look.
                // Nothing is deleted on this basis - it is reported, because deciding that two
                // textures are the same is a judgement about art, not about bytes.
                string signature = info.Width + "x" + info.Height + "@" + info.FileBytes;
                if (!bySignature.TryGetValue(signature, out var group))
                {
                    group = new List<string>();
                    bySignature[signature] = group;
                }
                group.Add(path);
            }

            foreach (var pair in bySignature)
            {
                if (pair.Value.Count > 1)
                    audit.DuplicateCandidates.Add(pair.Key + "  ->  " + string.Join(" | ", pair.Value.ToArray()));
            }

            var modelGuids = AssetDatabase.FindAssets("t:Model", search);
            for (int i = 0; i < modelGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(modelGuids[i]);
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null)
                    continue;

                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                int tris = 0, verts = 0;
                bool lod = false;

                if (go != null)
                {
                    lod = go.GetComponentsInChildren<LODGroup>(true).Length > 0;
                    var filters = go.GetComponentsInChildren<MeshFilter>(true);
                    for (int f = 0; f < filters.Length; f++)
                    {
                        var mesh = filters[f].sharedMesh;
                        if (mesh == null)
                            continue;

                        verts += mesh.vertexCount;
                        var triangles = mesh.triangles;
                        if (triangles != null)
                            tris += triangles.Length / 3;
                    }
                }

                audit.Models.Add(new MeshInfo
                {
                    Path = path,
                    Triangles = tris,
                    Vertices = verts,
                    Readable = importer.isReadable,
                    Compression = importer.meshCompression.ToString(),
                    HasLodGroup = lod,
                });
            }

            var matGuids = AssetDatabase.FindAssets("t:Material", search);
            audit.Materials = matGuids.Length;
            for (int i = 0; i < matGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(matGuids[i]);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null || mat.shader == null)
                    continue;

                string shader = mat.shader.name;
                if (!shader.StartsWith("Universal Render Pipeline") &&
                    !shader.StartsWith("Shader Graphs") &&
                    !shader.StartsWith("Skybox"))
                {
                    audit.LegacyShaderMaterials.Add(shader + "   <-   " + path);
                }
            }

            audit.Prefabs = AssetDatabase.FindAssets("t:Prefab", search).Length;
            audit.Scenes = AssetDatabase.FindAssets("t:Scene", search).Length;

            audit.Textures.Sort((a, b) => EstimateBytes(b.Width, b.Height, b.Mipmaps)
                                          .CompareTo(EstimateBytes(a.Width, a.Height, a.Mipmaps)));
            audit.Models.Sort((a, b) => b.Triangles.CompareTo(a.Triangles));
            return audit;
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

        private static string KindOf(string lower, bool isNormal, bool hasAlpha)
        {
            if (isNormal) return "Normal";
            if (lower.Contains("specular") || lower.Contains("_spec")) return "Specular";
            if (lower.Contains("smooth") || lower.Contains("gloss") || lower.Contains("rough")) return "Smoothness";
            if (lower.Contains("metal")) return "Metallic";
            if (lower.Contains("occlusion") || lower.Contains("_ao")) return "Occlusion";
            if (lower.Contains("emis")) return "Emission";
            if (lower.Contains("transparen") || lower.Contains("opacity")) return "Transparency";
            if (lower.Contains("albedo") || lower.Contains("basecolor") || lower.Contains("diffuse")
                || lower.Contains("_col") || lower.Contains("_d")) return hasAlpha ? "Albedo+Alpha" : "Albedo";
            return "?";
        }

        private static Tier TierOf(string lowerPath)
        {
            for (int i = 0; i < TierWords.Length; i++)
            {
                if (ContainsAny(lowerPath, TierWords[i].Words))
                    return TierWords[i].Tier;
            }

            return Tier.Prop;
        }

        /// <summary>
        /// The cap this texture should get. Never blanket 512: an architectural surface fills the
        /// screen when the player stands at a wall, and dropping it to 512 is the difference
        /// between a house and a blur. The reduction comes from the 8K and 4K source art, which
        /// no interior surface needs.
        /// </summary>
        private static int ProposeMax(TexInfo info)
        {
            int cap;
            switch (info.Tier)
            {
                case Tier.Architecture:   cap = 2048; break;
                case Tier.LargeFurniture: cap = 2048; break;
                case Tier.SmallDetail:    cap = 512;  break;
                default:                  cap = 1024; break;
            }

            // A normal map carries direction, not detail; one step below its albedo is invisible
            // in motion and halves the memory. It never goes below 512.
            if (info.IsNormalMap)
                cap = Mathf.Max(512, cap / 2);

            // Alpha that means something keeps resolution: fringing on a cutout is obvious.
            if (info.AlphaCritical)
                cap = Mathf.Max(cap, 1024);

            // Never propose an increase. The source may already be smaller than the cap.
            int source = Mathf.Max(info.Width, info.Height);
            return Mathf.Min(cap, Mathf.Max(source, 32));
        }

        /// <summary>
        /// Estimated VRAM at RGBA32, which is the honest worst case and the one that makes the
        /// editor unresponsive: an 8192x8192 albedo is 256 MB uncompressed, 341 MB with mips.
        /// Compressed on disk is a different number and is not what the import is holding.
        /// </summary>
        private static long EstimateBytes(int width, int height, bool mipmaps)
        {
            long b = (long)width * height * 4L;
            return mipmaps ? b * 4L / 3L : b;
        }

        private static long FileSize(string assetPath)
        {
            try { return new FileInfo(assetPath).Length; }
            catch { return 0L; }
        }

        private static string Mb(long bytes) => (bytes / 1048576.0).ToString("0.0") + " MB";

        // =========================================================================== Bericht

        private static string Describe(Audit a)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=========================================================");
            sb.AppendLine("HQ PACK AUDIT  -  ES WURDE NICHTS GEAENDERT");
            sb.AppendLine("=========================================================");
            sb.AppendLine("Ordner: " + a.Root);
            sb.AppendLine();

            if (a.Textures.Count == 0 && a.Models.Count == 0)
            {
                sb.AppendLine("Dort liegt nichts. Pfad pruefen, oder das Paket ist nicht importiert.");
                return sb.ToString();
            }

            sb.AppendLine("Texturen : " + a.Textures.Count);
            sb.AppendLine("Modelle  : " + a.Models.Count);
            sb.AppendLine("Materialien: " + a.Materials);
            sb.AppendLine("Prefabs  : " + a.Prefabs);
            sb.AppendLine("Szenen   : " + a.Scenes);
            sb.AppendLine();

            // ---- Aufloesungen
            var buckets = new SortedDictionary<int, int>();
            long current = 0, proposed = 0;
            int readable = 0, noMips = 0;

            for (int i = 0; i < a.Textures.Count; i++)
            {
                var t = a.Textures[i];
                int side = Mathf.Max(t.Width, t.Height);
                buckets.TryGetValue(side, out int n);
                buckets[side] = n + 1;

                int effectiveNow = Mathf.Min(side, t.CurrentMax);
                current += EstimateBytes(effectiveNow, effectiveNow, t.Mipmaps);
                proposed += EstimateBytes(t.ProposedMax, t.ProposedMax, true);

                if (t.Readable) readable++;
                if (!t.Mipmaps) noMips++;
            }

            sb.AppendLine("--- AUFLOESUNGSVERTEILUNG (laengste Kante) ---");
            foreach (var pair in buckets)
                sb.AppendLine(string.Format("  {0,6} px : {1,4}", pair.Key, pair.Value));
            sb.AppendLine();

            sb.AppendLine("--- GESCHAETZTER TEXTURSPEICHER (RGBA32, inkl. Mips) ---");
            sb.AppendLine("  jetzt      : " + Mb(current));
            sb.AppendLine("  nach Plan  : " + Mb(proposed));
            long saved = current - proposed;
            sb.AppendLine("  Ersparnis  : " + Mb(saved) + "   (" +
                          (current > 0 ? (100.0 * saved / current).ToString("0.0") : "0") + " %)");
            sb.AppendLine();
            sb.AppendLine("  Read/Write AN : " + readable + " von " + a.Textures.Count);
            sb.AppendLine("  ohne Mipmaps  : " + noMips + " von " + a.Textures.Count);
            sb.AppendLine();

            // ---- Typen
            var kinds = new SortedDictionary<string, int>();
            for (int i = 0; i < a.Textures.Count; i++)
            {
                kinds.TryGetValue(a.Textures[i].Kind, out int n);
                kinds[a.Textures[i].Kind] = n + 1;
            }

            sb.AppendLine("--- TEXTURTYPEN ---");
            foreach (var pair in kinds)
                sb.AppendLine(string.Format("  {0,-14} {1,4}", pair.Key, pair.Value));
            sb.AppendLine();

            // ---- Groesste
            sb.AppendLine("--- 25 GROESSTE TEXTUREN ---");
            sb.AppendLine(string.Format("  {0,-9} {1,-9} {2,-9} {3,-14} {4}",
                "PIXEL", "JETZT", "PLAN", "TYP", "DATEI"));
            for (int i = 0; i < Mathf.Min(25, a.Textures.Count); i++)
            {
                var t = a.Textures[i];
                sb.AppendLine(string.Format("  {0,-9} {1,-9} {2,-9} {3,-14} {4}{5}",
                    t.Width + "x" + t.Height,
                    Mb(EstimateBytes(Mathf.Min(Mathf.Max(t.Width, t.Height), t.CurrentMax),
                                     Mathf.Min(Mathf.Max(t.Width, t.Height), t.CurrentMax), t.Mipmaps)),
                    t.ProposedMax.ToString(),
                    t.Kind,
                    Path.GetFileName(t.Path),
                    t.AlphaCritical ? "   [ALPHA WICHTIG - bleibt >= 1024]" : ""));
            }
            sb.AppendLine();

            // ---- Meshes
            long totalTris = 0;
            int noLod = 0, readableMesh = 0;
            for (int i = 0; i < a.Models.Count; i++)
            {
                totalTris += a.Models[i].Triangles;
                if (!a.Models[i].HasLodGroup) noLod++;
                if (a.Models[i].Readable) readableMesh++;
            }

            sb.AppendLine("--- MESHES ---");
            sb.AppendLine("  Modelle gesamt : " + a.Models.Count);
            sb.AppendLine("  Dreiecke gesamt: " + totalTris.ToString("N0"));
            sb.AppendLine("  ohne LODGroup  : " + noLod + " von " + a.Models.Count);
            sb.AppendLine("  Read/Write AN  : " + readableMesh + " von " + a.Models.Count);
            sb.AppendLine();
            sb.AppendLine("--- 15 GROESSTE MESHES ---");
            for (int i = 0; i < Mathf.Min(15, a.Models.Count); i++)
            {
                var m = a.Models[i];
                sb.AppendLine(string.Format("  {0,10} Tris  {1,8} Verts  Kompression {2,-7} {3} {4}",
                    m.Triangles.ToString("N0"), m.Vertices.ToString("N0"), m.Compression,
                    m.HasLodGroup ? "LOD" : "   ", Path.GetFileName(m.Path)));
            }
            sb.AppendLine();

            // ---- Shader
            sb.AppendLine("--- MATERIALIEN AUF NICHT-URP-SHADERN ---");
            if (a.LegacyShaderMaterials.Count == 0)
            {
                sb.AppendLine("  keine. Die URP-Konvertierung hat alles im Paket erwischt.");
            }
            else
            {
                sb.AppendLine("  " + a.LegacyShaderMaterials.Count + " Stueck - die zeichnen magenta:");
                for (int i = 0; i < Mathf.Min(30, a.LegacyShaderMaterials.Count); i++)
                    sb.AppendLine("    " + a.LegacyShaderMaterials[i]);
            }
            sb.AppendLine();

            sb.AppendLine("--- MOEGLICHE DOPPELTE TEXTUREN (gleiche Pixelmasse UND Dateigroesse) ---");
            if (a.DuplicateCandidates.Count == 0)
                sb.AppendLine("  keine gefunden.");
            else
            {
                sb.AppendLine("  " + a.DuplicateCandidates.Count + " Gruppe(n). NICHTS wird geloescht -");
                sb.AppendLine("  ob zwei Texturen dieselben sind, ist eine Frage an die Kunst, nicht an Bytes.");
                for (int i = 0; i < Mathf.Min(15, a.DuplicateCandidates.Count); i++)
                    sb.AppendLine("    " + a.DuplicateCandidates[i]);
            }

            sb.AppendLine();
            sb.AppendLine("=========================================================");
            sb.AppendLine("ENDE DES AUDITS. Es wurde keine Datei und keine Einstellung geaendert.");
            sb.AppendLine("Bericht auch geschrieben nach: " + Path.GetFullPath(ReportFile));
            sb.AppendLine("=========================================================");
            return sb.ToString();
        }

        // ================================================================= Sichere Optimierung

        private static string ApplySafeOptimization(Audit a)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== SAFE OPTIMIERUNG ===");
            sb.AppendLine("Ordner: " + a.Root);
            sb.AppendLine();

            string prefix = a.Root.TrimEnd('/') + "/";
            int changedTex = 0, changedMesh = 0, skipped = 0;

            try
            {
                AssetDatabase.StartAssetEditing();

                for (int i = 0; i < a.Textures.Count; i++)
                {
                    var t = a.Textures[i];

                    // A path outside the pack is never touched, whatever the audit said.
                    if (!t.Path.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        skipped++;
                        continue;
                    }

                    var importer = AssetImporter.GetAtPath(t.Path) as TextureImporter;
                    if (importer == null)
                    {
                        skipped++;
                        continue;
                    }

                    bool dirty = false;

                    if (importer.maxTextureSize != t.ProposedMax)
                    {
                        importer.maxTextureSize = t.ProposedMax;
                        dirty = true;
                    }

                    // Mipmaps on for world-space geometry. Without them a wall shimmers at
                    // distance and costs MORE bandwidth, not less.
                    if (importer.textureType == TextureImporterType.Default && !importer.mipmapEnabled)
                    {
                        importer.mipmapEnabled = true;
                        dirty = true;
                    }

                    // Read/Write costs a second copy in system memory and nothing in this pack
                    // reads pixels at runtime.
                    if (importer.isReadable)
                    {
                        importer.isReadable = false;
                        dirty = true;
                    }

                    // alphaSource and alphaIsTransparency are NEVER written. Whatever the pack
                    // author set is what the material expects; changing it is how a window
                    // turns opaque and a curtain grows a black border.

                    if (ApplyPlatform(importer, "iPhone", MobileCap(t)))    dirty = true;
                    if (ApplyPlatform(importer, "Android", MobileCap(t)))   dirty = true;
                    if (ApplyPlatform(importer, "Standalone", t.ProposedMax)) dirty = true;

                    if (dirty)
                    {
                        importer.SaveAndReimport();
                        changedTex++;
                    }
                }

                for (int i = 0; i < a.Models.Count; i++)
                {
                    var m = a.Models[i];
                    if (!m.Path.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        skipped++;
                        continue;
                    }

                    var importer = AssetImporter.GetAtPath(m.Path) as ModelImporter;
                    if (importer == null || !importer.isReadable)
                        continue;

                    // Only Read/Write. Mesh compression is NOT touched: it is lossy on vertex
                    // positions and a wall that shifts by a millimetre opens a seam. LODs are
                    // not generated either - a destructive decimation of vendor art is not
                    // something a settings pass should do behind a single button.
                    importer.isReadable = false;
                    importer.SaveAndReimport();
                    changedMesh++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }

            sb.AppendLine("Texturen geaendert: " + changedTex);
            sb.AppendLine("Modelle geaendert : " + changedMesh + "  (nur Read/Write aus)");
            sb.AppendLine("Uebersprungen     : " + skipped);
            sb.AppendLine();
            sb.AppendLine("NICHT angefasst: Quelldateien, Alpha-Kanaele, Mesh-Kompression, LODs,");
            sb.AppendLine("Materialien, Shader und alles ausserhalb von " + a.Root);
            return sb.ToString();
        }

        /// <summary>Mobile takes one step down, but never below 512 and never below 1024 for alpha.</summary>
        private static int MobileCap(TexInfo t)
        {
            int cap = Mathf.Max(512, t.ProposedMax / 2);
            if (t.AlphaCritical)
                cap = Mathf.Max(cap, 1024);
            return Mathf.Min(cap, t.ProposedMax);
        }

        private static bool ApplyPlatform(TextureImporter importer, string platform, int maxSize)
        {
            var settings = importer.GetPlatformTextureSettings(platform);
            if (settings.overridden && settings.maxTextureSize == maxSize)
                return false;

            settings.name = platform;
            settings.overridden = true;
            settings.maxTextureSize = maxSize;
            // Format stays Automatic. Naming a concrete compressed format here would pin one
            // Unity version's enum spelling into the project, and Unity already picks the right
            // one per platform.
            settings.format = TextureImporterFormat.Automatic;
            settings.textureCompression = TextureImporterCompression.Compressed;
            importer.SetPlatformTextureSettings(settings);
            return true;
        }
    }
}
