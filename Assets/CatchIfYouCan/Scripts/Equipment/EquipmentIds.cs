namespace CatchIfYouCan.Equipment
{
    /// <summary>
    /// The eleven equipment ids, as constants, in catalogue order.
    ///
    /// <para>
    /// They were string literals scattered across the definition factory, the runtime factory,
    /// the loadout, the player factory and the labs. A typo in any of them produced an id that
    /// resolved to nothing, and - until V2 - to a working flashlight. The ids stay strings
    /// because they are the stable data identity that saves and a future wire format depend on;
    /// they are just no longer retyped.
    /// </para>
    ///
    /// <para>
    /// Order is meaningful: it is the catalogue order and the index a compact encoding would
    /// send.
    /// </para>
    /// </summary>
    public static class EquipmentIds
    {
        public const string Flashlight = "flashlight";
        public const string EmfDetector = "emf_detector";
        public const string UvLight = "uv_light";
        public const string Thermometer = "thermometer";
        public const string EvpRecorder = "evp_recorder";
        public const string ParabolicMicrophone = "parabolic_microphone";
        public const string PhotoCamera = "photo_camera";
        public const string SpectralGrid = "spectral_grid";
        public const string VideoCamera = "video_camera";
        public const string WardingRelic = "warding_relic";
        public const string Salt = "salt";

        /// <summary>Every canonical id, in catalogue order. The definitive roster.</summary>
        public static readonly string[] All =
        {
            Flashlight,
            EmfDetector,
            UvLight,
            Thermometer,
            EvpRecorder,
            ParabolicMicrophone,
            PhotoCamera,
            SpectralGrid,
            VideoCamera,
            WardingRelic,
            Salt,
        };

        public static bool IsCanonical(string id)
        {
            if (string.IsNullOrEmpty(id))
                return false;

            for (int i = 0; i < All.Length; i++)
                if (string.Equals(All[i], id, System.StringComparison.Ordinal))
                    return true;

            return false;
        }

        /// <summary>Catalogue index, or -1. What a byte-sized wire encoding would send.</summary>
        public static int IndexOf(string id)
        {
            for (int i = 0; i < All.Length; i++)
                if (string.Equals(All[i], id, System.StringComparison.Ordinal))
                    return i;

            return -1;
        }
    }
}
