namespace CatchIfYouCan.Electronics
{
    public interface IElectronicDevice
    {
        bool IsPowered { get; }
        bool IsActive { get; }
        float InterferenceStrength { get; }
        string DeviceId { get; }

        /// <summary>
        /// Where the device is. Needed because interference falls off with distance, and
        /// asking for it through the interface is what lets a detector read every device in
        /// the room without knowing what any of them are.
        /// </summary>
        UnityEngine.Vector3 DevicePosition { get; }
    }
}
