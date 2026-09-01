using UnityEngine;

namespace CatchIfYouCan.Audio
{
    public class HorrorSilenceSystem : MonoBehaviour
    {
        [SerializeField] private float minSilence = 0.5f;
        [SerializeField] private float maxSilence = 4f;
        [SerializeField] private float triggerChance = 0.22f;
        [SerializeField] private float cooldown = 45f;

        private AudioSnapshotController _snapshots;
        private RoomAudioInstaller _roomInstaller;
        private float _cooldownTimer;
        private bool _inSilence;

        private void Awake()
        {
            _snapshots = AudioManager.Instance?.SnapshotController ?? FindAnyObjectByType<AudioSnapshotController>();
        }

        public bool TrySilenceBeforeMajorEvent()
        {
            _cooldownTimer -= Time.deltaTime;
            if (_inSilence || _cooldownTimer > 0f) return false;
            if (Random.value > triggerChance) return false;
            StartSilenceWindow();
            return true;
        }

        public void StartSilenceWindow()
        {
            if (_inSilence) return;
            _inSilence = true;
            _cooldownTimer = cooldown;
            float duration = Random.Range(minSilence, maxSilence);
            _snapshots?.TransitionTo(AudioSnapshotId.Silence, duration * 0.35f);
            Invoke(nameof(EndSilence), duration);
        }

        private void EndSilence()
        {
            _inSilence = false;
            _snapshots?.TransitionTo(AudioSnapshotId.Normal);
        }

        public bool IsSilent => _inSilence;
    }
}
