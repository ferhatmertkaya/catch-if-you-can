using System.Collections.Generic;
using CatchIfYouCan.Interaction;
using CatchIfYouCan.Procedural.Deterministic;
using UnityEngine;

namespace CatchIfYouCan.Procedural
{
    public static class PrimitiveRoomFactory
    {
        public static readonly Vector3 DefaultRoomSize = new Vector3(6f, 3f, 6f);
        private const float WallThickness = 0.2f;
        private const float DoorWidth = 1.2f;
        private const float DoorHeight = 2.2f;

        private static Material _wallMaterial;
        private static Material _floorMaterial;
        private static Material _ceilingMaterial;
        private static Material _trimMaterial;

        /// <summary>
        /// Builds a room for an authoritative <see cref="LayoutRoom"/>.
        ///
        /// Wall, door and socket decisions all come from the layout's door/open masks in a
        /// frozen cardinal order. The previous version walked a HashSet to decide which
        /// walls to seal - unspecified enumeration order deciding geometry.
        /// </summary>
        public static GameObject CreateRoom(LayoutRoom room, Vector3 worldPosition, Transform parent)
        {
            EnsureMaterials();

            var roomRoot = new GameObject($"Room_{room.Category}_{room.RoomId}");
            roomRoot.transform.SetParent(parent, false);
            roomRoot.transform.position = worldPosition;

            var size = new Vector3(
                Quantize.Metres(room.SizeMm.X),
                Quantize.Metres(room.SizeMm.Y),
                Quantize.Metres(room.SizeMm.Z));

            BuildFloor(roomRoot.transform, size);
            BuildCeiling(roomRoot.transform, size);

            for (int d = 0; d < Directions.Cardinal.Length; d++)
            {
                var dir = Directions.Cardinal[d];
                BuildWall(roomRoot.transform, size, dir, room.HasDoor(dir));
            }

            for (int d = 0; d < Directions.Cardinal.Length; d++)
            {
                var dir = Directions.Cardinal[d];
                if (room.IsOpen(dir) && !room.HasDoor(dir))
                    SealOpenWall(roomRoot.transform, size, dir);
            }

            var module = roomRoot.AddComponent<RoomModule>();
            module.Configure(room.Category, new Bounds(Vector3.up * (size.y * 0.5f), size), room.RoomId);

            CreateSocketsFromLayout(roomRoot.transform, module, room);

            module.CollectSockets();
            return roomRoot;
        }

        private static void EnsureMaterials()
        {
            if (_wallMaterial != null)
                return;

            _wallMaterial = CreateMaterial(new Color(0.78f, 0.76f, 0.72f));
            _floorMaterial = CreateMaterial(new Color(0.35f, 0.28f, 0.22f));
            _ceilingMaterial = CreateMaterial(new Color(0.9f, 0.9f, 0.88f));
            _trimMaterial = CreateMaterial(new Color(0.55f, 0.52f, 0.48f));
        }

        private static Material CreateMaterial(Color color)
        {
            // Standard used to be tried first. It is a Built-in Render Pipeline shader and
            // it always resolves, so this room was magenta under URP in the editor as well as
            // on the device - the URP branch below it was never once reached.
            var shader = Art.CiycShaders.FindLit();
            if (shader == null)
                return null;

            var mat = new Material(shader);
            mat.color = color;
            return mat;
        }

        private static void BuildFloor(Transform parent, Vector3 size)
        {
            var floor = CreatePrimitive(PrimitiveType.Cube, parent, "Floor", _floorMaterial);
            floor.transform.localPosition = new Vector3(0f, -WallThickness * 0.5f, 0f);
            floor.transform.localScale = new Vector3(size.x, WallThickness, size.z);
            TagEnvironment(floor);
        }

        private static void BuildCeiling(Transform parent, Vector3 size)
        {
            var ceiling = CreatePrimitive(PrimitiveType.Cube, parent, "Ceiling", _ceilingMaterial);
            ceiling.transform.localPosition = new Vector3(0f, size.y + WallThickness * 0.5f, 0f);
            ceiling.transform.localScale = new Vector3(size.x, WallThickness, size.z);
            TagEnvironment(ceiling);
        }

        private static void BuildWall(Transform parent, Vector3 size, SocketDirection direction, bool withDoorGap)
        {
            float halfX = size.x * 0.5f;
            float halfZ = size.z * 0.5f;
            float yCenter = size.y * 0.5f;

            switch (direction)
            {
                case SocketDirection.North:
                    if (withDoorGap)
                        BuildWallWithDoor(parent, size, new Vector3(0f, yCenter, halfZ), new Vector3(size.x, size.y, WallThickness), Vector3.right);
                    else
                        BuildSolidWall(parent, "Wall_North", new Vector3(0f, yCenter, halfZ), new Vector3(size.x, size.y, WallThickness));
                    break;
                case SocketDirection.South:
                    if (withDoorGap)
                        BuildWallWithDoor(parent, size, new Vector3(0f, yCenter, -halfZ), new Vector3(size.x, size.y, WallThickness), Vector3.right);
                    else
                        BuildSolidWall(parent, "Wall_South", new Vector3(0f, yCenter, -halfZ), new Vector3(size.x, size.y, WallThickness));
                    break;
                case SocketDirection.East:
                    if (withDoorGap)
                        BuildWallWithDoor(parent, size, new Vector3(halfX, yCenter, 0f), new Vector3(WallThickness, size.y, size.z), Vector3.forward);
                    else
                        BuildSolidWall(parent, "Wall_East", new Vector3(halfX, yCenter, 0f), new Vector3(WallThickness, size.y, size.z));
                    break;
                case SocketDirection.West:
                    if (withDoorGap)
                        BuildWallWithDoor(parent, size, new Vector3(-halfX, yCenter, 0f), new Vector3(WallThickness, size.y, size.z), Vector3.forward);
                    else
                        BuildSolidWall(parent, "Wall_West", new Vector3(-halfX, yCenter, 0f), new Vector3(WallThickness, size.y, size.z));
                    break;
            }
        }

        private static void BuildSolidWall(Transform parent, string name, Vector3 position, Vector3 scale)
        {
            var wall = CreatePrimitive(PrimitiveType.Cube, parent, name, _wallMaterial);
            wall.transform.localPosition = position;
            wall.transform.localScale = scale;
            TagEnvironment(wall);
        }

        private static void BuildWallWithDoor(Transform parent, Vector3 roomSize, Vector3 wallCenter, Vector3 wallScale, Vector3 segmentAxis)
        {
            bool axisX = Mathf.Abs(segmentAxis.x) > 0.5f;
            float totalLength = axisX ? wallScale.x : wallScale.z;
            float sideLength = (totalLength - DoorWidth) * 0.5f;
            if (sideLength <= 0.1f)
            {
                BuildSolidWall(parent, "Wall_DoorSpan", wallCenter, wallScale);
                return;
            }

            Vector3 sideScale = wallScale;
            if (axisX)
                sideScale.x = sideLength;
            else
                sideScale.z = sideLength;

            float offset = (DoorWidth * 0.5f) + (sideLength * 0.5f);
            Vector3 leftPos = wallCenter - segmentAxis * offset;
            Vector3 rightPos = wallCenter + segmentAxis * offset;

            BuildSolidWall(parent, "Wall_Left", leftPos, sideScale);
            BuildSolidWall(parent, "Wall_Right", rightPos, sideScale);

            float headerHeight = roomSize.y - DoorHeight;
            if (headerHeight > 0.05f)
            {
                var headerScale = wallScale;
                if (axisX)
                    headerScale.x = DoorWidth;
                else
                    headerScale.z = DoorWidth;
                headerScale.y = headerHeight;

                var header = CreatePrimitive(PrimitiveType.Cube, parent, "DoorHeader", _trimMaterial);
                header.transform.localPosition = wallCenter + Vector3.up * (DoorHeight + headerHeight * 0.5f - roomSize.y * 0.5f);
                header.transform.localScale = headerScale;
                TagEnvironment(header);
            }
        }

        private static void SealOpenWall(Transform parent, Vector3 size, SocketDirection direction)
        {
            float halfX = size.x * 0.5f;
            float halfZ = size.z * 0.5f;
            float yCenter = size.y * 0.5f;

            switch (direction)
            {
                case SocketDirection.North:
                    BuildSolidWall(parent, "Seal_North", new Vector3(0f, yCenter, halfZ), new Vector3(size.x, size.y, WallThickness));
                    break;
                case SocketDirection.South:
                    BuildSolidWall(parent, "Seal_South", new Vector3(0f, yCenter, -halfZ), new Vector3(size.x, size.y, WallThickness));
                    break;
                case SocketDirection.East:
                    BuildSolidWall(parent, "Seal_East", new Vector3(halfX, yCenter, 0f), new Vector3(WallThickness, size.y, size.z));
                    break;
                case SocketDirection.West:
                    BuildSolidWall(parent, "Seal_West", new Vector3(-halfX, yCenter, 0f), new Vector3(WallThickness, size.y, size.z));
                    break;
            }
        }

        /// <summary>
        /// Creates every socket the layout says this room owns, at the positions
        /// RoomSocketLayout defines. That type is the single source of truth: Stage A used
        /// the same offsets to plan prop placement, so the built scene and the logical
        /// layout agree by construction rather than by two copies of the same constants.
        /// </summary>
        private static void CreateSocketsFromLayout(Transform parent, RoomModule module, LayoutRoom room)
        {
            var slots = new List<SocketSlot>(10);
            RoomSocketLayout.CollectSlots(room.Category, room.DoorMask, slots);

            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                var offset = RoomSocketLayout.LocalSocketOffset(slot, room.SizeMm);
                var localPos = new Vector3(
                    Quantize.Metres(offset.X),
                    Quantize.Metres(offset.Y),
                    Quantize.Metres(offset.Z));

                if (slot == SocketSlot.Light)
                {
                    CreateRoomLight(parent, module, localPos, room.Category);
                    continue;
                }

                var type = SocketSlots.TypeOf(slot);
                var direction = DirectionForSlot(slot);
                var socketGo = CreateSocket(parent, module, type, direction, localPos);

                if (slot == SocketSlot.Hide)
                    socketGo.AddComponent<HideSpot>();
            }
        }

        private static SocketDirection DirectionForSlot(SocketSlot slot)
        {
            switch (slot)
            {
                case SocketSlot.DoorNorth: return SocketDirection.North;
                case SocketSlot.DoorEast: return SocketDirection.East;
                case SocketSlot.DoorSouth: return SocketDirection.South;
                case SocketSlot.DoorWest: return SocketDirection.West;
                case SocketSlot.PropA: return SocketDirection.North;
                case SocketSlot.PropB: return SocketDirection.South;
                case SocketSlot.Evidence: return SocketDirection.East;
                case SocketSlot.GhostInteract: return SocketDirection.West;
                case SocketSlot.Hide: return SocketDirection.South;
                default: return SocketDirection.North;
            }
        }

        private static void CreateRoomLight(Transform parent, RoomModule module, Vector3 localPos, RoomCategory category)
        {
            var lightGo = new GameObject("RoomLight");
            lightGo.transform.SetParent(parent, false);
            lightGo.transform.localPosition = localPos;

            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 8f;
            light.intensity = CategoryLightIntensity(category);
            light.color = new Color(1f, 0.95f, 0.85f);

            var socketGo = new GameObject("Socket_Light");
            socketGo.transform.SetParent(lightGo.transform, false);
            var socket = socketGo.AddComponent<RoomSocket>();
            socket.Initialize(module, SocketType.Light, SocketDirection.Up);
        }

        private static float CategoryLightIntensity(RoomCategory category)
        {
            switch (category)
            {
                case RoomCategory.Bathroom:
                case RoomCategory.Kitchen:
                    return 1.35f;
                case RoomCategory.Basement:
                case RoomCategory.Attic:
                    return 0.75f;
                default:
                    return 1.1f;
            }
        }

        private static GameObject CreateSocket(Transform parent, RoomModule module, SocketType type, SocketDirection direction, Vector3 localPos)
        {
            var socketGo = new GameObject($"Socket_{type}_{direction}");
            socketGo.transform.SetParent(parent, false);
            socketGo.transform.localPosition = localPos;
            var socket = socketGo.AddComponent<RoomSocket>();
            socket.Initialize(module, type, direction);
            return socketGo;
        }

        private static Vector3 GetWallCenter(Vector3 size, SocketDirection direction)
        {
            float halfX = size.x * 0.5f;
            float halfZ = size.z * 0.5f;
            switch (direction)
            {
                case SocketDirection.North: return new Vector3(0f, 0f, halfZ);
                case SocketDirection.South: return new Vector3(0f, 0f, -halfZ);
                case SocketDirection.East: return new Vector3(halfX, 0f, 0f);
                case SocketDirection.West: return new Vector3(-halfX, 0f, 0f);
                default: return Vector3.zero;
            }
        }

        private static GameObject CreatePrimitive(PrimitiveType type, Transform parent, string name, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null && material != null)
                renderer.sharedMaterial = material;
            return go;
        }

        private static void TagEnvironment(GameObject go)
        {
            go.tag = "Environment";
            go.layer = LayerMask.NameToLayer("Default");
        }

        public static GameObject CreateFallbackProp(string propName, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = propName;
            go.transform.localScale = size;
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = material != null ? material : _trimMaterial ?? CreateMaterial(new Color(0.45f, 0.42f, 0.38f));
            return go;
        }
    }
}
