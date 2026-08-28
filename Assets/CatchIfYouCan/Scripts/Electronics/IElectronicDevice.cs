namespace CatchIfYouCan.Electronics
{
    public interface IElectronicDevice
    {
        bool IsPowered { get; }
        bool IsActive { get; }
        float InterferenceStrength { get; }
        string DeviceId { get; }
    }
}
