namespace CatchIfYouCan.Audio
{
    public enum DynamicRangeMode
    {
        Night,
        Normal,
        Wide
    }

    public enum HeadphoneMode
    {
        Off,
        Stereo,
        Spatial
    }

    public static class AudioAccessibilitySettings
    {
        public static DynamicRangeMode DynamicRange { get; set; } = DynamicRangeMode.Normal;
        public static HeadphoneMode Headphones { get; set; } = HeadphoneMode.Stereo;

        public static float GetDynamicRangeCompression(DynamicRangeMode mode)
        {
            return mode switch
            {
                DynamicRangeMode.Night => 0.55f,
                DynamicRangeMode.Wide => 1.25f,
                _ => 1f
            };
        }

        public static float GetHeadphoneSpatialBlend(HeadphoneMode mode)
        {
            return mode switch
            {
                HeadphoneMode.Off => 0f,
                HeadphoneMode.Spatial => 1f,
                _ => 0.35f
            };
        }
    }
}
