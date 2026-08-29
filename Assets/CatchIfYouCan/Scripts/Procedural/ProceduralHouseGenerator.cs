using System;
using System.Collections.Generic;
using CatchIfYouCan.Content;
using CatchIfYouCan.Core;
using CatchIfYouCan.Interaction;
using CatchIfYouCan.Procedural.Deterministic;
using UnityEngine;

namespace CatchIfYouCan.Procedural
{
    /// <summary>
    /// Two-stage house generation.
    ///
    ///   STAGE A - HouseLayoutBuilder produces an authoritative, engine-free HouseLayout
    ///             from (seed, generationVersion, mapDefinitionId, content). Validation and
    ///             retries happen entirely on that pure data.
    ///
    ///   STAGE B - this class instantiates the finished layout. It makes NO generation
    ///             decision: no RNG, no physics queries, no dependence on what is already
    ///             in the scene.
    ///
    /// The previous implementation interleaved the two: it instantiated a candidate house,
    /// validated the GameObjects, destroyed them and retried - all inside one frame, with
    /// Object.Destroy deferred to end of frame. Attempt N therefore saw attempts 0..N-1
    /// still present in the physics scene, and because the editor takes the DestroyImmediate
    /// branch while a player build does not, the editor and a device build produced
    /// different houses from the same seed.
    /// </summary>
    public class ProceduralHouseGenerator : MonoBehaviour
    {
        public const int MaxGenerationAttempts = HouseLayoutBuilder.MaxAttempts;

        [Header("Map")]
        [Tooltip("Which map definition to generate. Part of the layout identity together " +
                 "with the seed and the generation version.")]
        [SerializeField] private string mapDefinitionId = "HOUSE_DEFAULT_A";

        [Header("Layout")]
        [Tooltip("Derived from the MapDefinition now; kept only so existing scenes and " +
                 "prefabs deserialize unchanged. Room placement comes from the layout.")]
        [SerializeField] private Vector3 roomSpacing = PrimitiveRoomFactory.DefaultRoomSize;
        [SerializeField] private Transform houseRoot;
        [SerializeField] private Transform propRoot;

        [Header("Content")]
        [SerializeField] private RoomDefinition[] roomDefinitions;
        [SerializeField] private PropDefinition[] propDefinitions;

        [Header("Systems")]
        [SerializeField] private NavMeshRuntimeBuilder navMeshBuilder;
        [Tooltip("No longer used. Prop overlap is resolved analytically in Stage A; a " +
                 "physics query can never influence generation. Kept for scene compatibility.")]
        [SerializeField] private LayerMask overlapMask = ~0;

        [Header("Door Prefab")]
        [SerializeField] private GameObject doorPrefab;

        private Transform _activeHouseRoot;

        public GeneratedHouse LastGenerated { get; private set; }

        /// <summary>The authoritative layout behind <see cref="LastGenerated"/>.</summary>
        public HouseLayout LastLayout { get; private set; }

        /// <summary>Canonical hash of <see cref="LastLayout"/>, with per-section breakdown.</summary>
        public LayoutHash LastHash { get; private set; }

        public LayoutValidationResult LastValidation { get; private set; }

        public bool LastGenerationFailed => LastValidation != null && !LastValidation.IsValid;

        /// <summary>
        /// Raised when Stage A could not produce a valid layout. Generation failures must
        /// fail visibly; nothing silently substitutes a different seed.
        /// </summary>
        public static event Action<int, LayoutValidationResult> GenerationFailed;

        private void Awake()
        {
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

        // ================================================================ STAGE A

        /// <summary>
        /// Runs Stage A only. Pure: allocates no GameObjects and touches no scene state, so
        /// it is safe to call for hashing, for a multiplayer pre-flight check, or from a test.
        /// </summary>
        public HouseLayout BuildLayout(int seed, out LayoutValidationResult validation)
        {
            var content = ContentSnapshotFactory.Create(roomDefinitions, propDefinitions);
            var map = MapDefinition.ById(mapDefinitionId);
            return HouseLayoutBuilder.Generate(seed, map, content, out validation);
        }

        // ================================================================ STAGE A + B

        public GeneratedHouse Generate(int seed)
        {
            SeedManager.SetSeed(seed);

            var layout = BuildLayout(seed, out var validation);
            LastLayout = layout;
            LastValidation = validation;
            LastHash = LayoutHasher.Compute(layout);

            if (!validation.IsValid)
            {
                // Fail loudly and keep the layout we actually have. The old code silently
                // re-generated from KnownGoodSeed on failure, which is precisely the
                // "silently repair the layout" behaviour that desyncs a multiplayer session:
                // one client would have quietly built a different house from everyone else.
                CIYCLog.Error(
                    $"[Determinism] House generation FAILED for seed {seed} " +
                    $"(map {layout.MapDefinitionId}, generationVersion {layout.GenerationVersion}) " +
                    $"after {MaxGenerationAttempts} attempts: {validation}");
                CIYCLog.Error(LastHash.ToReport());
                GenerationFailed?.Invoke(seed, validation);
            }
            else
            {
                CIYCLog.Info(
                    $"House generated: seed {seed}, {layout.Rooms.Count} rooms, " +
                    $"attempt {layout.Attempt}, hash {LastHash.Final}");
            }

            var house = Instantiate(layout);
            LastGenerated = house;
            return house;
        }

        // ================================================================ STAGE B

        /// <summary>
        /// Builds the scene for a finished layout. Deterministic by construction: it reads
        /// only the layout, never the scene.
        /// </summary>
        public GeneratedHouse Instantiate(HouseLayout layout)
        {
            ClearExisting();

            // Build into a fresh root and swap, rather than reusing one that still holds
            // objects awaiting a deferred Destroy.
            var newRootGo = new GameObject($"House_{layout.Seed}_{Fnv1a64.ToShortHex(LastHash.FinalHash)}");
            _activeHouseRoot = newRootGo.transform;
            _activeHouseRoot.SetParent(houseRoot, false);

            if (propRoot == null || propRoot.parent != _activeHouseRoot)
            {
                var propGo = new GameObject("PropRoot");
                propRoot = propGo.transform;
                propRoot.SetParent(_activeHouseRoot, false);
            }

            var house = new GeneratedHouse
            {
                Seed = layout.Seed,
                Root = _activeHouseRoot,
                Layout = layout,
                LayoutHash = LastHash,
                LayoutGraph = HouseLayoutGraph.FromLayout(layout)
            };

            var roomsById = new Dictionary<int, GeneratedRoomInstance>(layout.Rooms.Count);

            for (int i = 0; i < layout.Rooms.Count; i++)
            {
                var instance = InstantiateRoom(layout.Rooms[i]);
                house.Rooms.Add(instance);
                roomsById[instance.NodeId] = instance;

                if (layout.Rooms[i].RoomId == layout.EntranceRoomId)
                    house.Entrance = instance;
            }

            ConnectDoors(house, layout, roomsById);
            SealUnusedOpenings(house, layout, roomsById);
            InstallRoomInteractables(house);
            SpawnProps(layout, roomsById);
            AssignGhostRoom(house, layout, roomsById);
            CollectHideSpots(house);
            EnsureMinimumHideSpot(house, layout, roomsById);
            BuildNavigation(house);

            return house;
        }

        private GeneratedRoomInstance InstantiateRoom(LayoutRoom room)
        {
            Vector3 position = new Vector3(
                Quantize.Metres(room.PositionMm.X),
                Quantize.Metres(room.PositionMm.Y),
                Quantize.Metres(room.PositionMm.Z));

            var definition = ContentSnapshotFactory.FindRoom(roomDefinitions, room.ArchetypeId);
            // The VARIANT was chosen in Stage A from the RoomVariants stream. Stage B only
            // looks it up - picking here would reintroduce a generation decision.
            GameObject prefab = definition != null ? definition.GetPrefabVariant(room.VariantIndex) : null;

            GameObject roomGo;
            RoomModule module;

            if (prefab != null)
            {
                roomGo = UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity, _activeHouseRoot);
                module = roomGo.GetComponent<RoomModule>();
                if (module == null)
                    module = roomGo.AddComponent<RoomModule>();

                Vector3 size = definition != null ? definition.Size : roomSpacing;
                module.Configure(room.Category, new Bounds(Vector3.up * (size.y * 0.5f), size), room.RoomId);
                module.CollectSockets();
            }
            else
            {
                roomGo = PrimitiveRoomFactory.CreateRoom(room, position, _activeHouseRoot);
                module = roomGo.GetComponent<RoomModule>();
            }

            return new GeneratedRoomInstance
            {
                NodeId = room.RoomId,
                Category = room.Category,
                Cell = room.Cell,
                Root = roomGo,
                Module = module
            };
        }

        private void ConnectDoors(GeneratedHouse house, HouseLayout layout,
            Dictionary<int, GeneratedRoomInstance> roomsById)
        {
            for (int i = 0; i < layout.Doors.Count; i++)
            {
                var door = layout.Doors[i];
                if (!roomsById.TryGetValue(door.RoomAId, out var roomA) ||
                    !roomsById.TryGetValue(door.RoomBId, out var roomB))
                    continue;

                if (roomA.Module == null || roomB.Module == null)
                    continue;

                var dirA = DirectionOfDoorSlot(door.SocketASlot);
                var dirB = DirectionOfDoorSlot(door.SocketBSlot);

                EnsureDoorSocket(roomA, dirA);
                EnsureDoorSocket(roomB, dirB);

                var socketA = roomA.Module.GetSocket(SocketType.Door, dirA);
                var socketB = roomB.Module.GetSocket(SocketType.Door, dirB);
                if (socketA == null || socketB == null)
                    continue;

                socketA.ConnectTo(socketB);

                Vector3 position = new Vector3(
                    Quantize.Metres(door.PositionMm.X),
                    Quantize.Metres(door.PositionMm.Y),
                    Quantize.Metres(door.PositionMm.Z));
                Quaternion rotation = Quaternion.Euler(0f, door.RotationIndex * 90f, 0f);

                var interactiveDoor = CreateDoorAt(position, rotation);
                house.Doors.Add(new GeneratedDoorConnection
                {
                    RoomA = roomA,
                    RoomB = roomB,
                    SocketA = socketA,
                    SocketB = socketB,
                    Door = interactiveDoor
                });
            }
        }

        private static SocketDirection DirectionOfDoorSlot(SocketSlot slot)
        {
            switch (slot)
            {
                case SocketSlot.DoorNorth: return SocketDirection.North;
                case SocketSlot.DoorEast: return SocketDirection.East;
                case SocketSlot.DoorSouth: return SocketDirection.South;
                case SocketSlot.DoorWest: return SocketDirection.West;
                default: return SocketDirection.North;
            }
        }

        private void EnsureDoorSocket(GeneratedRoomInstance room, SocketDirection direction)
        {
            if (room?.Module == null)
                return;

            if (room.Module.GetSocket(SocketType.Door, direction) != null)
                return;

            var slot = SocketSlots.DoorSlot(direction);
            var sizeMm = Vec3i.FromMetres(
                room.Module.LocalBounds.size.x,
                room.Module.LocalBounds.size.y,
                room.Module.LocalBounds.size.z);
            var offset = RoomSocketLayout.LocalSocketOffset(slot, sizeMm);

            var socketGo = new GameObject($"Socket_Door_{direction}");
            socketGo.transform.SetParent(room.Root.transform, false);
            socketGo.transform.localPosition = new Vector3(
                Quantize.Metres(offset.X), Quantize.Metres(offset.Y), Quantize.Metres(offset.Z));
            socketGo.transform.localRotation =
                Quaternion.LookRotation(RoomSocket.DirectionToLocalVector(direction), Vector3.up);

            var socket = socketGo.AddComponent<RoomSocket>();
            socket.Initialize(room.Module, SocketType.Door, direction);
            room.Module.CollectSockets();
        }

        private InteractiveDoor CreateDoorAt(Vector3 position, Quaternion rotation)
        {
            GameObject doorGo = doorPrefab != null
                ? UnityEngine.Object.Instantiate(doorPrefab, position, rotation, _activeHouseRoot)
                : BuildPrimitiveDoor(position, rotation);

            doorGo.tag = "Door";
            var door = doorGo.GetComponent<InteractiveDoor>();
            if (door == null)
                door = doorGo.AddComponent<InteractiveDoor>();

            return door;
        }

        private GameObject BuildPrimitiveDoor(Vector3 position, Quaternion rotation)
        {
            var doorRoot = new GameObject("Door");
            doorRoot.transform.SetParent(_activeHouseRoot, false);
            doorRoot.transform.SetPositionAndRotation(position, rotation);

            var frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frame.name = "DoorFrame";
            frame.transform.SetParent(doorRoot.transform, false);
            frame.transform.localScale = new Vector3(1.3f, 2.2f, 0.12f);
            DestroyImmediateSafe(frame.GetComponent<Collider>());

            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "DoorPanel";
            panel.transform.SetParent(doorRoot.transform, false);
            panel.transform.localPosition = new Vector3(0.55f, 0f, 0f);
            panel.transform.localScale = new Vector3(1.1f, 2.1f, 0.08f);
            panel.tag = "Door";

            return doorRoot;
        }

        private void SealUnusedOpenings(GeneratedHouse house, HouseLayout layout,
            Dictionary<int, GeneratedRoomInstance> roomsById)
        {
            for (int i = 0; i < layout.Rooms.Count; i++)
            {
                var room = layout.Rooms[i];
                if (!roomsById.TryGetValue(room.RoomId, out var instance) || instance.Module == null)
                    continue;

                // Canonical cardinal order, not a HashSet walk.
                for (int d = 0; d < Directions.Cardinal.Length; d++)
                {
                    var dir = Directions.Cardinal[d];
                    if (!room.IsOpen(dir))
                        continue;

                    EnsureDoorSocket(instance, dir);
                    var socket = instance.Module.GetSocket(SocketType.Door, dir);
                    socket?.MarkOccupied(true);
                }
            }
        }

        private void SpawnProps(HouseLayout layout, Dictionary<int, GeneratedRoomInstance> roomsById)
        {
            var spawner = new PropSpawner(propRoot);
            spawner.SpawnPlacements(layout.Furniture, propDefinitions, roomsById);
            spawner.SpawnPlacements(layout.Props, propDefinitions, roomsById);
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

        private void AssignGhostRoom(GeneratedHouse house, HouseLayout layout,
            Dictionary<int, GeneratedRoomInstance> roomsById)
        {
            // Chosen in Stage A from the GhostRoomCandidates stream and part of the layout
            // hash, so every client agrees on it without any further communication.
            if (layout.GhostRoomId >= 0 && roomsById.TryGetValue(layout.GhostRoomId, out var ghostRoom))
            {
                house.GhostRoom = ghostRoom;
                return;
            }

            house.GhostRoom = house.Rooms.Count > 1 ? house.Rooms[1] : house.Entrance;
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

        private void EnsureMinimumHideSpot(GeneratedHouse house, HouseLayout layout,
            Dictionary<int, GeneratedRoomInstance> roomsById)
        {
            if (house.HideSpots.Count > 0)
                return;

            // Fall back to the layout's own hide-spot anchors, which every client has.
            for (int i = 0; i < layout.HideSpots.Count; i++)
            {
                var anchor = layout.HideSpots[i];
                if (!roomsById.TryGetValue(anchor.RoomId, out var room) || room.Root == null)
                    continue;

                var hideGo = new GameObject("HideSpot_Layout");
                hideGo.transform.SetParent(room.Root.transform, true);
                hideGo.transform.position = new Vector3(
                    Quantize.Metres(anchor.PositionMm.X),
                    Quantize.Metres(anchor.PositionMm.Y),
                    Quantize.Metres(anchor.PositionMm.Z));
                house.HideSpots.Add(hideGo.AddComponent<HideSpot>());
            }

            if (house.HideSpots.Count > 0)
                return;

            var target = house.Rooms.Count > 0 ? house.Rooms[0] : null;
            if (target?.Root == null)
                return;

            var fallbackGo = new GameObject("HideSpot_Fallback");
            fallbackGo.transform.SetParent(target.Root.transform, false);
            fallbackGo.transform.localPosition = new Vector3(-1.5f, 0f, -1.5f);
            house.HideSpots.Add(fallbackGo.AddComponent<HideSpot>());
        }

        private void BuildNavigation(GeneratedHouse house)
        {
            if (navMeshBuilder == null)
                navMeshBuilder = gameObject.AddComponent<NavMeshRuntimeBuilder>();

            // The NavMesh is built FROM the finished layout. Its output is deliberately not
            // part of the layout hash: a runtime bake carries no cross-platform bit-identity
            // guarantee, and ghost pathing is host-authoritative (Docs/NETWORKING.md §4).
            navMeshBuilder.Build(house.Root);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            if (target == null)
                return;

            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(target, value);
        }

        /// <summary>
        /// Detaches previous house roots immediately, then destroys them.
        ///
        /// The detach matters: Object.Destroy is deferred to end of frame at runtime, so
        /// without it the old hierarchy would still be reachable while the new house is
        /// being built. Generation no longer reads the scene, so this is now hygiene rather
        /// than correctness - but leaving the trap in place invites the next regression.
        /// </summary>
        private void ClearExisting()
        {
            if (houseRoot == null)
                return;

            for (int i = houseRoot.childCount - 1; i >= 0; i--)
            {
                var child = houseRoot.GetChild(i).gameObject;
                child.transform.SetParent(null, false);
                DestroyImmediateSafe(child);
            }

            _activeHouseRoot = null;
            propRoot = null;
        }

        private static void DestroyImmediateSafe(UnityEngine.Object target)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(target);
            else
                UnityEngine.Object.DestroyImmediate(target);
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
        }
    }
}
