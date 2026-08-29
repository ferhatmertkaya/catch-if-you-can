using System;
using System.Collections.Generic;

namespace CatchIfYouCan.Procedural.Deterministic
{
    /// <summary>One generated room. Geometry is integer millimetres; identity is a stable id.</summary>
    public readonly struct LayoutRoom
    {
        public readonly int RoomId;
        public readonly string ArchetypeId;
        public readonly RoomCategory Category;
        public readonly GridCell Cell;
        public readonly int RotationIndex;
        public readonly Vec3i PositionMm;
        public readonly Vec3i SizeMm;
        public readonly int VariantIndex;
        /// <summary>Bitfield of door directions: N=1, E=2, S=4, W=8.</summary>
        public readonly int DoorMask;
        /// <summary>Bitfield of directions with no neighbour, which Stage B seals.</summary>
        public readonly int OpenMask;

        public LayoutRoom(int roomId, string archetypeId, RoomCategory category, GridCell cell,
            int rotationIndex, Vec3i positionMm, Vec3i sizeMm, int variantIndex, int doorMask, int openMask)
        {
            RoomId = roomId;
            ArchetypeId = archetypeId;
            Category = category;
            Cell = cell;
            RotationIndex = rotationIndex;
            PositionMm = positionMm;
            SizeMm = sizeMm;
            VariantIndex = variantIndex;
            DoorMask = doorMask;
            OpenMask = openMask;
        }

        public bool HasDoor(SocketDirection dir) => (DoorMask & DirectionMask(dir)) != 0;
        public bool IsOpen(SocketDirection dir) => (OpenMask & DirectionMask(dir)) != 0;

        public static int DirectionMask(SocketDirection dir)
        {
            switch (dir)
            {
                case SocketDirection.North: return 1;
                case SocketDirection.East: return 2;
                case SocketDirection.South: return 4;
                case SocketDirection.West: return 8;
                default: return 0;
            }
        }
    }

    /// <summary>A graph adjacency between two rooms. Hashed separately from the door that realises it.</summary>
    public readonly struct LayoutConnection
    {
        public readonly int ConnectionId;
        public readonly int RoomAId;
        public readonly int RoomBId;
        public readonly SocketDirection DirectionFromA;

        public LayoutConnection(int connectionId, int roomAId, int roomBId, SocketDirection directionFromA)
        {
            ConnectionId = connectionId;
            RoomAId = roomAId;
            RoomBId = roomBId;
            DirectionFromA = directionFromA;
        }
    }

    public readonly struct LayoutDoor
    {
        public readonly int DoorId;
        public readonly int RoomAId;
        public readonly int RoomBId;
        public readonly SocketSlot SocketASlot;
        public readonly SocketSlot SocketBSlot;
        public readonly Vec3i PositionMm;
        public readonly int RotationIndex;

        public LayoutDoor(int doorId, int roomAId, int roomBId, SocketSlot socketASlot,
            SocketSlot socketBSlot, Vec3i positionMm, int rotationIndex)
        {
            DoorId = doorId;
            RoomAId = roomAId;
            RoomBId = roomBId;
            SocketASlot = socketASlot;
            SocketBSlot = socketBSlot;
            PositionMm = positionMm;
            RotationIndex = rotationIndex;
        }
    }

    public readonly struct LayoutProp
    {
        public readonly int PropInstanceId;
        public readonly string PropDefinitionId;
        public readonly PropKind Kind;
        public readonly int RoomId;
        public readonly SocketSlot Slot;
        public readonly Vec3i PositionMm;
        public readonly int RotationIndex;

        public LayoutProp(int propInstanceId, string propDefinitionId, PropKind kind, int roomId,
            SocketSlot slot, Vec3i positionMm, int rotationIndex)
        {
            PropInstanceId = propInstanceId;
            PropDefinitionId = propDefinitionId;
            Kind = kind;
            RoomId = roomId;
            Slot = slot;
            PositionMm = positionMm;
            RotationIndex = rotationIndex;
        }
    }

    /// <summary>A gameplay anchor: hide spot, equipment drop, or evidence interaction point.</summary>
    public readonly struct LayoutAnchor
    {
        public readonly int AnchorId;
        public readonly int RoomId;
        public readonly SocketSlot Slot;
        public readonly Vec3i PositionMm;

        public LayoutAnchor(int anchorId, int roomId, SocketSlot slot, Vec3i positionMm)
        {
            AnchorId = anchorId;
            RoomId = roomId;
            Slot = slot;
            PositionMm = positionMm;
        }
    }

    /// <summary>A ranked ghost-room candidate. Score is fixed point so it is safe to hash.</summary>
    public readonly struct LayoutGhostCandidate
    {
        public readonly int RoomId;
        public readonly int ScoreFixed;

        public LayoutGhostCandidate(int roomId, int scoreFixed)
        {
            RoomId = roomId;
            ScoreFixed = scoreFixed;
        }
    }

    /// <summary>
    /// The authoritative, engine-free result of Stage A generation.
    ///
    /// This is the object that is hashed, compared between clients, and handed to Stage B
    /// for instantiation. It contains no GameObjects, Transforms or instance ids: those are
    /// an OUTPUT of generation and can never influence it.
    ///
    /// All collections are canonically sorted by the builder before construction, so
    /// enumeration order here is already the hash order.
    /// </summary>
    public sealed class HouseLayout
    {
        public int GenerationVersion { get; }
        public int Seed { get; }
        public string MapDefinitionId { get; }
        public ulong ContentHash { get; }
        /// <summary>Which retry attempt produced this layout. Diagnostic only; not hashed.</summary>
        public int Attempt { get; }

        public IReadOnlyList<LayoutRoom> Rooms { get; }
        public IReadOnlyList<LayoutConnection> Connections { get; }
        public IReadOnlyList<LayoutDoor> Doors { get; }
        public IReadOnlyList<LayoutProp> Furniture { get; }
        public IReadOnlyList<LayoutProp> Props { get; }
        public IReadOnlyList<LayoutAnchor> HideSpots { get; }
        public IReadOnlyList<LayoutAnchor> EquipmentSpawns { get; }
        public IReadOnlyList<LayoutAnchor> EvidencePoints { get; }
        public IReadOnlyList<LayoutGhostCandidate> GhostRoomCandidates { get; }

        public int EntranceRoomId { get; }
        public int GhostRoomId { get; }
        public int WeatherIndex { get; }

        public HouseLayout(
            int generationVersion,
            int seed,
            string mapDefinitionId,
            ulong contentHash,
            int attempt,
            IReadOnlyList<LayoutRoom> rooms,
            IReadOnlyList<LayoutConnection> connections,
            IReadOnlyList<LayoutDoor> doors,
            IReadOnlyList<LayoutProp> furniture,
            IReadOnlyList<LayoutProp> props,
            IReadOnlyList<LayoutAnchor> hideSpots,
            IReadOnlyList<LayoutAnchor> equipmentSpawns,
            IReadOnlyList<LayoutAnchor> evidencePoints,
            IReadOnlyList<LayoutGhostCandidate> ghostRoomCandidates,
            int entranceRoomId,
            int ghostRoomId,
            int weatherIndex)
        {
            GenerationVersion = generationVersion;
            Seed = seed;
            MapDefinitionId = mapDefinitionId;
            ContentHash = contentHash;
            Attempt = attempt;
            Rooms = rooms ?? Array.Empty<LayoutRoom>();
            Connections = connections ?? Array.Empty<LayoutConnection>();
            Doors = doors ?? Array.Empty<LayoutDoor>();
            Furniture = furniture ?? Array.Empty<LayoutProp>();
            Props = props ?? Array.Empty<LayoutProp>();
            HideSpots = hideSpots ?? Array.Empty<LayoutAnchor>();
            EquipmentSpawns = equipmentSpawns ?? Array.Empty<LayoutAnchor>();
            EvidencePoints = evidencePoints ?? Array.Empty<LayoutAnchor>();
            GhostRoomCandidates = ghostRoomCandidates ?? Array.Empty<LayoutGhostCandidate>();
            EntranceRoomId = entranceRoomId;
            GhostRoomId = ghostRoomId;
            WeatherIndex = weatherIndex;
        }

        public bool TryGetRoom(int roomId, out LayoutRoom room)
        {
            for (int i = 0; i < Rooms.Count; i++)
            {
                if (Rooms[i].RoomId == roomId)
                {
                    room = Rooms[i];
                    return true;
                }
            }

            room = default;
            return false;
        }
    }
}
