using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CatchIfYouCan.Audio;
using UnityEditor;
using UnityEngine;

namespace CatchIfYouCan.EditorTools
{
    public static class CatchIfYouCanAudioMixerBuilder
    {
        private const string MixerFolder = "Assets/CatchIfYouCan/Audio/Mixer";
        private const string ConfigPath = MixerFolder + "/AudioMixerConfig.asset";
        private const string MixerReadmePath = MixerFolder + "/README_MIXER.md";
        private const string EventsFolder = "Assets/CatchIfYouCan/ScriptableObjects/Audio";
        private const string GeneratedClipsFolder = "Assets/CatchIfYouCan/Audio/Generated";

        [MenuItem("Catch If You Can/Audio/Build Audio Mixer")]
        public static void BuildAudioMixer()
        {
            EnsureAudioFolders();
            var config = EnsureMixerConfig();
            WriteMixerReadme(config);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Catch If You Can — Audio Mixer",
                "Created/updated AudioMixerConfig.asset and README_MIXER.md.\n\n" +
                "Unity does not expose a public API to author .mixer YAML safely. " +
                "Create CatchIfYouCanAudioMixer.mixer manually in the Mixer window " +
                "using the group/snapshot layout in README_MIXER.md, or rely on " +
                "RuntimeAudioBusRouter volume routing when no mixer asset is assigned.",
                "OK");
        }

        [MenuItem("Catch If You Can/Audio/Generate Default Audio Events")]
        public static void GenerateDefaultAudioEvents()
        {
            var result = GenerateDefaultAudioEventsInternal();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Catch If You Can — Audio Events",
                $"Generated {result.EventsCreated} AudioEventDefinition asset(s) and {result.ClipsCreated} procedural clip(s).\n" +
                $"Events: {EventsFolder}\nClips: {GeneratedClipsFolder}",
                "OK");
        }

        internal static void EnsureAudioFolders()
        {
            string[] folders =
            {
                "Assets/CatchIfYouCan/Audio",
                "Assets/CatchIfYouCan/Audio/Ambience/Exterior",
                "Assets/CatchIfYouCan/Audio/Ambience/RoomTone",
                "Assets/CatchIfYouCan/Audio/Ghost",
                "Assets/CatchIfYouCan/Audio/Foley/Footsteps",
                "Assets/CatchIfYouCan/Audio/UI",
                "Assets/CatchIfYouCan/Audio/Equipment",
                "Assets/CatchIfYouCan/Audio/Music",
                GeneratedClipsFolder,
                MixerFolder,
                EventsFolder
            };

            for (int i = 0; i < folders.Length; i++)
                EnsureFolder(folders[i]);
        }

        internal static AudioMixerConfig EnsureMixerConfig()
        {
            EnsureFolder(MixerFolder);
            var config = AssetDatabase.LoadAssetAtPath<AudioMixerConfig>(ConfigPath);
            if (config == null)
            {
                config = AudioMixerConfig.CreateDefault();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }
            else
            {
                EditorUtility.SetDirty(config);
            }

            return config;
        }

        internal static void WriteMixerReadme(AudioMixerConfig config)
        {
            File.WriteAllText(Path.GetFullPath(MixerReadmePath), BuildMixerReadme(config));
        }

        internal struct GenerateEventsResult
        {
            public int EventsCreated;
            public int ClipsCreated;
        }

        internal static GenerateEventsResult GenerateDefaultAudioEventsInternal()
        {
            EnsureFolder(EventsFolder);
            EnsureFolder(GeneratedClipsFolder);

            int eventsCreated = 0;
            int clipsCreated = 0;
            var bakedClips = new Dictionary<string, AudioClip>();

            foreach (var entry in GetDefaultEventEntries())
            {
                string safeId = entry.EventId.Replace('.', '_').Replace('/', '_');
                string clipPath = $"{GeneratedClipsFolder}/proc_{safeId}.asset";
                if (!bakedClips.TryGetValue(entry.EventId, out var clip))
                {
                    clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
                    if (clip == null)
                    {
                        clip = entry.ClipFactory();
                        if (clip != null)
                        {
                            clip.name = $"proc_{safeId}";
                            AssetDatabase.CreateAsset(clip, clipPath);
                            bakedClips[entry.EventId] = clip;
                            clipsCreated++;
                        }
                    }
                    else
                    {
                        bakedClips[entry.EventId] = clip;
                    }
                }

                string eventPath = $"{EventsFolder}/{safeId}.asset";
                if (AssetDatabase.LoadAssetAtPath<AudioEventDefinition>(eventPath) != null)
                    continue;

                var def = ScriptableObject.CreateInstance<AudioEventDefinition>();
                def.EventId = entry.EventId;
                def.MixerGroup = entry.Group;
                def.Priority = entry.Priority;
                def.SpatialBlend = entry.SpatialBlend;
                def.MaxDistance = entry.MaxDistance;
                def.Loop = entry.Loop;
                def.ReverbSend = entry.ReverbSend;
                def.CanInterrupt = entry.CanInterrupt;
                if (clip != null)
                    def.ClipVariants = new[] { clip };
                def.name = safeId;

                AssetDatabase.CreateAsset(def, eventPath);
                eventsCreated++;
            }

            return new GenerateEventsResult
            {
                EventsCreated = eventsCreated,
                ClipsCreated = clipsCreated
            };
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            var parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static string BuildMixerReadme(AudioMixerConfig config)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Catch If You Can — Audio Mixer Setup");
            sb.AppendLine();
            sb.AppendLine("Target asset: `CatchIfYouCanAudioMixer.mixer`");
            sb.AppendLine();
            sb.AppendLine("Unity does not expose a stable editor API to generate `.mixer` YAML. " +
                          "Use this guide to build the mixer manually, or leave the slot empty — " +
                          "`AudioManager` + `AudioSnapshotController` apply procedural bus volumes.");
            sb.AppendLine();
            sb.AppendLine("## Recommended Groups");
            sb.AppendLine();
            sb.AppendLine("| Group | Exposed Parameter |");
            sb.AppendLine("|-------|-------------------|");
            for (int i = 0; i < config.Groups.Count; i++)
            {
                var g = config.Groups[i];
                sb.AppendLine($"| {g.DisplayName} | {g.ExposedParameter} |");
            }

            sb.AppendLine();
            sb.AppendLine("## Snapshots");
            sb.AppendLine();
            sb.AppendLine("Create snapshots matching `AudioSnapshotId`: Normal, HighTension, Hunt, GhostEvent, " +
                          "PlayerDeath, Pause, VanInterior, HouseInterior, Exterior, Silence, PsychoEvent.");
            sb.AppendLine();
            sb.AppendLine("## Snapshot Transition Times (seconds)");
            sb.AppendLine();
            for (int i = 0; i < config.SnapshotTransitions.Length; i++)
            {
                var t = config.SnapshotTransitions[i];
                sb.AppendLine($"- {t.From} → {t.To}: {t.TransitionSeconds:F2}s");
            }

            sb.AppendLine();
            sb.AppendLine("Assign the finished mixer to `AudioManager.mixer` on the runtime prefab or scene object.");
            return sb.ToString();
        }

        private struct DefaultEventEntry
        {
            public string EventId;
            public AudioMixerGroupId Group;
            public AudioPriority Priority;
            public float SpatialBlend;
            public float MaxDistance;
            public bool Loop;
            public float ReverbSend;
            public bool CanInterrupt;
            public Func<AudioClip> ClipFactory;
        }

        private static IEnumerable<DefaultEventEntry> GetDefaultEventEntries()
        {
            yield return Entry("ui.click", AudioMixerGroupId.UI, AudioPriority.High, 0f, 1f, false, 0f, false,
                () => ProceduralAudioSynth.CreateBeep(880f, 0.06f, 0.35f));
            yield return Entry("ui.back", AudioMixerGroupId.UI, AudioPriority.High, 0f, 1f, false, 0f, false,
                () => ProceduralAudioSynth.CreateBeep(440f, 0.05f, 0.3f));
            yield return Entry("ui.confirm", AudioMixerGroupId.UI, AudioPriority.High, 0f, 1f, false, 0f, false,
                () => ProceduralAudioSynth.CreateBeep(660f, 0.08f, 0.4f));
            yield return Entry("UI.Button.Press", AudioMixerGroupId.UI, AudioPriority.High, 0f, 1f, false, 0f, false,
                () => ProceduralAudioSynth.CreateBeep(880f, 0.06f, 0.35f));
            yield return Entry("UI.Tab.Switch", AudioMixerGroupId.UI, AudioPriority.High, 0f, 1f, false, 0f, false,
                () => ProceduralAudioSynth.CreateBeep(720f, 0.04f, 0.28f));
            yield return Entry("UI.Journal.Open", AudioMixerGroupId.UI, AudioPriority.High, 0f, 1f, false, 0f, false,
                () => ProceduralAudioSynth.CreateBeep(520f, 0.07f, 0.32f));
            yield return Entry("UI.Journal.Close", AudioMixerGroupId.UI, AudioPriority.High, 0f, 1f, false, 0f, false,
                () => ProceduralAudioSynth.CreateBeep(480f, 0.05f, 0.28f));
            yield return Entry("UI.Journal.EvidenceClick", AudioMixerGroupId.UI, AudioPriority.Medium, 0f, 1f, false, 0f, false,
                () => ProceduralAudioSynth.CreateBeep(640f, 0.04f, 0.25f));
            yield return Entry("UI.Evidence.Found", AudioMixerGroupId.UI, AudioPriority.High, 0f, 1f, false, 0f, false,
                () => ProceduralAudioSynth.CreateBeep(990f, 0.12f, 0.45f));
            yield return Entry("UI.Objective.Complete", AudioMixerGroupId.UI, AudioPriority.High, 0f, 1f, false, 0f, false,
                () => ProceduralAudioSynth.CreateBeep(770f, 0.1f, 0.4f));
            yield return Entry("UI.Entity.Discovered", AudioMixerGroupId.UI, AudioPriority.High, 0f, 1f, false, 0f, false,
                () => ProceduralAudioSynth.CreateImpact(0.45f, 140f));
            yield return Entry("UI.Mission.Success", AudioMixerGroupId.UI, AudioPriority.High, 0f, 1f, false, 0f, false,
                () => ProceduralAudioSynth.CreateBeep(660f, 0.15f, 0.5f));
            yield return Entry("UI.Mission.Fail", AudioMixerGroupId.UI, AudioPriority.High, 0f, 1f, false, 0f, false,
                () => ProceduralAudioSynth.CreateNoiseBurst(0.35f, 0.35f));
            yield return Entry("door.open", AudioMixerGroupId.Foley, AudioPriority.Medium, 1f, 40f, false, 0f, false,
                () => ProceduralAudioSynth.CreateCreak(0.45f));
            yield return Entry("door.close", AudioMixerGroupId.Foley, AudioPriority.Medium, 1f, 40f, false, 0f, false,
                () => ProceduralAudioSynth.CreateImpact(0.35f, 120f));
            yield return Entry("door.slam", AudioMixerGroupId.Foley, AudioPriority.High, 1f, 40f, false, 0f, false,
                () => ProceduralAudioSynth.CreateImpact(0.55f, 80f));
            yield return Entry("footstep", AudioMixerGroupId.Foley, AudioPriority.Low, 1f, 18f, false, 0f, false,
                () => ProceduralAudioSynth.CreateFootstepThud());
            yield return Entry("equipment.beep", AudioMixerGroupId.Equipment, AudioPriority.Medium, 1f, 40f, false, 0f, false,
                () => ProceduralAudioSynth.CreateBeep(1200f, 0.04f, 0.25f));
            yield return Entry("equipment.scan", AudioMixerGroupId.Equipment, AudioPriority.Medium, 1f, 40f, true, 0f, false,
                () => ProceduralAudioSynth.CreateClickTrain(24f, 0.25f));
            yield return Entry("ghost.whisper", AudioMixerGroupId.GhostVoice, AudioPriority.High, 1f, 40f, false, 0.6f, false,
                () => ProceduralAudioSynth.CreateWhisperTexture(1.2f));
            yield return Entry("ghost.event", AudioMixerGroupId.Ghost, AudioPriority.High, 1f, 40f, false, 0.45f, false,
                () => ProceduralAudioSynth.CreateNoiseBurst(0.35f, 0.4f));
            yield return Entry("Ghost.Interact.Subtle", AudioMixerGroupId.Ghost, AudioPriority.Medium, 1f, 40f, false, 0.35f, false,
                () => ProceduralAudioSynth.CreateWhisperTexture(0.6f));
            yield return Entry("Ghost.Event.Pulse", AudioMixerGroupId.Ghost, AudioPriority.High, 1f, 40f, false, 0.5f, false,
                () => ProceduralAudioSynth.CreateNoiseBurst(0.4f, 0.45f));
            yield return Entry("ghost.hunt.start", AudioMixerGroupId.GhostHunt, AudioPriority.Critical, 1f, 40f, false, 0f, true,
                () => ProceduralAudioSynth.CreateImpact(0.7f, 55f));
            yield return Entry("ghost.hunt.loop", AudioMixerGroupId.GhostHunt, AudioPriority.Critical, 0.2f, 40f, true, 0f, false,
                () => ProceduralAudioSynth.CreateHeartbeatLoop());
            yield return Entry("player.heartbeat", AudioMixerGroupId.Player, AudioPriority.High, 0f, 1f, true, 0f, false,
                () => ProceduralAudioSynth.CreateHeartbeatLoop());
            yield return Entry("player.death", AudioMixerGroupId.Player, AudioPriority.Critical, 0f, 1f, false, 0f, false,
                () => ProceduralAudioSynth.CreateNoiseBurst(0.8f, 0.25f));
            yield return Entry("ambient.rain", AudioMixerGroupId.Weather, AudioPriority.Low, 0f, 1f, true, 0f, false,
                () => ProceduralAudioSynth.CreateRainLoop());
            yield return Entry("ambient.hum", AudioMixerGroupId.Ambience, AudioPriority.Low, 0f, 1f, true, 0f, false,
                () => ProceduralAudioSynth.CreateHumLoop(60f));
            yield return Entry("van.idle", AudioMixerGroupId.Van, AudioPriority.Low, 0f, 1f, true, 0f, false,
                () => ProceduralAudioSynth.CreateHumLoop(45f));
            yield return Entry("evidence.detected", AudioMixerGroupId.UI, AudioPriority.High, 0f, 1f, false, 0f, false,
                () => ProceduralAudioSynth.CreateBeep(990f, 0.12f, 0.45f));
            yield return Entry("noise.clatter", AudioMixerGroupId.Environment, AudioPriority.Medium, 1f, 40f, false, 0f, false,
                () => ProceduralAudioSynth.CreateClickTrain(18f, 0.18f));
            yield return Entry("Tension.Bed.Low", AudioMixerGroupId.Ambience, AudioPriority.Low, 0f, 1f, true, 0f, false,
                () => ProceduralAudioSynth.CreateLoopingNoise(0.08f, 0.35f));
            yield return Entry("Tension.Bed.Mid", AudioMixerGroupId.Ambience, AudioPriority.Low, 0f, 1f, true, 0f, false,
                () => ProceduralAudioSynth.CreateLoopingNoise(0.1f, 0.38f));
            yield return Entry("Tension.Bed.High", AudioMixerGroupId.Ambience, AudioPriority.Medium, 0f, 1f, true, 0f, false,
                () => ProceduralAudioSynth.CreateLoopingNoise(0.12f, 0.42f));
            yield return Entry("Tension.Bed.Extreme", AudioMixerGroupId.GhostHunt, AudioPriority.High, 0f, 1f, true, 0f, false,
                () => ProceduralAudioSynth.CreateLoopingNoise(0.15f, 0.48f));
        }

        private static DefaultEventEntry Entry(
            string eventId,
            AudioMixerGroupId group,
            AudioPriority priority,
            float spatialBlend,
            float maxDistance,
            bool loop,
            float reverbSend,
            bool canInterrupt,
            Func<AudioClip> clipFactory)
        {
            return new DefaultEventEntry
            {
                EventId = eventId,
                Group = group,
                Priority = priority,
                SpatialBlend = spatialBlend,
                MaxDistance = maxDistance,
                Loop = loop,
                ReverbSend = reverbSend,
                CanInterrupt = canInterrupt,
                ClipFactory = clipFactory
            };
        }
    }
}
