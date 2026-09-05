using CatchIfYouCan.Content;
using CatchIfYouCan.Procedural.Deterministic;
using UnityEngine;

namespace CatchIfYouCan.Procedural
{
    /// <summary>
    /// Turns one logical room into modular geometry.
    ///
    /// It consumes the layout and never adds to it. Everything it needs - the room's size,
    /// its category, which of its four walls carry a door, which are open to the outside -
    /// is already in <see cref="LayoutRoom"/>, decided in Stage A and folded into the layout
    /// hash. This class reads those fields and places meshes. It cannot move a room, change a
    /// door, or pick a different neighbour, because it is never asked to.
    ///
    /// That is why the layout hash does not change when the art does.
    ///
    /// <para>
    /// Where a choice remains - which of three interchangeable wall meshes this particular
    /// wall gets - it is DERIVED, not rolled. Drawing from a CiycRandom stream would advance
    /// that stream and change every later draw, which is the one way a visual pass can reach
    /// back into generation. So the variant index comes from an FNV hash of the room's id,
    /// the role and the direction: stable for a given room, identical on every machine, and
    /// invisible to the streams. The house lighting director already works this way.
    /// </para>
    /// </summary>
    public static class ModularRoomBuilder
    {
        /// <summary>
        /// Thicknesses. Structural, not artistic: they decide where a collider stands, so they
        /// live here rather than in a catalog that art can edit.
        /// </summary>
        public const float WallThickness = 0.15f;
        public const float FloorThickness = 0.20f;
        public const float CeilingThickness = 0.20f;

        /// <summary>
        /// The doorway the project already builds, unchanged. The pack's own opening is
        /// 1.25 x 2.60 and would fit its door leaf untouched, but swapping to it is a later
        /// phase - this one changes how the structure is made, not what size it is.
        /// </summary>
        public const float DoorWidth = 1.20f;
        public const float DoorHeight = 2.20f;

        /// <summary>
        /// The window opening, taken from the pack's own measured window 7 (2.05 x 0.90, sill
        /// 1.55). Head height is 1.55 + 0.90 = 2.45 m, which leaves 0.55 m of wall under a
        /// 3.00 m ceiling. Windows 6 and 8 fit the same way; window 9 does not - its sill is at
        /// 2.00 and it reaches 3.25 m, straight through the ceiling. Docs/HQ_MODULAR_MIGRATION.md
        /// carries the measurements.
        /// </summary>
        public const float WindowWidth = 2.05f;
        public const float WindowHeight = 0.90f;
        public const float WindowSill = 1.55f;

        /// <summary>
        /// Builds the room's shell from generated geometry.
        ///
        /// Everything it needs is already in the LayoutRoom, decided in Stage A and folded into
        /// the layout hash: the size, the category, which of the four walls carry a door. This
        /// reads those fields and makes meshes. It cannot move a room, change a door or pick a
        /// neighbour, because it is never asked to - which is why the layout hash does not move
        /// when the art does.
        /// </summary>
        public static GameObject Build(LayoutRoom room, Vector3 worldPosition, Transform parent,
            ModularInteriorCatalog catalog, out string error)
        {
            error = null;

            var size = new Vector3(
                Quantize.Metres(room.SizeMm.X),
                Quantize.Metres(room.SizeMm.Y),
                Quantize.Metres(room.SizeMm.Z));

            if (size.x < 0.5f || size.y < 0.5f || size.z < 0.5f)
            {
                error = "die Raumgroesse " + size + " ist zu klein zum Bauen";
                return null;
            }

            var roomRoot = new GameObject($"Room_{room.Category}_{room.RoomId}");
            roomRoot.transform.SetParent(parent, false);
            roomRoot.transform.position = worldPosition;

            BuildFloor(roomRoot.transform, size, catalog);
            BuildCeiling(roomRoot.transform, size, catalog);

            for (int d = 0; d < Directions.Cardinal.Length; d++)
                BuildWall(roomRoot.transform, room, Directions.Cardinal[d], size, catalog);

            var module = roomRoot.GetComponent<RoomModule>();
            if (module == null)
                module = roomRoot.AddComponent<RoomModule>();

            module.Configure(room.Category, new Bounds(Vector3.up * (size.y * 0.5f), size), room.RoomId);
            module.CollectSockets();

            return roomRoot;
        }

        // ------------------------------------------------------------------ surfaces

        private static void BuildFloor(Transform parent, Vector3 size, ModularInteriorCatalog catalog)
        {
            var mesh = StructuralMeshFactory.Floor(size.x, size.z, FloorThickness);
            var go = Piece(parent, "Floor", mesh, FloorMaterial(catalog));
            go.transform.localPosition = Vector3.zero;

            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, -FloorThickness * 0.5f, 0f);
            box.size = new Vector3(size.x, FloorThickness, size.z);
        }

        private static void BuildCeiling(Transform parent, Vector3 size, ModularInteriorCatalog catalog)
        {
            var mesh = StructuralMeshFactory.Ceiling(size.x, size.z, CeilingThickness);
            var go = Piece(parent, "Ceiling", mesh, CeilingMaterial(catalog));
            go.transform.localPosition = new Vector3(0f, size.y, 0f);

            // No collider. The player cannot reach it and every one that exists is one the
            // physics engine tests against for nothing.
        }

        // --------------------------------------------------------------------- walls

        private static void BuildWall(Transform parent, LayoutRoom room,
            SocketDirection direction, Vector3 size, ModularInteriorCatalog catalog)
        {
            // The layout decides what this wall is. A door connection means a real hole, not a
            // solid wall with a door drawn on it.
            bool hasDoor = room.HasDoor(direction);

            // A window only where there is no door and the wall faces outside. Derived from the
            // room's identity, never rolled: a draw from a CiycRandom stream would advance that
            // stream and reach back into generation.
            bool hasWindow = !hasDoor && room.IsOpen(direction) && WantsWindow(room, direction);

            ModuleRole role = hasDoor ? ModuleRole.WallWithDoorway
                            : hasWindow ? ModuleRole.WallWithWindow
                            : ModuleRole.WallSolid;

            bool alongX = direction == SocketDirection.North || direction == SocketDirection.South;
            float span = alongX ? size.x : size.z;

            Mesh mesh;
            if (hasDoor)
                mesh = StructuralMeshFactory.WallWithOpening(span, size.y, WallThickness,
                    DoorWidth, DoorHeight, 0f);
            else if (hasWindow)
                mesh = StructuralMeshFactory.WallWithOpening(span, size.y, WallThickness,
                    WindowWidth, WindowHeight, WindowSill);
            else
                mesh = StructuralMeshFactory.SolidWall(span, size.y, WallThickness);

            var go = Piece(parent, role + "_" + direction, mesh, WallMaterial(catalog));

            // The wall's own space is centred on X across its span and centred on Z across its
            // thickness, rising from y = 0. So it goes on the wall line with no correction, and
            // the yaw turns its length along the right axis.
            switch (direction)
            {
                case SocketDirection.North:
                    go.transform.localPosition = new Vector3(0f, 0f, size.z * 0.5f - WallThickness * 0.5f);
                    go.transform.localRotation = Quaternion.identity;
                    break;
                case SocketDirection.South:
                    go.transform.localPosition = new Vector3(0f, 0f, -size.z * 0.5f + WallThickness * 0.5f);
                    go.transform.localRotation = Quaternion.identity;
                    break;
                case SocketDirection.East:
                    go.transform.localPosition = new Vector3(size.x * 0.5f - WallThickness * 0.5f, 0f, 0f);
                    go.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                    break;
                default:
                    go.transform.localPosition = new Vector3(-size.x * 0.5f + WallThickness * 0.5f, 0f, 0f);
                    go.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                    break;
            }

            AddWallColliders(go, hasDoor, hasWindow, span, size.y);

            // The pack's contribution to a wall: the leaf that swings in a doorway, the frame
            // and glass that sit in a window. Never a whole vendor wall - its pivot can be 29 m
            // from its own mesh and its UVs are normalised to its own width.
            if (hasDoor)
                AddInsert(go.transform, catalog, ModuleRole.WallWithDoorway, room, direction,
                          new Vector3(0f, 0f, 0f));
            else if (hasWindow)
                AddInsert(go.transform, catalog, ModuleRole.WallWithWindow, room, direction,
                          new Vector3(0f, WindowSill + WindowHeight * 0.5f, 0f));
        }

        /// <summary>
        /// Puts one vendor piece into the opening this wall already has.
        ///
        /// <para>
        /// Instantiated as a child of the generated wall, so it inherits the wall's placement
        /// and rotation and cannot drift from the hole it belongs to. Every collider it brings
        /// is removed: gameplay collision is the generated boxes' job, and a MeshCollider across
        /// vendor geometry is the expensive way to get the same answer wrong. Shadow casting on
        /// a decorative insert is switched off for the same reason - it is a door leaf, not a
        /// wall.
        /// </para>
        /// </summary>
        private static void AddInsert(Transform wall, ModularInteriorCatalog catalog,
            ModuleRole role, LayoutRoom room, SocketDirection direction, Vector3 localPosition)
        {
            if (catalog == null)
                return;

            GameObject prefab = Pick(catalog.FindVariants(role, room.Category),
                                     room.RoomId, (int)role, (int)direction);
            if (prefab == null)
                return;

            GameObject insert = Object.Instantiate(prefab, wall);
            insert.name = role + "_Insert";
            insert.transform.localPosition = localPosition;
            insert.transform.localRotation = Quaternion.identity;

            // Switched OFF rather than destroyed. A disabled collider contributes no physics
            // geometry, which is the whole point, and it does it identically in the editor and
            // in a build - Destroy is deferred and DestroyImmediate is edit-mode-only, and
            // picking between them by context is exactly how this project once got an editor
            // house and a device house that differed.
            insert.GetComponentsInChildren(true, _insertColliders);
            for (int i = 0; i < _insertColliders.Count; i++)
                _insertColliders[i].enabled = false;
            _insertColliders.Clear();

            insert.GetComponentsInChildren(true, _insertRenderers);
            for (int i = 0; i < _insertRenderers.Count; i++)
            {
                _insertRenderers[i].shadowCastingMode =
                    UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            _insertRenderers.Clear();
        }

        private static readonly System.Collections.Generic.List<Collider> _insertColliders =
            new System.Collections.Generic.List<Collider>(8);
        private static readonly System.Collections.Generic.List<Renderer> _insertRenderers =
            new System.Collections.Generic.List<Renderer>(8);

        /// <summary>
        /// Collision that matches the geometry, taken from the same sections the mesh was built
        /// from rather than computed a second time. A doorway that is open to the eye and shut
        /// to the body is the failure this exists to prevent, and computing the rectangles
        /// twice is how it happens.
        /// </summary>
        private static void AddWallColliders(GameObject go, bool hasDoor, bool hasWindow,
            float span, float height)
        {
            // Only a doorway is cut. A window is not a way through, so a window wall gets ONE
            // box across the whole span: correct, cheaper than three, and splitting it around
            // the opening would let the player climb through the window.
            bool oneBoxAcross = !hasDoor || hasWindow;
            if (oneBoxAcross)
            {
                var solid = go.AddComponent<BoxCollider>();
                solid.center = new Vector3(0f, height * 0.5f, 0f);
                solid.size = new Vector3(span, height, WallThickness);
                return;
            }

            var sections = StructuralMeshFactory.Sections(span, height, WallThickness,
                DoorWidth, DoorHeight, 0f);

            if (!sections.HasOpening)
            {
                var fallback = go.AddComponent<BoxCollider>();
                fallback.center = new Vector3(0f, height * 0.5f, 0f);
                fallback.size = new Vector3(span, height, WallThickness);
                return;
            }

            AddSectionCollider(go, sections.Left);
            AddSectionCollider(go, sections.Right);
            AddSectionCollider(go, sections.Header);
        }

        private static void AddSectionCollider(GameObject go, Bounds section)
        {
            if (section.size.x < 0.001f || section.size.y < 0.001f)
                return;

            var box = go.AddComponent<BoxCollider>();
            box.center = section.center;
            box.size = section.size;
        }

        // ----------------------------------------------------------------- materials

        // Three materials for the whole house, resolved once. One per wall would be forty
        // materials in a ten-room house and forty separate draw calls to go with them - so
        // these are shared across every room, and the density below is applied once rather
        // than per surface.
        private static Material _wall;
        private static Material _floor;
        private static Material _ceiling;

        private static Material WallMaterial(ModularInteriorCatalog catalog) =>
            Surface(ref _wall, catalog != null ? catalog.WallSurface : default,
                    new Color(0.72f, 0.70f, 0.67f), "CIYC_Wall");

        private static Material FloorMaterial(ModularInteriorCatalog catalog) =>
            Surface(ref _floor, catalog != null ? catalog.FloorSurface : default,
                    new Color(0.42f, 0.39f, 0.36f), "CIYC_Floor");

        private static Material CeilingMaterial(ModularInteriorCatalog catalog) =>
            Surface(ref _ceiling, catalog != null ? catalog.CeilingSurface : default,
                    new Color(0.86f, 0.86f, 0.84f), "CIYC_Ceiling");

        /// <summary>A fresh process has resolved nothing. Unity keeps statics across play mode.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSurfaces()
        {
            _wall = null;
            _floor = null;
            _ceiling = null;
        }

        /// <summary>
        /// The pack's material for this surface, at the density it was measured at - or the
        /// neutral stand-in if the catalog names none, or names one that cannot be drawn.
        ///
        /// <para>
        /// The vendor material is never modified. A COPY carries the tiling, because the pack
        /// normalises its UVs per piece while generated geometry writes them in metres: the two
        /// cannot agree unless one side is rescaled, and it must not be the side that is
        /// somebody's purchased asset. One copy per surface for the whole house, so this is
        /// three materials, not three per room.
        /// </para>
        /// </summary>
        private static Material Surface(ref Material slot, Content.SurfaceMaterial surface,
            Color fallbackColour, string name)
        {
            if (slot != null)
                return slot;

            if (surface.IsSet && IsDrawable(surface.Material, name))
            {
                // Density of zero means "unknown": use the material exactly as authored rather
                // than inventing a number. Applying a zero would collapse the texture to one
                // texel, which reads as a flat colour and looks like a missing texture.
                if (surface.RepeatsPerMetre.x <= 0f || surface.RepeatsPerMetre.y <= 0f)
                {
                    slot = surface.Material;
                    return slot;
                }

                slot = new Material(surface.Material)
                {
                    name = name + "_" + surface.Material.name + "_perMetre"
                };

                if (slot.HasProperty(BaseMapId)) slot.SetTextureScale(BaseMapId, surface.RepeatsPerMetre);
                if (slot.HasProperty(BumpMapId)) slot.SetTextureScale(BumpMapId, surface.RepeatsPerMetre);
                if (slot.HasProperty(MainTexId)) slot.SetTextureScale(MainTexId, surface.RepeatsPerMetre);
                return slot;
            }

            return Neutral(ref slot, fallbackColour, "CIYC_Raw" + name);
        }

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int BumpMapId = Shader.PropertyToID("_BumpMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        /// <summary>
        /// Whether this material will actually draw, rather than draw magenta.
        ///
        /// <para>
        /// Four ways a material reaches here unusable, and all four look identical on screen:
        /// a null shader, a shader the platform does not support, Unity's internal error shader
        /// (which IS the magenta), and an HDRP shader in a URP project. None of them throws and
        /// none of them logs, so each is checked and named. Refusing gives the neutral stand-in,
        /// which is a dull grey room - wrong, but legibly wrong.
        /// </para>
        /// </summary>
        private static bool IsDrawable(Material material, string role)
        {
            Shader shader = material.shader;

            if (shader == null)
            {
                Core.CIYCLog.Error("[CIYC][House] " + role + ": Material '" + material.name +
                                   "' hat keinen Shader. Es wird NICHT benutzt.");
                return false;
            }

            string shaderName = shader.name ?? string.Empty;

            if (!shader.isSupported)
            {
                Core.CIYCLog.Error("[CIYC][House] " + role + ": Shader '" + shaderName +
                                   "' wird auf dieser Plattform nicht unterstuetzt. Das " +
                                   "Material wird NICHT benutzt.");
                return false;
            }

            if (shaderName.IndexOf("InternalErrorShader", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                shaderName.StartsWith("Hidden/", System.StringComparison.Ordinal))
            {
                Core.CIYCLog.Error("[CIYC][House] " + role + ": Shader '" + shaderName +
                                   "' ist Unitys Fehler-Shader - genau das, was als magenta " +
                                   "Flaeche erscheint. Das Material wird NICHT benutzt.");
                return false;
            }

            if (shaderName.IndexOf("HDRP", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                shaderName.IndexOf("High Definition", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Core.CIYCLog.Error("[CIYC][House] " + role + ": Shader '" + shaderName +
                                   "' ist HDRP. Dieses Projekt ist URP - HDRP-Shader zeichnen " +
                                   "hier magenta. Das Material wird NICHT benutzt.");
                return false;
            }

            return true;
        }

        private static Material Neutral(ref Material slot, Color colour, string name)
        {
            if (slot != null)
                return slot;

            // Never Shader.Find("Standard"): it resolves everywhere and draws solid magenta
            // under URP. Ask for the project's lit shader and accept null - a missing object is
            // better than a magenta one.
            var shader = Art.CiycShaders.FindLit();
            if (shader == null)
            {
                Core.CIYCLog.Error("[CIYC][House] Kein URP-Lit-Shader gefunden. Die Rohstruktur " +
                                   "bleibt ohne Material, statt magenta zu werden.");
                return null;
            }

            slot = new Material(shader) { name = name };
            slot.color = colour;
            return slot;
        }

        private static GameObject Piece(Transform parent, string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            var renderer = go.AddComponent<MeshRenderer>();
            if (material != null)
                renderer.sharedMaterial = material;

            return go;
        }

        private static GameObject Pick(GameObject[] variants, int roomId, int role, int index)
        {
            if (variants == null || variants.Length == 0)
                return null;

            if (variants.Length == 1)
                return variants[0];

            var hash = Fnv1a64.Create();
            hash.WriteInt32(roomId);
            hash.WriteInt32(role);
            hash.WriteInt32(index);

            int pick = (int)(hash.Value % (ulong)variants.Length);
            return variants[pick];
        }

        private static bool WantsWindow(LayoutRoom room, SocketDirection direction)
        {
            if (room.Category == RoomCategory.Basement || room.Category == RoomCategory.Storage)
                return false;

            var hash = Fnv1a64.Create();
            hash.WriteInt32(room.RoomId);
            hash.WriteInt32((int)direction);
            hash.WriteString("window");
            return (hash.Value & 1UL) == 1UL;
        }

   }
}
