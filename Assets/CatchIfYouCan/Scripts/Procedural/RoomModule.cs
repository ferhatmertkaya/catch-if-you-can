using System.Collections.Generic;
using UnityEngine;

namespace CatchIfYouCan.Procedural
{
    public class RoomModule : MonoBehaviour
    {
        [SerializeField] private RoomCategory category = RoomCategory.Hallway;
        [SerializeField] private List<RoomSocket> sockets = new List<RoomSocket>();
        [SerializeField] private Bounds localBounds = new Bounds(Vector3.zero, new Vector3(6f, 3f, 6f));
        [SerializeField] private int graphNodeId = -1;

        public RoomCategory Category => category;
        public IReadOnlyList<RoomSocket> Sockets => sockets;
        public Bounds LocalBounds => localBounds;
        public int GraphNodeId => graphNodeId;

        public void Configure(RoomCategory roomCategory, Bounds bounds, int nodeId)
        {
            category = roomCategory;
            localBounds = bounds;
            graphNodeId = nodeId;
        }

        public void CollectSockets()
        {
            sockets.Clear();
            GetComponentsInChildren(true, sockets);
        }

        public List<RoomSocket> GetSockets(SocketType type)
        {
            var result = new List<RoomSocket>();
            for (int i = 0; i < sockets.Count; i++)
            {
                if (sockets[i] != null && sockets[i].Type == type)
                    result.Add(sockets[i]);
            }

            return result;
        }

        public RoomSocket GetSocket(SocketType type, SocketDirection direction)
        {
            for (int i = 0; i < sockets.Count; i++)
            {
                var socket = sockets[i];
                if (socket != null && socket.Type == type && socket.Direction == direction)
                    return socket;
            }

            return null;
        }

        public void MarkOccupied(SocketType type, SocketDirection direction, bool occupied)
        {
            var socket = GetSocket(type, direction);
            if (socket != null)
                socket.MarkOccupied(occupied);
        }

        public Bounds GetWorldBounds()
        {
            Vector3 center = transform.TransformPoint(localBounds.center);
            Vector3 extents = Vector3.Scale(localBounds.extents, transform.lossyScale);
            return new Bounds(center, extents * 2f);
        }

        public bool Overlaps(RoomModule other, float padding = 0.05f)
        {
            if (other == null)
                return false;

            Bounds a = GetWorldBounds();
            Bounds b = other.GetWorldBounds();
            a.Expand(-padding);
            b.Expand(-padding);
            return a.Intersects(b);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(localBounds.center, localBounds.size);
        }
    }
}
