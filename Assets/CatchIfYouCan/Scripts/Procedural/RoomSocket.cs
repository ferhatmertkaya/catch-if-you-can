using UnityEngine;

namespace CatchIfYouCan.Procedural
{
    public enum SocketType
    {
        Door,
        Wall,
        Window,
        Prop,
        Evidence,
        Hide,
        GhostInteract,
        Light
    }

    public enum SocketDirection
    {
        North,
        East,
        South,
        West,
        Up,
        Down
    }

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

        public static SocketDirection Opposite(SocketDirection dir)
        {
            switch (dir)
            {
                case SocketDirection.North: return SocketDirection.South;
                case SocketDirection.South: return SocketDirection.North;
                case SocketDirection.East: return SocketDirection.West;
                case SocketDirection.West: return SocketDirection.East;
                case SocketDirection.Up: return SocketDirection.Down;
                case SocketDirection.Down: return SocketDirection.Up;
                default: return SocketDirection.South;
            }
        }

        public static Vector2Int DirectionToGridOffset(SocketDirection dir)
        {
            switch (dir)
            {
                case SocketDirection.North: return new Vector2Int(0, 1);
                case SocketDirection.South: return new Vector2Int(0, -1);
                case SocketDirection.East: return new Vector2Int(1, 0);
                case SocketDirection.West: return new Vector2Int(-1, 0);
                default: return Vector2Int.zero;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = occupied ? Color.red : Color.cyan;
            Gizmos.DrawSphere(transform.position, 0.15f);
            Gizmos.DrawRay(transform.position, GetWorldDirection() * 0.75f);
        }
    }
}
