using System.Collections.Generic;
using CatchIfYouCan.Interaction;
using UnityEngine;

namespace CatchIfYouCan.Procedural
{
    public class GeneratedRoomInstance
    {
        public int NodeId;
        public RoomCategory Category;
        public Vector2Int GridCell;
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
