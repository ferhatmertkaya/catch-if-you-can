using System.Collections.Generic;
using System.Text;
using CatchIfYouCan.Content;
using CatchIfYouCan.Interaction;
using CatchIfYouCan.Procedural;
using UnityEditor;
using UnityEngine;

namespace CatchIfYouCan.EditorTools
{
    public static class KenneyRoomPrefabBuilder
    {
        private const string RoomPrefabsRoot = "Assets/CatchIfYouCan/Prefabs/Rooms/Kenney";
        private const float WallThickness = 0.2f;
        private const float DoorWidth = 1.2f;
        private const float DoorHeight = 2.2f;

        public static RoomDefinition[] BuildAllRoomDefinitions(StringBuilder report)
        {
            EnsureFolder(RoomPrefabsRoot);
            EnsureFolder("Assets/CatchIfYouCan/ScriptableObjects/Rooms");

            var defaults = RoomDefinitionFactory.CreateAllDefaults();
            int built = 0;

            for (int i = 0; i < defaults.Length; i++)
            {
                var def = defaults[i];
                string prefabPath = $"{RoomPrefabsRoot}/Room_{def.Category}.prefab";
                var prefab = BuildRoomPrefab(def.Category, prefabPath);
                if (prefab == null)
                    continue;

                def.PrefabVariants = new[] { prefab };
                def.Size = PrimitiveRoomFactory.DefaultRoomSize;

                string assetPath = $"Assets/CatchIfYouCan/ScriptableObjects/Rooms/room_{def.Category}.asset";
                var existing = AssetDatabase.LoadAssetAtPath<RoomDefinition>(assetPath);
                if (existing != null)
                {
                    existing.Category = def.Category;
                    existing.PrefabVariants = def.PrefabVariants;
                    existing.Size = def.Size;
                    existing.Weight = def.Weight;
                    EditorUtility.SetDirty(existing);
                    defaults[i] = existing;
                }
                else
                {
                    AssetDatabase.CreateAsset(def, assetPath);
                }

                built++;
            }

            report?.AppendLine($"Kenney room prefabs: {built}/{defaults.Length}.");
            return defaults;
        }

        private static GameObject BuildRoomPrefab(RoomCategory category, string prefabPath)
        {
            var size = PrimitiveRoomFactory.DefaultRoomSize;
            bool rustic = category == RoomCategory.Basement ||
                          category == RoomCategory.Garage ||
                          category == RoomCategory.Attic ||
                          category == RoomCategory.UtilityRoom;

            string floorModel = rustic
                ? $"{ExternalAssetPaths.KenneyDungeonModels}/floor.fbx"
                : $"{ExternalAssetPaths.KenneyFurnitureModels}/floorFull.fbx";
            string wallModel = rustic
                ? $"{ExternalAssetPaths.KenneyDungeonModels}/wall.fbx"
                : $"{ExternalAssetPaths.KenneyFurnitureModels}/wall.fbx";

            if (!System.IO.File.Exists(floorModel) || !System.IO.File.Exists(wallModel))
                return null;

            var root = new GameObject($"Room_{category}");
            BuildScaledMesh(floorModel, root.transform, "Floor", new Vector3(size.x, 0.15f, size.z), Vector3.zero);

            BuildWallSet(root.transform, size, wallModel, category);

            var module = root.AddComponent<RoomModule>();
            module.Configure(category, new Bounds(Vector3.up * (size.y * 0.5f), size), -1);

            CreateLight(root.transform, size, category);
            CreateDoorSockets(root.transform, size, module);
            CreateInteriorSockets(root.transform, size, category, module);

            module.CollectSockets();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void BuildWallSet(Transform parent, Vector3 size, string wallModel, RoomCategory category)
        {
            BuildWall(parent, size, wallModel, SocketDirection.North, true);
            BuildWall(parent, size, wallModel, SocketDirection.South, true);
            BuildWall(parent, size, wallModel, SocketDirection.East, true);
            BuildWall(parent, size, wallModel, SocketDirection.West, true);

            if (category == RoomCategory.Bathroom || category == RoomCategory.Kitchen)
            {
                string detail = $"{ExternalAssetPaths.KenneyFurnitureModels}/paneling.fbx";
                if (System.IO.File.Exists(detail))
                    BuildScaledMesh(detail, parent, "Paneling", new Vector3(size.x * 0.5f, 1f, 0.1f), new Vector3(0f, 1f, size.z * 0.45f));
            }
        }

        private static void BuildWall(Transform parent, Vector3 size, string wallModel, SocketDirection direction, bool withDoorGap)
        {
            float halfX = size.x * 0.5f;
            float halfZ = size.z * 0.5f;
            float yCenter = size.y * 0.5f;

            switch (direction)
            {
                case SocketDirection.North:
                    PlaceWallSegment(parent, wallModel, $"Wall_{direction}", new Vector3(0f, yCenter, halfZ), new Vector3(size.x, size.y, WallThickness), withDoorGap, Vector3.right);
                    break;
                case SocketDirection.South:
                    PlaceWallSegment(parent, wallModel, $"Wall_{direction}", new Vector3(0f, yCenter, -halfZ), new Vector3(size.x, size.y, WallThickness), withDoorGap, Vector3.right);
                    break;
                case SocketDirection.East:
                    PlaceWallSegment(parent, wallModel, $"Wall_{direction}", new Vector3(halfX, yCenter, 0f), new Vector3(WallThickness, size.y, size.z), withDoorGap, Vector3.forward);
                    break;
                case SocketDirection.West:
                    PlaceWallSegment(parent, wallModel, $"Wall_{direction}", new Vector3(-halfX, yCenter, 0f), new Vector3(WallThickness, size.y, size.z), withDoorGap, Vector3.forward);
                    break;
            }
        }

        private static void PlaceWallSegment(
            Transform parent,
            string wallModel,
            string name,
            Vector3 center,
            Vector3 targetSize,
            bool withDoorGap,
            Vector3 segmentAxis)
        {
            if (!withDoorGap)
            {
                BuildScaledMesh(wallModel, parent, name, targetSize, center);
                return;
            }

            bool axisX = Mathf.Abs(segmentAxis.x) > 0.5f;
            float totalLength = axisX ? targetSize.x : targetSize.z;
            float sideLength = (totalLength - DoorWidth) * 0.5f;
            if (sideLength <= 0.1f)
            {
                BuildScaledMesh(wallModel, parent, name, targetSize, center);
                return;
            }

            Vector3 sideSize = targetSize;
            if (axisX) sideSize.x = sideLength;
            else sideSize.z = sideLength;

            float offset = (DoorWidth * 0.5f) + (sideLength * 0.5f);
            BuildScaledMesh(wallModel, parent, name + "_L", sideSize, center - segmentAxis * offset);
            BuildScaledMesh(wallModel, parent, name + "_R", sideSize, center + segmentAxis * offset);
        }

        private static void BuildScaledMesh(string modelPath, Transform parent, string name, Vector3 targetSize, Vector3 localPosition)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (source == null)
                return;

            var instance = Object.Instantiate(source, parent);
            instance.name = name;
            instance.transform.localPosition = localPosition;

            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return;

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            var size = bounds.size;
            if (size.x <= 0.001f || size.y <= 0.001f || size.z <= 0.001f)
                return;

            float sx = targetSize.x / size.x;
            float sy = targetSize.y / size.y;
            float sz = targetSize.z / size.z;
            float uniform = axisDominantScale(targetSize, sx, sy, sz);
            instance.transform.localScale = Vector3.one * uniform;

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            instance.transform.position -= new Vector3(0f, bounds.min.y - parent.position.y, 0f);

            foreach (var col in instance.GetComponentsInChildren<Collider>())
                Object.DestroyImmediate(col);

            var box = instance.AddComponent<BoxCollider>();
            box.size = targetSize;
            box.center = Vector3.up * (targetSize.y * 0.5f);

            instance.tag = "Environment";
        }

        private static float axisDominantScale(Vector3 target, float sx, float sy, float sz)
        {
            if (target.y < target.x && target.y < target.z)
                return sy;
            if (target.x > target.z)
                return sx;
            return sz;
        }

        private static void CreateLight(Transform parent, Vector3 size, RoomCategory category)
        {
            var lightGo = new GameObject("RoomLight");
            lightGo.transform.SetParent(parent, false);
            lightGo.transform.localPosition = new Vector3(0f, size.y - 0.25f, 0f);

            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 8f;
            light.intensity = category == RoomCategory.Basement || category == RoomCategory.Attic ? 0.75f : 1.1f;
            light.color = new Color(1f, 0.95f, 0.85f);
        }

        private static void CreateDoorSockets(Transform parent, Vector3 size, RoomModule module)
        {
            foreach (SocketDirection direction in new[] { SocketDirection.North, SocketDirection.South, SocketDirection.East, SocketDirection.West })
            {
                var socketGo = new GameObject($"Socket_Door_{direction}");
                socketGo.transform.SetParent(parent, false);
                socketGo.transform.localPosition = GetWallCenter(size, direction) + Vector3.up * (DoorHeight * 0.5f);
                socketGo.transform.localRotation = Quaternion.LookRotation(RoomSocket.DirectionToLocalVector(direction), Vector3.up);
                var socket = socketGo.AddComponent<RoomSocket>();
                socket.Initialize(module, SocketType.Door, direction);
            }
        }

        private static void CreateInteriorSockets(Transform parent, Vector3 size, RoomCategory category, RoomModule module)
        {
            int propCount = GetPropSocketCount(category);
            for (int i = 0; i < propCount; i++)
            {
                float angle = (i / (float)propCount) * Mathf.PI * 2f;
                var pos = new Vector3(Mathf.Cos(angle) * size.x * 0.22f, 0f, Mathf.Sin(angle) * size.z * 0.22f);
                var dir = i % 2 == 0 ? SocketDirection.North : SocketDirection.South;
                CreateSocket(parent, module, SocketType.Prop, dir, pos);
            }

            CreateSocket(parent, module, SocketType.Evidence, SocketDirection.East, new Vector3(size.x * 0.15f, 1f, 0.4f));
            CreateSocket(parent, module, SocketType.GhostInteract, SocketDirection.West, new Vector3(-size.x * 0.1f, 0f, 0f));

            if (ShouldHaveHideSpot(category))
            {
                var hideGo = CreateSocket(parent, module, SocketType.Hide, SocketDirection.South, new Vector3(-1.5f, 0f, -1.5f));
                hideGo.AddComponent<HideSpot>();
            }
        }

        private static int GetPropSocketCount(RoomCategory category)
        {
            switch (category)
            {
                case RoomCategory.LivingRoom:
                case RoomCategory.Kitchen:
                case RoomCategory.Bedroom:
                    return 5;
                case RoomCategory.Hallway:
                case RoomCategory.Entrance:
                    return 2;
                case RoomCategory.Bathroom:
                case RoomCategory.Storage:
                    return 3;
                default:
                    return 4;
            }
        }

        private static GameObject CreateSocket(Transform parent, RoomModule module, SocketType type, SocketDirection direction, Vector3 localPos)
        {
            var socketGo = new GameObject($"Socket_{type}_{direction}_{localPos}");
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

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            var parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
