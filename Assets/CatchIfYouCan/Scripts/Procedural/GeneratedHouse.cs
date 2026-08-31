using System.Collections.Generic;
using CatchIfYouCan.Interaction;
using CatchIfYouCan.Procedural.Deterministic;
using UnityEngine;

namespace CatchIfYouCan.Procedural
{
    public class GeneratedRoomInstance
    {
        public int NodeId;
        public RoomCategory Category;
        public GridCell Cell;
        public GameObject Root;
        public RoomModule Module;
    }

    public class GeneratedDoorConnection
    {
        public GeneratedRoomInstance RoomA;
        public GeneratedRoomInstance RoomB;
        public RoomSocket SocketA;
        public RoomSocket SocketB;
        public InteractiveDoor Door;
    }

    /// <summary>
    /// The instantiated house: Stage B's output.
    ///
    /// <see cref="Layout"/> is the authoritative data this was built from, and
    /// <see cref="LayoutHash"/> is its canonical hash. Everything else here is scene state
    /// derived from that layout - never the other way round, and never an input to
    /// generation.
    /// </summary>
    public class GeneratedHouse
    {
        public int Seed;
        public Transform Root;
        public List<GeneratedRoomInstance> Rooms = new List<GeneratedRoomInstance>();
        public GeneratedRoomInstance Entrance;
        public GeneratedRoomInstance GhostRoom;
        public List<GeneratedDoorConnection> Doors = new List<GeneratedDoorConnection>();
        public List<HideSpot> HideSpots = new List<HideSpot>();
        public HouseLayoutGraph LayoutGraph;

        /// <summary>The authoritative logical layout. Hash this, not the GameObjects.</summary>
        public HouseLayout Layout;

        public LayoutHash LayoutHash;

        public IEnumerable<Transform> GetRoomAnchors()
        {
            for (int i = 0; i < Rooms.Count; i++)
            {
                if (Rooms[i]?.Root != null)
                    yield return Rooms[i].Root.transform;
            }
        }
    }
}
