namespace CatchIfYouCan.Procedural.Deterministic
{
    /// <summary>
    /// Independent RNG streams derived from one session seed.
    ///
    /// Each generation subsystem draws from its OWN stream so that adding, removing or
    /// reordering draws in one subsystem cannot shift another subsystem's output. Without
    /// this, adding a single cosmetic prop roll would relocate the ghost room.
    ///
    /// Stream ids are append-only and frozen: never renumber, never reuse a retired id.
    /// A subsystem must never read another subsystem's stream.
    /// </summary>
    public enum CiycStream : ulong
    {
        Layout = 1,
        Rooms = 2,
        Corridors = 3,
        Doors = 4,
        Furniture = 5,
        Props = 6,
        EvidenceSpawns = 7,
        GhostRoomCandidates = 8,
        HidingSpots = 9,
        EquipmentSpawns = 10,
        Weather = 11,
        RoomVariants = 12,
    }
}
