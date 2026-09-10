namespace CatchIfYouCan.Procedural
{
    // These enums live in their own file, free of UnityEngine, so the deterministic
    // generation core (CatchIfYouCan.Procedural.Deterministic) can reference them without
    // pulling in the engine. Moved out of RoomSocket.cs; the names and namespace are
    // unchanged, so every existing reference keeps working.

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
}
