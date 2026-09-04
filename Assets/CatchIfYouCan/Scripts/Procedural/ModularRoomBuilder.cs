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
        /// <summary>Wall pieces are placed against the inside face, not the centreline.</summary>
        private const float WallInset = 0.0f;

        /// <summary>
        /// Builds the shell. Returns null and sets <paramref name="error"/> if the catalog
        /// cannot supply a structural role - never a partial room, and never a substitute.
        /// A room missing its north wall looks like a room until the player walks north.
        /// </summary>
        public static GameObject Build(LayoutRoom room, Vector3 worldPosition, Transform parent,
            ModularInteriorCatalog catalog, out string error)
        {
            error = null;

            if (catalog == null)
            {
                error = "no modular interior catalog is assigned";
                return null;
            }

            if (!catalog.TryValidate(out string catalogError))
            {
                error = "the modular interior catalog is incomplete: " + catalogError;
                return null;
            }

            var size = new Vector3(
                Quantize.Metres(room.SizeMm.X),
                Quantize.Metres(room.SizeMm.Y),
                Quantize.Metres(room.SizeMm.Z));

            var roomRoot = new GameObject($"Room_{room.Category}_{room.RoomId}");
            roomRoot.transform.SetParent(parent, false);
            roomRoot.transform.position = worldPosition;

            BuildSurface(roomRoot.transform, room, catalog, ModuleRole.Floor, size, 0f);
            BuildSurface(roomRoot.transform, room, catalog, ModuleRole.Ceiling, size, size.y);

            for (int d = 0; d < Directions.Cardinal.Length; d++)
            {
                var direction = Directions.Cardinal[d];
                if (!BuildWall(roomRoot.transform, room, catalog, direction, size, out string wallError))
                {
                    error = wallError;
                    Object.DestroyImmediate(roomRoot);
                    return null;
                }
            }

            BuildBaseboard(roomRoot.transform, room, catalog, size);

            var module = roomRoot.GetComponent<RoomModule>();
            if (module == null)
                module = roomRoot.AddComponent<RoomModule>();

            module.Configure(room.Category, new Bounds(Vector3.up * (size.y * 0.5f), size), room.RoomId);
            module.CollectSockets();

            return roomRoot;
        }

        // ------------------------------------------------------------------ surfaces

        private static void BuildSurface(Transform parent, LayoutRoom room,
            ModularInteriorCatalog catalog, ModuleRole role, Vector3 size, float height)
        {
            var variants = catalog.FindVariants(role, room.Category);
            if (variants.Length == 0)
                return;

            Vector3 tile = catalog.FindModuleSize(role, room.Category);
            int countX = TileCount(size.x, tile.x);
            int countZ = TileCount(size.z, tile.z);

            float stepX = size.x / countX;
            float stepZ = size.z / countZ;

            for (int ix = 0; ix < countX; ix++)
            {
                for (int iz = 0; iz < countZ; iz++)
                {
                    var prefab = Pick(variants, room.RoomId, (int)role, ix * 31 + iz);
                    if (prefab == null)
                        continue;

                    var local = new Vector3(
                        -size.x * 0.5f + stepX * (ix + 0.5f),
                        height,
                        -size.z * 0.5f + stepZ * (iz + 0.5f));

                    var piece = Object.Instantiate(prefab, parent);
                    piece.transform.localPosition = local;
                    piece.transform.localRotation = role == ModuleRole.Ceiling
                        ? Quaternion.Euler(180f, 0f, 0f)
                        : Quaternion.identity;
                    piece.name = role + "_" + ix + "_" + iz;
                }
            }
        }

        // --------------------------------------------------------------------- walls

        private static bool BuildWall(Transform parent, LayoutRoom room,
            ModularInteriorCatalog catalog, SocketDirection direction, Vector3 size,
            out string error)
        {
            error = null;

            // The layout decides what this wall is, not the art. A door connection means a
            // doorway module, so the opening is real geometry with nothing standing in it.
            ModuleRole role = room.HasDoor(direction)
                ? ModuleRole.WallWithDoorway
                : ModuleRole.WallSolid;

            var variants = catalog.FindVariants(role, room.Category);

            // A window is enrichment, and only where the layout says nothing connects.
            if (role == ModuleRole.WallSolid && !room.IsOpen(direction))
            {
                var windows = catalog.FindVariants(ModuleRole.WallWithWindow, room.Category);
                if (windows.Length > 0 && WantsWindow(room, direction))
                {
                    role = ModuleRole.WallWithWindow;
                    variants = windows;
                }
            }

            if (variants.Length == 0)
            {
                error = "the catalog supplies no " + role + " for a " + room.Category +
                        " (room " + room.RoomId + ", " + direction + " wall)";
                return false;
            }

            bool alongX = direction == SocketDirection.North || direction == SocketDirection.South;
            float span = alongX ? size.x : size.z;

            Vector3 tile = catalog.FindModuleSize(role, room.Category);
            float tileSpan = alongX ? tile.x : tile.z;
            if (tileSpan <= 0.01f)
                tileSpan = Mathf.Max(tile.x, tile.z);

            int count = TileCount(span, tileSpan);
            float step = span / count;

            // The doorway goes in the middle piece so it lines up with the door socket, which
            // RoomSocketLayout puts at the wall centre. An even count has no middle, so the
            // piece just past centre carries it - the same piece on every machine.
            int doorIndex = count / 2;

            for (int i = 0; i < count; i++)
            {
                ModuleRole pieceRole = role;
                GameObject[] pieceVariants = variants;

                if (role == ModuleRole.WallWithDoorway && i != doorIndex)
                {
                    pieceRole = ModuleRole.WallSolid;
                    pieceVariants = catalog.FindVariants(ModuleRole.WallSolid, room.Category);
                    if (pieceVariants.Length == 0)
                    {
                        error = "the catalog supplies no WallSolid for a " + room.Category;
                        return false;
                    }
                }

                var prefab = Pick(pieceVariants, room.RoomId, (int)pieceRole, (int)direction * 97 + i);
                if (prefab == null)
                    continue;

                float offset = -span * 0.5f + step * (i + 0.5f);
                Vector3 local;
                float yaw;

                switch (direction)
                {
                    case SocketDirection.North:
                        local = new Vector3(offset, 0f, size.z * 0.5f - WallInset);
                        yaw = 180f;
                        break;
                    case SocketDirection.South:
                        local = new Vector3(offset, 0f, -size.z * 0.5f + WallInset);
                        yaw = 0f;
                        break;
                    case SocketDirection.East:
                        local = new Vector3(size.x * 0.5f - WallInset, 0f, offset);
                        yaw = 270f;
                        break;
                    default:
                        local = new Vector3(-size.x * 0.5f + WallInset, 0f, offset);
                        yaw = 90f;
                        break;
                }

                var piece = Object.Instantiate(prefab, parent);
                piece.transform.localPosition = local;
                piece.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                piece.name = pieceRole + "_" + direction + "_" + i;
            }

            return true;
        }

        private static void BuildBaseboard(Transform parent, LayoutRoom room,
            ModularInteriorCatalog catalog, Vector3 size)
        {
            var variants = catalog.FindVariants(ModuleRole.Baseboard, room.Category);
            if (variants.Length == 0)
                return;

            for (int d = 0; d < Directions.Cardinal.Length; d++)
            {
                var direction = Directions.Cardinal[d];
                if (room.HasDoor(direction))
                    continue;

                var prefab = Pick(variants, room.RoomId, (int)ModuleRole.Baseboard, (int)direction);
                if (prefab == null)
                    continue;

                bool alongX = direction == SocketDirection.North || direction == SocketDirection.South;
                Vector3 local;
                float yaw;

                switch (direction)
                {
                    case SocketDirection.North: local = new Vector3(0f, 0f, size.z * 0.5f); yaw = 180f; break;
                    case SocketDirection.South: local = new Vector3(0f, 0f, -size.z * 0.5f); yaw = 0f; break;
                    case SocketDirection.East: local = new Vector3(size.x * 0.5f, 0f, 0f); yaw = 270f; break;
                    default: local = new Vector3(-size.x * 0.5f, 0f, 0f); yaw = 90f; break;
                }

                var piece = Object.Instantiate(prefab, parent);
                piece.transform.localPosition = local;
                piece.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                piece.name = "Baseboard_" + direction;

                // Stretched along the wall it sits on; the other axes stay as authored.
                var scale = piece.transform.localScale;
                if (alongX) scale.x *= size.x; else scale.z *= size.z;
                piece.transform.localScale = scale;
            }
        }

        // ---------------------------------------------------------------- derivation

        /// <summary>
        /// Which interchangeable mesh this piece gets. Derived from the room's own identity,
        /// so it is the same on the host and on every client without any of them agreeing on
        /// anything first, and no generation stream is touched.
        /// </summary>
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

        /// <summary>
        /// Whether an outside wall of this room takes a window. Derived like the variant, so
        /// two players see the same windows. Roughly half of eligible walls get one.
        /// </summary>
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

        private static int TileCount(float span, float tile)
        {
            if (tile <= 0.01f)
                return 1;

            return Mathf.Max(1, Mathf.RoundToInt(span / tile));
        }
    }
}
