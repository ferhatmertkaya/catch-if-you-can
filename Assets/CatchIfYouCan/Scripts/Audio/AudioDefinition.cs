using UnityEngine;

namespace CatchIfYouCan.Audio
{
    [CreateAssetMenu(fileName = "AudioDefinition", menuName = "Catch If You Can/Audio Definition")]
    public class AudioDefinition : ScriptableObject
    {
        public string Id;
        public AudioClip Clip;
        [Range(0f, 1f)] public float Volume = 1f;
        [Range(0.1f, 3f)] public float Pitch = 1f;
        public bool Loop;
        public bool Spatial;
        [Range(0f, 1f)] public float SpatialBlend = 1f;
        [Range(1f, 500f)] public float MaxDistance = 40f;

        public AudioEventDefinition ToEventDefinition()
        {
            var def = ScriptableObject.CreateInstance<AudioEventDefinition>();
            def.EventId = string.IsNullOrWhiteSpace(Id) ? name : Id;
            def.ClipVariants = Clip != null ? new[] { Clip } : System.Array.Empty<AudioClip>();
            def.VolumeMin = Volume;
            def.VolumeMax = Volume;
            def.PitchMin = Pitch;
            def.PitchMax = Pitch;
            def.SpatialBlend = Spatial ? SpatialBlend : 0f;
            def.MaxDistance = MaxDistance;
            def.Loop = Loop;
            def.MixerGroup = Spatial ? AudioMixerGroupId.Environment : AudioMixerGroupId.UI;
            return def;
        }
    }
}
