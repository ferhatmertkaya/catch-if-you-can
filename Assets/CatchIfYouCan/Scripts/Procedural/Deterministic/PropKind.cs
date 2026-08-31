namespace CatchIfYouCan.Procedural.Deterministic
{
    /// <summary>
    /// Placement class of a prop. Furniture and small props are hashed as separate
    /// sections so a divergence report names which of the two diverged.
    /// </summary>
    public enum PropKind
    {
        Prop = 0,
        Furniture = 1,
    }
}
