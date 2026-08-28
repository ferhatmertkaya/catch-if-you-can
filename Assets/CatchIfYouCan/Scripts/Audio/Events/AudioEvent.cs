using UnityEngine;

namespace CatchIfYouCan.Audio
{
    public readonly struct AudioEvent
    {
        public AudioEventDefinition Definition { get; }
        public Vector3? Position { get; }
        public float VolumeScale { get; }
        public AudioClip ResolvedClip { get; }
        public float Volume { get; }
        public float Pitch { get; }

        public AudioEvent(AudioEventDefinition definition, Vector3? position = null, float volumeScale = 1f,
            AudioClip resolvedClip = null, float volume = 1f, float pitch = 1f)
        {
            Definition = definition;
            Position = position;
            VolumeScale = volumeScale;
            ResolvedClip = resolvedClip;
            Volume = volume;
            Pitch = pitch;
        }

        public static AudioEvent FromDefinition(AudioEventDefinition definition, Vector3? position = null,
            float volumeScale = 1f)
        {
            if (definition == null)
                return default;

            var clip = definition.PickClip();
            var volume = definition.SampleVolume() * volumeScale;
            var pitch = definition.SamplePitch();
            return new AudioEvent(definition, position, volumeScale, clip, volume, pitch);
        }

        public bool IsValid => Definition != null && ResolvedClip != null;
    }
}
