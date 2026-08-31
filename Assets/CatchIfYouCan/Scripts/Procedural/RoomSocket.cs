using CatchIfYouCan.Procedural.Deterministic;
using UnityEngine;

namespace CatchIfYouCan.Procedural
{
    // SocketType and SocketDirection now live in SocketEnums.cs so the deterministic
    // generation core can use them without referencing UnityEngine.

    public class RoomSocket : MonoBehaviour
    {
        [SerializeField] private SocketType socketType = SocketType.Door;
        [SerializeField] private SocketDirection direction = SocketDirection.North;
        [SerializeField] private bool occupied;
        [SerializeField] private RoomSocket connectedSocket;

        public SocketType Type => socketType;
        public SocketDirection Direction => direction;
        public bool IsOccupied => occupied;
        public RoomSocket ConnectedSocket => connectedSocket;
        public RoomModule Owner { get; private set; }

        public void Initialize(RoomModule owner, SocketType type, SocketDirection dir)
        {
            Owner = owner;
            socketType = type;
            direction = dir;
        }

        public void SetDirection(SocketDirection dir) => direction = dir;

        public bool ConnectTo(RoomSocket other)
        {
            if (other == null || other == this)
                return false;

            connectedSocket = other;
            occupied = true;
            other.connectedSocket = this;
            other.occupied = true;
            return true;
        }

        public void MarkOccupied(bool value)
        {
            occupied = value;
        }

        public void Disconnect()
        {
            if (connectedSocket != null && connectedSocket.connectedSocket == this)
                connectedSocket.connectedSocket = null;

            connectedSocket = null;
            occupied = false;
        }

        public Vector3 GetWorldDirection()
        {
            Vector3 local = DirectionToLocalVector(direction);
            return transform.TransformDirection(local).normalized;
        }

        public static Vector3 DirectionToLocalVector(SocketDirection dir)
        {
            switch (dir)
            {
                case SocketDirection.North: return Vector3.forward;
                case SocketDirection.South: return Vector3.back;
                case SocketDirection.East: return Vector3.right;
                case SocketDirection.West: return Vector3.left;
                case SocketDirection.Up: return Vector3.up;
                case SocketDirection.Down: return Vector3.down;
                default: return Vector3.forward;
            }
        }

        // Single implementation lives in the deterministic core; these stay as the
        // Unity-side entry points so existing call sites are unaffected.
        public static SocketDirection Opposite(SocketDirection dir) => Directions.Opposite(dir);

        public static Vector2Int DirectionToGridOffset(SocketDirection dir)
        {
            var cell = Directions.ToGridOffset(dir);
            return new Vector2Int(cell.X, cell.Z);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = occupied ? Color.red : Color.cyan;
            Gizmos.DrawSphere(transform.position, 0.15f);
            Gizmos.DrawRay(transform.position, GetWorldDirection() * 0.75f);
        }
    }
}
