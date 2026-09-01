using UnityEngine;

namespace CatchIfYouCan.Audio
{
    public class SpatialAudioEmitter : MonoBehaviour
    {
        [SerializeField] private AudioEmitter emitter;
        [SerializeField] private bool trackPosition = true;
        [SerializeField] private LayerMask occlusionMask = ~0;
        [SerializeField] private float occlusionUpdateInterval = 0.12f;
        [SerializeField] private float maxOcclusionAttenuation = 0.65f;

        private float _baseVolume = 1f;
        private float _nextOcclusionUpdate;
        private AudioEventDefinition _activeDefinition;

        public AudioEmitter Emitter => emitter;
        public bool IsPlaying => emitter != null && emitter.IsPlaying;

        private void Awake()
        {
            if (emitter == null)
                emitter = GetComponent<AudioEmitter>() ?? gameObject.AddComponent<AudioEmitter>();
        }

        private void Update()
        {
            if (!IsPlaying)
                return;

            if (trackPosition)
                emitter.transform.position = transform.position;

            if (_activeDefinition != null && _activeDefinition.OcclusionEnabled &&
                Time.unscaledTime >= _nextOcclusionUpdate)
            {
                _nextOcclusionUpdate = Time.unscaledTime + occlusionUpdateInterval;
                ApplyOcclusion();
            }
        }

        public void Play(AudioEvent request)
        {
            if (emitter == null || !request.IsValid)
                return;

            _activeDefinition = request.Definition;
            _baseVolume = request.Volume;
            emitter.transform.position = request.Position ?? transform.position;
            emitter.Play(request);
            ApplyOcclusion();
        }

        public void Stop()
        {
            _activeDefinition = null;
            emitter?.Stop();
        }

        public void Release()
        {
            Stop();
            emitter?.Release();
        }

        private void ApplyOcclusion()
        {
            if (emitter?.Source == null)
                return;

            var listener = FindListenerTransform();
            if (listener == null)
            {
                emitter.Source.volume = _baseVolume;
                return;
            }

            Vector3 origin = emitter.transform.position;
            Vector3 target = listener.position;
            float distance = Vector3.Distance(origin, target);
            if (distance <= 0.01f)
            {
                emitter.Source.volume = _baseVolume;
                return;
            }

            float occlusion = 0f;
            if (Physics.Raycast(origin, (target - origin).normalized, out var hit, distance, occlusionMask,
                    QueryTriggerInteraction.Ignore))
            {
                if (hit.collider != null && !hit.collider.isTrigger)
                    occlusion = maxOcclusionAttenuation;
            }

            emitter.Source.volume = _baseVolume * (1f - occlusion);
        }

        private static Transform FindListenerTransform()
        {
            var listener = Object.FindAnyObjectByType<AudioListener>();
            return listener != null ? listener.transform : Camera.main != null ? Camera.main.transform : null;
        }
    }
}
