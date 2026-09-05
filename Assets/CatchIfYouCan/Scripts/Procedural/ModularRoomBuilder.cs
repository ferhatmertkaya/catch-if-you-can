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

            BuildFloor(roomRoot.transform, size);
            BuildCeiling(roomRoot.transform, size);

            for (int d = 0; d < Directions.Cardinal.Length; d++)
                BuildWall(roomRoot.transform, room, Directions.Cardinal[d], size);

            var module = roomRoot.GetComponent<RoomModule>();
            if (module == null)
                module = roomRoot.AddComponent<RoomModule>();

            module.Configure(room.Category, new Bounds(Vector3.up * (size.y * 0.5f), size), room.RoomId);
            module.CollectSockets();

            return roomRoot;
        }

        // ------------------------------------------------------------------ surfaces

        private static void BuildFloor(Transform parent, Vector3 size)
        {
            var mesh = StructuralMeshFactory.Floor(size.x, size.z, FloorThickness);
            var go = Piece(parent, "Floor", mesh, FloorMaterial());
            go.transform.localPosition = Vector3.zero;

            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, -FloorThickness * 0.5f, 0f);
            box.size = new Vector3(size.x, FloorThickness, size.z);
        }

        private static void BuildCeiling(Transform parent, Vector3 size)
        {
            var mesh = StructuralMeshFactory.Ceiling(size.x, size.z, CeilingThickness);
            var go = Piece(parent, "Ceiling", mesh, CeilingMaterial());
            go.transform.localPosition = new Vector3(0f, size.y, 0f);

            // No collider. The player cannot reach it and every one that exists is one the
            // physics engine tests against for nothing.
        }

        // --------------------------------------------------------------------- walls

        private static void BuildWall(Transform parent, LayoutRoom room,
            SocketDirection direction, Vector3 size)
        {
            // The layout decides what this wall is. A door connection means a real hole, not a
            // solid wall with a door drawn on it.
            bool hasDoor = room.HasDoor(direction);
            ModuleRole role = hasDoor ? ModuleRole.WallWithDoorway : ModuleRole.WallSolid;

            bool alongX = direction == SocketDirection.North || direction == SocketDirection.South;
            float span = alongX ? size.x : size.z;

            Mesh mesh = hasDoor
                ? StructuralMeshFactory.WallWithOpening(span, size.y, WallThickness,
                    DoorWidth, DoorHeight, 0f)
                : StructuralMeshFactory.SolidWall(span, size.y, WallThickness);

            var go = Piece(parent, role + "_" + direction, mesh, WallMaterial());

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

            AddWallColliders(go, hasDoor, span, size.y);
        }

        /// <summary>
        /// Collision that matches the geometry, taken from the same sections the mesh was built
        /// from rather than computed a second time. A doorway that is open to the eye and shut
        /// to the body is the failure this exists to prevent, and computing the rectangles
        /// twice is how it happens.
        /// </summary>
        private static void AddWallColliders(GameObject go, bool hasDoor, float span, float height)
        {
            if (!hasDoor)
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

        // Three materials for the whole house, created once. One per wall would be forty
        // materials in a ten-room house and forty separate draw calls to go with them.
        private static Material _wall;
        private static Material _floor;
        private static Material _ceiling;

        private static Material WallMaterial() => Neutral(ref _wall, new Color(0.72f, 0.70f, 0.67f), "CIYC_RawWall");
        private static Material FloorMaterial() => Neutral(ref _floor, new Color(0.42f, 0.39f, 0.36f), "CIYC_RawFloor");
        private static Material CeilingMaterial() => Neutral(ref _ceiling, new Color(0.86f, 0.86f, 0.84f), "CIYC_RawCeiling");

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
