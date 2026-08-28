using System.Collections.Generic;
using UnityEngine;

namespace CatchIfYouCan.Audio
{
    public static class ProceduralAudioSynth
    {
        public const int SampleRate = 22050;

        private static readonly Dictionary<string, AudioClip> Cache = new Dictionary<string, AudioClip>();

        public static AudioClip CreateForEventId(string eventId)
        {
            if (string.IsNullOrWhiteSpace(eventId))
                return null;

            string id = eventId.ToLowerInvariant().Replace('_', '.');
            var patternClip = MatchPattern(id);
            if (patternClip != null)
                return patternClip;

            return id switch
            {
                "ui.click" or "ui_click" => CreateBeep(880f, 0.06f, 0.35f),
                "ui.back" => CreateBeep(440f, 0.05f, 0.3f),
                "ui.confirm" => CreateBeep(660f, 0.08f, 0.4f),
                "door.open" or "door_open" => CreateCreak(0.45f),
                "door.close" or "door_close" => CreateImpact(0.35f, 120f),
                "door.slam" => CreateImpact(0.55f, 80f),
                "footstep" => CreateFootstepThud(),
                "equipment.beep" => CreateBeep(1200f, 0.04f, 0.25f),
                "equipment.scan" => CreateClickTrain(24f, 0.25f),
                "ghost.whisper" or "ghost_whisper" => CreateWhisperTexture(1.2f),
                "ghost.event" => CreateNoiseBurst(0.35f, 0.4f),
                "ghost.hunt.start" => CreateImpact(0.7f, 55f),
                "ghost.hunt.loop" => CreateHeartbeatLoop(),
                "player.heartbeat" => CreateHeartbeatLoop(),
                "player.death" => CreateNoiseBurst(0.8f, 0.25f),
                "ambient.rain" => CreateRainLoop(),
                "ambient.hum" => CreateHumLoop(60f),
                "van.idle" => CreateHumLoop(45f),
                "evidence.detected" => CreateBeep(990f, 0.12f, 0.45f),
                "noise.clatter" => CreateClickTrain(18f, 0.18f),
                _ => CreateBeep(520f, 0.05f, 0.2f)
            };
        }

        private static AudioClip MatchPattern(string id)
        {
            if (id.Contains("footstep")) return CreateFootstepThud();
            if (id.Contains("heartbeat") || id.Contains("heart")) return CreateHeartbeatLoop();
            if (id.Contains("whisper") || id.Contains("breath")) return CreateWhisperTexture(0.8f);
            if (id.Contains("door") && id.Contains("slam")) return CreateImpact(0.55f, 80f);
            if (id.Contains("door")) return CreateCreak(0.35f);
            if (id.Contains("knock")) return CreateImpact(0.12f, 180f);
            if (id.Contains("emf") || id.Contains("beep") || id.Contains("thermo")) return CreateBeep(880f, 0.05f, 0.3f);
            if (id.Contains("rain")) return CreateRainLoop();
            if (id.Contains("thunder")) return CreateNoiseBurst(1.2f, 0.55f);
            if (id.Contains("wind")) return CreateLoopingNoise(0.12f, 0.4f);
            if (id.Contains("hum") || id.Contains("vent") || id.Contains("fridge")) return CreateHumLoop(55f);
            if (id.Contains("ui") || id.Contains("button") || id.Contains("journal")) return CreateBeep(640f, 0.04f, 0.25f);
            if (id.Contains("static") || id.Contains("tinnitus") || id.Contains("evp")) return CreateLoopingNoise(0.08f, 0.6f);
            if (id.Contains("creak") || id.Contains("settling")) return CreateCreak(0.3f);
            if (id.Contains("camera") || id.Contains("shutter")) return CreateBeep(1200f, 0.05f, 0.3f);
            if (id.Contains("salt")) return CreateNoiseBurst(0.15f, 0.35f);
            if (id.Contains("relic") || id.Contains("spectral")) return CreateHumLoop(220f);
            if (id.Contains("tension") || id.Contains("hunt")) return CreateLoopingNoise(0.1f, 0.45f);
            if (id.Contains("psycho") || id.Contains("false")) return CreateWhisperTexture(0.5f);
            return null;
        }

        public static AudioClip ResolveOrSynthesize(string eventId) => CreateForEventId(eventId);

        public static AudioClip CreateBeep(float frequency, float duration, float amplitude = 0.5f)
        {
            return GetOrCreate($"beep_{frequency:F0}_{duration:F3}_{amplitude:F2}", () =>
            {
                int samples = Mathf.Max(1, Mathf.CeilToInt(duration * SampleRate));
                var data = new float[samples];
                for (int i = 0; i < samples; i++)
                {
                    float t = i / (float)SampleRate;
                    float env = Mathf.Clamp01(1f - t / duration);
                    data[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * amplitude * env * env;
                }
                return BuildClip("Beep", data, false);
            });
        }

        public static AudioClip CreateNoiseBurst(float duration, float amplitude = 0.5f)
        {
            return GetOrCreate($"noise_burst_{duration:F3}_{amplitude:F2}", () =>
            {
                int samples = Mathf.Max(1, Mathf.CeilToInt(duration * SampleRate));
                var data = new float[samples];
                var rng = new System.Random(17);
                for (int i = 0; i < samples; i++)
                {
                    float t = i / (float)SampleRate;
                    float env = Mathf.Exp(-t * 8f);
                    data[i] = ((float)rng.NextDouble() * 2f - 1f) * amplitude * env;
                }
                return BuildClip("NoiseBurst", data, false);
            });
        }

        public static AudioClip CreateLoopingNoise(float amplitude = 0.15f, float lowPass = 0.35f)
        {
            return GetOrCreate($"loop_noise_{amplitude:F2}_{lowPass:F2}", () =>
            {
                int samples = SampleRate * 2;
                var data = new float[samples];
                var rng = new System.Random(31);
                float state = 0f;
                for (int i = 0; i < samples; i++)
                {
                    float white = ((float)rng.NextDouble() * 2f - 1f) * amplitude;
                    state += (white - state) * lowPass;
                    data[i] = state;
                }
                return BuildClip("LoopNoise", data, true);
            });
        }

        public static AudioClip CreateClickTrain(float rateHz, float duration, float amplitude = 0.35f)
        {
            return GetOrCreate($"click_{rateHz:F1}_{duration:F3}_{amplitude:F2}", () =>
            {
                int samples = Mathf.Max(1, Mathf.CeilToInt(duration * SampleRate));
                var data = new float[samples];
                float interval = 1f / Mathf.Max(1f, rateHz);
                for (int i = 0; i < samples; i++)
                {
                    float t = i / (float)SampleRate;
                    float phase = t % interval;
                    if (phase < 0.004f)
                    {
                        float clickEnv = 1f - phase / 0.004f;
                        data[i] = clickEnv * amplitude;
                    }
                }
                return BuildClip("ClickTrain", data, false);
            });
        }

        public static AudioClip CreateImpact(float amplitude = 0.5f, float decayHz = 100f)
        {
            return GetOrCreate($"impact_{amplitude:F2}_{decayHz:F0}", () =>
            {
                float duration = 0.35f;
                int samples = Mathf.CeilToInt(duration * SampleRate);
                var data = new float[samples];
                var rng = new System.Random(42);
                for (int i = 0; i < samples; i++)
                {
                    float t = i / (float)SampleRate;
                    float env = Mathf.Exp(-t * decayHz * 0.05f);
                    float tone = Mathf.Sin(2f * Mathf.PI * (120f - t * 200f) * t);
                    float noise = ((float)rng.NextDouble() * 2f - 1f) * 0.35f;
                    data[i] = (tone * 0.65f + noise) * env * amplitude;
                }
                return BuildClip("Impact", data, false);
            });
        }

        public static AudioClip CreateHeartbeatLoop(float bpm = 92f, float amplitude = 0.45f)
        {
            return GetOrCreate($"heartbeat_{bpm:F0}_{amplitude:F2}", () =>
            {
                float beatInterval = 60f / bpm;
                int samples = Mathf.CeilToInt(beatInterval * 2f * SampleRate);
                var data = new float[samples];
                for (int i = 0; i < samples; i++)
                {
                    float t = i / (float)SampleRate;
                    float local = t % beatInterval;
                    float pulse = local < 0.08f
                        ? Mathf.Sin(local / 0.08f * Mathf.PI) * amplitude
                        : local < 0.14f
                            ? Mathf.Sin((local - 0.08f) / 0.06f * Mathf.PI) * amplitude * 0.55f
                            : 0f;
                    data[i] = pulse;
                }
                return BuildClip("Heartbeat", data, true);
            });
        }

        public static AudioClip CreateRainLoop(float amplitude = 0.12f)
        {
            return GetOrCreate($"rain_{amplitude:F2}", () =>
            {
                int samples = SampleRate * 3;
                var data = new float[samples];
                var rng = new System.Random(77);
                float lp = 0f;
                for (int i = 0; i < samples; i++)
                {
                    float white = ((float)rng.NextDouble() * 2f - 1f) * amplitude;
                    lp += (white - lp) * 0.08f;
                    if (rng.NextDouble() < 0.0025)
                        lp += (float)rng.NextDouble() * amplitude * 0.5f;
                    data[i] = lp;
                }
                return BuildClip("Rain", data, true);
            });
        }

        public static AudioClip CreateHumLoop(float frequency = 60f, float amplitude = 0.08f)
        {
            return GetOrCreate($"hum_{frequency:F0}_{amplitude:F2}", () =>
            {
                int samples = SampleRate * 2;
                var data = new float[samples];
                for (int i = 0; i < samples; i++)
                {
                    float t = i / (float)SampleRate;
                    data[i] = (Mathf.Sin(2f * Mathf.PI * frequency * t) * 0.7f +
                               Mathf.Sin(2f * Mathf.PI * frequency * 2f * t) * 0.25f) * amplitude;
                }
                return BuildClip("Hum", data, true);
            });
        }

        public static AudioClip CreateWhisperTexture(float duration = 1f, float amplitude = 0.22f)
        {
            return GetOrCreate($"whisper_{duration:F2}_{amplitude:F2}", () =>
            {
                int samples = Mathf.CeilToInt(duration * SampleRate);
                var data = new float[samples];
                var rng = new System.Random(99);
                float band = 0f;
                for (int i = 0; i < samples; i++)
                {
                    float t = i / (float)SampleRate;
                    float env = Mathf.Clamp01(Mathf.Min(Mathf.Min(t * 3f, 1f), (duration - t) * 3f));
                    float noise = ((float)rng.NextDouble() * 2f - 1f);
                    band += (noise - band) * 0.12f;
                    float flutter = 0.5f + 0.5f * Mathf.Sin(t * 14f);
                    data[i] = band * flutter * amplitude * env;
                }
                return BuildClip("Whisper", data, false);
            });
        }

        public static AudioClip CreateFootstepThud(float amplitude = 0.35f)
        {
            return GetOrCreate($"footstep_{amplitude:F2}", () =>
            {
                float duration = 0.12f;
                int samples = Mathf.CeilToInt(duration * SampleRate);
                var data = new float[samples];
                for (int i = 0; i < samples; i++)
                {
                    float t = i / (float)SampleRate;
                    float env = Mathf.Exp(-t * 45f);
                    float thud = Mathf.Sin(2f * Mathf.PI * (90f - t * 120f) * t);
                    data[i] = thud * env * amplitude;
                }
                return BuildClip("Footstep", data, false);
            });
        }

        public static AudioClip CreateCreak(float duration = 0.4f, float amplitude = 0.3f)
        {
            return GetOrCreate($"creak_{duration:F2}_{amplitude:F2}", () =>
            {
                int samples = Mathf.CeilToInt(duration * SampleRate);
                var data = new float[samples];
                var rng = new System.Random(55);
                for (int i = 0; i < samples; i++)
                {
                    float t = i / (float)SampleRate;
                    float env = Mathf.Clamp01(Mathf.Min(Mathf.Min(t * 4f, 1f), (duration - t) * 4f));
                    float freq = 180f + t * 420f + Mathf.Sin(t * 22f) * 40f;
                    float tone = Mathf.Sin(2f * Mathf.PI * freq * t);
                    float grit = ((float)rng.NextDouble() * 2f - 1f) * 0.08f;
                    data[i] = (tone * 0.55f + grit) * env * amplitude;
                }
                return BuildClip("Creak", data, false);
            });
        }

        public static AudioClip CreateFilteredNoise(float duration, float amplitude, float cutoff = 0.2f)
        {
            return GetOrCreate($"filt_noise_{duration:F3}_{amplitude:F2}_{cutoff:F2}", () =>
            {
                int samples = Mathf.Max(1, Mathf.CeilToInt(duration * SampleRate));
                var data = new float[samples];
                var rng = new System.Random(66);
                float state = 0f;
                for (int i = 0; i < samples; i++)
                {
                    float t = i / (float)SampleRate;
                    float env = Mathf.Clamp01(1f - t / duration);
                    float white = ((float)rng.NextDouble() * 2f - 1f) * amplitude;
                    state += (white - state) * cutoff;
                    data[i] = state * env;
                }
                return BuildClip("FilteredNoise", data, false);
            });
        }

        public static void ClearCache()
        {
            foreach (var clip in Cache.Values)
            {
                if (clip != null)
                    Object.Destroy(clip);
            }
            Cache.Clear();
        }

        private static AudioClip GetOrCreate(string key, System.Func<AudioClip> factory)
        {
            if (Cache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            var clip = factory();
            Cache[key] = clip;
            return clip;
        }

        private static AudioClip BuildClip(string name, float[] data, bool loop)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            clip.LoadAudioData();
            return clip;
        }
    }
}
