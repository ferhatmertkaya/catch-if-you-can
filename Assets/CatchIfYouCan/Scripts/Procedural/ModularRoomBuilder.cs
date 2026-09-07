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
        /// <summary>
        /// The pack's own measured door opening, 1.25 x 2.60. The project used to build 1.20 x
        /// 2.20 here; matching the pack means its door leaf drops in at authored scale instead
        /// of being squeezed, and 2.60 under a 3.00 m ceiling still leaves 0.40 m of lintel.
        /// Docs/HQ_MODULAR_MIGRATION.md marks both numbers negotiable: they are private to
        /// Stage B, outside the engine-free assembly, and absent from the layout hash.
        /// </summary>
        public const float DoorWidth = 1.25f;
        public const float DoorHeight = 2.60f;

        /// <summary>
        /// The window opening, taken from the pack's own measured window 7 (2.05 x 0.90, sill
        /// 1.55). Head height is 1.55 + 0.90 = 2.45 m, which leaves 0.55 m of wall under a
        /// 3.00 m ceiling. Windows 6 and 8 fit the same way; window 9 does not - its sill is at
        /// 2.00 and it reaches 3.25 m, straight through the ceiling. Docs/HQ_MODULAR_MIGRATION.md
        /// carries the measurements.
        /// </summary>
        public const float WindowWidth = 2.05f;
        public const float WindowHeight = 0.90f;
        // 0.90 m, not 1.55. The sill was set from the vendor wall the window was extracted
        // from, and that piece is 4 m tall - under this project's 3.00 m ceiling the opening
        // ran 1.55 to 2.45 and left 0.55 m of wall above it, which reads as a window near the
        // ceiling. A residential sill is 0.85 to 1.00 m; 0.90 puts the head at 1.80 m, just
        // above eye level at 1.68.
        public const float WindowSill = 0.90f;

        /// <summary>
        /// The clear height a generated room is built to, used to check that an insert does not
        /// reach through the ceiling. Read from the same place the rooms are sized from.
        /// </summary>
        public const float CeilingClearance = 3.00f;

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
            // Which outside walls get a window is DERIVED from the room's identity here, never
            // rolled: a draw from a CiycRandom stream would advance it and reach back into
            // generation. Hand authoring passes its own mask to the overload below instead.
            int windowMask = 0;
            for (int d = 0; d < Directions.Cardinal.Length; d++)
            {
                var dir = Directions.Cardinal[d];
                if (!room.HasDoor(dir) && room.IsOpen(dir) && WantsWindow(room, dir))
                    windowMask |= LayoutRoom.DirectionMask(dir);
            }

            return Build(room, worldPosition, parent, catalog, windowMask, out error);
        }

        /// <summary>
        /// The same room, with the windows named explicitly rather than derived.
        ///
        /// This is the entry point for hand authoring: a person deciding which wall gets a
        /// window is making a choice, and a choice does not belong in a hash of anything.
        /// </summary>
        public static GameObject Build(LayoutRoom room, Vector3 worldPosition, Transform parent,
            ModularInteriorCatalog catalog, int windowMask, out string error)
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
                BuildWall(roomRoot.transform, room, Directions.Cardinal[d], size, catalog, windowMask);

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
            SocketDirection direction, Vector3 size, ModularInteriorCatalog catalog, int windowMask)
        {
            // The layout decides what this wall is. A door connection means a real hole, not a
            // solid wall with a door drawn on it.
            bool hasDoor = room.HasDoor(direction);
            bool hasWindow = !hasDoor && (windowMask & LayoutRoom.DirectionMask(direction)) != 0;

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
            if (hasDoor && catalog != null)
                AddInsert(go.transform, catalog.DoorInsert, "Door",
                          new Vector3(0f, DoorHeight * 0.5f, 0f));
            else if (hasWindow && catalog != null)
                AddInsert(go.transform, catalog.WindowInsert, "Window",
                          new Vector3(0f, WindowSill + WindowHeight * 0.5f, 0f));
        }

        /// <summary>
        /// Puts one vendor piece into the opening this wall already has.
        ///
        /// <para>
        /// The pack ships no door leaf and no window as objects of their own: each is a child of
        /// a whole 4 m wall prefab that carries its own wallpaper. Instantiating one of those
        /// into a 3 m room would put a second wall through the ceiling, so the prefab is reduced
        /// to the parts that are the insert - identified by the MATERIALS they carry, because
        /// the child objects are numbered and the materials are named.
        /// </para>
        /// <para>
        /// Instantiated as a child of the generated wall, so it inherits the wall's placement
        /// and rotation and cannot drift from the hole it belongs to. Every collider it brings
        /// is switched off: gameplay collision is the generated boxes' job, and a MeshCollider
        /// across vendor geometry is the expensive way to get the same answer wrong. Shadow
        /// casting goes with it - a door leaf is not a wall.
        /// </para>
        /// </summary>
        private static void AddInsert(Transform wall, Content.StructuralInsert insert,
            string role, Vector3 localPosition)
        {
            if (!insert.IsSet)
                return;

            GameObject go = Object.Instantiate(insert.Prefab, wall);
            go.name = role + "_Insert";
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.Euler(insert.LocalEuler);

            int kept = KeepOnlyInsertParts(go, insert.KeepMaterials);
            if (kept == 0)
            {
                Core.CIYCLog.Error("[CIYC][House] " + role + ": am Prefab '" + insert.Prefab.name +
                                   "' traegt kein Teil eines der Materialien " +
                                   Join(insert.KeepMaterials) + ". Es waere die ganze " +
                                   "Vendor-Wand eingesetzt worden - das Teil wird stattdessen " +
                                   "weggelassen.");

                // Switched off, not destroyed. Destroy is deferred and refuses outright in edit
                // mode, which is exactly where the one-room test tool builds - so destroying
                // here would throw in the editor and merely take a frame in a build. One
                // behaviour, both modes.
                go.SetActive(false);
                return;
            }

            OrientUpright(go.transform, role);

            // Placed by the MESH, never by the pivot.
            //
            // This pack's pivots sit 13 to 40 metres from the geometry they belong to - the
            // inventory reads "Pivot 32.5 m" for the door wall and "31.4 m" for the window -
            // because these were exported from one authored apartment and every piece kept that
            // scene's origin. Setting localPosition puts the PIVOT there, so the door itself
            // landed tens of metres away: on screen, a door frame somewhere near the ceiling and
            // a window above it.
            //
            // So the wanted point is where the kept geometry's centre has to end up, and the
            // transform is offset by whatever it takes to put it there. Measured after the
            // orientation, because turning the piece upright moves its centre too.
            Vector3 target = localPosition + insert.LocalOffset;
            string source = insert.Prefab != null ? insert.Prefab.name : "<null>";

            if (!TryMeasureInSpace(go.transform, wall, out Bounds placed))
            {
                // NOT placed by the pivot. This pack's pivots sit 13 to 40 m from their own
                // geometry, so falling back to one does not put the door roughly right - it puts
                // it tens of metres away, which on screen is a door frame near the ceiling. An
                // insert that cannot be measured is refused, and the prefab is named.
                Core.CIYCLog.Error("[CIYC][House][Insert] role=" + role +
                                   " prefab=" + source +
                                   " measured=NO kept=" + kept +
                                   " -> REFUSED. Ohne Messung waere er ueber seinen Pivot " +
                                   "gesetzt worden, und der liegt in diesem Paket bis zu 40 m " +
                                   "neben der eigenen Geometrie. Die Oeffnung bleibt frei.");
                go.SetActive(false);
                return;
            }

            go.transform.localPosition = target - placed.center;

            // Measured again AFTER the move, because what matters is where it ended up, not
            // what it was asked to do.
            float bottom, top;
            if (TryMeasureInSpace(go.transform, wall, out Bounds finalBounds))
            {
                bottom = finalBounds.min.y;
                top = finalBounds.max.y;
            }
            else
            {
                bottom = float.NaN;
                top = float.NaN;
            }

            float wantedTop = role == "Door" ? DoorHeight : WindowSill + WindowHeight;
            float wantedBottom = role == "Door" ? 0f : WindowSill;

            Core.CIYCLog.Info("[CIYC][House][Insert] role=" + role +
                              " prefab=" + source +
                              " kept=" + kept +
                              " measured=YES" +
                              " meshSize=" + placed.size.ToString("F3") +
                              " meshCenterBefore=" + placed.center.ToString("F3") +
                              " pivotOffset=" + placed.center.magnitude.ToString("F2") + "m" +
                              " target=" + target.ToString("F3") +
                              " finalLocal=" + go.transform.localPosition.ToString("F3") +
                              " finalWorld=" + go.transform.position.ToString("F3") +
                              " bottomLocalY=" + bottom.ToString("F3") +
                              " topLocalY=" + top.ToString("F3") +
                              " wantedBottom=" + wantedBottom.ToString("F2") +
                              " wantedTop=" + wantedTop.ToString("F2"));

            // The two claims worth checking, checked rather than assumed: the thing stands on
            // the finished floor, and it does not reach through the ceiling.
            const float Tolerance = 0.05f;
            if (!float.IsNaN(bottom) && Mathf.Abs(bottom - wantedBottom) > Tolerance)
                Core.CIYCLog.Error("[CIYC][House][Insert] role=" + role + " prefab=" + source +
                                   " steht NICHT auf der gewollten Hoehe: Unterkante " +
                                   bottom.ToString("F3") + " statt " + wantedBottom.ToString("F2") +
                                   " (Abweichung " + Mathf.Abs(bottom - wantedBottom).ToString("F3") +
                                   " m).");

            if (!float.IsNaN(top) && top > CeilingClearance)
                Core.CIYCLog.Error("[CIYC][House][Insert] role=" + role + " prefab=" + source +
                                   " ragt mit Oberkante " + top.ToString("F3") +
                                   " ueber die lichte Hoehe " + CeilingClearance.ToString("F2") +
                                   " - er schneidet die Decke.");
        }

        /// <summary>
        /// Switches off every renderer whose material is not one of the wanted ones, and reports
        /// how many were kept. Zero kept means the naming is wrong, and inserting the whole
        /// vendor wall would be far worse than inserting nothing.
        /// </summary>
        private static int KeepOnlyInsertParts(GameObject go, string[] keepMaterials)
        {
            go.GetComponentsInChildren(true, _insertRenderers);

            if (keepMaterials == null || keepMaterials.Length == 0)
            {
                int all = _insertRenderers.Count;
                for (int i = 0; i < all; i++)
                    _insertRenderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                _insertRenderers.Clear();
                DisableColliders(go);
                return all;
            }

            int kept = 0;
            for (int i = 0; i < _insertRenderers.Count; i++)
            {
                Renderer renderer = _insertRenderers[i];
                if (Wanted(renderer, keepMaterials))
                {
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    kept++;
                }
                else
                {
                    renderer.enabled = false;
                }
            }

            _insertRenderers.Clear();
            DisableColliders(go);
            return kept;
        }

        private static bool Wanted(Renderer renderer, string[] keepMaterials)
        {
            Material[] materials = renderer.sharedMaterials;
            for (int m = 0; m < materials.Length; m++)
            {
                if (materials[m] == null)
                    continue;

                for (int k = 0; k < keepMaterials.Length; k++)
                {
                    if (string.Equals(materials[m].name, keepMaterials[k],
                                      System.StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Turns a piece that was authored Z-up onto its feet, decided by MEASUREMENT.
        ///
        /// <para>
        /// The pack's wall prefabs measure about 4 m wide, 4 m in Z and a tenth of a metre in Y:
        /// their height is on Z, which is the 3ds Max convention and not Unity's. Whether the
        /// prefab already corrects that is not something a document can say, so it is measured
        /// on the instantiated object: a piece taller in Z than in Y, and thin in Y, is lying
        /// down. Logged either way, because a silent rotation is impossible to argue with when
        /// the window ends up on the floor.
        /// </para>
        /// </summary>
        private static void OrientUpright(Transform insert, string role)
        {
            if (!TryMeasureInSpace(insert, insert, out Bounds bounds))
                return;

            Vector3 size = bounds.size;
            bool lyingDown = size.z > size.y * 2f && size.y < size.x * 0.5f;

            if (!lyingDown)
            {
                Core.CIYCLog.Info("[CIYC][House] " + role + "-Einsatz steht aufrecht: " +
                                  size.ToString("F2") + " - keine Drehung noetig.");
                return;
            }

            insert.localRotation = Quaternion.Euler(-90f, 0f, 0f) * insert.localRotation;
            Core.CIYCLog.Info("[CIYC][House] " + role + "-Einsatz lag flach (" +
                              size.ToString("F2") + ", Hoehe auf Z) und wurde um -90 Grad um X " +
                              "aufgerichtet.");
        }

        /// <summary>
        /// The bounds of the parts that are actually VISIBLE, expressed in <paramref name="space"/>.
        ///
        /// <para>
        /// Visible only. Most of a vendor wall prefab has just been switched off - it is the wall
        /// shell around the door - and measuring it would centre the door on the shell it was
        /// separated from.
        /// </para>
        /// <para>
        /// Eight corners per mesh rather than a centre and a size, because a rotated child's
        /// axis-aligned size is not its size in the parent's frame. This runs a handful of times
        /// per room, so correctness costs nothing worth saving.
        /// </para>
        /// </summary>
        private static bool TryMeasureInSpace(Transform root, Transform space, out Bounds bounds)
        {
            bounds = default;
            root.GetComponentsInChildren(true, _insertRenderers);

            bool started = false;
            for (int i = 0; i < _insertRenderers.Count; i++)
            {
                Renderer renderer = _insertRenderers[i];
                if (!renderer.enabled)
                    continue;

                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                    continue;

                Bounds local = filter.sharedMesh.bounds;
                for (int c = 0; c < 8; c++)
                {
                    Vector3 corner = local.center + Vector3.Scale(local.extents, Corner(c));
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

            _insertRenderers.Clear();
            return started;
        }

        private static Vector3 Corner(int index)
        {
            return new Vector3((index & 1) == 0 ? -1f : 1f,
                               (index & 2) == 0 ? -1f : 1f,
                               (index & 4) == 0 ? -1f : 1f);
        }

        private static void DisableColliders(GameObject go)
        {
            // Switched off rather than destroyed. A disabled collider contributes no physics
            // geometry, which is the whole point, and it does it identically in the editor and
            // in a build - Destroy is deferred and DestroyImmediate is edit-mode-only, and
            // picking between them by context is exactly how this project once got an editor
            // house and a device house that differed.
            go.GetComponentsInChildren(true, _insertColliders);
            for (int i = 0; i < _insertColliders.Count; i++)
                _insertColliders[i].enabled = false;

            _insertColliders.Clear();
        }

        private static string Join(string[] values)
        {
            if (values == null || values.Length == 0)
                return "<keine genannt>";

            return "'" + string.Join("', '", values) + "'";
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
                // An unknown size means "leave it alone": use the material exactly as authored
                // rather than inventing a number. Dividing by a zero would blow the texture up
                // to a single texel across the whole wall, which reads as a flat colour and
                // looks exactly like the missing texture this is meant to fix.
                if (!surface.HasDensity)
                {
                    slot = surface.Material;
                    return slot;
                }

                slot = new Material(surface.Material)
                {
                    name = name + "_" + surface.Material.name + "_perMetre"
                };

                RebaseToMetres(slot, surface.AuthoredAcrossMetres);
                return slot;
            }

            return Neutral(ref slot, fallbackColour, "CIYC_Raw" + name);
        }

        /// <summary>
        /// Re-expresses a material whose UVs were normalised across one piece so that it reads
        /// correctly on geometry whose UVs are in metres.
        ///
        /// <para>
        /// EVERY texture property, not the colour map alone. A URP Lit material carries up to
        /// eight of them, and the measured wall materials use several - wallpaper3 has a detail
        /// normal and an occlusion map, beton adds a parallax map. Rescaling three of them and
        /// leaving the rest is not "mostly right": the colour moves and the surface detail
        /// stays, so the bumps stop sitting on the pattern they belong to. That is what a warped
        /// wall actually is, and it is much harder to recognise than a plainly wrong size.
        /// </para>
        /// <para>
        /// A single divisor for all of them, because they all shared one UV set to begin with.
        /// Relative differences between the maps - a detail map deliberately tiled eight times
        /// finer - survive, because each is divided rather than overwritten.
        /// </para>
        /// </summary>
        private static void RebaseToMetres(Material material, Vector2 authoredAcrossMetres)
        {
            var divisor = new Vector2(1f / authoredAcrossMetres.x, 1f / authoredAcrossMetres.y);

            string[] names = material.GetTexturePropertyNames();
            if (names == null)
                return;

            for (int i = 0; i < names.Length; i++)
            {
                if (!material.HasProperty(names[i]))
                    continue;

                Vector2 authored = material.GetTextureScale(names[i]);
                material.SetTextureScale(names[i],
                    new Vector2(authored.x * divisor.x, authored.y * divisor.y));
            }
        }

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
