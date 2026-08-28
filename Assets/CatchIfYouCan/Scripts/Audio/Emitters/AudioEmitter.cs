using System;
using UnityEngine;

namespace CatchIfYouCan.Audio
{
    [RequireComponent(typeof(AudioSource))]
    public class AudioEmitter : MonoBehaviour
    {
        public AudioSource Source { get; private set; }
        public AudioPriority Priority { get; private set; } = AudioPriority.Medium;
        public AudioMixerGroupId MixerGroup { get; private set; } = AudioMixerGroupId.Environment;
        public float DistanceToListener { get; set; }
        public bool CanInterrupt { get; private set; }
        public string EventId { get; private set; }

        public bool IsPlaying => Source != null && Source.isPlaying;

        public Action<AudioEmitter> OnReleased;

        private void Awake()
        {
            Source = GetComponent<AudioSource>();
            if (Source == null)
                Source = gameObject.AddComponent<AudioSource>();
            ConfigureDefaults();
        }

        public void Play(AudioEvent request, Transform followTarget = null)
        {
            if (request.Definition == null || request.ResolvedClip == null || Source == null)
                return;

            var def = request.Definition;
            Priority = def.Priority;
            MixerGroup = def.MixerGroup;
            CanInterrupt = def.CanInterrupt;
            EventId = def.EventId;

            if (request.Position.HasValue)
                transform.position = request.Position.Value;

            Source.clip = request.ResolvedClip;
            Source.volume = Mathf.Clamp01(request.Volume);
            Source.pitch = Mathf.Clamp(def.Pitch, 0.1f, 3f);
            Source.loop = def.Loop;
            Source.spatialBlend = def.SpatialBlend;
            Source.minDistance = def.MinDistance;
            Source.maxDistance = def.MaxDistance;
            Source.dopplerLevel = def.DopplerLevel;
            Source.reverbZoneMix = 1f + def.ReverbSend;

            if (def.OptionalRandomStartOffset && !def.Loop && request.ResolvedClip.length > 0.05f)
                Source.time = UnityEngine.Random.Range(0f, request.ResolvedClip.length * 0.25f);

            Source.Play();
            UpdateListenerDistance();
        }

        public void PlayClip(AudioClip clip, float volume, float pitch, bool loop, float spatialBlend,
            float minDistance, float maxDistance, AudioPriority priority, AudioMixerGroupId group)
        {
            if (clip == null || Source == null)
                return;

            Priority = priority;
            MixerGroup = group;
            EventId = clip.name;
            Source.clip = clip;
            Source.volume = Mathf.Clamp01(volume);
            Source.pitch = Mathf.Clamp(pitch, 0.1f, 3f);
            Source.loop = loop;
            Source.spatialBlend = spatialBlend;
            Source.minDistance = minDistance;
            Source.maxDistance = maxDistance;
            Source.Play();
            UpdateListenerDistance();
        }

        public void Stop()
        {
            if (Source == null) return;
            Source.Stop();
            Source.clip = null;
        }

        public void Release()
        {
            Stop();
            OnReleased?.Invoke(this);
        }

        public void UpdateListenerDistance()
        {
            var listener = GetListenerTransform();
            if (listener == null)
            {
                DistanceToListener = 0f;
                return;
            }
            DistanceToListener = Vector3.Distance(transform.position, listener.position);
        }

        private void ConfigureDefaults()
        {
            Source.playOnAwake = false;
            Source.rolloffMode = AudioRolloffMode.Linear;
            Source.spread = 45f;
        }

        private static Transform GetListenerTransform()
        {
            var listener = Object.FindFirstObjectByType<AudioListener>();
            return listener != null ? listener.transform : Camera.main != null ? Camera.main.transform : null;
        }
    }
}
