using System.Collections.Generic;
using CatchIfYouCan.Interaction;
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

        public static GameObject CreateRoom(
            RoomCategory category,
            Vector3 worldPosition,
            IEnumerable<SocketDirection> doorDirections,
            IEnumerable<SocketDirection> openDirections,
            int nodeId,
            Transform parent)
        {
            EnsureMaterials();

            var roomRoot = new GameObject($"Room_{category}_{nodeId}");
            roomRoot.transform.SetParent(parent, false);
            roomRoot.transform.position = worldPosition;

            var size = DefaultRoomSize;
            BuildFloor(roomRoot.transform, size);
            BuildCeiling(roomRoot.transform, size);

            var doorSet = new HashSet<SocketDirection>();
            if (doorDirections != null)
            {
                foreach (var dir in doorDirections)
                    doorSet.Add(dir);
            }

            var openSet = new HashSet<SocketDirection>();
            if (openDirections != null)
            {
                foreach (var dir in openDirections)
                    openSet.Add(dir);
            }

            BuildWall(roomRoot.transform, size, SocketDirection.North, doorSet.Contains(SocketDirection.North));
            BuildWall(roomRoot.transform, size, SocketDirection.South, doorSet.Contains(SocketDirection.South));
            BuildWall(roomRoot.transform, size, SocketDirection.East, doorSet.Contains(SocketDirection.East));
            BuildWall(roomRoot.transform, size, SocketDirection.West, doorSet.Contains(SocketDirection.West));

            foreach (var openDir in openSet)
            {
                if (!doorSet.Contains(openDir))
                    SealOpenWall(roomRoot.transform, size, openDir);
            }

            var module = roomRoot.AddComponent<RoomModule>();
            module.Configure(category, new Bounds(Vector3.up * (size.y * 0.5f), size), nodeId);

            CreateLightSocket(roomRoot.transform, size);
            CreateDoorSockets(roomRoot.transform, size, doorSet);
            CreateInteriorSockets(roomRoot.transform, size, category);

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
            var shader = Shader.Find("Standard");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Lit");

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

        private static void CreateDoorSockets(Transform parent, Vector3 size, HashSet<SocketDirection> doorDirections)
        {
            foreach (var direction in doorDirections)
            {
                var socketGo = new GameObject($"Socket_Door_{direction}");
                socketGo.transform.SetParent(parent, false);
                socketGo.transform.localPosition = GetWallCenter(size, direction) + Vector3.up * (DoorHeight * 0.5f);
                socketGo.transform.localRotation = Quaternion.LookRotation(RoomSocket.DirectionToLocalVector(direction), Vector3.up);

                var socket = socketGo.AddComponent<RoomSocket>();
                socket.Initialize(parent.GetComponent<RoomModule>(), SocketType.Door, direction);
            }
        }

        private static void CreateLightSocket(Transform parent, Vector3 size)
        {
            var lightGo = new GameObject("RoomLight");
            lightGo.transform.SetParent(parent, false);
            lightGo.transform.localPosition = new Vector3(0f, size.y - 0.25f, 0f);

            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 8f;
            light.intensity = categoryLightIntensity(parent.name);
            light.color = new Color(1f, 0.95f, 0.85f);

            var socketGo = new GameObject("Socket_Light");
            socketGo.transform.SetParent(lightGo.transform, false);
            var socket = socketGo.AddComponent<RoomSocket>();
            socket.Initialize(parent.GetComponent<RoomModule>(), SocketType.Light, SocketDirection.Up);
        }

        private static float categoryLightIntensity(string roomName)
        {
            if (roomName.Contains("Bathroom") || roomName.Contains("Kitchen"))
                return 1.35f;
            if (roomName.Contains("Basement") || roomName.Contains("Attic"))
                return 0.75f;
            return 1.1f;
        }

        private static void CreateInteriorSockets(Transform parent, Vector3 size, RoomCategory category)
        {
            var module = parent.GetComponent<RoomModule>();
            CreateSocket(parent, module, SocketType.Prop, SocketDirection.North, new Vector3(0f, 0f, size.z * 0.2f));
            CreateSocket(parent, module, SocketType.Prop, SocketDirection.South, new Vector3(0.8f, 0f, -size.z * 0.25f));
            CreateSocket(parent, module, SocketType.Evidence, SocketDirection.East, new Vector3(size.x * 0.15f, 1f, 0.4f));
            CreateSocket(parent, module, SocketType.GhostInteract, SocketDirection.West, new Vector3(-size.x * 0.1f, 0f, 0f));

            if (ShouldHaveHideSpot(category))
            {
                var hideGo = CreateSocket(parent, module, SocketType.Hide, SocketDirection.South, new Vector3(-1.5f, 0f, -1.5f));
                hideGo.AddComponent<HideSpot>();
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

        private static bool ShouldHaveHideSpot(RoomCategory category)
        {
            switch (category)
            {
                case RoomCategory.Bedroom:
                case RoomCategory.KidsRoom:
                case RoomCategory.Office:
                case RoomCategory.Storage:
                case RoomCategory.Garage:
                case RoomCategory.Basement:
                    return true;
                default:
                    return false;
            }
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
