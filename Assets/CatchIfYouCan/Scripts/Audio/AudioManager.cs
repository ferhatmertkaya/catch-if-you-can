using CatchIfYouCan.Save;
using CatchIfYouCan.Utilities;
using UnityEngine;
using UnityEngine.Audio;

namespace CatchIfYouCan.Audio
{
    public class AudioManager : SingletonBehaviour<AudioManager>
    {
        [SerializeField] private AudioMixer mixer;
        [SerializeField] private string masterParam = "MasterVolume";
        [SerializeField] private string musicParam = "MusicVolume";
        [SerializeField] private string ambientParam = "AmbientVolume";
        [SerializeField] private string effectsParam = "EffectsVolume";
        [SerializeField] private string voiceParam = "VoiceVolume";
        [SerializeField] private string ghostParam = "GhostVolume";

        private AudioSource _ambientSource;
        private AudioSource _musicSource;
        private AudioSource _uiSource;
        private AudioSource _effectsSource;
        private AudioSource _ghostSource;
        private AudioSource _playerSource;
        private AudioSource _equipmentSource;

        private AudioEventLibrary _eventLibrary;
        private AudioSnapshotController _snapshotController;
        private AudioQualityController _qualityController;

        private float _masterVolume = 1f;
        private float _musicVolume = 1f;
        private float _ambientVolume = 1f;
        private float _effectsVolume = 1f;
        private float _voiceVolume = 1f;
        private float _ghostVolume = 1f;
        private float _equipmentVolume = 1f;
        private float _uiVolume = 1f;

        private float _snapshotMaster = 1f;
        private float _snapshotMusic = 1f;
        private float _snapshotAmbient = 1f;
        private float _snapshotGhost = 1f;

        public DynamicRangeMode DynamicRangeMode
        {
            get => AudioAccessibilitySettings.DynamicRange;
            set
            {
                AudioAccessibilitySettings.DynamicRange = value;
                RefreshChannelVolumes();
            }
        }

        public HeadphoneMode HeadphoneMode
        {
            get => AudioAccessibilitySettings.Headphones;
            set => AudioAccessibilitySettings.Headphones = value;
        }

        public AudioEventLibrary EventLibrary => _eventLibrary;
        public AudioSnapshotController SnapshotController => _snapshotController;
        public AudioQualityController QualityController => _qualityController;

        protected override void Awake()
        {
            persist = true;
            base.Awake();
            EnsureSubsystems();
            BuildSources();
            RefreshChannelVolumes();
        }

        private void EnsureSubsystems()
        {
            if (AudioEmitterPool.Instance == null)
            {
                var poolGo = new GameObject("AudioEmitterPool");
                poolGo.transform.SetParent(transform, false);
                poolGo.AddComponent<AudioEmitterPool>();
            }

            _eventLibrary = GetComponent<AudioEventLibrary>();
            if (_eventLibrary == null)
                _eventLibrary = gameObject.AddComponent<AudioEventLibrary>();
            _eventLibrary.Initialize();

            _snapshotController = GetComponent<AudioSnapshotController>();
            if (_snapshotController == null)
                _snapshotController = gameObject.AddComponent<AudioSnapshotController>();
            _snapshotController.Initialize(mixer);

            if (AudioQualityController.Instance == null)
            {
                var qualityGo = new GameObject("AudioQualityController");
                qualityGo.transform.SetParent(transform, false);
                _qualityController = qualityGo.AddComponent<AudioQualityController>();
            }
            else
            {
                _qualityController = AudioQualityController.Instance;
            }

            if (RuntimeAudioBusRouter.Instance == null)
            {
                var routerGo = new GameObject("RuntimeAudioBusRouter");
                routerGo.transform.SetParent(transform, false);
                routerGo.AddComponent<RuntimeAudioBusRouter>();
            }
        }

        private void BuildSources()
        {
            _ambientSource = CreateSource("Ambient", loop: true, spatial: false);
            _musicSource = CreateSource("Music", loop: true, spatial: false);
            _uiSource = CreateSource("UI", loop: false, spatial: false);
            _effectsSource = CreateSource("Effects", loop: false, spatial: false);
            _ghostSource = CreateSource("Ghost", loop: false, spatial: true);
            _playerSource = CreateSource("Player", loop: false, spatial: true);
            _equipmentSource = CreateSource("Equipment", loop: false, spatial: true);
        }

        private AudioSource CreateSource(string label, bool loop, bool spatial)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = loop;
            src.spatialBlend = spatial ? 1f : 0f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.maxDistance = 45f;
            return src;
        }

        public void ApplyFromSettings(SettingsManager settings)
        {
            if (settings == null) return;

            SetMasterVolume(settings.MasterVolume);
            SetMusicVolume(settings.MusicVolume);
            SetAmbientVolume(settings.AmbientVolume);
            SetVoiceVolume(settings.VoiceVolume);
            SetEffectsVolume(settings.EffectsVolume);
            SetGhostVolume(settings.GhostVolume);
            SetEquipmentVolume(settings.EquipmentVolume);
            SetUIVolume(settings.UIVolume);

            DynamicRangeMode = settings.DynamicRangeMode;
            HeadphoneMode = settings.HeadphoneMode;

            _qualityController?.ApplyFromSettings(settings);
            RefreshChannelVolumes();
        }

        public float GetMasterVolume() => _masterVolume;
        public float GetMusicVolume() => _musicVolume;
        public float GetAmbientVolume() => _ambientVolume;
        public float GetEffectsVolume() => _effectsVolume;
        public float GetVoiceVolume() => _voiceVolume;
        public float GetGhostVolume() => _ghostVolume;
        public float GetEquipmentVolume() => _equipmentVolume;
        public float GetUIVolume() => _uiVolume;

        public void SetMasterVolume(float linear) { _masterVolume = Mathf.Clamp01(linear); RefreshChannelVolumes(); }
        public void SetMusicVolume(float linear) { _musicVolume = Mathf.Clamp01(linear); RefreshChannelVolumes(); }
        public void SetAmbientVolume(float linear) { _ambientVolume = Mathf.Clamp01(linear); RefreshChannelVolumes(); }
        public void SetEffectsVolume(float linear) { _effectsVolume = Mathf.Clamp01(linear); RefreshChannelVolumes(); }
        public void SetVoiceVolume(float linear) { _voiceVolume = Mathf.Clamp01(linear); RefreshChannelVolumes(); }
        public void SetGhostVolume(float linear) { _ghostVolume = Mathf.Clamp01(linear); RefreshChannelVolumes(); }
        public void SetEquipmentVolume(float linear) { _equipmentVolume = Mathf.Clamp01(linear); RefreshChannelVolumes(); }
        public void SetUIVolume(float linear) { _uiVolume = Mathf.Clamp01(linear); RefreshChannelVolumes(); }

        public void ApplySnapshotMix(float master, float music, float ambient, float ghost)
        {
            _snapshotMaster = master;
            _snapshotMusic = music;
            _snapshotAmbient = ambient;
            _snapshotGhost = ghost;
            RefreshChannelVolumes();
        }

        public void TransitionSnapshot(AudioSnapshotId snapshot, float? transitionTime = null)
        {
            _snapshotController?.TransitionTo(snapshot, transitionTime);
        }

        public bool PlayEvent(string eventId, Vector3? position = null, float volumeScale = 1f)
        {
            if (_eventLibrary == null || string.IsNullOrWhiteSpace(eventId))
                return false;
            return _eventLibrary.Play(eventId, position, volumeScale);
        }

        public bool PlayEvent(AudioEventDefinition definition, Vector3? position = null, float volumeScale = 1f)
        {
            if (_eventLibrary == null || definition == null)
                return false;
            return _eventLibrary.Play(definition, position, volumeScale);
        }

        internal bool PlayEventInternal(AudioEvent request)
        {
            if (!request.IsValid)
                return false;

            var pool = AudioEmitterPool.Instance;
            if (pool == null)
                return PlayEventOnChannel(request);

            pool.UpdateActiveDistances();
            if (!pool.TryEvictForPriority(request.Definition.Priority))
                return false;

            var emitter = pool.Get();
            if (emitter == null)
                return false;

            ApplyAccessibilityToRequest(ref request);
            emitter.Play(request);

            if (!request.Definition.Loop)
                StartCoroutine(ReleaseWhenDone(emitter, request.ResolvedClip, request.Pitch));

            return true;
        }

        public void PlayOneShot(AudioClip clip, float volume = 1f, AudioChannel channel = AudioChannel.Effects)
        {
            if (clip == null) return;
            var src = GetChannelSource(channel);
            if (src == null) return;
            src.PlayOneShot(clip, ApplyDynamicRange(Mathf.Clamp01(volume), channel));
        }

        public void PlayDefinition(AudioDefinition def, Vector3? worldPos = null)
        {
            if (def == null || def.Clip == null) return;
            if (def.Spatial && worldPos.HasValue)
            {
                PlayAtPosition(def.Clip, worldPos.Value, def.Volume, def.SpatialBlend, def.MaxDistance, def.Pitch);
                return;
            }
            PlayOneShot(def.Clip, def.Volume);
        }

        public void PlayAtPosition(AudioClip clip, Vector3 position, float volume = 1f,
            float spatialBlend = 1f, float maxDistance = 40f, float pitch = 1f)
        {
            if (clip == null) return;

            var pool = AudioEmitterPool.Instance;
            if (pool != null)
            {
                pool.UpdateActiveDistances();
                if (!pool.TryEvictForPriority(AudioPriority.Medium))
                    return;

                var emitter = pool.Get();
                if (emitter == null) return;

                emitter.transform.position = position;
                emitter.PlayClip(clip, ApplyDynamicRange(volume, AudioChannel.Effects), pitch, false,
                    spatialBlend, 1f, maxDistance, AudioPriority.Medium, AudioMixerGroupId.Environment);
                StartCoroutine(ReleaseWhenDone(emitter, clip, pitch));
                return;
            }

            var go = new GameObject("OneShot3D");
            go.transform.position = position;
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.volume = ApplyDynamicRange(Mathf.Clamp01(volume), AudioChannel.Effects);
            src.spatialBlend = spatialBlend;
            src.maxDistance = maxDistance;
            src.pitch = pitch;
            src.Play();
            Object.Destroy(go, clip.length / Mathf.Max(0.01f, pitch) + 0.1f);
        }

        public void PlayAmbient(AudioClip clip, float volume = 1f)
        {
            if (_ambientSource == null) return;
            if (clip == null)
            {
                _ambientSource.Stop();
                return;
            }
            if (_ambientSource.clip == clip && _ambientSource.isPlaying) return;
            _ambientSource.clip = clip;
            _ambientSource.volume = ApplyDynamicRange(Mathf.Clamp01(volume), AudioChannel.Ambient);
            _ambientSource.loop = true;
            _ambientSource.Play();
        }

        public void PlayMusic(AudioClip clip, float volume = 1f)
        {
            if (_musicSource == null) return;
            if (clip == null)
            {
                _musicSource.Stop();
                return;
            }
            if (_musicSource.clip == clip && _musicSource.isPlaying) return;
            _musicSource.clip = clip;
            _musicSource.volume = ApplyDynamicRange(Mathf.Clamp01(volume), AudioChannel.Music);
            _musicSource.loop = true;
            _musicSource.Play();
        }

        public void PlayUI(AudioClip clip, float volume = 1f) => PlayOneShot(clip, volume, AudioChannel.UI);
        public void PlayGhost(AudioClip clip, float volume = 1f) => PlayOneShot(clip, volume, AudioChannel.Ghost);
        public void PlayPlayer(AudioClip clip, float volume = 1f) => PlayOneShot(clip, volume, AudioChannel.Player);
        public void PlayEquipment(AudioClip clip, float volume = 1f) => PlayOneShot(clip, volume, AudioChannel.Equipment);

        public void StopLoopingEvent(AudioChannel channel)
        {
            var src = GetChannelSource(channel);
            if (src == null) return;
            src.loop = false;
            src.Stop();
            src.clip = null;
        }

        private bool PlayEventOnChannel(AudioEvent request)
        {
            var channel = MapMixerGroupToChannel(request.Definition.MixerGroup);
            if (request.Position.HasValue && request.Definition.SpatialBlend > 0.01f)
            {
                PlayAtPosition(request.ResolvedClip, request.Position.Value, request.Volume,
                    request.Definition.SpatialBlend, request.Definition.MaxDistance, request.Pitch);
                return true;
            }

            PlayOneShot(request.ResolvedClip, request.Volume, channel);
            return true;
        }

        private System.Collections.IEnumerator ReleaseWhenDone(AudioEmitter emitter, AudioClip clip, float pitch)
        {
            if (emitter == null || clip == null)
                yield break;

            float duration = clip.length / Mathf.Max(0.01f, pitch) + 0.05f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (emitter == null || !emitter.IsPlaying)
                    break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            emitter?.Release();
        }

        private void ApplyAccessibilityToRequest(ref AudioEvent request)
        {
            if (request.Definition == null)
                return;

            float headphoneBlend = AudioAccessibilitySettings.GetHeadphoneSpatialBlend(HeadphoneMode);
            if (HeadphoneMode != HeadphoneMode.Off && request.Definition.SpatialBlend > 0f)
            {
                var def = request.Definition;
                float blend = Mathf.Lerp(request.Definition.SpatialBlend, headphoneBlend, 0.35f);
                // Spatial blend is applied on emitter from definition; volume scaled here.
                float channel = GetChannelLinear(MapMixerGroupToChannel(def.MixerGroup));
                request = new AudioEvent(def, request.Position, request.VolumeScale, request.ResolvedClip,
                    ApplyDynamicRange(request.Volume * channel, MapMixerGroupToChannel(def.MixerGroup)), request.Pitch);
            }
        }

        private AudioSource GetChannelSource(AudioChannel channel)
        {
            RefreshSourceVolume(channel);
            return channel switch
            {
                AudioChannel.Ambient => _ambientSource,
                AudioChannel.Music => _musicSource,
                AudioChannel.UI => _uiSource,
                AudioChannel.Ghost => _ghostSource,
                AudioChannel.Player => _playerSource,
                AudioChannel.Equipment => _equipmentSource,
                AudioChannel.Voice => _effectsSource,
                _ => _effectsSource
            };
        }

        private void RefreshChannelVolumes()
        {
            SetMixerVolume(masterParam, _masterVolume * _snapshotMaster);
            SetMixerVolume(musicParam, _musicVolume * _snapshotMusic);
            SetMixerVolume(ambientParam, _ambientVolume * _snapshotAmbient);
            SetMixerVolume(effectsParam, _effectsVolume);
            SetMixerVolume(voiceParam, _voiceVolume);
            SetMixerVolume(ghostParam, _ghostVolume * _snapshotGhost);

            RefreshSourceVolume(AudioChannel.Ambient);
            RefreshSourceVolume(AudioChannel.Music);
            RefreshSourceVolume(AudioChannel.UI);
            RefreshSourceVolume(AudioChannel.Effects);
            RefreshSourceVolume(AudioChannel.Ghost);
            RefreshSourceVolume(AudioChannel.Player);
            RefreshSourceVolume(AudioChannel.Equipment);
        }

        private void RefreshSourceVolume(AudioChannel channel)
        {
            var src = channel switch
            {
                AudioChannel.Ambient => _ambientSource,
                AudioChannel.Music => _musicSource,
                AudioChannel.UI => _uiSource,
                AudioChannel.Ghost => _ghostSource,
                AudioChannel.Player => _playerSource,
                AudioChannel.Equipment => _equipmentSource,
                AudioChannel.Voice => _effectsSource,
                _ => _effectsSource
            };
            if (src == null) return;
            src.volume = ApplyDynamicRange(GetChannelLinear(channel), channel);
        }

        private float GetChannelLinear(AudioChannel channel)
        {
            return channel switch
            {
                AudioChannel.Ambient => _ambientVolume * _snapshotAmbient,
                AudioChannel.Music => _musicVolume * _snapshotMusic,
                AudioChannel.UI => _uiVolume,
                AudioChannel.Ghost => _ghostVolume * _snapshotGhost,
                AudioChannel.Player => _effectsVolume,
                AudioChannel.Equipment => _equipmentVolume,
                AudioChannel.Voice => _voiceVolume,
                _ => _effectsVolume
            };
        }

        private float ApplyDynamicRange(float linear, AudioChannel channel)
        {
            float compression = AudioAccessibilitySettings.GetDynamicRangeCompression(DynamicRangeMode);
            if (channel == AudioChannel.UI)
                return Mathf.Clamp01(linear);

            return Mathf.Clamp01(Mathf.Pow(linear, compression));
        }

        private static AudioChannel MapMixerGroupToChannel(AudioMixerGroupId group)
        {
            return group switch
            {
                AudioMixerGroupId.Master => AudioChannel.Effects,
                AudioMixerGroupId.Ambience or AudioMixerGroupId.Weather or AudioMixerGroupId.Van => AudioChannel.Ambient,
                AudioMixerGroupId.Ghost or AudioMixerGroupId.GhostVoice or AudioMixerGroupId.GhostHunt => AudioChannel.Ghost,
                AudioMixerGroupId.Equipment => AudioChannel.Equipment,
                AudioMixerGroupId.UI => AudioChannel.UI,
                AudioMixerGroupId.Music => AudioChannel.Music,
                AudioMixerGroupId.Player => AudioChannel.Player,
                _ => AudioChannel.Effects
            };
        }

        private void SetMixerVolume(string param, float linear)
        {
            if (mixer == null || string.IsNullOrEmpty(param)) return;
            float db = linear <= 0.0001f ? -80f : Mathf.Log10(Mathf.Clamp01(linear)) * 20f;
            mixer.SetFloat(param, db);
        }
    }

    public enum AudioChannel
    {
        Effects,
        UI,
        Ambient,
        Music,
        Ghost,
        Player,
        Equipment,
        Voice
    }
}
