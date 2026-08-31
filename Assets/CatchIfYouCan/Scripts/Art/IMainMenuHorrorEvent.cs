namespace CatchIfYouCan.Art
{
    /// <summary>
    /// One self-contained supernatural beat in the main menu — a red-light surge, a blackout,
    /// a door slam, a ghost teleport.
    ///
    /// <para>
    /// Deliberately tiny. It exists so a future menu director can ask "is anything running?"
    /// before starting a second event, and so several events can be held in one array without
    /// knowing each other's types. It is not a lifecycle framework, and events are not
    /// required to know about each other.
    /// </para>
    ///
    /// <para>
    /// The contract every implementation owes the scene: capture authored values once, drive
    /// them from those captures, and restore them exactly — including when the event is
    /// interrupted by the component being disabled or the scene unloading.
    /// </para>
    /// </summary>
    public interface IMainMenuHorrorEvent
    {
        /// <summary>True while the event is mid-flight.</summary>
        bool IsPlaying { get; }

        /// <summary>
        /// Offers the event a chance to fire. Implementations return false when they decline —
        /// because one is already running, because references are missing, or because the
        /// event's own probability roll failed. A false return is normal, not an error: the
        /// caller carries on with whatever it was doing.
        /// </summary>
        bool TryBegin();

        /// <summary>
        /// Ends the event immediately and puts every value it touched back to its authored
        /// state. Safe to call when nothing is running.
        /// </summary>
        void CancelAndRestore();
    }
}
