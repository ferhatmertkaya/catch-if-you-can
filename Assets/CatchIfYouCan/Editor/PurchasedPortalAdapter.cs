using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CatchIfYouCan.Art;
using CatchIfYouCan.Environment;
using UnityEditor;
using UnityEngine;

namespace CatchIfYouCan.EditorTools
{
    /// <summary>
    /// Adopts a bought portal pack into this project's own portal.
    ///
    /// <para>
    /// <b>Why a pack cannot simply be dropped in.</b> A portal asset sold for HDRP is four
    /// different kinds of thing in one folder, and they do not travel together:
    /// </para>
    /// <list type="bullet">
    /// <item><b>Shaders</b> - compiled against HDRP's shader library. This project is URP.
    /// The two pipelines are mutually exclusive; an HDRP shader in a URP project does not
    /// degrade, it fails to compile and Unity draws the magenta error shader. There is no
    /// setting for this and no import option that fixes it.</item>
    /// <item><b>Materials</b> - only ever as good as the shader they point at, so they arrive
    /// magenta with the shaders.</item>
    /// <item><b>Textures</b> - just images. Pipeline-independent, and where the pack's look
    /// actually lives.</item>
    /// <item><b>Meshes and particle definitions</b> - also pipeline-independent, though a
    /// particle still needs a URP material to be drawn with.</item>
    /// </list>
    ///
    /// <para>
    /// So this tool carries across the half that can cross. It reads the pack's own materials
    /// to learn which texture the pack itself treats as its energy and which as its mask -
    /// rather than guessing from filenames, which is how a mirror once got classified as a
    /// door - copies those images into the project, and points
    /// <see cref="PortalStyle"/> at them.
    /// </para>
    ///
    /// <para>
    /// <b>It never writes into the pack.</b> Every file it produces lands under
    /// <see cref="DestinationFolder"/>. The purchased folder is opened read-only, which is what
    /// lets it stay outside version control without this project depending on a path that only
    /// exists on one machine.
    /// </para>
    ///
    /// <para>
    /// <b>It scans exactly the path it is given.</b> No suffix is appended, no parent is
    /// searched, and the report header prints the path that was actually read. A tool that
    /// silently looks somewhere else produces a report about a folder nobody asked about.
    /// </para>
    /// </summary>
    public sealed class PurchasedPortalAdapter : EditorWindow
    {
        /// <summary>Where copies land. Inside the project, so they are tracked normally.</summary>
        public const string DestinationFolder = "Assets/CatchIfYouCan/Resources/Portal";

        private const string PortalMaterialPath =
            "Assets/CatchIfYouCan/Resources/Materials/MAT_Portal.mat";

        // Only a starting value for the text field. The scan uses whatever is in the field.
        private string _packFolder = "Assets/Knife/Portal HDRP";

        private Vector2 _scroll;
        private ScanResult _scan;

        [MenuItem("Catch If You Can/Portal/Gekauftes Portal-Paket uebernehmen [SCHREIBT]", false, 701)]
        private static void Open()
        {
            PurchasedPortalAdapter window = GetWindow<PurchasedPortalAdapter>(
                true, "Adopt Purchased Portal Pack", true);
            window.minSize = new Vector2(560f, 420f);
            window.Show();
        }

        // ---- the model ---------------------------------------------------------------------

        /// <summary>What a pack texture is used AS, according to the pack's own materials.</summary>
        private enum Role
        {
            /// <summary>Bound to a colour/emission slot: the visible energy.</summary>
            Energy,

            /// <summary>Bound to an opacity, mask or dissolve slot: the shape of the energy.</summary>
            Mask,

            /// <summary>Bound in a material whose shader draws particles: a spark image.</summary>
            Particle,

            /// <summary>Bound to something this portal has no use for (normal, smoothness).</summary>
            Unused
        }

        private sealed class Candidate
        {
            public Texture Texture;
            public string AssetPath;
            public Role Role;
            public string BoundAs;     // the pack property name that produced the role
            public string BoundIn;     // the pack material that binds it
        }

        private sealed class ScanResult
        {
            public string ScannedPath = string.Empty;
            public bool FolderExists;
            public int MaterialCount;
            public int HdrpMaterialCount;
            public int ShaderCount;
            public readonly List<Candidate> Candidates = new List<Candidate>();
            public readonly List<string> Notes = new List<string>();
            public int EnergyChoice = -1;
            public int MaskChoice = -1;
            public int ParticleChoice = -1;
        }

        // ---- role mapping -------------------------------------------------------------------
        //
        // Keyed on what the PACK's material calls the slot, because that is the pack telling us
        // what the image is for. Filenames are not consulted: "glow_02" is a guess, a binding to
        // _EmissiveColorMap is a fact.

        private static readonly string[] EnergySlots =
        {
            "_MainTex", "_BaseColorMap", "_BaseMap", "_EmissiveColorMap", "_EmissionMap",
            "_EmissiveColor", "_Albedo", "_ColorMap"
        };

        private static readonly string[] MaskSlots =
        {
            "_OpacityMask", "_AlphaMask", "_MaskMap", "_Mask", "_DissolveMap", "_DissolveTex",
            "_NoiseTex", "_NoiseMap", "_DistortionMap", "_FlowMap"
        };

        private static Role RoleOf(string slot)
        {
            if (EnergySlots.Any(s => string.Equals(s, slot, StringComparison.OrdinalIgnoreCase)))
                return Role.Energy;
            if (MaskSlots.Any(s => string.Equals(s, slot, StringComparison.OrdinalIgnoreCase)))
                return Role.Mask;
            return Role.Unused;
        }

        // ---- the scan -----------------------------------------------------------------------

        /// <summary>
        /// Reads the pack. Opens nothing for writing and changes nothing.
        /// </summary>
        private static ScanResult Scan(string folder)
        {
            ScanResult result = new ScanResult { ScannedPath = folder };

            // Exactly this path. Not this path plus a guessed subfolder, and not the parent when
            // it is missing - a scan that quietly moves produces a report about the wrong folder.
            if (string.IsNullOrWhiteSpace(folder) || !AssetDatabase.IsValidFolder(folder))
            {
                result.FolderExists = false;
                result.Notes.Add("There is no folder at '" + folder + "'. Nothing was scanned. " +
                                 "Type the pack's real path - the adapter does not search for it.");
                return result;
            }

            result.FolderExists = true;
            string[] searchIn = { folder };

            foreach (string guid in AssetDatabase.FindAssets("t:Shader", searchIn))
                result.ShaderCount++;

            Dictionary<string, Candidate> byPath = new Dictionary<string, Candidate>();

            foreach (string guid in AssetDatabase.FindAssets("t:Material", searchIn))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                    continue;

                result.MaterialCount++;

                string shaderName = mat.shader != null ? mat.shader.name : "<none>";
                if (shaderName.IndexOf("HDRP", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    shaderName.IndexOf("HDRenderPipeline", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    result.HdrpMaterialCount++;
                }

                // The pack tells us this material draws particles. Detected on the shader
                // rather than on the slot, because a particle material binds its sprite to
                // _MainTex like everything else - the slot cannot distinguish them.
                bool particleMaterial =
                    shaderName.IndexOf("particle", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    Path.GetFileName(path).IndexOf("particle", StringComparison.OrdinalIgnoreCase) >= 0;

                foreach (string slot in mat.GetTexturePropertyNames())
                {
                    Texture tex = mat.GetTexture(slot);
                    if (tex == null)
                        continue;

                    string texPath = AssetDatabase.GetAssetPath(tex);
                    if (string.IsNullOrEmpty(texPath))
                        continue;

                    Role role = RoleOf(slot);
                    if (particleMaterial && role == Role.Energy)
                        role = Role.Particle;

                    // First binding wins, except that a real role beats a previously recorded
                    // Unused - one image is often bound in several materials.
                    if (byPath.TryGetValue(texPath, out Candidate existing))
                    {
                        if (existing.Role == Role.Unused && role != Role.Unused)
                        {
                            existing.Role = role;
                            existing.BoundAs = slot;
                            existing.BoundIn = Path.GetFileName(path);
                        }
                        continue;
                    }

                    byPath[texPath] = new Candidate
                    {
                        Texture = tex,
                        AssetPath = texPath,
                        Role = role,
                        BoundAs = slot,
                        BoundIn = Path.GetFileName(path)
                    };
                }
            }

            result.Candidates.AddRange(byPath.Values.OrderBy(c => c.Role).ThenBy(c => c.AssetPath));

            result.EnergyChoice = result.Candidates.FindIndex(c => c.Role == Role.Energy);
            result.MaskChoice = result.Candidates.FindIndex(c => c.Role == Role.Mask);
            result.ParticleChoice = result.Candidates.FindIndex(c => c.Role == Role.Particle);

            if (result.ShaderCount > 0)
            {
                result.Notes.Add(result.ShaderCount + " shader(s) in this pack cannot be used. " +
                                 "They are authored against HDRP and this project is URP; under " +
                                 "URP they compile to the magenta error shader. The project's own " +
                                 "URP portal shader is used instead, driven by the artwork below.");
            }

            if (result.MaterialCount > 0)
            {
                result.Notes.Add(result.MaterialCount + " material(s) found, " +
                                 result.HdrpMaterialCount + " of them on an HDRP shader. " +
                                 "None of them can be used directly, for the same reason.");
            }

            if (result.EnergyChoice < 0)
            {
                result.Notes.Add("No texture in this pack is bound to a colour or emission slot, " +
                                 "so there is no energy image to adopt. Nothing will change if " +
                                 "you adopt now.");
            }

            return result;
        }

        // ---- the adoption -------------------------------------------------------------------

        /// <summary>
        /// Copies the chosen images into the project and points the portal at them.
        ///
        /// <para>
        /// The copy is the point. Referencing the images where they lie would make this project
        /// depend on a purchased folder that is deliberately not in version control - the
        /// reference would resolve on one machine and be a missing texture everywhere else,
        /// which is the failure the asset-reference guard exists to catch.
        /// </para>
        /// </summary>
        private static string Adopt(ScanResult scan)
        {
            if (scan == null || !scan.FolderExists || scan.EnergyChoice < 0)
                return "Nothing to adopt.";

            EnsureFolder(DestinationFolder);

            Texture energy = CopyIn(scan.Candidates[scan.EnergyChoice], "Portal_Energy");
            Texture mask = scan.MaskChoice >= 0
                ? CopyIn(scan.Candidates[scan.MaskChoice], "Portal_Mask")
                : null;
            Texture spark = scan.ParticleChoice >= 0
                ? CopyIn(scan.Candidates[scan.ParticleChoice], "Portal_Spark")
                : null;

            if (energy == null)
                return "The energy texture could not be copied. Nothing was changed.";

            System.Text.StringBuilder log = new System.Text.StringBuilder();
            log.AppendLine("Copied into " + DestinationFolder + ":");
            log.AppendLine("  energy  " + AssetDatabase.GetAssetPath(energy));
            log.AppendLine("  mask    " + (mask != null ? AssetDatabase.GetAssetPath(mask) : "(none)"));
            log.AppendLine("  spark   " + (spark != null ? AssetDatabase.GetAssetPath(spark)
                                                        : "(none - the generated dot stays)"));

            // The material, so the look is visible on the asset in the editor without entering
            // play mode.
            Material portal = AssetDatabase.LoadAssetAtPath<Material>(PortalMaterialPath);
            if (portal != null)
            {
                portal.SetTexture("_EnergyTex", energy);
                portal.SetTexture("_MaskTex", mask);
                portal.SetFloat("_Textured", 1f);
                portal.EnableKeyword("_PORTAL_TEXTURED");
                EditorUtility.SetDirty(portal);
                log.AppendLine("Wrote " + PortalMaterialPath + ".");
            }
            else
            {
                log.AppendLine("WARNING: " + PortalMaterialPath + " was not found, so the " +
                               "material was not updated.");
            }

            // ...and the style, which is what actually survives. PortalSurface pushes the style
            // onto the material every time the portal is built, so a material edited alone is
            // overwritten on the first frame of play.
            int portals = 0;
            foreach (LobbyPortal lobbyPortal in
                     UnityEngine.Object.FindObjectsByType<LobbyPortal>(FindObjectsInactive.Include))
            {
                SerializedObject so = new SerializedObject(lobbyPortal);
                if (!WriteStyle(so, energy, mask, spark))
                    continue;

                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(lobbyPortal);
                portals++;
            }

            if (portals > 0)
            {
                log.AppendLine("Wrote the style on " + portals + " LobbyPortal(s) in the open " +
                               "scene(s). SAVE THE SCENE or the change is lost.");
            }
            else
            {
                log.AppendLine("No LobbyPortal is in an open scene, so no style was written. " +
                               "Open the lobby scene and adopt again, or set 'Use purchased " +
                               "artwork' on the portal by hand - the textures are copied either " +
                               "way.");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return log.ToString();
        }

        /// <summary>
        /// Writes the six style fields, or reports that it could not find them.
        ///
        /// <para>
        /// Every field is looked up by name and checked for null. A serialized path that stops
        /// resolving after a rename is the silent-failure shape this project has been bitten by
        /// repeatedly; here it is a visible refusal instead.
        /// </para>
        /// </summary>
        private static bool WriteStyle(SerializedObject so, Texture energy, Texture mask,
                                       Texture spark)
        {
            SerializedProperty use = so.FindProperty("style.usePurchasedArtwork");
            SerializedProperty energyProperty = so.FindProperty("style.energyTexture");
            SerializedProperty maskProperty = so.FindProperty("style.maskTexture");
            SerializedProperty sparkProperty = so.FindProperty("style.sparkTexture");

            if (use == null || energyProperty == null || maskProperty == null ||
                sparkProperty == null)
            {
                Debug.LogError("[CIYC][Portal] PortalStyle has no 'usePurchasedArtwork' / " +
                               "'energyTexture' / 'maskTexture' / 'sparkTexture' under 'style'. " +
                               "The adapter and PortalStyle have drifted apart; nothing was " +
                               "written.");
                return false;
            }

            use.boolValue = true;
            energyProperty.objectReferenceValue = energy;
            maskProperty.objectReferenceValue = mask;

            // Only when the pack actually had one. Clearing it would swap the generated dot for
            // nothing, and a particle with no texture is the opaque square this whole slot
            // exists to get rid of.
            if (spark != null)
                sparkProperty.objectReferenceValue = spark;

            return true;
        }

        private static Texture CopyIn(Candidate candidate, string basename)
        {
            if (candidate == null || string.IsNullOrEmpty(candidate.AssetPath))
                return null;

            string extension = Path.GetExtension(candidate.AssetPath);
            string destination = DestinationFolder + "/" + basename + extension;

            // Overwrite deliberately: adopting twice must converge on one pair of files rather
            // than accumulate Portal_Energy 1, Portal_Energy 2. DeleteAsset on a path that holds
            // nothing is a no-op, so this needs no existence test - and an existence test through
            // System.IO would be assuming the process working directory is the project root.
            AssetDatabase.DeleteAsset(destination);

            if (!AssetDatabase.CopyAsset(candidate.AssetPath, destination))
            {
                Debug.LogError("[CIYC][Portal] Could not copy '" + candidate.AssetPath +
                               "' to '" + destination + "'.");
                return null;
            }

            AssetDatabase.ImportAsset(destination);
            return AssetDatabase.LoadAssetAtPath<Texture>(destination);
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;

            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            string leaf = Path.GetFileName(folder);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        // ---- the window ---------------------------------------------------------------------

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Adopt a purchased portal pack", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "URP and HDRP are different render pipelines and a shader written for one does " +
                "not run on the other - it draws magenta. This project is URP because it ships " +
                "on phones, and HDRP has no mobile support at all, so a bought HDRP pack can " +
                "never be dropped in whole.\n\n" +
                "What CAN cross is the artwork. This reads the pack's own materials to find out " +
                "which image the pack treats as its energy, copies it into the project, and " +
                "drives this game's URP portal shader with it.",
                MessageType.Info);

            EditorGUILayout.Space();
            _packFolder = EditorGUILayout.TextField("Pack folder", _packFolder);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Scan (read-only)"))
                _scan = Scan(_packFolder != null ? _packFolder.Trim() : string.Empty);

            // Always drawn, and it refuses out loud rather than being greyed out. A disabled
            // button tells you that you cannot press it and never why.
            if (GUILayout.Button("Adopt into the portal"))
            {
                if (_scan == null || !_scan.FolderExists)
                {
                    EditorUtility.DisplayDialog("Adopt purchased portal pack",
                        "Scan a real pack folder first.", "OK");
                }
                else if (_scan.EnergyChoice < 0)
                {
                    EditorUtility.DisplayDialog("Adopt purchased portal pack",
                        "Nothing in '" + _scan.ScannedPath + "' is bound to a colour or " +
                        "emission slot, so there is no energy image to adopt.", "OK");
                }
                else if (EditorUtility.DisplayDialog(
                             "Adopt purchased portal pack",
                             "This copies the chosen images into " + DestinationFolder +
                             " and switches the portal to them.\n\nThe purchased folder is not " +
                             "modified.",
                             "Adopt", "Cancel"))
                {
                    Debug.Log("[CIYC][Portal] " + Adopt(_scan));
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            DrawReport();
        }

        private void DrawReport()
        {
            if (_scan == null)
            {
                EditorGUILayout.LabelField("Nothing scanned yet.");
                return;
            }

            // The header names the path that was READ, never the path that was typed, so a scan
            // of the wrong folder is visible rather than inferred.
            EditorGUILayout.LabelField("Scanned", _scan.ScannedPath);

            if (!_scan.FolderExists)
            {
                foreach (string note in _scan.Notes)
                    EditorGUILayout.HelpBox(note, MessageType.Error);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            foreach (string note in _scan.Notes)
                EditorGUILayout.HelpBox(note, MessageType.Warning);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Textures the pack binds", EditorStyles.boldLabel);

            for (int i = 0; i < _scan.Candidates.Count; i++)
            {
                Candidate candidate = _scan.Candidates[i];
                string chosen = i == _scan.EnergyChoice ? "  <- ENERGY"
                    : i == _scan.MaskChoice ? "  <- MASK"
                    : i == _scan.ParticleChoice ? "  <- SPARK"
                    : string.Empty;

                EditorGUILayout.LabelField(
                    candidate.Role + "  " + Path.GetFileName(candidate.AssetPath),
                    "bound as " + candidate.BoundAs + " in " + candidate.BoundIn + chosen);
            }

            if (_scan.Candidates.Count == 0)
                EditorGUILayout.LabelField("(no material in this folder binds any texture)");

            EditorGUILayout.EndScrollView();
        }
    }
}
