using System.Collections.Generic;
using UnityEngine;

namespace CatchIfYouCan.Electronics
{
    /// <summary>
    /// Every electronic device currently in the world.
    ///
    /// <para>
    /// Interference is mutual: a detector wants to know what else is running near it, and the
    /// only alternative to a list is a scene sweep. The EMF reader used to call
    /// <c>FindObjectsByType</c> every frame it was switched on, which walks every object in the
    /// scene to find a handful of them - and that is the cost this exists to remove, before
    /// eleven items each start paying it.
    /// </para>
    ///
    /// <para>
    /// Devices add themselves when they wake and remove themselves when they sleep, so a
    /// destroyed device is not a null in someone's loop next frame.
    /// </para>
    /// </summary>
    public static class ElectronicDeviceRegistry
    {
        private static readonly List<IElectronicDevice> Devices = new List<IElectronicDevice>();

        /// <summary>Read-only view. Do not hold onto it across frames.</summary>
        public static IReadOnlyList<IElectronicDevice> Active => Devices;

        public static void Register(IElectronicDevice device)
        {
            if (device != null && !Devices.Contains(device))
                Devices.Add(device);
        }

        public static void Unregister(IElectronicDevice device)
        {
            if (device != null)
                Devices.Remove(device);
        }

        /// <summary>
        /// Total interference reaching a point, falling off linearly to nothing at
        /// <paramref name="range"/>. Skips <paramref name="self"/> so a device does not
        /// measure itself.
        /// </summary>
        public static float InterferenceAt(Vector3 point, float range, IElectronicDevice self = null)
        {
            if (range <= 0f)
                return 0f;

            float total = 0f;
            for (int i = 0; i < Devices.Count; i++)
            {
                var device = Devices[i];
                if (device == null || ReferenceEquals(device, self) || !device.IsActive)
                    continue;

                float distance = Vector3.Distance(device.DevicePosition, point);
                if (distance >= range)
                    continue;

                total += device.InterferenceStrength * (1f - distance / range);
            }

            return total;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => Devices.Clear();
    }
}
