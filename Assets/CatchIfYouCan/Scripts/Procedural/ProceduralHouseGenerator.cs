using System.Collections.Generic;
using CatchIfYouCan.Content;
using CatchIfYouCan.Core;
using CatchIfYouCan.Interaction;
using UnityEngine;

namespace CatchIfYouCan.Procedural
{
    public class ProceduralHouseGenerator : MonoBehaviour
    {
        public const int MaxGenerationAttempts = 6;

        [Header("Layout")]
        [SerializeField] private Vector3 roomSpacing = PrimitiveRoomFactory.DefaultRoomSize;
        [SerializeField] private Transform houseRoot;
        [SerializeField] private Transform propRoot;

        [Header("Content")]
        [SerializeField] private RoomDefinition[] roomDefinitions;
        [SerializeField] private PropDefinition[] propDefinitions;

        [Header("Systems")]
        [SerializeField] private NavMeshRuntimeBuilder navMeshBuilder;
        [SerializeField] private LayerMask overlapMask = ~0;

        [Header("Door Prefab")]
        [SerializeField] private GameObject doorPrefab;

        private readonly Dictionary<RoomCategory, RoomDefinition> _definitionLookup = new Dictionary<RoomCategory, RoomDefinition>();
        private System.Random _rng;

        public GeneratedHouse LastGenerated { get; private set; }

        private void Awake()
        {
            CacheDefinitions();
            InvestigationContentLoader.ApplyToGenerator(this);

            if (houseRoot == null)
            {
                var rootGo = new GameObject("GeneratedHouseRoot");
                houseRoot = rootGo.transform;
            }

            if (propRoot == null)
            {
                var propGo = new GameObject("PropRoot");
                propRoot = propGo.transform;
                propRoot.SetParent(houseRoot, false);
            }

            if (navMeshBuilder == null)
                navMeshBuilder = GetComponent<NavMeshRuntimeBuilder>();
        }

        public GeneratedHouse Generate(int seed)
        {
            SeedManager.SetSeed(seed);
            _rng = SeedManager.CreateRandom(seed);

            for (int attempt = 0; attempt < MaxGenerationAttempts; attempt++)
            {
                int attemptSeed = attempt == 0 ? seed : seed + attempt * 7919;
                var house = GenerateInternal(attemptSeed);
                var validation = HouseValidator.Validate(house);
                if (validation.IsValid)
                {
                    LastGenerated = house;
                    CIYCLog.Info($"House generated with seed {attemptSeed} ({house.Rooms.Count} rooms).");
                    return house;
                }

                HouseValidator.LogValidation(validation);
                DestroyHouseObjects(house);
            }

            if (seed != SeedManager.KnownGoodSeed)
            {
                CIYCLog.Warn($"Generation failed for seed {seed}; falling back to {SeedManager.KnownGoodSeed}.");
                return Generate(SeedManager.KnownGoodSeed);
            }

            var fallback = GenerateInternal(SeedManager.KnownGoodSeed);
            LastGenerated = fallback;
            CIYCLog.Error("House generation failed even for known good seed; returning best effort layout.");
            return fallback;
        }

        private GeneratedHouse GenerateInternal(int seed)
        {
            _rng = SeedManager.CreateRandom(seed);
            ClearExisting();

            var graph = HouseLayoutGraph.Build(seed);
            var house = new GeneratedHouse
            {
                Seed = seed,
                Root = houseRoot,
                LayoutGraph = graph
            };

            var nodeDoors = BuildDoorDirectionMap(graph);
            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                var node = graph.Nodes[i];
                nodeDoors.TryGetValue(node.Id, out var doorDirs);

                var roomInstance = InstantiateRoom(node, doorDirs, node.OpenDirections);
                house.Rooms.Add(roomInstance);

                if (node.Category == RoomCategory.Entrance)
                    house.Entrance = roomInstance;
            }

            ConnectDoors(house, graph);
            SealUnusedOpenings(house);
            InstallRoomInteractables(house);
            SpawnProps(house);
            AssignGhostRoom(house);
            CollectHideSpots(house);
            EnsureMinimumHideSpot(house);
            BuildNavigation(house);

            return house;
        }

        private Dictionary<int, HashSet<SocketDirection>> BuildDoorDirectionMap(HouseLayoutGraph graph)
        {
            var map = new Dictionary<int, HashSet<SocketDirection>>();
            for (int i = 0; i < graph.Edges.Count; i++)
            {
                var edge = graph.Edges[i];
                AddDoorDirection(map, edge.NodeAId, edge.DirectionFromA);
                AddDoorDirection(map, edge.NodeBId, RoomSocket.Opposite(edge.DirectionFromA));
            }

            return map;
        }

        private static void AddDoorDirection(Dictionary<int, HashSet<SocketDirection>> map, int nodeId, SocketDirection direction)
        {
            if (!map.TryGetValue(nodeId, out var set))
            {
                set = new HashSet<SocketDirection>();
                map[nodeId] = set;
            }

            set.Add(direction);
        }

        private GeneratedRoomInstance InstantiateRoom(
            HouseLayoutNode node,
            HashSet<SocketDirection> doorDirections,
            List<SocketDirection> openDirections)
        {
            Vector3 position = new Vector3(node.GridCell.x * roomSpacing.x, 0f, node.GridCell.y * roomSpacing.z);
            GameObject roomGo = null;
            RoomModule module = null;

            var definition = GetDefinition(node.Category);
            GameObject prefab = definition?.PickPrefab(_rng);
            if (prefab != null)
            {
                roomGo = Instantiate(prefab, position, Quaternion.identity, houseRoot);
                module = roomGo.GetComponent<RoomModule>();
                if (module == null)
                    module = roomGo.AddComponent<RoomModule>();

                module.Configure(node.Category, definition != null ? new Bounds(Vector3.up * (definition.Size.y * 0.5f), definition.Size) : new Bounds(Vector3.up * 1.5f, roomSpacing), node.Id);
                module.CollectSockets();
            }
            else
            {
                roomGo = PrimitiveRoomFactory.CreateRoom(
                    node.Category,
                    position,
                    doorDirections,
                    openDirections,
                    node.Id,
                    houseRoot);
                module = roomGo.GetComponent<RoomModule>();
            }

            return new GeneratedRoomInstance
            {
                NodeId = node.Id,
                Category = node.Category,
                GridCell = node.GridCell,
                Root = roomGo,
                Module = module
            };
        }

        private void ConnectDoors(GeneratedHouse house, HouseLayoutGraph graph)
        {
            for (int i = 0; i < graph.Edges.Count; i++)
            {
                var edge = graph.Edges[i];
                var roomA = FindRoom(house, edge.NodeAId);
                var roomB = FindRoom(house, edge.NodeBId);
                if (roomA?.Module == null || roomB?.Module == null)
                    continue;

                var socketA = roomA.Module.GetSocket(SocketType.Door, edge.DirectionFromA);
                var socketB = roomB.Module.GetSocket(SocketType.Door, RoomSocket.Opposite(edge.DirectionFromA));

                if (socketA == null || socketB == null)
                {
                    EnsureDoorSockets(roomA, edge.DirectionFromA);
                    EnsureDoorSockets(roomB, RoomSocket.Opposite(edge.DirectionFromA));
                    socketA = roomA.Module.GetSocket(SocketType.Door, edge.DirectionFromA);
                    socketB = roomB.Module.GetSocket(SocketType.Door, RoomSocket.Opposite(edge.DirectionFromA));
                }

                if (socketA == null || socketB == null)
                    continue;

                socketA.ConnectTo(socketB);
                var door = CreateDoorBetween(socketA, socketB);
                house.Doors.Add(new GeneratedDoorConnection
                {
                    RoomA = roomA,
                    RoomB = roomB,
                    SocketA = socketA,
                    SocketB = socketB,
                    Door = door
                });
            }
        }

        private void EnsureDoorSockets(GeneratedRoomInstance room, SocketDirection direction)
        {
            if (room?.Module == null)
                return;

            if (room.Module.GetSocket(SocketType.Door, direction) != null)
                return;

            var socketGo = new GameObject($"Socket_Door_{direction}");
            socketGo.transform.SetParent(room.Root.transform, false);
            socketGo.transform.localPosition = GetLocalDoorPosition(room.Module.LocalBounds.size, direction);
            socketGo.transform.localRotation = Quaternion.LookRotation(RoomSocket.DirectionToLocalVector(direction), Vector3.up);
            var socket = socketGo.AddComponent<RoomSocket>();
            socket.Initialize(room.Module, SocketType.Door, direction);
            room.Module.CollectSockets();
        }

        private static Vector3 GetLocalDoorPosition(Vector3 size, SocketDirection direction)
        {
            float halfX = size.x * 0.5f;
            float halfZ = size.z * 0.5f;
            switch (direction)
            {
                case SocketDirection.North: return new Vector3(0f, 1.1f, halfZ);
                case SocketDirection.South: return new Vector3(0f, 1.1f, -halfZ);
                case SocketDirection.East: return new Vector3(halfX, 1.1f, 0f);
                case SocketDirection.West: return new Vector3(-halfX, 1.1f, 0f);
                default: return Vector3.up;
            }
        }

        private InteractiveDoor CreateDoorBetween(RoomSocket socketA, RoomSocket socketB)
        {
            Vector3 midpoint = (socketA.transform.position + socketB.transform.position) * 0.5f;
            Quaternion rotation = Quaternion.LookRotation(socketA.GetWorldDirection(), Vector3.up);

            GameObject doorGo;
            if (doorPrefab != null)
            {
                doorGo = Instantiate(doorPrefab, midpoint, rotation, houseRoot);
            }
            else
            {
                doorGo = BuildPrimitiveDoor(midpoint, rotation);
            }

            doorGo.tag = "Door";
            var door = doorGo.GetComponent<InteractiveDoor>();
            if (door == null)
                door = doorGo.AddComponent<InteractiveDoor>();

            return door;
        }

        private GameObject BuildPrimitiveDoor(Vector3 position, Quaternion rotation)
        {
            var doorRoot = new GameObject("Door");
            doorRoot.transform.SetParent(houseRoot, false);
            doorRoot.transform.SetPositionAndRotation(position, rotation);

            var frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frame.name = "DoorFrame";
            frame.transform.SetParent(doorRoot.transform, false);
            frame.transform.localScale = new Vector3(1.3f, 2.2f, 0.12f);
            Object.Destroy(frame.GetComponent<Collider>());

            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "DoorPanel";
            panel.transform.SetParent(doorRoot.transform, false);
            panel.transform.localPosition = new Vector3(0.55f, 0f, 0f);
            panel.transform.localScale = new Vector3(1.1f, 2.1f, 0.08f);
            panel.tag = "Door";

            return doorRoot;
        }

        private void SealUnusedOpenings(GeneratedHouse house)
        {
            for (int i = 0; i < house.Rooms.Count; i++)
            {
                var room = house.Rooms[i];
                if (room?.Module == null || house.LayoutGraph == null)
                    continue;

                var node = house.LayoutGraph.GetNode(room.NodeId);
                if (node == null)
                    continue;

                for (int d = 0; d < node.OpenDirections.Count; d++)
                {
                    var dir = node.OpenDirections[d];
                    var doorSocket = room.Module.GetSocket(SocketType.Door, dir);
                    if (doorSocket == null)
                    {
                        EnsureDoorSockets(room, dir);
                        doorSocket = room.Module.GetSocket(SocketType.Door, dir);
                    }

                    if (doorSocket != null)
                        doorSocket.MarkOccupied(true);
                }
            }
        }

        private void InstallRoomInteractables(GeneratedHouse house)
        {
            var lightControllers = new List<LightController>();

            for (int i = 0; i < house.Rooms.Count; i++)
            {
                var room = house.Rooms[i];
                if (room?.Root == null)
                    continue;

                var roomLight = room.Root.GetComponentInChildren<Light>();
                if (roomLight == null)
                    continue;

                var lightController = roomLight.gameObject.GetComponent<LightController>();
                if (lightController == null)
                    lightController = roomLight.gameObject.AddComponent<LightController>();

                SetPrivateField(lightController, "lights", new[] { roomLight });
                lightControllers.Add(lightController);

                CreateLightSwitch(room, lightController);
            }

            InstallBreakerBox(house, lightControllers);
        }

        private static void CreateLightSwitch(GeneratedRoomInstance room, LightController lightController)
        {
            var switchGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            switchGo.name = "LightSwitch";
            switchGo.tag = "LightSwitch";
            switchGo.transform.SetParent(room.Root.transform, false);
            switchGo.transform.localPosition = new Vector3(-2.2f, 1.2f, 0f);
            switchGo.transform.localScale = new Vector3(0.12f, 0.18f, 0.04f);

            var collider = switchGo.GetComponent<Collider>();
            if (collider != null)
                collider.isTrigger = false;

            var lightSwitch = switchGo.AddComponent<InteractiveLightSwitch>();
            SetPrivateField(lightSwitch, "lightController", lightController);
        }

        private void InstallBreakerBox(GeneratedHouse house, List<LightController> lightControllers)
        {
            GeneratedRoomInstance target = house.Entrance;
            for (int i = 0; i < house.Rooms.Count; i++)
            {
                if (house.Rooms[i].Category == RoomCategory.Garage ||
                    house.Rooms[i].Category == RoomCategory.Basement)
                {
                    target = house.Rooms[i];
                    break;
                }
            }

            if (target?.Root == null)
                return;

            var breakerGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            breakerGo.name = "BreakerBox";
            breakerGo.transform.SetParent(target.Root.transform, false);
            breakerGo.transform.localPosition = new Vector3(2f, 1.1f, -2f);
            breakerGo.transform.localScale = new Vector3(0.35f, 0.5f, 0.12f);

            var breaker = breakerGo.AddComponent<BreakerBox>();
            SetPrivateField(breaker, "houseLights", lightControllers.ToArray());
        }

        private void EnsureMinimumHideSpot(GeneratedHouse house)
        {
            CollectHideSpots(house);
            if (house.HideSpots.Count > 0)
                return;

            GeneratedRoomInstance target = null;
            for (int i = 0; i < house.Rooms.Count; i++)
            {
                var room = house.Rooms[i];
                if (room.Category == RoomCategory.Bedroom ||
                    room.Category == RoomCategory.KidsRoom ||
                    room.Category == RoomCategory.Storage)
                {
                    target = room;
                    break;
                }
            }

            target ??= house.Rooms.Count > 0 ? house.Rooms[0] : null;
            if (target?.Root == null)
                return;

            var hideGo = new GameObject("HideSpot_Fallback");
            hideGo.transform.SetParent(target.Root.transform, false);
            hideGo.transform.localPosition = new Vector3(-1.5f, 0f, -1.5f);
            hideGo.AddComponent<HideSpot>();
            house.HideSpots.Add(hideGo.GetComponent<HideSpot>());
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            if (target == null)
                return;

            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(target, value);
        }

        private void SpawnProps(GeneratedHouse house)
        {
            if (propDefinitions == null || propDefinitions.Length == 0)
                return;

            var spawner = new PropSpawner(propRoot, overlapMask);
            spawner.SpawnProps(house.Rooms, propDefinitions, _rng, spawnChancePerSocket: 0.82f);
        }

        private void AssignGhostRoom(GeneratedHouse house)
        {
            GeneratedRoomInstance best = null;
            float bestScore = float.MinValue;

            for (int i = 0; i < house.Rooms.Count; i++)
            {
                var room = house.Rooms[i];
                if (room.Category == RoomCategory.Entrance || room.Category == RoomCategory.Hallway)
                    continue;

                float score = ScoreGhostRoom(house, room);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = room;
                }
            }

            house.GhostRoom = best ?? (house.Rooms.Count > 1 ? house.Rooms[1] : house.Entrance);
        }

        private float ScoreGhostRoom(GeneratedHouse house, GeneratedRoomInstance room)
        {
            if (house.Entrance == null)
                return RandomRange(0f, 1f);

            float distance = Vector3.Distance(room.Root.transform.position, house.Entrance.Root.transform.position);
            float categoryBonus = 0f;
            switch (room.Category)
            {
                case RoomCategory.Basement:
                case RoomCategory.Attic:
                    categoryBonus = 2f;
                    break;
                case RoomCategory.Bedroom:
                case RoomCategory.KidsRoom:
                    categoryBonus = 1.5f;
                    break;
                case RoomCategory.Bathroom:
                    categoryBonus = 1f;
                    break;
            }

            return distance + categoryBonus + RandomRange(0f, 0.5f);
        }

        private void CollectHideSpots(GeneratedHouse house)
        {
            house.HideSpots.Clear();
            for (int i = 0; i < house.Rooms.Count; i++)
            {
                if (house.Rooms[i]?.Root == null)
                    continue;

                house.Rooms[i].Root.GetComponentsInChildren(true, house.HideSpots);
            }
        }

        private void BuildNavigation(GeneratedHouse house)
        {
            if (navMeshBuilder == null)
            {
                navMeshBuilder = gameObject.AddComponent<NavMeshRuntimeBuilder>();
            }

            navMeshBuilder.Build(house.Root);
        }

        private GeneratedRoomInstance FindRoom(GeneratedHouse house, int nodeId)
        {
            for (int i = 0; i < house.Rooms.Count; i++)
            {
                if (house.Rooms[i].NodeId == nodeId)
                    return house.Rooms[i];
            }

            return null;
        }

        private RoomDefinition GetDefinition(RoomCategory category)
        {
            if (_definitionLookup.TryGetValue(category, out var def))
                return def;
            return null;
        }

        private void CacheDefinitions()
        {
            _definitionLookup.Clear();
            if (roomDefinitions == null)
                return;

            for (int i = 0; i < roomDefinitions.Length; i++)
            {
                var def = roomDefinitions[i];
                if (def != null)
                    _definitionLookup[def.Category] = def;
            }
        }

        private void ClearExisting()
        {
            if (houseRoot == null)
                return;

            for (int i = houseRoot.childCount - 1; i >= 0; i--)
                DestroyImmediateSafe(houseRoot.GetChild(i).gameObject);
        }

        private static void DestroyHouseObjects(GeneratedHouse house)
        {
            if (house?.Root == null)
                return;

            for (int i = house.Root.childCount - 1; i >= 0; i--)
                DestroyImmediateSafe(house.Root.GetChild(i).gameObject);
        }

        private static void DestroyImmediateSafe(GameObject go)
        {
            if (go == null)
                return;

            if (Application.isPlaying)
                Object.Destroy(go);
            else
                Object.DestroyImmediate(go);
        }

        private float RandomRange(float min, float max)
        {
            return SeedManager.NextFloat(_rng, min, max);
        }

        public void ApplyContentCatalog(InvestigationContentCatalog catalog)
        {
            if (catalog == null)
                return;

            if (propDefinitions == null || propDefinitions.Length == 0)
                propDefinitions = catalog.PropDefinitions;

            if (roomDefinitions == null || roomDefinitions.Length == 0)
                roomDefinitions = catalog.RoomDefinitions;

            if (doorPrefab == null)
                doorPrefab = catalog.DoorPrefab;

            CacheDefinitions();
        }
    }
}
