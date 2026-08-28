using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatchIfYouCan.Audio
{
    [CreateAssetMenu(fileName = "AudioMixerConfig", menuName = "Catch If You Can/Audio Mixer Config")]
    public class AudioMixerConfig : ScriptableObject
    {
        [Serializable]
        public class GroupEntry
        {
            public AudioMixerGroupId Id;
            public string DisplayName;
            public string ExposedParameter;
            [Range(0f, 1f)] public float DefaultVolume = 1f;
        }

        [Serializable]
        public class SnapshotTransition
        {
            public AudioSnapshotId From;
            public AudioSnapshotId To;
            [Min(0f)] public float TransitionSeconds = 1f;
        }

        public List<GroupEntry> Groups = new List<GroupEntry>();
        public SnapshotTransition[] SnapshotTransitions = Array.Empty<SnapshotTransition>();
        public string MixerAssetPath = "Assets/CatchIfYouCan/Audio/Mixer/CatchIfYouCanAudioMixer.mixer";

        public float GetTransitionTime(AudioSnapshotId from, AudioSnapshotId to, float fallback = 1f)
        {
            if (SnapshotTransitions == null)
                return fallback;

            for (int i = 0; i < SnapshotTransitions.Length; i++)
            {
                var entry = SnapshotTransitions[i];
                if (entry != null && entry.From == from && entry.To == to)
                    return entry.TransitionSeconds;
            }

            return fallback;
        }

        public static AudioMixerConfig CreateDefault()
        {
            var config = CreateInstance<AudioMixerConfig>();
            config.name = "AudioMixerConfig";
            config.Groups = new List<GroupEntry>
            {
                new GroupEntry { Id = AudioMixerGroupId.Master, DisplayName = "Master", ExposedParameter = "MasterVolume" },
                new GroupEntry { Id = AudioMixerGroupId.Music, DisplayName = "Music", ExposedParameter = "MusicVolume" },
                new GroupEntry { Id = AudioMixerGroupId.Ambience, DisplayName = "Ambience", ExposedParameter = "AmbientVolume" },
                new GroupEntry { Id = AudioMixerGroupId.Ghost, DisplayName = "Ghost", ExposedParameter = "GhostVolume" },
                new GroupEntry { Id = AudioMixerGroupId.GhostVoice, DisplayName = "Ghost Voice", ExposedParameter = "GhostVolume" },
                new GroupEntry { Id = AudioMixerGroupId.GhostHunt, DisplayName = "Ghost Hunt", ExposedParameter = "GhostVolume" },
                new GroupEntry { Id = AudioMixerGroupId.Equipment, DisplayName = "Equipment", ExposedParameter = "EffectsVolume" },
                new GroupEntry { Id = AudioMixerGroupId.Foley, DisplayName = "Foley", ExposedParameter = "EffectsVolume" },
                new GroupEntry { Id = AudioMixerGroupId.Environment, DisplayName = "Environment", ExposedParameter = "EffectsVolume" },
                new GroupEntry { Id = AudioMixerGroupId.UI, DisplayName = "UI", ExposedParameter = "EffectsVolume" },
                new GroupEntry { Id = AudioMixerGroupId.Player, DisplayName = "Player", ExposedParameter = "EffectsVolume" },
                new GroupEntry { Id = AudioMixerGroupId.Weather, DisplayName = "Weather", ExposedParameter = "AmbientVolume" },
                new GroupEntry { Id = AudioMixerGroupId.Van, DisplayName = "Van", ExposedParameter = "AmbientVolume" }
            };
            config.SnapshotTransitions = new[]
            {
                new SnapshotTransition { From = AudioSnapshotId.Normal, To = AudioSnapshotId.HighTension, TransitionSeconds = 2f },
                new SnapshotTransition { From = AudioSnapshotId.HighTension, To = AudioSnapshotId.Hunt, TransitionSeconds = 0.4f },
                new SnapshotTransition { From = AudioSnapshotId.Hunt, To = AudioSnapshotId.Normal, TransitionSeconds = 4f },
                new SnapshotTransition { From = AudioSnapshotId.Normal, To = AudioSnapshotId.Pause, TransitionSeconds = 0.15f }
            };
            return config;
        }
    }
}
