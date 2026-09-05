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

        /// <summary>
        /// How important the OBJECT is. One of the two axes; on its own it decides nothing.
        /// </summary>
        private enum Quality
        {
            A_Architecture,   // walls, wallpaper, floor, ceiling, large doors, stairs, panels
            B_HeroFurniture,  // beds, sofas, large cabinets, kitchen units, baths, pianos
            C_Prop,           // ordinary objects
            D_SmallDetail,    // handles, switches, cutlery
            E_AlphaCritical,  // windows, glass, curtains, real cutouts
        }

        /// <summary>
        /// What the MAP does. The other axis. A specular map does not need the resolution its
        /// albedo does; a wall normal map does. Judging either axis alone produced the pair the
        /// audit caught: a 2048 specular kept while another object's normal map went to 512.
        /// </summary>
        private enum MapKind
        {
            Albedo,
            Normal,
            SpecularSmoothness,   // specular, smoothness, gloss, roughness, metallic, AO, masks
            Emission,
            Unknown,
        }

        /// <summary>The cap for one (quality, kind) pair on the three platforms.</summary>
        private struct Caps
        {
            public int Desktop;
            public int Android;
            public int IOS;

            public Caps(int desktop, int android, int ios)
            {
                Desktop = desktop; Android = android; IOS = ios;
            }
        }

        /// <summary>
        /// The whole policy, in one table, on purpose. Every number here is a judgement and
        /// belongs somewhere it can be read and argued with rather than scattered through
        /// branches.
        ///
        /// <para>
        /// Where the brief gave a range, desktop takes the top of it and mobile the bottom:
        /// desktop is where the quality goes, mobile is where the memory matters. The one
        /// exception is specular/smoothness, capped at 1024 on desktop even for architecture -
        /// a gloss map carries a slowly varying quantity and 2048 of it is not visible, which
        /// is why it was singled out.
        /// </para>
        /// </summary>
        private static Caps CapFor(Quality quality, MapKind kind)
        {
            switch (quality)
            {
                case Quality.A_Architecture:
                    switch (kind)
                    {
                        case MapKind.Albedo: return new Caps(2048, 1024, 1024);
                        case MapKind.Normal: return new Caps(2048, 1024, 1024);
                        case MapKind.Emission: return new Caps(1024, 512, 512);
                        default: return new Caps(1024, 512, 512);
                    }

                case Quality.B_HeroFurniture:
                    switch (kind)
                    {
                        case MapKind.Albedo: return new Caps(2048, 1024, 1024);
                        case MapKind.Normal: return new Caps(2048, 1024, 1024);
                        case MapKind.Emission: return new Caps(1024, 512, 512);
                        default: return new Caps(1024, 512, 512);
                    }

                case Quality.D_SmallDetail:
                    return new Caps(1024, 512, 512);

                case Quality.E_AlphaCritical:
                    switch (kind)
                    {
                        case MapKind.Albedo: return new Caps(2048, 1024, 1024);
                        case MapKind.Normal: return new Caps(1024, 1024, 1024);
                        default: return new Caps(1024, 512, 512);
                    }

                default: // C_Prop
                    switch (kind)
                    {
                        case MapKind.Albedo: return new Caps(1024, 512, 512);
                        case MapKind.Normal: return new Caps(1024, 512, 512);
                        default: return new Caps(512, 512, 512);
                    }
            }
        }

        private class TexInfo
        {
            public string Path;
            public int Width;
            public int Height;
            public long FileBytes;
            public bool Mipmaps;
            public bool Readable;
            public bool HasAlpha;
            public bool TransparentMaterial;   // a material that references it really is transparent
            public bool AlphaNameHint;         // the filename claims transparency
            public MapKind Kind;
            public Quality Quality;
            public int CurrentMax;
            public int Desktop;
            public int Android;
            public int IOS;
            public string Reason;
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
            public int TransparentMaterials;
            public int Prefabs;
            public int Scenes;
        }

        // Filename and folder fragments. Ordinary English words, not one pack's asset list, so
        // the same tool works on the next pack.
        private static readonly (Quality Quality, string[] Words)[] QualityWords =
        {
            (Quality.A_Architecture, new[] { "wall", "wallpaper", "floor", "ceiling", "roof",
                                             "stair", "door", "plaster", "brick", "concrete",
                                             "parquet", "panel", "column", "beam", "arch",
                                             "baseboard", "skirting", "molding", "moulding" }),
            (Quality.B_HeroFurniture, new[] { "bed", "sofa", "couch", "wardrobe", "closet",
                                              "cabinet", "kitchen", "fridge", "refrigerator",
                                              "bath", "shower", "table", "desk", "bookshelf",
                                              "shelf", "piano", "fireplace", "stove", "oven",
                                              "sink", "toilet", "dresser", "armchair" }),
            (Quality.D_SmallDetail, new[] { "handle", "knob", "switch", "socket", "screw",
                                            "nail", "cutlery", "spoon", "fork", "key",
                                            "button", "hinge", "plug", "coin", "pen" }),
        };

        // A filename claiming transparency is a HINT, never a verdict. The audit found albedo
        // maps named "AlbedoTransparency" whose material is fully opaque - the name is a
        // convention of the authoring tool, not a statement about this material. A texture is
        // treated as alpha-critical only when a material that actually references it is really
        // transparent or alpha-clipped.
        private static readonly string[] AlphaNameHints =
        {
            "transparen", "alpha", "glass", "window", "curtain", "foliage", "leaf", "leaves",
            "plant", "cutout", "opacity", "decal", "fence", "lace", "net",
        };

        /// <summary>
        /// Which textures are referenced by a material that genuinely renders transparent.
        ///
        /// Under URP Lit, _Surface = 1 is the Transparent surface type and _AlphaClip = 1 is
        /// cutout; a render queue at or past 2450 covers shaders that express it neither way.
        /// Any of the three is enough - all three being absent, with the material opaque, means
        /// the alpha channel is decoration in the file and nothing reads it.
        /// </summary>
        private static HashSet<string> CollectTransparentlyUsedTextures(string root, out int transparentMaterials)
        {
            var used = new HashSet<string>();
            transparentMaterials = 0;

            var guids = AssetDatabase.FindAssets("t:Material", new[] { root });
            for (int i = 0; i < guids.Length; i++)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (mat == null || mat.shader == null)
                    continue;

                bool transparent = mat.renderQueue >= 2450;
                if (!transparent && mat.HasProperty("_Surface") && mat.GetFloat("_Surface") > 0.5f)
                    transparent = true;
                if (!transparent && mat.HasProperty("_AlphaClip") && mat.GetFloat("_AlphaClip") > 0.5f)
                    transparent = true;

                if (!transparent)
                    continue;

                transparentMaterials++;

                var names = mat.GetTexturePropertyNames();
                for (int n = 0; n < names.Length; n++)
                {
                    var tex = mat.GetTexture(names[n]);
                    if (tex == null)
                        continue;

                    string path = AssetDatabase.GetAssetPath(tex);
                    if (!string.IsNullOrEmpty(path))
                        used.Add(path);
                }
            }

            return used;
        }

        private static Audit RunAudit(string root)
        {
            var audit = new Audit { Root = root };

            if (string.IsNullOrEmpty(root) || !AssetDatabase.IsValidFolder(root))
                return audit;

            string[] search = { root };

            var texGuids = AssetDatabase.FindAssets("t:Texture2D", search);
            var bySignature = new Dictionary<string, List<string>>();

            // Which textures a really-transparent material actually references. Done once, for
            // the whole pack, before any texture is classified.
            var transparentlyUsed = CollectTransparentlyUsedTextures(root, out int transparentMats);
            audit.TransparentMaterials = transparentMats;

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
                    HasAlpha = importer.DoesSourceTextureHaveAlpha(),
                    CurrentMax = importer.maxTextureSize,
                };

                info.Kind = KindOf(lower, importer.textureType == TextureImporterType.NormalMap);
                info.AlphaNameHint = ContainsAny(lowerPath, AlphaNameHints);
                info.TransparentMaterial = transparentlyUsed.Contains(path);
                info.Quality = QualityOf(lowerPath, info);
                ApplyCaps(info);

                audit.Textures.Add(info);

                // Two files of the same pixel size AND the same byte length are worth a look.
                // Nothing is deleted on this basis - deciding that two textures are the same is
                // a judgement about art, not about bytes.
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

        private static MapKind KindOf(string lower, bool importedAsNormalMap)
        {
            if (importedAsNormalMap || lower.Contains("normal") || lower.EndsWith("_nrm")
                || lower.EndsWith("_n") || lower.Contains("_bump"))
                return MapKind.Normal;

            if (lower.Contains("specular") || lower.Contains("_spec") || lower.Contains("smooth")
                || lower.Contains("gloss") || lower.Contains("rough") || lower.Contains("metal")
                || lower.Contains("occlusion") || lower.Contains("_ao") || lower.Contains("mask")
                || lower.Contains("height") || lower.Contains("displac"))
                return MapKind.SpecularSmoothness;

            if (lower.Contains("emis") || lower.Contains("glow") || lower.Contains("light"))
                return MapKind.Emission;

            if (lower.Contains("albedo") || lower.Contains("basecolor") || lower.Contains("base_color")
                || lower.Contains("diffuse") || lower.Contains("_col") || lower.Contains("_d"))
                return MapKind.Albedo;

            // Unknown is treated as albedo for capping: guessing "it is only a mask" and being
            // wrong shows up as a blurred surface, which is the expensive direction to be wrong in.
            return MapKind.Unknown;
        }

        /// <summary>
        /// Which quality class the OBJECT falls in. Alpha-critical wins over everything, but
        /// only when a material proves it - a filename saying "AlbedoTransparency" on a texture
        /// no transparent material references is a naming convention, not a requirement.
        /// </summary>
        private static Quality QualityOf(string lowerPath, TexInfo info)
        {
            if (info.HasAlpha && info.TransparentMaterial)
            {
                info.Reason = "E: ein Material, das sie benutzt, rendert wirklich transparent";
                return Quality.E_AlphaCritical;
            }

            for (int i = 0; i < QualityWords.Length; i++)
            {
                if (ContainsAny(lowerPath, QualityWords[i].Words))
                {
                    info.Reason = QualityWords[i].Quality.ToString().Substring(0, 1) +
                                  ": Pfad/Name passt auf diese Klasse";
                    return QualityWords[i].Quality;
                }
            }

            info.Reason = info.AlphaNameHint && info.HasAlpha
                ? "C: Name behauptet Transparenz, aber kein Material rendert transparent"
                : "C: keine speziellere Klasse erkannt";
            return Quality.C_Prop;
        }

        /// <summary>
        /// The three caps, never above what the source actually is. The source may already be
        /// smaller than the policy allows, and raising maxTextureSize would import nothing new
        /// while making the numbers lie.
        /// </summary>
        private static void ApplyCaps(TexInfo info)
        {
            var caps = CapFor(info.Quality, info.Kind);
            int source = Mathf.Max(32, Mathf.Max(info.Width, info.Height));

            info.Desktop = Mathf.Min(caps.Desktop, source);
            info.Android = Mathf.Min(caps.Android, source);
            info.IOS = Mathf.Min(caps.IOS, source);
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

        private static string Mb(long bytes) => (bytes / 1048576.0).ToString("N1") + " MB";

        private static string Pct(long before, long after)
        {
            if (before <= 0) return "-";
            return "-" + (100.0 * (before - after) / before).ToString("0.0") + " %";
        }

        private static void Bump<T>(SortedDictionary<T, int> map, T key)
        {
            map.TryGetValue(key, out int n);
            map[key] = n + 1;
        }

        private static int Count(SortedDictionary<int, int> map, int key)
        {
            map.TryGetValue(key, out int n);
            return n;
        }

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

            // ---- Aufloesungen jetzt und je Zielplattform
            var now = new SortedDictionary<int, int>();
            var desk = new SortedDictionary<int, int>();
            var andr = new SortedDictionary<int, int>();
            var ios = new SortedDictionary<int, int>();
            long mNow = 0, mDesk = 0, mAndr = 0, mIos = 0;
            int readable = 0, noMips = 0, alphaReal = 0, alphaNameOnly = 0;

            for (int i = 0; i < a.Textures.Count; i++)
            {
                var t = a.Textures[i];
                int source = Mathf.Max(t.Width, t.Height);
                int effNow = Mathf.Min(source, t.CurrentMax);

                Bump(now, effNow); Bump(desk, t.Desktop); Bump(andr, t.Android); Bump(ios, t.IOS);

                mNow  += EstimateBytes(effNow, effNow, t.Mipmaps);
                mDesk += EstimateBytes(t.Desktop, t.Desktop, true);
                mAndr += EstimateBytes(t.Android, t.Android, true);
                mIos  += EstimateBytes(t.IOS, t.IOS, true);

                if (t.Readable) readable++;
                if (!t.Mipmaps) noMips++;
                if (t.Quality == Quality.E_AlphaCritical) alphaReal++;
                else if (t.AlphaNameHint && t.HasAlpha) alphaNameOnly++;
            }

            sb.AppendLine("--- AUFLOESUNGSVERTEILUNG (laengste Kante) ---");
            sb.AppendLine(string.Format("  {0,8} {1,10} {2,10} {3,10} {4,10}",
                "PIXEL", "JETZT", "DESKTOP", "ANDROID", "iOS"));
            foreach (int side in new[] { 4096, 2048, 1024, 512, 256, 128, 64, 32 })
            {
                if (Count(now, side) + Count(desk, side) + Count(andr, side) + Count(ios, side) == 0)
                    continue;

                sb.AppendLine(string.Format("  {0,8} {1,10} {2,10} {3,10} {4,10}",
                    side, Count(now, side), Count(desk, side), Count(andr, side), Count(ios, side)));
            }
            sb.AppendLine();

            sb.AppendLine("--- GESCHAETZTER TEXTURSPEICHER (RGBA32, inkl. Mipmaps) ---");
            sb.AppendLine(string.Format("  {0,-10} {1,14} {2,12}", "ZIEL", "SPEICHER", "ERSPARNIS"));
            sb.AppendLine(string.Format("  {0,-10} {1,14} {2,12}", "JETZT", Mb(mNow), "-"));
            sb.AppendLine(string.Format("  {0,-10} {1,14} {2,12}", "DESKTOP", Mb(mDesk), Pct(mNow, mDesk)));
            sb.AppendLine(string.Format("  {0,-10} {1,14} {2,12}", "ANDROID", Mb(mAndr), Pct(mNow, mAndr)));
            sb.AppendLine(string.Format("  {0,-10} {1,14} {2,12}", "iOS", Mb(mIos), Pct(mNow, mIos)));
            sb.AppendLine();
            sb.AppendLine("  RGBA32 ist der ehrliche Worst Case und die Zahl, die den Editor");
            sb.AppendLine("  lahmlegt. Im Build komprimiert (DXT/BC auf Desktop, ASTC auf Mobile)");
            sb.AppendLine("  liegt der echte VRAM-Bedarf typischerweise bei einem Viertel bis");
            sb.AppendLine("  einem Achtel davon. Die VERHAELTNISSE oben stimmen trotzdem.");
            sb.AppendLine();
            sb.AppendLine("  Read/Write AN : " + readable + " von " + a.Textures.Count);
            sb.AppendLine("  ohne Mipmaps  : " + noMips + " von " + a.Textures.Count);
            sb.AppendLine();

            // ---- Klassen
            var byQuality = new SortedDictionary<string, int>();
            var byKind = new SortedDictionary<string, int>();
            for (int i = 0; i < a.Textures.Count; i++)
            {
                Bump(byQuality, a.Textures[i].Quality.ToString());
                Bump(byKind, a.Textures[i].Kind.ToString());
            }

            sb.AppendLine("--- QUALITAETSKLASSE (Objekt-Wichtigkeit) ---");
            foreach (var pair in byQuality)
                sb.AppendLine(string.Format("  {0,-18} {1,4}", pair.Key, pair.Value));
            sb.AppendLine();

            sb.AppendLine("--- MAP-TYP ---");
            foreach (var pair in byKind)
                sb.AppendLine(string.Format("  {0,-20} {1,4}", pair.Key, pair.Value));
            sb.AppendLine();

            sb.AppendLine("--- ALPHA, AUS DEN MATERIALIEN STATT AUS DEN NAMEN ---");
            sb.AppendLine("  wirklich transparente Materialien : " + a.TransparentMaterials +
                          " von " + a.Materials);
            sb.AppendLine("  Texturen davon benutzt (Klasse E) : " + alphaReal);
            sb.AppendLine("  Name behauptet Alpha, Material nicht: " + alphaNameOnly +
                          "   <- diese werden NICHT als E behandelt");
            sb.AppendLine();

            // ---- Groesste
            sb.AppendLine("--- 25 GROESSTE TEXTUREN ---");
            sb.AppendLine(string.Format("  {0,-10} {1,-8} {2,-8} {3,-6} {4,-6} {5,-6} {6,-20} {7}",
                "PIXEL", "JETZT", "TYP", "DESK", "ANDR", "iOS", "KLASSE", "DATEI"));
            for (int i = 0; i < Mathf.Min(25, a.Textures.Count); i++)
            {
                var t = a.Textures[i];
                int effNow = Mathf.Min(Mathf.Max(t.Width, t.Height), t.CurrentMax);
                sb.AppendLine(string.Format("  {0,-10} {1,-8} {2,-8} {3,-6} {4,-6} {5,-6} {6,-20} {7}",
                    t.Width + "x" + t.Height,
                    Mb(EstimateBytes(effNow, effNow, t.Mipmaps)),
                    t.Kind,
                    t.Desktop, t.Android, t.IOS,
                    t.Quality,
                    Path.GetFileName(t.Path)));
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
            sb.AppendLine("  VORSCHLAG: Read/Write bleibt AN. Begruendung:");
            sb.AppendLine();
            sb.AppendLine("  NavMeshRuntimeBuilder baut die NavMesh zur LAUFZEIT aus RENDER-Meshes,");
            sb.AppendLine("  nicht aus Collidern - auf beiden Wegen. Der NavMeshBuilder-Pfad setzt");
            sb.AppendLine("  sourceObject = MeshFilter.sharedMesh, und der NavMeshSurface-Pfad setzt");
            sb.AppendLine("  useGeometry auf RenderMeshes. Beides liest Vertexdaten auf der CPU, und");
            sb.AppendLine("  dafuer muss Read/Write an sein.");
            sb.AppendLine();
            sb.AppendLine("  Welche HQ-Meshes den Sammler wirklich erreichen, entscheidet");
            sb.AppendLine("  ShouldInclude: Tag 'Environment' ODER ein Name mit 'Floor'/'Wall' -");
            sb.AppendLine("  geprueft am MeshFilter-Objekt SELBST, also an den Kind-Objekten der");
            sb.AppendLine("  Vendor-Prefabs, deren Namen wir nicht vergeben. Das laesst sich statisch");
            sb.AppendLine("  nicht entscheiden, und die sichere Antwort auf eine unentscheidbare");
            sb.AppendLine("  Frage ist, nichts abzuschalten.");
            sb.AppendLine();
            sb.AppendLine("  Der Preis dafuer ist klein: " + totalTris.ToString("N0") + " Dreiecke im");
            sb.AppendLine("  ganzen Paket. Die CPU-Kopie liegt in der Groessenordnung einiger");
            sb.AppendLine("  zehn MB - gegen ein Texturproblem von mehreren GB. Read/Write");
            sb.AppendLine("  abzuschalten spart fast nichts und riskiert, dass der Geist nicht");
            sb.AppendLine("  mehr laufen kann.");
            sb.AppendLine();
            sb.AppendLine("  Wer das aendern will, aendert nicht die Importer, sondern den Sammler:");
            sb.AppendLine("  NavMesh aus PhysicsColliders statt RenderMeshes. Das ist eine");
            sb.AppendLine("  Aenderung an der Generierung, nicht an Import-Einstellungen.");
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
            sb.AppendLine("  Mesh-Kompression wird NICHT gesetzt: sie ist verlustbehaftet auf");
            sb.AppendLine("  Vertexpositionen, und eine Wand, die sich um einen Millimeter");
            sb.AppendLine("  verschiebt, reisst eine Fuge auf. LODs werden NICHT erzeugt.");
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
            int changedTex = 0, skipped = 0;

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

                    // The default is the desktop cap; the platform overrides below take mobile
                    // down from there.
                    if (importer.maxTextureSize != t.Desktop)
                    {
                        importer.maxTextureSize = t.Desktop;
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

                    if (ApplyPlatform(importer, "iPhone", t.IOS))         dirty = true;
                    if (ApplyPlatform(importer, "Android", t.Android))     dirty = true;
                    if (ApplyPlatform(importer, "Standalone", t.Desktop))  dirty = true;

                    if (dirty)
                    {
                        importer.SaveAndReimport();
                        changedTex++;
                    }
                }

                // Meshes werden NICHT angefasst. Read/Write bleibt an, weil die
                // Laufzeit-NavMesh aus Render-Meshes gebaut wird - siehe Auditbericht.
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }

            sb.AppendLine("Texturen geaendert: " + changedTex);
            sb.AppendLine("Modelle geaendert : 0  (Read/Write bleibt an - NavMesh liest sie)");
            sb.AppendLine("Uebersprungen     : " + skipped);
            sb.AppendLine();
            sb.AppendLine("NICHT angefasst: Quelldateien, Alpha-Kanaele, Meshes jeder Art, LODs,");
            sb.AppendLine("Materialien, Shader und alles ausserhalb von " + a.Root);
            return sb.ToString();
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
