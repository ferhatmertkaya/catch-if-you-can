using System.Collections.Generic;
using CatchIfYouCan.Utilities;
using UnityEngine;

namespace CatchIfYouCan.Audio
{
    /// <summary>
    /// Simulates mixer group mutes/volumes when no AudioMixer asset is wired.
    /// AudioManager channel volumes are scaled by group mute state.
    /// </summary>
    public class RuntimeAudioBusRouter : SingletonBehaviour<RuntimeAudioBusRouter>
    {
        private readonly Dictionary<AudioMixerGroupId, bool> _muted = new Dictionary<AudioMixerGroupId, bool>();
        private readonly Dictionary<AudioMixerGroupId, float> _savedVolumes = new Dictionary<AudioMixerGroupId, float>();

        protected override void Awake()
        {
            persist = true;
            base.Awake();
            foreach (AudioMixerGroupId id in System.Enum.GetValues(typeof(AudioMixerGroupId)))
                _muted[id] = false;
        }

        public bool IsMuted(AudioMixerGroupId group) =>
            _muted.TryGetValue(group, out bool muted) && muted;

        public void SetMuted(AudioMixerGroupId group, bool muted)
        {
            _muted[group] = muted;
            ApplyToManager(group, muted);
        }

        public void ToggleMuted(AudioMixerGroupId group)
        {
            SetMuted(group, !IsMuted(group));
        }

        public float GetEffectiveVolumeScale(AudioMixerGroupId group)
        {
            return IsMuted(group) ? 0f : 1f;
        }

        private void ApplyToManager(AudioMixerGroupId group, bool muted)
        {
            var manager = AudioManager.Instance;
            if (manager == null)
                return;

            if (muted)
            {
                CaptureAndZero(group, manager);
                return;
            }

            Restore(group, manager);
        }

        private void CaptureAndZero(AudioMixerGroupId group, AudioManager manager)
        {
            switch (group)
            {
                case AudioMixerGroupId.Music:
                    _savedVolumes[group] = manager.GetMusicVolume();
                    manager.SetMusicVolume(0f);
                    break;
                case AudioMixerGroupId.Ambience:
                case AudioMixerGroupId.Weather:
                case AudioMixerGroupId.Van:
                    _savedVolumes[group] = manager.GetAmbientVolume();
                    manager.SetAmbientVolume(0f);
                    break;
                case AudioMixerGroupId.Ghost:
                case AudioMixerGroupId.GhostVoice:
                case AudioMixerGroupId.GhostHunt:
                    _savedVolumes[group] = manager.GetGhostVolume();
                    manager.SetGhostVolume(0f);
                    break;
                case AudioMixerGroupId.Equipment:
                    _savedVolumes[group] = manager.GetEquipmentVolume();
                    manager.SetEquipmentVolume(0f);
                    break;
                case AudioMixerGroupId.UI:
                    _savedVolumes[group] = manager.GetUIVolume();
                    manager.SetUIVolume(0f);
                    break;
                case AudioMixerGroupId.Player:
                case AudioMixerGroupId.Foley:
                case AudioMixerGroupId.Environment:
                    _savedVolumes[group] = manager.GetEffectsVolume();
                    manager.SetEffectsVolume(0f);
                    break;
            }
        }

        private void Restore(AudioMixerGroupId group, AudioManager manager)
        {
            if (!_savedVolumes.TryGetValue(group, out float linear))
                return;

            switch (group)
            {
                case AudioMixerGroupId.Music:
                    manager.SetMusicVolume(linear);
                    break;
                case AudioMixerGroupId.Ambience:
                case AudioMixerGroupId.Weather:
                case AudioMixerGroupId.Van:
                    manager.SetAmbientVolume(linear);
                    break;
                case AudioMixerGroupId.Ghost:
                case AudioMixerGroupId.GhostVoice:
                case AudioMixerGroupId.GhostHunt:
                    manager.SetGhostVolume(linear);
                    break;
                case AudioMixerGroupId.Equipment:
                    manager.SetEquipmentVolume(linear);
                    break;
                case AudioMixerGroupId.UI:
                    manager.SetUIVolume(linear);
                    break;
                case AudioMixerGroupId.Player:
                case AudioMixerGroupId.Foley:
                case AudioMixerGroupId.Environment:
                    manager.SetEffectsVolume(linear);
                    break;
            }
        }
    }
}
