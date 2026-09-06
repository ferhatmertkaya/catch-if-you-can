using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CatchIfYouCan.EditorTools
{
    /// <summary>
    /// Says why a selected object draws white, per renderer and per material slot - and proposes
    /// nothing until it can point at the evidence.
    ///
    /// <para>
    /// White has three causes in this pack and they look identical on screen. The material may be
    /// MEANT to be white: <c>arch big white</c> carries a real 1024 base map and is painted trim
    /// beside wallpaper, and nothing is wrong with it. The slot may be EMPTY, or the shader may
    /// not draw. Or - and this is the common one - the renderer may point at the material Unity
    /// generated from the FBX rather than the one the pack authored.
    /// </para>
    /// <para>
    /// That third case is provable rather than guessable, because the pack names its textures
    /// <c>&lt;fbx&gt;_&lt;slot&gt;_AlbedoTransparency</c>. So a textureless material called
    /// <c>SHKAF3</c> has its authored twin in whichever material carries the base map
    /// <c>4_SHKAF3_AlbedoTransparency</c> - which is <c>commode1</c>. Twenty of the pack's thirty
    /// textureless material names line up that way; <c>bachek</c> to <c>part1</c>,
    /// <c>Tumba 2</c> to <c>nighstand</c>, <c>BRA</c> to <c>sconce</c>, and so on.
    /// </para>
    /// <para>
    /// It is a correspondence, not a law - <c>beth</c>'s texture is spelled <c>bath</c>, and ten
    /// names have no twin at all. Which is exactly why this reports and proposes, and changes
    /// nothing. It writes no asset, edits no material and touches no vendor prefab.
    /// </para>
    /// </summary>
    public static class HQMaterialDoctor
    {
        private const string PackRoot = "Assets/HQ Modular House";

        [MenuItem("Catch If You Can/2. HQ MODULAR HOUSE/Materialien pruefen (Auswahl) [NUR LESEN]", false, 204)]
        private static void Diagnose()
        {
            GameObject[] selection = Selection.gameObjects;
            if (selection == null || selection.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "Nichts ausgewaehlt",
                    "Das weisse Objekt in der Hierarchie oder im Project-Fenster auswaehlen " +
                    "und noch einmal aufrufen.\n\n" +
                    "Es wird nur die Auswahl geprueft - das Paket wird nicht durchsucht.",
                    "OK");
                return;
            }

            // Built once, on demand. This is the only thing that looks beyond the selection, and
            // it is an index query plus the materials it returns - not a reimport, not a scan of
            // meshes or textures, and not something that runs per repaint.
            Dictionary<string, List<Material>> bySlotName = IndexPackMaterials();

            var sb = new StringBuilder();
            sb.AppendLine("=== MATERIAL-DOKTOR ===");
            sb.AppendLine(selection.Length + " ausgewaehlt. Es wird NICHTS geaendert.");
            sb.AppendLine();

            int white = 0, lost = 0, empty = 0, nonUrp = 0;

            for (int i = 0; i < selection.Length; i++)
                Examine(selection[i], bySlotName, sb, ref white, ref lost, ref empty, ref nonUrp);

            sb.AppendLine();
            sb.AppendLine("--- ZUSAMMENFASSUNG ---");
            sb.AppendLine("  einfarbig ohne BaseMap, mit Gegenstueck im Paket : " + lost);
            sb.AppendLine("  einfarbig ohne BaseMap, ohne Gegenstueck         : " + white);
            sb.AppendLine("  leere Slots                                      : " + empty);
            sb.AppendLine("  Shader nicht URP oder nicht unterstuetzt         : " + nonUrp);
            sb.AppendLine();

            if (lost > 0)
            {
                sb.AppendLine("Die kleinste sichere Korrektur waere, das vorgeschlagene Material");
                sb.AppendLine("am RENDERER DER INSTANZ zu setzen - als Override im CIYC-Wrapper,");
                sb.AppendLine("nicht am gekauften Prefab und nicht am gekauften Material. Das");
                sb.AppendLine("bleibt umkehrbar und laesst das Paket unberuehrt.");
                sb.AppendLine();
                sb.AppendLine("Angewendet wird hier nichts.");
            }
            else
            {
                sb.AppendLine("Kein verlorenes Mapping in der Auswahl. Wo eine BaseMap fehlt und");
                sb.AppendLine("kein Gegenstueck existiert, ist die Flaeche vermutlich wirklich");
                sb.AppendLine("einfarbig - beim Paket sind das die gestrichenen Zierteile.");
            }

            Debug.Log(sb.ToString());
        }

        // ------------------------------------------------------------------------- inspection

        private static void Examine(GameObject go, Dictionary<string, List<Material>> bySlotName,
            StringBuilder sb, ref int white, ref int lost, ref int empty, ref int nonUrp)
        {
            sb.AppendLine("### " + go.name + "   " + Path(go));

            var renderers = go.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length == 0)
            {
                sb.AppendLine("   kein MeshRenderer - hier gibt es nichts zu zeichnen.");
                sb.AppendLine();
                return;
            }

            ReportModelSource(renderers, sb);

            for (int r = 0; r < renderers.Length; r++)
            {
                MeshRenderer renderer = renderers[r];
                var filter = renderer.GetComponent<MeshFilter>();
                Mesh mesh = filter != null ? filter.sharedMesh : null;

                Material[] slots = renderer.sharedMaterials;
                int submeshes = mesh != null ? mesh.subMeshCount : -1;

                sb.Append("   ").Append(renderer.name)
                  .Append("   Slots ").Append(slots.Length)
                  .Append("   Submeshes ").Append(submeshes < 0 ? "?" : submeshes.ToString());

                // More slots than submeshes wastes a draw call; fewer means the tail submeshes
                // reuse the last material, which is a common way for one part of a mesh to end
                // up wearing something it was never given.
                if (submeshes >= 0 && submeshes != slots.Length)
                    sb.Append("   ABWEICHUNG");

                if (mesh != null)
                    sb.Append("   Mesh ").Append(AssetDatabase.GetAssetPath(mesh));

                sb.AppendLine();

                for (int s = 0; s < slots.Length; s++)
                {
                    Material material = slots[s];
                    if (material == null)
                    {
                        empty++;
                        sb.AppendLine("      [" + s + "] LEERER SLOT - zeichnet Unitys Default, " +
                                      "das unter URP magenta ist");
                        continue;
                    }

                    Describe(material, s, bySlotName, sb, ref white, ref lost, ref nonUrp);
                }
            }

            sb.AppendLine();
        }

        /// <summary>
        /// Says when every material on this object comes out of a model file.
        ///
        /// <para>
        /// This is the first thing to check and the cheapest thing to fix. A model dragged from
        /// the project window is a MODEL INSTANCE: its renderers use the materials embedded in
        /// the FBX, which in this pack carry no textures at all. The pack's own prefabs, beside
        /// the FBX in <c>walls prefabs/</c>, carry the authored materials instead - prefab 1 uses
        /// blue, door detail, wallpaper3 and white, none of which the FBX knows about.
        /// </para>
        /// <para>
        /// So a wall that came from the FBX is not a broken material assignment. It is the wrong
        /// source, and no material swap is needed to fix it: use the prefab.
        /// </para>
        /// </summary>
        private static void ReportModelSource(MeshRenderer[] renderers, StringBuilder sb)
        {
            int fromModel = 0, total = 0;

            for (int r = 0; r < renderers.Length; r++)
            {
                Material[] slots = renderers[r].sharedMaterials;
                for (int s = 0; s < slots.Length; s++)
                {
                    if (slots[s] == null)
                        continue;

                    total++;
                    if (IsModelFile(AssetDatabase.GetAssetPath(slots[s])))
                        fromModel++;
                }
            }

            if (total == 0 || fromModel < total)
                return;

            sb.AppendLine("   >>> MODELL-INSTANZ: alle " + total + " Materialien liegen IM " +
                          "Modell (FBX), nicht als eigene Assets.");
            sb.AppendLine("       Das ist kein kaputtes Material, sondern die falsche Quelle. " +
                          "Die eingebetteten");
            sb.AppendLine("       FBX-Materialien dieses Pakets haben keine Texturen; die " +
                          "fertigen Wandteile liegen");
            sb.AppendLine("       daneben in 'walls prefabs/' und tragen wallpaper3, white und " +
                          "die Tuermaterialien.");
            sb.AppendLine("       ZUERST das PREFAB statt der FBX in die Szene ziehen - dann " +
                          "eruebrigt sich alles");
            sb.AppendLine("       Weitere unten.");
            sb.AppendLine();
        }

        private static bool IsModelFile(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            string lower = path.ToLowerInvariant();
            return lower.EndsWith(".fbx", StringComparison.Ordinal) ||
                   lower.EndsWith(".obj", StringComparison.Ordinal) ||
                   lower.EndsWith(".blend", StringComparison.Ordinal) ||
                   lower.EndsWith(".dae", StringComparison.Ordinal) ||
                   lower.EndsWith(".max", StringComparison.Ordinal);
        }

        private static void Describe(Material material, int slot,
            Dictionary<string, List<Material>> bySlotName, StringBuilder sb,
            ref int white, ref int lost, ref int nonUrp)
        {
            Shader shader = material.shader;
            string shaderName = shader != null ? shader.name : "<null>";
            string path = AssetDatabase.GetAssetPath(material);

            bool urp = shader != null && shader.isSupported &&
                       !shaderName.StartsWith("Hidden/", StringComparison.Ordinal) &&
                       (shaderName.StartsWith("Universal Render Pipeline", StringComparison.Ordinal) ||
                        shaderName.StartsWith("Shader Graphs", StringComparison.Ordinal));

            Texture baseMap = material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : null;

            sb.Append("      [").Append(slot).Append("] ").Append(material.name)
              .Append("   ").Append(shaderName);

            if (!urp)
            {
                nonUrp++;
                sb.Append("   [KEIN URP / NICHT UNTERSTUETZT]");
            }

            sb.AppendLine();
            sb.AppendLine("           Material : " + path);

            if (baseMap != null)
            {
                sb.AppendLine("           BaseMap  : " + baseMap.name + "  " +
                              baseMap.width + "x" + baseMap.height);
                sb.AppendLine("           Textur   : " + AssetDatabase.GetAssetPath(baseMap));
                sb.AppendLine("           -> OK: dieses Material hat eine Textur. Wirkt es " +
                              "trotzdem weiss, ist die Textur weiss.");
                return;
            }

            // No base map. Which of the two reasons applies is decided by looking for the
            // authored twin, not by the name looking innocent.
            sb.AppendLine("           BaseMap  : KEINE - zeichnet einfarbig");

            if (IsTooWeakToMatch(material.name.Trim()))
            {
                white++;
                sb.AppendLine("           -> Der Slot heisst '" + material.name + "'. Ein " +
                              "blosser Zahlenname beweist keine Zuordnung:");
                sb.AppendLine("              dieses Paket hat Wand-Slots 1-6 UND " +
                              "Fenstermaterialien 1-4 aus einer anderen FBX.");
                sb.AppendLine("              Es wird nichts vorgeschlagen. Kommt das Teil aus " +
                              "einer FBX, siehe oben.");
                return;
            }

            List<Material> twins = Twins(material.name, bySlotName);
            if (twins == null || twins.Count == 0)
            {
                white++;
                sb.AppendLine("           -> Kein Material im Paket traegt eine Textur namens " +
                              "*_" + material.name + "_*.");
                sb.AppendLine("              Vermutlich ist die Flaeche wirklich einfarbig. " +
                              "NICHT ersetzen.");
                return;
            }

            lost++;
            sb.AppendLine("           -> VERLORENES MAPPING. Das Paket enthaelt ein Material, " +
                          "dessen Textur nach genau diesem Slot benannt ist:");

            for (int i = 0; i < twins.Count && i < 3; i++)
            {
                Texture twinMap = twins[i].HasProperty("_BaseMap")
                    ? twins[i].GetTexture("_BaseMap") : null;

                sb.AppendLine("              JETZT  " + material.name + "  (keine Textur)");
                sb.AppendLine("              STATT  " + twins[i].name +
                              "  BaseMap " + (twinMap != null ? twinMap.name : "?"));
                sb.AppendLine("              Beleg  " + AssetDatabase.GetAssetPath(twins[i]));
            }

            if (twins.Count > 1)
                sb.AppendLine("              " + twins.Count + " Kandidaten - die Zuordnung ist " +
                              "eine Namensentsprechung, kein Gesetz. Selbst entscheiden.");
        }

        // ----------------------------------------------------------------------------- index

        /// <summary>
        /// Every pack material that HAS a base map, filed under the slot name its texture is
        /// named after.
        ///
        /// <para>
        /// The pack names its textures <c>&lt;fbx&gt;_&lt;slot&gt;_AlbedoTransparency</c>, so the
        /// middle part is the FBX material slot the texture was baked for - which is also the
        /// name Unity gives the material it generates from that slot. That correspondence is what
        /// lets a textureless <c>SHKAF3</c> be matched to the authored <c>commode1</c>.
        /// </para>
        /// </summary>
        private static Dictionary<string, List<Material>> IndexPackMaterials()
        {
            var index = new Dictionary<string, List<Material>>(StringComparer.OrdinalIgnoreCase);

            if (!AssetDatabase.IsValidFolder(PackRoot))
                return index;

            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { PackRoot });
            for (int i = 0; i < guids.Length; i++)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(
                    AssetDatabase.GUIDToAssetPath(guids[i]));

                if (material == null || !material.HasProperty("_BaseMap"))
                    continue;

                Texture baseMap = material.GetTexture("_BaseMap");
                if (baseMap == null)
                    continue;

                string slot = SlotNameOf(baseMap.name);
                if (string.IsNullOrEmpty(slot))
                    continue;

                if (!index.TryGetValue(slot, out List<Material> list))
                {
                    list = new List<Material>();
                    index[slot] = list;
                }

                list.Add(material);
            }

            return index;
        }

        /// <summary>
        /// The middle of <c>&lt;fbx&gt;_&lt;slot&gt;_AlbedoTransparency</c>, or null if the
        /// texture is not named that way.
        /// </summary>
        private static string SlotNameOf(string textureName)
        {
            if (string.IsNullOrEmpty(textureName))
                return null;

            int tail = textureName.IndexOf("_Albedo", StringComparison.OrdinalIgnoreCase);
            if (tail <= 0)
                return null;

            string head = textureName.Substring(0, tail);
            int split = head.IndexOf('_');
            if (split < 0 || split + 1 >= head.Length)
                return null;

            return head.Substring(split + 1).Trim();
        }

        private static List<Material> Twins(string materialName,
            Dictionary<string, List<Material>> bySlotName)
        {
            if (string.IsNullOrEmpty(materialName))
                return null;

            string name = materialName.Trim();

            // A bare number is not evidence of anything. This pack has slots called 1 to 6 on
            // the wall meshes AND materials called 1 to 4 for the windows, named after a
            // completely different FBX - matching those to each other proposed window glass for
            // a door wall, which is the correspondence being taken for a law. A name has to
            // carry something of its own before it can prove a match.
            if (IsTooWeakToMatch(name))
                return null;

            bySlotName.TryGetValue(name, out List<Material> twins);
            return twins;
        }

        /// <summary>Digits and punctuation alone say nothing about which surface this is.</summary>
        private static bool IsTooWeakToMatch(string name)
        {
            for (int i = 0; i < name.Length; i++)
            {
                if (char.IsLetter(name[i]))
                    return name.Length < 3;
            }

            return true;
        }

        private static string Path(GameObject go)
        {
            string assetPath = AssetDatabase.GetAssetPath(go);
            if (!string.IsNullOrEmpty(assetPath))
                return assetPath;

            var sb = new StringBuilder(go.name);
            Transform t = go.transform.parent;
            while (t != null)
            {
                sb.Insert(0, t.name + "/");
                t = t.parent;
            }

            return sb.ToString();
        }
    }
}
