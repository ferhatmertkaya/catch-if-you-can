using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CatchIfYouCan.EditorTools
{
    /// <summary>
    /// Looks at the purchased pieces one by one and says what each one actually is, so a room
    /// can be built by hand out of things that were checked rather than things that looked right
    /// in the project window.
    ///
    /// <para>
    /// It answers three questions that keep coming back. Why is that piece so much bigger - a
    /// wrong import scale, or a wall that is genuinely two modules wide? Why is that piece white
    /// - a lost material, a material with no texture, or a material that is SUPPOSED to be white
    /// paint? And where is the thing relative to its own origin, which in this pack is regularly
    /// twenty or thirty metres away.
    /// </para>
    /// <para>
    /// It writes nothing to the pack. No reimport, no material edit, no scale change. The audit
    /// runs only when the button is pressed and is cached until it is pressed again; nothing
    /// here touches the asset database per frame.
    /// </para>
    /// </summary>
    public class HQPieceBrowser : EditorWindow
    {
        private const string DefaultFolder = "Assets/HQ Modular House/interior/moduls/walls prefabs";

        /// <summary>Bigger than this on any axis and it is a room, not a piece.</summary>
        private const float SceneExportMetres = 15f;

        /// <summary>A pivot further than this from the geometry cannot be snapped to anything.</summary>
        private const float BadPivotMetres = 1f;

        private string _folder = DefaultFolder;
        private List<Piece> _pieces;
        private Vector2 _scroll;
        private bool _showDetail = true;
        private bool _onlyReady;
        private float _moduleWidth;

        [MenuItem("Catch If You Can/2. HQ MODULAR HOUSE/Bauteile ansehen und setzen [UNDO]", false, 200)]
        public static void Open()
        {
            var w = GetWindow<HQPieceBrowser>(false, "HQ Bauteile", true);
            w.minSize = new Vector2(560f, 400f);
        }

        // ------------------------------------------------------------------------------- UI

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Prueft die gekauften Teile und setzt sie in die Szene.\n\n" +
                "Das Paket wird NICHT veraendert: kein Reimport, keine Materialaenderung, " +
                "keine Skalierung. Die Pruefung laeuft nur auf Knopfdruck.",
                MessageType.Info);

            _folder = EditorGUILayout.TextField("Ordner", _folder);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Pruefen"))
                    _pieces = Audit(_folder, out _moduleWidth);

                using (new EditorGUI.DisabledScope(_pieces == null))
                {
                    if (GUILayout.Button("Bericht in die Konsole"))
                        Debug.Log(Report(_pieces, _moduleWidth, _folder));
                }
            }

            if (_pieces == null)
            {
                EditorGUILayout.LabelField("Noch nichts geprueft.");
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                _showDetail = EditorGUILayout.ToggleLeft("Details", _showDetail, GUILayout.Width(80f));
                _onlyReady = EditorGUILayout.ToggleLeft("nur BEREIT", _onlyReady, GUILayout.Width(100f));
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(
                    _pieces.Count + " Teile, Modulbreite " + _moduleWidth.ToString("F2") + " m",
                    EditorStyles.miniLabel);
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (int i = 0; i < _pieces.Count; i++)
            {
                Piece piece = _pieces[i];
                if (_onlyReady && !piece.Ready)
                    continue;

                DrawPiece(piece);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawPiece(Piece piece)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    // Lazy: AssetPreview builds the thumbnail in the background and returns null
                    // until it is ready. Asking is cheap; forcing one is not, so nothing forces.
                    Texture icon = AssetPreview.GetAssetPreview(piece.Asset);
                    GUILayout.Label(icon != null ? icon : Texture2D.blackTexture,
                                    GUILayout.Width(48f), GUILayout.Height(48f));

                    using (new EditorGUILayout.VerticalScope())
                    {
                        EditorGUILayout.LabelField(piece.Name + "   " + string.Join("  ", piece.Badges),
                                                   EditorStyles.boldLabel);
                        EditorGUILayout.LabelField(
                            piece.Size.ToString("F2") + " m" +
                            (piece.ModuleMultiple > 0
                                ? "   = " + piece.ModuleMultiple + "x Modul"
                                : "   Sondermass") +
                            "   Pivot " + piece.PivotOffset.ToString("F1") + " m",
                            EditorStyles.miniLabel);
                    }

                    using (new EditorGUILayout.VerticalScope(GUILayout.Width(110f)))
                    {
                        // The production button first, and named after what it produces. The
                        // other two stay because forensics needs them - comparing a corrected
                        // piece against the vendor's own size is how a wrong factor is caught -
                        // but a piece placed ORIGINAL no longer matches the room, and that is
                        // not the button somebody reaches for by accident.
                        if (GUILayout.Button("Setzen auf CIYC-Spielmass"))
                            Place(piece, fixPivot: true, gameScale: true);

                        if (GUILayout.Button("Setzen + Pivot fix (Vendor-Groesse)"))
                            Place(piece, fixPivot: true, gameScale: false);

                        if (GUILayout.Button("Setzen ORIGINAL (nur zum Vergleichen)"))
                            Place(piece, fixPivot: false, gameScale: false);
                    }
                }

                if (!_showDetail)
                    return;

                EditorGUILayout.LabelField(piece.Path, EditorStyles.miniLabel);
                EditorGUILayout.LabelField(
                    "Root-Scale " + piece.RootScale.ToString("F3") +
                    "   Renderer " + piece.Renderers +
                    "   Slots " + piece.MaterialSlots +
                    (piece.NullSlots > 0 ? "   LEERE SLOTS " + piece.NullSlots : "") +
                    (piece.SubmeshMismatch > 0 ? "   Slot/Submesh-Abweichung " + piece.SubmeshMismatch : ""),
                    EditorStyles.miniLabel);

                if (!string.IsNullOrEmpty(piece.ImporterScales))
                    EditorGUILayout.LabelField("Import-ScaleFactor: " + piece.ImporterScales,
                                               EditorStyles.miniLabel);

                if (!string.IsNullOrEmpty(piece.ChildScales))
                    EditorGUILayout.LabelField("Kind-Skalierungen != 1: " + piece.ChildScales,
                                               EditorStyles.miniLabel);

                for (int m = 0; m < piece.Materials.Count; m++)
                    EditorGUILayout.LabelField("   " + piece.Materials[m].Line, EditorStyles.miniLabel);
            }
        }

        // ---------------------------------------------------------------------------- place

        /// <summary>
        /// Puts the piece in the scene inside a CIYC-owned wrapper.
        ///
        /// <para>
        /// The vendor prefab goes in untouched, as a normal prefab instance: its link, its
        /// materials and its geometry are exactly as purchased. What the wrapper adds is a
        /// USABLE ORIGIN. In this pack a piece's own origin is regularly 20 to 40 m from its
        /// geometry, because everything kept the origin of the apartment scene it was exported
        /// from - so dragging one in and typing a position moves it somewhere else entirely.
        /// </para>
        /// <para>
        /// With the pivot fix the wrapper's origin sits at the BOTTOM CENTRE of the piece, which
        /// is where a wall wants to be gripped: put the wrapper at floor level, snap it to a
        /// grid, and the wall stands on the floor. Nothing is scaled and nothing is rotated -
        /// the audit found no reason to, and scaling to make things match is exactly what turns
        /// a mismatch into a mystery.
        /// </para>
        /// </summary>
        private static void Place(Piece piece, bool fixPivot, bool gameScale)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(piece.Path);
            if (prefab == null)
            {
                Debug.LogError("[CIYC][HQ] " + piece.Path + " liess sich nicht laden.");
                return;
            }

            if (!fixPivot)
            {
                var plain = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                Undo.RegisterCreatedObjectUndo(plain, "HQ-Teil setzen");
                Selection.activeGameObject = plain;
                return;
            }

            var wrapper = new GameObject("HQ_" + piece.Name);
            Undo.RegisterCreatedObjectUndo(wrapper, "HQ-Teil setzen");

            // Into 05_HQ_MANUAL_HOUSE/<Kategorie>, if that structure is already there. It is not
            // created on the fly: a placement is not the moment to reorganise somebody's scene,
            // and a wrapper left at the root is easy to move afterwards.
            Transform category = FindCategory(piece);
            if (category != null)
                wrapper.transform.SetParent(category, true);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, wrapper.transform);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;

            if (TryMeasureInSpace(instance.transform, wrapper.transform, out Bounds bounds))
            {
                // Bottom centre: X and Z centred, Y at the underside.
                var grip = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                instance.transform.localPosition = -grip;

                Debug.Log("[CIYC][HQ] " + piece.Name + " gesetzt. Der Pivot lag " +
                          grip.magnitude.ToString("F1") + " m neben dem Teil; der Wrapper-" +
                          "Ursprung sitzt jetzt unten in der Mitte. Nichts wurde skaliert.");
            }
            else
            {
                Debug.LogWarning("[CIYC][HQ] " + piece.Name + ": keine sichtbare Geometrie " +
                                 "messbar, der Pivot wurde NICHT korrigiert.");
            }

            if (gameScale)
            {
                // On the WRAPPER, uniformly, once. The purchased prefab inside keeps its own
                // local values and its prefab link; nothing is applied back to the package.
                wrapper.transform.localScale = Vector3.one * HQScale.Factor;
                Debug.Log("[CIYC][HQ] " + piece.Name + " steht auf Spielmass " +
                          HQScale.Factor.ToString("F6") + " (" +
                          HQScale.TargetClearHeight.ToString("F2") + " / " +
                          HQScale.ReferenceClearHeight.ToString("F2") +
                          "). Das gekaufte Prefab darin ist unveraendert.");
            }

            Selection.activeGameObject = wrapper;
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        /// <summary>
        /// The folder this piece belongs in, if the scene already has that structure.
        ///
        /// <para>
        /// Classified by what the audit measured, not by the filename - this pack numbers its
        /// prefabs. A window is a piece that carries the glass material, a door is one that
        /// carries a door material, an arch carries an arch material. Anything unrecognised goes
        /// to the walls, which is where a wall-shaped piece with wallpaper on it belongs, and is
        /// one drag away from anywhere else.
        /// </para>
        /// </summary>
        private static Transform FindCategory(Piece piece)
        {
            Transform house = null;
            foreach (GameObject root in UnityEngine.SceneManagement.SceneManager
                         .GetActiveScene().GetRootGameObjects())
            {
                if (root.name == MainMenuHierarchyTool.HouseRoot)
                {
                    house = root.transform;
                    break;
                }
            }

            if (house == null)
                return null;

            string wanted = CategoryFor(piece);
            return wanted != null ? house.Find(wanted) : null;
        }

        private static string CategoryFor(Piece piece)
        {
            bool glass = false, door = false, arch = false;
            for (int i = 0; i < piece.Materials.Count; i++)
            {
                string line = piece.Materials[i].Line;
                if (line.IndexOf("Steklo", StringComparison.OrdinalIgnoreCase) >= 0) glass = true;
                if (line.IndexOf("door", StringComparison.OrdinalIgnoreCase) >= 0) door = true;
                if (line.IndexOf("arch", StringComparison.OrdinalIgnoreCase) >= 0) arch = true;
            }

            if (arch) return "05_ARCHES";
            if (glass) return "04_WINDOWS";
            if (door) return "03_DOORS";

            // A piece far narrower than it is tall is a column or a trim, not a wall.
            float across = Mathf.Max(piece.Size.x, piece.Size.z);
            float up = Mathf.Max(piece.Size.y, Mathf.Min(piece.Size.x, piece.Size.z));
            if (across > 0.01f && up > across * 1.6f)
                return "06_COLUMNS_TRIM";

            return "02_WALLS";
        }

        // ---------------------------------------------------------------------------- audit

        private class MaterialInfo
        {
            public string Line;
            public bool Missing;
            public bool NonUrp;
        }

        private class Piece
        {
            public GameObject Asset;
            public string Path;
            public string Name;
            public Vector3 RootScale;
            public Vector3 Size;
            public float PivotOffset;
            public int Renderers;
            public int MaterialSlots;
            public int NullSlots;
            public int SubmeshMismatch;
            public int ModuleMultiple;
            public string ImporterScales;
            public string ChildScales;
            public readonly List<MaterialInfo> Materials = new List<MaterialInfo>();
            public readonly List<string> Badges = new List<string>();
            public bool Ready;
        }

        private static List<Piece> Audit(string folder, out float moduleWidth)
        {
            moduleWidth = 0f;
            var pieces = new List<Piece>();

            if (!AssetDatabase.IsValidFolder(folder))
            {
                Debug.LogError("[CIYC][HQ] Den Ordner " + folder + " gibt es nicht.");
                return pieces;
            }

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
            for (int i = 0; i < guids.Length; i++)
            {
                Piece piece = Inspect(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (piece != null)
                    pieces.Add(piece);
            }

            pieces.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));

            // The module width is the MEDIAN of the pieces that are not scene exports. A pack's
            // ladder is only visible against its own commonest piece; guessing 4 m because this
            // pack happens to use 4 would be a number that stops being true for the next pack.
            var widths = new List<float>();
            for (int i = 0; i < pieces.Count; i++)
            {
                if (Mathf.Max(pieces[i].Size.x, pieces[i].Size.z) < SceneExportMetres)
                    widths.Add(Mathf.Max(pieces[i].Size.x, pieces[i].Size.z));
            }

            if (widths.Count > 0)
            {
                widths.Sort();
                moduleWidth = widths[widths.Count / 2];
            }

            for (int i = 0; i < pieces.Count; i++)
                Classify(pieces[i], moduleWidth);

            return pieces;
        }

        private static Piece Inspect(string path)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null)
                return null;

            var piece = new Piece
            {
                Asset = go,
                Path = path,
                Name = System.IO.Path.GetFileNameWithoutExtension(path),
                RootScale = go.transform.localScale,
            };

            var renderers = go.GetComponentsInChildren<MeshRenderer>(true);
            piece.Renderers = renderers.Length;

            var scales = new StringBuilder();
            var importers = new List<string>();
            var seenMaterials = new HashSet<string>();

            for (int r = 0; r < renderers.Length; r++)
            {
                Transform t = renderers[r].transform;
                if ((t.localScale - Vector3.one).sqrMagnitude > 0.0001f && scales.Length < 120)
                    scales.Append(t.name).Append(t.localScale.ToString("F2")).Append(' ');

                var filter = renderers[r].GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                {
                    string meshPath = AssetDatabase.GetAssetPath(filter.sharedMesh);
                    var importer = AssetImporter.GetAtPath(meshPath) as ModelImporter;
                    if (importer != null)
                    {
                        string entry = importer.globalScale.ToString("F3");
                        if (!importers.Contains(entry))
                            importers.Add(entry);
                    }
                }

                Material[] materials = renderers[r].sharedMaterials;
                piece.MaterialSlots += materials.Length;

                int submeshes = filter != null && filter.sharedMesh != null
                    ? filter.sharedMesh.subMeshCount
                    : materials.Length;

                if (submeshes != materials.Length)
                    piece.SubmeshMismatch++;

                for (int m = 0; m < materials.Length; m++)
                {
                    if (materials[m] == null)
                    {
                        piece.NullSlots++;
                        continue;
                    }

                    string key = AssetDatabase.GetAssetPath(materials[m]) + "|" + materials[m].name;
                    if (!seenMaterials.Add(key))
                        continue;

                    piece.Materials.Add(Describe(materials[m]));
                }
            }

            piece.ChildScales = scales.ToString();
            piece.ImporterScales = string.Join(", ", importers.ToArray());

            if (TryMeasureInSpace(go.transform, go.transform, out Bounds bounds))
            {
                piece.Size = bounds.size;
                piece.PivotOffset = bounds.center.magnitude;
            }

            return piece;
        }

        /// <summary>
        /// What this material will actually draw, said plainly.
        ///
        /// <para>
        /// A white piece has three quite different causes and they are not guessable from the
        /// screen. The material may be MISSING - a null slot, or a shader that does not draw.
        /// It may have no base map, which is what happens when a prefab points at the material
        /// Unity generated from the FBX rather than the one the pack ships: this pack carries
        /// several such pairs, "door base" and "door detail" and "mirror" among them, one
        /// textured and one not. Or it may be white ON PURPOSE - "arch big white" carries a real
        /// 1024 base map and is meant to be painted trim next to wallpaper.
        /// </para>
        /// </summary>
        private static MaterialInfo Describe(Material material)
        {
            var info = new MaterialInfo();
            var sb = new StringBuilder();

            Shader shader = material.shader;
            string shaderName = shader != null ? shader.name : "<null>";

            bool urp = shader != null && shader.isSupported &&
                       !shaderName.StartsWith("Hidden/", StringComparison.Ordinal) &&
                       (shaderName.StartsWith("Universal Render Pipeline", StringComparison.Ordinal) ||
                        shaderName.StartsWith("Shader Graphs", StringComparison.Ordinal) ||
                        shaderName.StartsWith("CIYC", StringComparison.Ordinal));

            info.NonUrp = !urp;

            Texture baseMap = material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : null;
            info.Missing = baseMap == null;

            sb.Append(material.name).Append("  ").Append(shaderName);

            if (!urp)
                sb.Append("  [KEIN URP]");

            sb.Append(baseMap != null
                ? "  BaseMap " + baseMap.name + " " + baseMap.width + "x" + baseMap.height
                : "  KEINE BaseMap - zeichnet einfarbig");

            sb.Append("  ").Append(AssetDatabase.GetAssetPath(material));
            info.Line = sb.ToString();
            return info;
        }

        private static void Classify(Piece piece, float moduleWidth)
        {
            float widest = Mathf.Max(piece.Size.x, Mathf.Max(piece.Size.y, piece.Size.z));

            bool sceneExport = widest > SceneExportMetres;
            bool badPivot = piece.PivotOffset > BadPivotMetres;

            bool missingMaterial = piece.NullSlots > 0;
            bool nonUrp = false;
            for (int i = 0; i < piece.Materials.Count; i++)
            {
                if (piece.Materials[i].Missing) missingMaterial = true;
                if (piece.Materials[i].NonUrp) nonUrp = true;
            }

            // A multiple of the module is a DESIGNED size, not an error. Prefab 15 is exactly
            // twice the module and 16 exactly three times, to the centimetre - marking those
            // "oversized" and scaling them down would break a wall that was right.
            piece.ModuleMultiple = 0;
            if (moduleWidth > 0.1f && !sceneExport)
            {
                float across = Mathf.Max(piece.Size.x, piece.Size.z);
                float multiple = across / moduleWidth;
                int nearest = Mathf.RoundToInt(multiple);
                if (nearest >= 1 && Mathf.Abs(multiple - nearest) < 0.04f)
                    piece.ModuleMultiple = nearest;
            }

            if (sceneExport) piece.Badges.Add("SZENEN-EXPORT");
            if (missingMaterial) piece.Badges.Add("MATERIAL FEHLT");
            if (nonUrp) piece.Badges.Add("KEIN URP");
            if (badPivot) piece.Badges.Add("PIVOT " + piece.PivotOffset.ToString("F0") + "m");
            if (piece.ModuleMultiple > 1) piece.Badges.Add("MODUL x" + piece.ModuleMultiple);

            piece.Ready = !sceneExport && !missingMaterial && !nonUrp;
            if (piece.Ready && piece.Badges.Count == 0)
                piece.Badges.Add("BEREIT");
            else if (piece.Ready)
                piece.Badges.Insert(0, "BEREIT");
        }

        /// <summary>
        /// Bounds of the visible geometry, in <paramref name="space"/>, from eight transformed
        /// corners per mesh - a rotated child's axis-aligned size is not its size in the parent.
        /// </summary>
        private static bool TryMeasureInSpace(Transform root, Transform space, out Bounds bounds)
        {
            bounds = default;
            var renderers = root.GetComponentsInChildren<MeshRenderer>(true);

            bool started = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (!renderers[i].enabled)
                    continue;

                var filter = renderers[i].GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                    continue;

                Bounds local = filter.sharedMesh.bounds;
                for (int c = 0; c < 8; c++)
                {
                    var corner = new Vector3(
                        local.center.x + ((c & 1) == 0 ? -local.extents.x : local.extents.x),
                        local.center.y + ((c & 2) == 0 ? -local.extents.y : local.extents.y),
                        local.center.z + ((c & 4) == 0 ? -local.extents.z : local.extents.z));

                    Vector3 point = space.InverseTransformPoint(
                        filter.transform.TransformPoint(corner));

                    if (!started)
                    {
                        bounds = new Bounds(point, Vector3.zero);
                        started = true;
                    }
                    else
                    {
                        bounds.Encapsulate(point);
                    }
                }
            }

            return started;
        }

        // --------------------------------------------------------------------------- report

        private static string Report(List<Piece> pieces, float moduleWidth, string folder)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== HQ BAUTEIL-PRUEFUNG ===");
            sb.AppendLine("Ordner       : " + folder);
            sb.AppendLine("Teile        : " + pieces.Count);
            sb.AppendLine("Modulbreite  : " + moduleWidth.ToString("F2") + " m (Median)");
            sb.AppendLine();

            for (int i = 0; i < pieces.Count; i++)
            {
                Piece p = pieces[i];
                sb.AppendLine(p.Name + "   " + string.Join("  ", p.Badges));
                sb.AppendLine("   " + p.Path);
                sb.AppendLine("   Groesse " + p.Size.ToString("F2") + " m" +
                              (p.ModuleMultiple > 0 ? "  = " + p.ModuleMultiple + "x Modul" : "  Sondermass") +
                              "   Pivot " + p.PivotOffset.ToString("F1") + " m" +
                              "   Root-Scale " + p.RootScale.ToString("F3"));
                sb.AppendLine("   Renderer " + p.Renderers + "  Slots " + p.MaterialSlots +
                              "  leere Slots " + p.NullSlots +
                              "  Slot/Submesh-Abweichung " + p.SubmeshMismatch +
                              "  Import-Scale " + (string.IsNullOrEmpty(p.ImporterScales) ? "-" : p.ImporterScales));

                if (!string.IsNullOrEmpty(p.ChildScales))
                    sb.AppendLine("   Kind-Skalierungen != 1: " + p.ChildScales);

                for (int m = 0; m < p.Materials.Count; m++)
                    sb.AppendLine("      " + p.Materials[m].Line);

                sb.AppendLine();
            }

            sb.AppendLine("Ein Vielfaches der Modulbreite ist eine ABSICHT, kein Fehler: solche");
            sb.AppendLine("Teile sind zwei oder drei Module breit und duerfen nicht kleiner");
            sb.AppendLine("skaliert werden. 'KEINE BaseMap' heisst einfarbig - entweder ein");
            sb.AppendLine("aus dem FBX erzeugtes Material statt des mitgelieferten, oder eine");
            sb.AppendLine("Flaeche, die wirklich weiss gestrichen ist.");
            return sb.ToString();
        }
    }
}
