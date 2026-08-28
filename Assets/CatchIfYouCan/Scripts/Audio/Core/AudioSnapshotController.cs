using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace CatchIfYouCan.Audio
{
    public class AudioSnapshotController : MonoBehaviour
    {
        [SerializeField] private AudioMixer mixer;
        [SerializeField] private float normalToHighTension = 2f;
        [SerializeField] private float highToHunt = 0.4f;
        [SerializeField] private float huntToNormal = 4f;
        [SerializeField] private float normalToPause = 0.15f;
        [SerializeField] private float defaultTransition = 1f;

        private AudioSnapshotId _current = AudioSnapshotId.Normal;
        private Coroutine _transitionRoutine;
        private readonly Dictionary<AudioSnapshotId, float> _proceduralWeights = new Dictionary<AudioSnapshotId, float>();

        public AudioSnapshotId CurrentSnapshot => _current;

        public void Initialize(AudioMixer audioMixer)
        {
            if (audioMixer != null)
                mixer = audioMixer;
            ResetProceduralWeights();
            _current = AudioSnapshotId.Normal;
        }

        public void TransitionTo(AudioSnapshotId snapshot, float? overrideTime = null)
        {
            if (_current == snapshot)
                return;

            float time = overrideTime ?? GetTransitionTime(_current, snapshot);
            if (_transitionRoutine != null)
                StopCoroutine(_transitionRoutine);

            if (mixer != null && TryTransitionMixerSnapshots(snapshot, time))
            {
                _current = snapshot;
                return;
            }

            _transitionRoutine = StartCoroutine(ProceduralTransition(snapshot, time));
        }

        public void SetImmediate(AudioSnapshotId snapshot)
        {
            if (_transitionRoutine != null)
            {
                StopCoroutine(_transitionRoutine);
                _transitionRoutine = null;
            }

            _current = snapshot;
            ApplyProceduralWeight(snapshot, 1f);
        }

        private bool TryTransitionMixerSnapshots(AudioSnapshotId target, float time)
        {
            var snapshotName = target.ToString();
            var snapshot = mixer.FindSnapshot(snapshotName);
            if (snapshot == null)
                return false;

            mixer.TransitionToSnapshots(new[] { snapshot }, new[] { 1f }, time);
            return true;
        }

        private float GetTransitionTime(AudioSnapshotId from, AudioSnapshotId to)
        {
            if (from == AudioSnapshotId.Normal && to == AudioSnapshotId.HighTension)
                return normalToHighTension;
            if (from == AudioSnapshotId.HighTension && to == AudioSnapshotId.Hunt)
                return highToHunt;
            if (from == AudioSnapshotId.Hunt && to == AudioSnapshotId.Normal)
                return huntToNormal;
            if (from == AudioSnapshotId.Normal && to == AudioSnapshotId.Pause)
                return normalToPause;
            if (to == AudioSnapshotId.Pause)
                return normalToPause;
            return defaultTransition;
        }

        private IEnumerator ProceduralTransition(AudioSnapshotId target, float duration)
        {
            float startWeight = _proceduralWeights.TryGetValue(_current, out var w) ? w : 1f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
                float weight = Mathf.Lerp(startWeight, 1f, t);
                ApplyProceduralWeight(target, weight);
                yield return null;
            }

            _current = target;
            ApplyProceduralWeight(target, 1f);
            _transitionRoutine = null;
        }

        private void ApplyProceduralWeight(AudioSnapshotId snapshot, float weight)
        {
            ResetProceduralWeights();
            _proceduralWeights[snapshot] = weight;

            if (AudioManager.Instance == null)
                return;

            float tension = snapshot switch
            {
                AudioSnapshotId.HighTension => 0.85f,
                AudioSnapshotId.Hunt => 0.65f,
                AudioSnapshotId.GhostEvent => 0.75f,
                AudioSnapshotId.PlayerDeath => 0.4f,
                AudioSnapshotId.Pause => 0.25f,
                AudioSnapshotId.VanInterior => 0.9f,
                AudioSnapshotId.HouseInterior => 0.95f,
                AudioSnapshotId.Exterior => 1f,
                AudioSnapshotId.Silence => 0.55f,
                AudioSnapshotId.PsychoEvent => 0.7f,
                _ => 1f
            };

            float master = Mathf.Lerp(1f, tension, weight);
            float music = snapshot switch
            {
                AudioSnapshotId.Hunt => Mathf.Lerp(1f, 0.35f, weight),
                AudioSnapshotId.HighTension => Mathf.Lerp(1f, 0.55f, weight),
                AudioSnapshotId.Pause => Mathf.Lerp(1f, 0.2f, weight),
                _ => 1f
            };
            float ambient = snapshot switch
            {
                AudioSnapshotId.Exterior => Mathf.Lerp(1f, 1.15f, weight),
                AudioSnapshotId.VanInterior => Mathf.Lerp(1f, 0.75f, weight),
                AudioSnapshotId.Silence => Mathf.Lerp(1f, 0.2f, weight),
                _ => 1f
            };
            float ghost = snapshot switch
            {
                AudioSnapshotId.Hunt or AudioSnapshotId.GhostEvent or AudioSnapshotId.HighTension
                    or AudioSnapshotId.PsychoEvent
                    => Mathf.Lerp(1f, 1.35f, weight),
                AudioSnapshotId.Silence => Mathf.Lerp(1f, 0.45f, weight),
                _ => 1f
            };

            AudioManager.Instance.ApplySnapshotMix(master, music, ambient, ghost);
        }

        private void ResetProceduralWeights()
        {
            _proceduralWeights.Clear();
            foreach (AudioSnapshotId id in System.Enum.GetValues(typeof(AudioSnapshotId)))
                _proceduralWeights[id] = id == AudioSnapshotId.Normal ? 1f : 0f;
        }
    }
}
