namespace CatchIfYouCan.Procedural.Deterministic
{
    /// <summary>
    /// Pure, immutable description of a generatable map. Together with the generation
    /// version and the seed this forms the full layout identity.
    ///
    /// All dimensions are integer millimetres so Stage A never touches floating point
    /// for geometry.
    /// </summary>
    public sealed class MapDefinition
    {
        public string MapDefinitionId { get; }
        public int MinRooms { get; }
        public int MaxRooms { get; }
        public Vec3i RoomSizeMm { get; }
        public Vec3i RoomSpacingMm { get; }
        public int PropSpawnPermille { get; }
        public int MaxSpecialRooms { get; }

        public MapDefinition(
            string mapDefinitionId,
            int minRooms,
            int maxRooms,
            Vec3i roomSizeMm,
            Vec3i roomSpacingMm,
            int propSpawnPermille,
            int maxSpecialRooms)
        {
            MapDefinitionId = mapDefinitionId;
            MinRooms = minRooms;
            MaxRooms = maxRooms;
            RoomSizeMm = roomSizeMm;
            RoomSpacingMm = roomSpacingMm;
            PropSpawnPermille = propSpawnPermille;
            MaxSpecialRooms = maxSpecialRooms;
        }

        /// <summary>
        /// The default house. Values mirror the previous hard-coded generator settings
        /// (6x3x6 m rooms, 6-14 rooms, 0.82 prop spawn chance) so the game plays the same.
        /// </summary>
        public static readonly MapDefinition HouseDefault = new MapDefinition(
            mapDefinitionId: "HOUSE_DEFAULT_A",
            minRooms: 6,
            maxRooms: 14,
            roomSizeMm: new Vec3i(6000, 3000, 6000),
            roomSpacingMm: new Vec3i(6000, 3000, 6000),
            propSpawnPermille: 820,
            maxSpecialRooms: 2);

        /// <summary>Small fixed map used by the training scene.</summary>
        public static readonly MapDefinition HouseTraining = new MapDefinition(
            mapDefinitionId: "HOUSE_TRAINING_A",
            minRooms: 6,
            maxRooms: 8,
            roomSizeMm: new Vec3i(6000, 3000, 6000),
            roomSpacingMm: new Vec3i(6000, 3000, 6000),
            propSpawnPermille: 600,
            maxSpecialRooms: 0);

        public static MapDefinition ById(string id)
        {
            if (id == HouseTraining.MapDefinitionId) return HouseTraining;
            return HouseDefault;
        }
    }
}
