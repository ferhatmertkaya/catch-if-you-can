using System;
using System.Collections.Generic;
using System.Text;
using CatchIfYouCan.Content;
using CatchIfYouCan.Procedural;
using UnityEditor;
using UnityEngine;

namespace CatchIfYouCan.EditorTools
{
    /// <summary>
    /// Writes the modular catalog from paths that were LOOKED AT, not from filenames that were
    /// guessed at.
    ///
    /// <para>
    /// The automatic classifier matched English words - wall, floor, doorway - against a pack
    /// that numbers its prefabs. It found three of a hundred and five, one of which measures
    /// 36 x 57 m, and the surface density was then measured off those. This replaces the guess
    /// with the inventory's answer: eighteen wall prefabs in one folder, of which five are
    /// usable at a 3 m ceiling, and the material each surface is meant to wear.
    /// </para>
    /// <para>
    /// Nothing here is inferred from a name at runtime. The catalog it writes holds direct
    /// object references, so the generator never searches the pack.
    /// </para>
    /// </summary>
    public static class HQVerifiedCatalog
    {
        private const string CatalogPath =
            "Assets/CatchIfYouCan/ScriptableObjects/Content/ModularInteriorCatalog.asset";

        private const string ContentCatalogPath =
            "Assets/CatchIfYouCan/Resources/CatchIfYouCan/InvestigationContentCatalog.asset";

        private const string Walls = "Assets/HQ Modular House/interior/moduls/walls prefabs/";

        // Verified against the pack inventory. The trailing space in "6 " and the absence of one
        // in "7" are the pack's own naming, not a typo: a path is matched literally.
        private const string SolidWallReference = Walls + "5.prefab";
        private const string DoorWallPrefab = Walls + "1.prefab";
        private const string DoorWallAlternate = Walls + "11.prefab";
        private const string WindowNarrow = Walls + "6 .prefab";
        private const string WindowMedium = Walls + "7.prefab";
        private const string WindowWide = Walls + "8.prefab";

        /// <summary>
        /// The materials that are the DOOR, as opposed to the wall it is hung in.
        ///
        /// Prefab 1 carries four materials: "blue" and "door detail" are the leaf, "wallpaper3"
        /// and "white" are the wall shell around it. Only the first two come through; the shell
        /// is a 4 m wall and would stand through a 3 m ceiling.
        /// </summary>
        private static readonly string[] DoorParts = { "blue", "door detail", "brown", "door base" };

        /// <summary>
        /// The materials that are the WINDOW. Prefab 7 carries "1" (the frame, textured
        /// window LP 1-2_1) and "Steklo" (the glass); "wallpaper3" and "white" are again the
        /// wall shell.
        /// </summary>
        private static readonly string[] WindowParts = { "1", "Steklo" };

        [MenuItem("Catch If You Can/Modular Interior/Katalog aus GEPRUEFTEN Pfaden schreiben")]
        public static void WriteVerifiedCatalog()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== KATALOG AUS GEPRUEFTEN PFADEN ===");

            var catalog = AssetDatabase.LoadAssetAtPath<ModularInteriorCatalog>(CatalogPath);
            bool created = catalog == null;
            if (created)
            {
                catalog = ScriptableObject.CreateInstance<ModularInteriorCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.PackRootFolder = "Assets/HQ Modular House";
            catalog.PackDisplayName = "HQ Modular House Interior Pack";

            GameObject solid = Load(SolidWallReference, sb);
            GameObject door = Load(DoorWallPrefab, sb) ?? Load(DoorWallAlternate, sb);
            GameObject window = Load(WindowMedium, sb);

            if (solid == null)
            {
                sb.AppendLine();
                sb.AppendLine("ABBRUCH: ohne " + SolidWallReference + " gibt es keine Referenz, " +
                              "an der die Musterdichte gemessen werden koennte.");
                Finish(sb, catalog, created, false);
                return;
            }

            // ---- surfaces -------------------------------------------------------------------

            Material wallpaper = MaterialOn(solid, "wallpaper3", sb);
            Material white = MaterialOn(solid, "white", sb);
            Material planks = MaterialInPack("planks 1", sb);
            Material glass = window != null ? MaterialOn(window, "Steklo", sb) : null;

            // The one MEASURED number in this file. Prefab 5 is a plain wall: its two large
            // extents are the surface the texture is stretched 0..1 across, so the pattern is
            // that size divided by the material's own tiling. Everything else is derived from
            // it, and said to be derived.
            Vector2 wallPattern = MeasurePattern(solid, wallpaper, sb);
            catalog.WallSurface = new SurfaceMaterial
            {
                Material = wallpaper,
                AuthoredAcrossMetres = wallPattern,
            };

            float pixelsPerMetre = PixelsPerMetre(wallpaper, wallPattern);
            sb.AppendLine();
            sb.AppendLine(string.Format(
                "Wand    : {0}  Muster {1} m  GEMESSEN an {2}  ({3:F0} px/m)",
                Name(wallpaper), wallPattern.ToString("F2"), SolidWallReference, pixelsPerMetre));

            catalog.FloorSurface = ByTexelParity(planks, pixelsPerMetre, "Boden ", sb);
            catalog.CeilingSurface = ByTexelParity(white, pixelsPerMetre, "Decke ", sb);

            sb.AppendLine();
            sb.AppendLine("Boden und Decke sind ABGELEITET, nicht gemessen: das Paket hat keine " +
                          "Boden- und Deckenteile, an denen sich eine Dichte ablesen liesse. Sie " +
                          "bekommen dieselbe Texeldichte wie die gemessene Wand, damit eine " +
                          "1024er Textur ueberall gleich fein wirkt. Sieht es falsch aus, ist es " +
                          "im Katalog eine Zahl.");

            // ---- inserts --------------------------------------------------------------------

            catalog.DoorInsert = new StructuralInsert
            {
                Prefab = door,
                KeepMaterials = DoorParts,
            };

            catalog.WindowInsert = new StructuralInsert
            {
                Prefab = window,
                KeepMaterials = WindowParts,
            };

            sb.AppendLine();
            sb.AppendLine("Tuer    : " + Path(door) + "  behalten: " + string.Join(", ", DoorParts));
            sb.AppendLine("Fenster : " + Path(window) + "  behalten: " + string.Join(", ", WindowParts));
            sb.AppendLine();
            sb.AppendLine("Die Wandschale dieser Prefabs (wallpaper3, white) wird beim Einsetzen");
            sb.AppendLine("abgeschaltet. Sie ist 4 m hoch und stuende sonst als zweite Wand durch");
            sb.AppendLine("die 3 m Decke.");

            // ---- modules --------------------------------------------------------------------
            //
            // Recorded for reference rather than for building: CIYC generates the shell. Floor
            // and Ceiling get no entry, because the pack has no floor and no ceiling part - the
            // 36 x 57 m objects the classifier put there were demo assemblies.
            var sets = new List<ModuleSet>();
            AddSet(sets, ModuleRole.WallSolid, solid);
            AddSet(sets, ModuleRole.WallWithDoorway, door);
            AddSet(sets, ModuleRole.WallWithWindow, window);
            catalog.Modules = sets.ToArray();

            Finish(sb, catalog, created, true);
        }

        // ------------------------------------------------------------------------- helpers

        private static void Finish(StringBuilder sb, ModularInteriorCatalog catalog,
            bool created, bool wire)
        {
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            sb.AppendLine();
            sb.AppendLine((created ? "Angelegt: " : "Aktualisiert: ") + CatalogPath);

            if (wire)
            {
                var content = AssetDatabase.LoadAssetAtPath<InvestigationContentCatalog>(ContentCatalogPath);
                if (content == null)
                {
                    sb.AppendLine("WARNUNG: " + ContentCatalogPath + " gibt es nicht - der " +
                                  "Generator liest diesen Katalog also nicht.");
                }
                else
                {
                    content.ModularInterior = catalog;
                    EditorUtility.SetDirty(content);
                    AssetDatabase.SaveAssets();
                    sb.AppendLine("Verdrahtet: " + ContentCatalogPath + " -> Modular Interior");
                }

                sb.AppendLine();
                sb.AppendLine("NAECHSTER SCHRITT: Modular Interior > Build ONE Test Room.");
            }

            Debug.Log(sb.ToString());
        }

        private static GameObject Load(string path, StringBuilder sb)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null)
                sb.AppendLine("FEHLT: " + path);

            return go;
        }

        /// <summary>
        /// The material of that name ON THIS PREFAB.
        ///
        /// Not by a project-wide search: the pack holds three materials called "white", three
        /// called "blue" and nineteen called "1". Asking the piece that actually wears it is the
        /// only lookup that cannot pick the wrong one.
        /// </summary>
        private static Material MaterialOn(GameObject prefab, string name, StringBuilder sb)
        {
            var renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                Material[] materials = renderers[r].sharedMaterials;
                for (int m = 0; m < materials.Length; m++)
                {
                    if (materials[m] != null &&
                        string.Equals(materials[m].name, name, StringComparison.OrdinalIgnoreCase))
                        return materials[m];
                }
            }

            sb.AppendLine("FEHLT: Material '" + name + "' liegt nicht auf " + prefab.name);
            return null;
        }

        /// <summary>
        /// A material that no wall prefab wears - the floor's, which the pack only uses inside
        /// its demo assemblies. Searched by exact name inside the pack, and refused rather than
        /// guessed at if the name is not unique.
        /// </summary>
        private static Material MaterialInPack(string name, StringBuilder sb)
        {
            string[] guids = AssetDatabase.FindAssets("\"" + name + "\" t:Material",
                                                      new[] { "Assets/HQ Modular House" });

            var exact = new List<Material>();
            for (int i = 0; i < guids.Length; i++)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (mat != null && string.Equals(mat.name, name, StringComparison.Ordinal))
                    exact.Add(mat);
            }

            if (exact.Count == 1)
                return exact[0];

            if (exact.Count == 0)
                sb.AppendLine("FEHLT: kein Material '" + name + "' im Paket.");
            else
                sb.AppendLine("MEHRDEUTIG: " + exact.Count + " Materialien heissen '" + name +
                              "'. Keins wird genommen - im Katalog von Hand setzen.");

            return null;
        }

        /// <summary>
        /// How large one repeat of this material's pattern is, in metres, measured on the piece
        /// it is authored for: the piece's two largest extents divided by the material's tiling.
        ///
        /// <para>
        /// The two LARGEST, rather than X and Y. The pack's wall meshes are about 4 m by 4 m by
        /// a tenth of a metre, and which axis carries the height depends on whether the prefab
        /// corrects the exporter's Z-up convention. The thin axis is never the one the texture
        /// spans, and that is all this needs to know.
        /// </para>
        /// </summary>
        private static Vector2 MeasurePattern(GameObject prefab, Material material, StringBuilder sb)
        {
            if (material == null)
                return Vector2.zero;

            Vector2 tiling = material.HasProperty("_BaseMap")
                ? material.GetTextureScale("_BaseMap")
                : Vector2.one;

            if (tiling.x <= 0f || tiling.y <= 0f)
                tiling = Vector2.one;

            var filters = prefab.GetComponentsInChildren<MeshFilter>(true);
            Vector3 largest = Vector3.zero;
            for (int i = 0; i < filters.Length; i++)
            {
                if (filters[i].sharedMesh == null)
                    continue;

                Vector3 size = Vector3.Scale(filters[i].sharedMesh.bounds.size,
                                             filters[i].transform.lossyScale);
                if (size.sqrMagnitude > largest.sqrMagnitude)
                    largest = size;
            }

            float a = Mathf.Max(largest.x, Mathf.Max(largest.y, largest.z));
            float c = Mathf.Min(largest.x, Mathf.Min(largest.y, largest.z));
            float b = largest.x + largest.y + largest.z - a - c;

            if (a < 0.5f || b < 0.5f)
            {
                sb.AppendLine("WARNUNG: " + prefab.name + " misst " + largest.ToString("F2") +
                              " - daran laesst sich keine Musterdichte ablesen.");
                return Vector2.zero;
            }

            return new Vector2(b / tiling.x, a / tiling.y);
        }

        private static float PixelsPerMetre(Material material, Vector2 pattern)
        {
            if (material == null || pattern.x <= 0.001f)
                return 0f;

            Texture texture = material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : null;
            if (texture == null)
                return 0f;

            return texture.width / pattern.x;
        }

        /// <summary>
        /// The same texel density as the measured wall, so a 1024 texture reads equally fine on
        /// every surface. Derived, and reported as derived - the pack ships no floor or ceiling
        /// part to measure one against.
        /// </summary>
        private static SurfaceMaterial ByTexelParity(Material material, float pixelsPerMetre,
            string role, StringBuilder sb)
        {
            if (material == null)
            {
                sb.AppendLine(role + "  : <keins> - bleibt neutral grau");
                return default;
            }

            Texture texture = material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : null;
            if (texture == null || pixelsPerMetre <= 0.001f)
            {
                sb.AppendLine(role + "  : " + material.name + "  keine Dichte ableitbar, wird " +
                              "benutzt wie authored");
                return new SurfaceMaterial { Material = material };
            }

            float metres = texture.width / pixelsPerMetre;
            sb.AppendLine(string.Format("{0}  : {1}  Muster {2:F2} m  ABGELEITET aus {3}x{4}",
                role, material.name, metres, texture.width, texture.height));

            return new SurfaceMaterial
            {
                Material = material,
                AuthoredAcrossMetres = new Vector2(metres, metres),
            };
        }

        private static void AddSet(List<ModuleSet> sets, ModuleRole role, GameObject prefab)
        {
            if (prefab == null)
                return;

            sets.Add(new ModuleSet
            {
                Role = role,
                Categories = new RoomCategory[0],
                Variants = new[] { prefab },
                ModuleSize = Vector3.zero,
            });
        }

        private static string Name(Material m) => m != null ? m.name : "<keins>";

        private static string Path(GameObject go) =>
            go != null ? AssetDatabase.GetAssetPath(go) : "<keins>";
    }
}
