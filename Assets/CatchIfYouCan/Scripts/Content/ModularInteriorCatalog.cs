using System;
using System.Collections.Generic;
using CatchIfYouCan.Procedural;
using UnityEngine;

namespace CatchIfYouCan.Content
{
    /// <summary>The structural job a modular piece does in a room shell.</summary>
    /// <remarks>
    /// Append-only. The role is what the builder asks for; which mesh answers is content.
    /// That separation is the whole point: the deterministic layout says "this wall carries a
    /// door", and the catalog decides which vendor mesh a doorway wall is made of. Swapping
    /// the pack changes the second half only.
    /// </remarks>
    public enum ModuleRole
    {
        Floor = 0,
        Ceiling = 1,
        WallSolid = 2,
        WallWithDoorway = 3,
        WallWithWindow = 4,
        CornerTrim = 5,
        Baseboard = 6,
        Stairs = 7,
    }

    /// <summary>
    /// One material from the pack, and the texture density it is to be shown at.
    ///
    /// <para>
    /// The density is not decoration. The pack normalises its UVs PER PIECE - every wall maps
    /// its texture 0..1 across its own width, so the same material appears at a different size
    /// on every prefab (measured: 0.25 U/m on the 3.95 m piece, 0.10 U/m on the 11.90 m one).
    /// Those numbers cannot be inherited. Generated geometry writes its UVs in metres, so the
    /// density has to be applied here, once, as the material's tiling.
    /// </para>
    /// <para>
    /// Taking prefab 5 as the reference for wallpaper3: 1.5 tiling over a 3.95 m piece is
    /// 0.3797 repeats per metre across, and 1.5 over 4.00 m is 0.3750 up. A 6 x 3 m wall then
    /// spans U 2.278 and V 1.125 - the same pattern size as on the vendor piece beside it, with
    /// no stretching and no seam. See Docs/HQ_MODULAR_MIGRATION.md.
    /// </para>
    /// </summary>
    [Serializable]
    public struct SurfaceMaterial
    {
        public Material Material;

        [Tooltip("Texturwiederholungen pro Meter, gemessen am Paket. 0 heisst: unbekannt, " +
                 "dann wird das Material so benutzt, wie es authored ist.")]
        public Vector2 RepeatsPerMetre;

        public bool IsSet => Material != null;
    }

    /// <summary>One role, the room categories it serves, and the meshes that can play it.</summary>
    [Serializable]
    public struct ModuleSet
    {
        public ModuleRole Role;

        /// <summary>Empty means "any room". A non-empty list restricts the set to those.</summary>
        public RoomCategory[] Categories;

        /// <summary>
        /// Interchangeable meshes for this role. More than one gives variety; which one a
        /// given wall gets is derived from the room's identity, never rolled - see
        /// <see cref="ModularRoomBuilder"/>.
        /// </summary>
        public GameObject[] Variants;

        /// <summary>
        /// The footprint one piece covers, in metres. A 4 m room needs two 2 m wall pieces.
        /// Zero on either axis means "one piece stretches the whole span", which is what a
        /// non-modular prefab does.
        /// </summary>
        public Vector3 ModuleSize;
    }

    /// <summary>
    /// The production source of house interior geometry.
    ///
    /// This replaces the old whole-room prefab path. That path asked a RoomDefinition for a
    /// finished Room_* prefab and instantiated it; every room of a category looked identical,
    /// and the doorway was wherever the prefab author had put it, not where the layout said.
    /// A modular catalog inverts that: the layout decides the shell, the catalog supplies the
    /// pieces, and a doorway is open geometry because the builder placed a doorway module
    /// there rather than a solid wall with a door object parked in front of it.
    ///
    /// The catalog holds no vendor-specific knowledge. It is filled by an editor tool from
    /// whatever pack is imported; nothing here names a publisher, a folder or a mesh.
    /// </summary>
    [CreateAssetMenu(menuName = "Catch If You Can/Modular Interior Catalog",
                     fileName = "ModularInteriorCatalog")]
    public class ModularInteriorCatalog : ScriptableObject
    {
        [Tooltip("Where the imported pack lives, for validation messages only. Nothing loads " +
                 "by this path - every reference below is a real object reference.")]
        public string PackRootFolder;

        [Tooltip("A human-readable name for the pack this catalog was built from.")]
        public string PackDisplayName;

        public ModuleSet[] Modules = new ModuleSet[0];

        [Header("Room Surfaces")]
        [Tooltip("Die Materialien fuer die vom Code gebaute Huelle. Das Paket liefert KEINE " +
                 "Boden- und Deckenteile - null davon unter interior/ - und seine Waende sind " +
                 "kein Kit: die Pivots liegen bis zu 29 m neben dem Mesh. Deshalb baut CIYC " +
                 "die Struktur und das Paket liefert die Oberflaeche.")]
        public SurfaceMaterial WallSurface;
        public SurfaceMaterial FloorSurface;
        public SurfaceMaterial CeilingSurface;

        /// <summary>
        /// The roles a house cannot be built without.
        ///
        /// <para>
        /// Floor and Ceiling are NOT among them, and that is a measurement rather than a
        /// preference: the pack contains zero floor and zero ceiling parts - its own demo
        /// builds both from a scaled Unity Plane. Requiring them made every catalog built from
        /// this pack report itself invalid forever, which is a false failure, and a validator
        /// that cries wolf is one nobody reads. CIYC generates both surfaces at exact size with
        /// its own UVs; what the pack is asked for is the surface material and the small pieces
        /// that genuinely fit - the door leaf and the window insert.
        /// </para>
        /// <para>
        /// WallSolid and WallWithDoorway stay optional-to-supply too, for the same reason: the
        /// builder generates the wall shell. They are listed because a catalog that supplies
        /// NEITHER a wall variant nor a doorway variant has nothing of the pack in it at all,
        /// and that is worth saying out loud. See Docs/HQ_MODULAR_MIGRATION.md.
        /// </para>
        /// </summary>
        public static readonly ModuleRole[] RequiredStructuralRoles =
        {
            ModuleRole.WallWithDoorway,
        };

        /// <summary>
        /// The meshes that can play this role in this room, most specific first: a set naming
        /// the category wins over a set naming none. Returns an empty array rather than null.
        /// </summary>
        public GameObject[] FindVariants(ModuleRole role, RoomCategory category)
        {
            GameObject[] generic = null;

            for (int i = 0; i < Modules.Length; i++)
            {
                if (Modules[i].Role != role)
                    continue;

                var categories = Modules[i].Categories;
                if (categories != null && categories.Length > 0)
                {
                    for (int c = 0; c < categories.Length; c++)
                    {
                        if (categories[c] == category)
                            return Modules[i].Variants ?? EmptyVariants;
                    }

                    continue;
                }

                if (generic == null)
                    generic = Modules[i].Variants;
            }

            return generic ?? EmptyVariants;
        }

        /// <summary>The footprint of the pieces that play this role, or zero if unknown.</summary>
        public Vector3 FindModuleSize(ModuleRole role, RoomCategory category)
        {
            Vector3 generic = Vector3.zero;

            for (int i = 0; i < Modules.Length; i++)
            {
                if (Modules[i].Role != role)
                    continue;

                var categories = Modules[i].Categories;
                if (categories != null && categories.Length > 0)
                {
                    for (int c = 0; c < categories.Length; c++)
                    {
                        if (categories[c] == category)
                            return Modules[i].ModuleSize;
                    }

                    continue;
                }

                if (generic == Vector3.zero)
                    generic = Modules[i].ModuleSize;
            }

            return generic;
        }

        /// <summary>
        /// Whether this catalog can build a house at all, and if not, exactly what is missing.
        ///
        /// This is deliberately strict about nulls. A ModuleSet with a null entry in Variants
        /// is the shape that produces a room with one wall silently absent - it looks like a
        /// working catalog right up until that wall is the one the player walks at.
        /// </summary>
        public bool TryValidate(out string error)
        {
            var problems = new List<string>();

            for (int r = 0; r < RequiredStructuralRoles.Length; r++)
            {
                var role = RequiredStructuralRoles[r];
                if (!HasAnyVariant(role))
                    problems.Add("no module set supplies " + role);
            }

            for (int i = 0; i < Modules.Length; i++)
            {
                var set = Modules[i];
                if (set.Variants == null || set.Variants.Length == 0)
                {
                    problems.Add(set.Role + " set " + i + " has no variants");
                    continue;
                }

                for (int v = 0; v < set.Variants.Length; v++)
                {
                    if (set.Variants[v] == null)
                        problems.Add(set.Role + " set " + i + " variant " + v + " is null");
                }
            }

            error = problems.Count == 0 ? null : string.Join("; ", problems.ToArray());
            return problems.Count == 0;
        }

        private bool HasAnyVariant(ModuleRole role)
        {
            for (int i = 0; i < Modules.Length; i++)
            {
                if (Modules[i].Role != role)
                    continue;

                var variants = Modules[i].Variants;
                if (variants == null)
                    continue;

                for (int v = 0; v < variants.Length; v++)
                {
                    if (variants[v] != null)
                        return true;
                }
            }

            return false;
        }

        private static readonly GameObject[] EmptyVariants = new GameObject[0];
    }
}
