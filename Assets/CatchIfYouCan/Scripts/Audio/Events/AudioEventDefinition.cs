using UnityEngine;

namespace CatchIfYouCan.Audio
{
    [CreateAssetMenu(fileName = "AudioEvent", menuName = "Catch If You Can/Audio Event")]
    public class AudioEventDefinition : ScriptableObject
    {
        public string EventId;
        public AudioClip[] ClipVariants = System.Array.Empty<AudioClip>();
        public AudioMixerGroupId MixerGroup = AudioMixerGroupId.Environment;
        [Range(0f, 1f)] public float VolumeMin = 0.85f;
        [Range(0f, 1f)] public float VolumeMax = 1f;
        [Range(0.1f, 3f)] public float PitchMin = 0.95f;
        [Range(0.1f, 3f)] public float PitchMax = 1.05f;
        [Range(0f, 1f)] public float SpatialBlend = 1f;
        [Min(0.1f)] public float MinDistance = 1f;
        [Min(1f)] public float MaxDistance = 40f;
        [Min(0f)] public float Cooldown = 0f;
        public AudioPriority Priority = AudioPriority.Medium;
        public bool Loop;
        public bool OcclusionEnabled = true;
        [Range(0f, 1f)] public float ReverbSend;
        public bool CanInterrupt;
        public bool OptionalRandomStartOffset;
        [Range(0f, 5f)] public float DopplerLevel;

        [System.NonSerialized] private AudioClipShuffleBag _shuffleBag;

        public AudioClip PickClip()
        {
            if (ClipVariants == null || ClipVariants.Length == 0)
                return null;

            _shuffleBag ??= new AudioClipShuffleBag(EventId?.GetHashCode() ?? GetInstanceID());
            return _shuffleBag.Pick(ClipVariants);
        }

        public float SampleVolume()
        {
            return Random.Range(Mathf.Min(VolumeMin, VolumeMax), Mathf.Max(VolumeMin, VolumeMax));
        }

        public float SamplePitch()
        {
            return Random.Range(Mathf.Min(PitchMin, PitchMax), Mathf.Max(PitchMin, PitchMax));
        }
    }
}
