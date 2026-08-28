using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatchIfYouCan.Audio
{
    public class AudioEventLibrary : MonoBehaviour
    {
        [SerializeField] private List<AudioEventDefinition> assignedEvents = new List<AudioEventDefinition>();
        [SerializeField] private bool loadFromResources = true;
        [SerializeField] private string resourcesPath = "AudioEvents";

        private readonly Dictionary<string, AudioEventDefinition> _byId =
            new Dictionary<string, AudioEventDefinition>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, float> _lastPlayTime =
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

        private bool _defaultsRegistered;

        public IReadOnlyDictionary<string, AudioEventDefinition> Events => _byId;

        public void Initialize()
        {
            RebuildIndex();
            EnsureDefaults();
        }

        public void RebuildIndex()
        {
            _byId.Clear();

            if (loadFromResources && !string.IsNullOrEmpty(resourcesPath))
            {
                var loaded = Resources.LoadAll<AudioEventDefinition>(resourcesPath);
                foreach (var def in loaded)
                    Register(def);
            }

            foreach (var def in assignedEvents)
                Register(def);
        }

        public void Register(AudioEventDefinition definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.EventId))
                return;
            _byId[definition.EventId] = definition;
        }

        public AudioEventDefinition Find(string eventId)
        {
            if (string.IsNullOrWhiteSpace(eventId))
                return null;
            if (_byId.TryGetValue(eventId, out var def))
                return def;

            // Auto-bind procedural fallbacks for dotted gameplay event ids.
            EnsureDefaults();
            if (_byId.TryGetValue(eventId, out def))
                return def;

            var clip = ProceduralAudioSynth.CreateForEventId(eventId);
            if (clip == null)
                return null;

            var group = InferGroup(eventId);
            RegisterProcedural(eventId, clip, group, InferPriority(eventId),
                spatialBlend: InferSpatial(eventId),
                maxDistance: InferMaxDistance(eventId),
                loop: InferLoop(eventId),
                reverbSend: InferReverb(eventId));
            return FindExisting(eventId);
        }

        private AudioEventDefinition FindExisting(string eventId)
        {
            return _byId.TryGetValue(eventId, out var def) ? def : null;
        }

        private static AudioMixerGroupId InferGroup(string eventId)
        {
            string id = eventId.ToLowerInvariant();
            if (id.StartsWith("ui.") || id.Contains("journal") || id.Contains("mission")) return AudioMixerGroupId.UI;
            if (id.StartsWith("ghost.hunt") || id.Contains(".hunt")) return AudioMixerGroupId.GhostHunt;
            if (id.Contains("whisper") || id.Contains("voice") || id.Contains("evp") || id.Contains("sob") || id.Contains("breath"))
                return AudioMixerGroupId.GhostVoice;
            if (id.StartsWith("ghost.") || id.StartsWith("psycho.")) return AudioMixerGroupId.Ghost;
            if (id.StartsWith("equip.")) return AudioMixerGroupId.Equipment;
            if (id.StartsWith("weather.")) return AudioMixerGroupId.Weather;
            if (id.StartsWith("player.") || id.Contains("footstep") || id.Contains("heartbeat")) return AudioMixerGroupId.Player;
            if (id.StartsWith("env.van") || id.Contains("van.")) return AudioMixerGroupId.Van;
            if (id.StartsWith("env.door") || id.Contains("furniture") || id.Contains("wardrobe") || id.Contains("creak"))
                return AudioMixerGroupId.Environment;
            if (id.StartsWith("ambience.") || id.Contains("roomtone") || id.Contains("hum")) return AudioMixerGroupId.Ambience;
            if (id.Contains("music") || id.Contains("tension")) return AudioMixerGroupId.Music;
            return AudioMixerGroupId.Environment;
        }

        private static AudioPriority InferPriority(string eventId)
        {
            string id = eventId.ToLowerInvariant();
            if (id.Contains("hunt") || id.Contains("death") || id.Contains("slam")) return AudioPriority.Critical;
            if (id.Contains("whisper") || id.Contains("emf") || id.Contains("manifest")) return AudioPriority.High;
            if (id.Contains("footstep") || id.Contains("ambience") || id.Contains("weather")) return AudioPriority.Low;
            return AudioPriority.Medium;
        }

        private static float InferSpatial(string eventId)
        {
            string id = eventId.ToLowerInvariant();
            if (id.StartsWith("ui.") || id.Contains("heartbeat") || id.Contains("tinnitus") || id.Contains("mission"))
                return 0f;
            if (id.Contains("rain.interior") || id.Contains("weather.clear") || id.Contains("tension"))
                return 0.15f;
            return 1f;
        }

        private static float InferMaxDistance(string eventId)
        {
            string id = eventId.ToLowerInvariant();
            if (id.Contains("whisper") || id.Contains("breath")) return 8f;
            if (id.Contains("footstep")) return 18f;
            if (id.Contains("slam") || id.Contains("thunder")) return 55f;
            return 35f;
        }

        private static bool InferLoop(string eventId)
        {
            string id = eventId.ToLowerInvariant();
            return id.Contains("hum") || id.Contains("rain") || id.Contains("loop") ||
                   id.Contains("heartbeat") || id.Contains("presence") || id.Contains("tension") ||
                   id.Contains("active") || id.Contains("fog.ambient") || id.Contains("nv.hum");
        }

        private static float InferReverb(string eventId)
        {
            string id = eventId.ToLowerInvariant();
            if (id.Contains("whisper") || id.Contains("manifest") || id.Contains("hollow")) return 0.55f;
            if (id.Contains("slam") || id.Contains("basement")) return 0.4f;
            return 0.15f;
        }

        public bool Play(string eventId, Vector3? position = null, float volumeScale = 1f)
        {
            var def = Find(eventId);
            if (def == null)
            {
                // Last-resort direct synth playback so gameplay never goes fully silent.
                var clip = ProceduralAudioSynth.CreateForEventId(eventId);
                if (clip == null || AudioManager.Instance == null)
                    return false;
                if (position.HasValue)
                    AudioManager.Instance.PlayAtPosition(clip, position.Value, volumeScale);
                else
                    AudioManager.Instance.PlayOneShot(clip, volumeScale);
                return true;
            }

            return Play(def, position, volumeScale);
        }

        public bool Play(AudioEventDefinition definition, Vector3? position = null, float volumeScale = 1f)
        {
            if (definition == null)
                return false;

            if (definition.Cooldown > 0f && !string.IsNullOrEmpty(definition.EventId))
            {
                if (_lastPlayTime.TryGetValue(definition.EventId, out float last) &&
                    Time.unscaledTime - last < definition.Cooldown)
                    return false;
                _lastPlayTime[definition.EventId] = Time.unscaledTime;
            }

            var request = AudioEvent.FromDefinition(definition, position, volumeScale);
            if (!request.IsValid)
            {
                EnsureClipForDefinition(definition);
                request = AudioEvent.FromDefinition(definition, position, volumeScale);
            }

            if (!request.IsValid)
                return false;

            if (AudioManager.Instance != null)
                return AudioManager.Instance.PlayEventInternal(request);

            return false;
        }

        public void EnsureDefaults()
        {
            if (_defaultsRegistered)
                return;

            _defaultsRegistered = true;
            RegisterProcedural("ui.click", ProceduralAudioSynth.CreateBeep(880f, 0.06f, 0.35f),
                AudioMixerGroupId.UI, AudioPriority.High, spatialBlend: 0f);
            RegisterProcedural("ui.back", ProceduralAudioSynth.CreateBeep(440f, 0.05f, 0.3f),
                AudioMixerGroupId.UI, AudioPriority.High, spatialBlend: 0f);
            RegisterProcedural("ui.confirm", ProceduralAudioSynth.CreateBeep(660f, 0.08f, 0.4f),
                AudioMixerGroupId.UI, AudioPriority.High, spatialBlend: 0f);
            RegisterProcedural("door.open", ProceduralAudioSynth.CreateCreak(0.45f),
                AudioMixerGroupId.Foley, AudioPriority.Medium);
            RegisterProcedural("door.close", ProceduralAudioSynth.CreateImpact(0.35f, 120f),
                AudioMixerGroupId.Foley, AudioPriority.Medium);
            RegisterProcedural("door.slam", ProceduralAudioSynth.CreateImpact(0.55f, 80f),
                AudioMixerGroupId.Foley, AudioPriority.High);
            RegisterProcedural("footstep", ProceduralAudioSynth.CreateFootstepThud(),
                AudioMixerGroupId.Foley, AudioPriority.Low, maxDistance: 18f);
            RegisterProcedural("equipment.beep", ProceduralAudioSynth.CreateBeep(1200f, 0.04f, 0.25f),
                AudioMixerGroupId.Equipment, AudioPriority.Medium);
            RegisterProcedural("equipment.scan", ProceduralAudioSynth.CreateClickTrain(24f, 0.25f),
                AudioMixerGroupId.Equipment, AudioPriority.Medium, loop: true);
            RegisterProcedural("ghost.whisper", ProceduralAudioSynth.CreateWhisperTexture(1.2f),
                AudioMixerGroupId.GhostVoice, AudioPriority.High, reverbSend: 0.6f);
            RegisterProcedural("ghost.event", ProceduralAudioSynth.CreateNoiseBurst(0.35f, 0.4f),
                AudioMixerGroupId.Ghost, AudioPriority.High, reverbSend: 0.45f);
            RegisterProcedural("ghost.hunt.start", ProceduralAudioSynth.CreateImpact(0.7f, 55f),
                AudioMixerGroupId.GhostHunt, AudioPriority.Critical, canInterrupt: true);
            RegisterProcedural("ghost.hunt.loop", ProceduralAudioSynth.CreateHeartbeatLoop(),
                AudioMixerGroupId.GhostHunt, AudioPriority.Critical, loop: true, spatialBlend: 0.2f);
            RegisterProcedural("player.heartbeat", ProceduralAudioSynth.CreateHeartbeatLoop(),
                AudioMixerGroupId.Player, AudioPriority.High, loop: true, spatialBlend: 0f);
            RegisterProcedural("player.death", ProceduralAudioSynth.CreateNoiseBurst(0.8f, 0.25f),
                AudioMixerGroupId.Player, AudioPriority.Critical, spatialBlend: 0f);
            RegisterProcedural("ambient.rain", ProceduralAudioSynth.CreateRainLoop(),
                AudioMixerGroupId.Weather, AudioPriority.Low, loop: true, spatialBlend: 0f, maxDistance: 1f);
            RegisterProcedural("ambient.hum", ProceduralAudioSynth.CreateHumLoop(60f),
                AudioMixerGroupId.Ambience, AudioPriority.Low, loop: true, spatialBlend: 0f, maxDistance: 1f);
            RegisterProcedural("van.idle", ProceduralAudioSynth.CreateHumLoop(45f),
                AudioMixerGroupId.Van, AudioPriority.Low, loop: true, spatialBlend: 0f, maxDistance: 1f);
            RegisterProcedural("evidence.detected", ProceduralAudioSynth.CreateBeep(990f, 0.12f, 0.45f),
                AudioMixerGroupId.UI, AudioPriority.High, spatialBlend: 0f);
            RegisterProcedural("noise.clatter", ProceduralAudioSynth.CreateClickTrain(18f, 0.18f),
                AudioMixerGroupId.Environment, AudioPriority.Medium);
        }

        private void RegisterProcedural(string eventId, AudioClip clip, AudioMixerGroupId group,
            AudioPriority priority, float spatialBlend = 1f, float maxDistance = 40f, bool loop = false,
            float reverbSend = 0f, bool canInterrupt = false)
        {
            if (string.IsNullOrWhiteSpace(eventId) || FindExisting(eventId) != null)
                return;

            var def = ScriptableObject.CreateInstance<AudioEventDefinition>();
            def.EventId = eventId;
            def.ClipVariants = clip != null ? new[] { clip } : null;
            def.MixerGroup = group;
            def.Priority = priority;
            def.SpatialBlend = spatialBlend;
            def.MaxDistance = maxDistance;
            def.Loop = loop;
            def.ReverbSend = reverbSend;
            def.CanInterrupt = canInterrupt;
            def.name = $"Procedural_{eventId}";
            if (def.ClipVariants == null || def.ClipVariants.Length == 0)
            {
                var generated = ProceduralAudioSynth.CreateForEventId(eventId);
                if (generated != null)
                    def.ClipVariants = new[] { generated };
            }
            Register(def);
        }

        private static void EnsureClipForDefinition(AudioEventDefinition definition)
        {
            if (definition == null || definition.ClipVariants != null && definition.ClipVariants.Length > 0)
                return;

            var clip = ProceduralAudioSynth.CreateForEventId(definition.EventId);
            if (clip != null)
                definition.ClipVariants = new[] { clip };
        }
    }
}
