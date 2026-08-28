using CatchIfYouCan.Core;
using CatchIfYouCan.Save;
using UnityEngine;

namespace CatchIfYouCan.Audio
{
    public static class AudioBootstrap
    {
        private static bool _initialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            Initialize();
        }

        public static void Initialize()
        {
            if (_initialized)
                return;

            _initialized = true;
            EnsureAudioManager();
            SubscribeGameEvents();
        }

        private static void EnsureAudioManager()
        {
            if (AudioManager.Instance != null)
                return;

            var go = new GameObject("AudioManager");
            go.AddComponent<AudioManager>();
        }

        private static void SubscribeGameEvents()
        {
            GameEvents.OnHuntStarted += HandleHuntStarted;
            GameEvents.OnHuntEnded += HandleHuntEnded;
            GameEvents.OnPlayerDied += HandlePlayerDied;
            GameEvents.OnGhostActivityChanged += HandleGhostActivityChanged;
        }

        private static void HandleHuntStarted()
        {
            var manager = AudioManager.Instance;
            if (manager == null) return;
            manager.TransitionSnapshot(AudioSnapshotId.Hunt);
            manager.PlayEvent("ghost.hunt.start");
            manager.PlayEvent("ghost.hunt.loop");
        }

        private static void HandleHuntEnded()
        {
            AudioManager.Instance?.TransitionSnapshot(AudioSnapshotId.Normal);
        }

        private static void HandlePlayerDied()
        {
            var manager = AudioManager.Instance;
            if (manager == null) return;
            manager.TransitionSnapshot(AudioSnapshotId.PlayerDeath);
            manager.PlayEvent("player.death");
        }

        private static void HandleGhostActivityChanged(float activity)
        {
            if (activity >= 0.75f)
                AudioManager.Instance?.TransitionSnapshot(AudioSnapshotId.HighTension);
            else if (activity <= 0.35f && AudioManager.Instance != null)
                AudioManager.Instance.TransitionSnapshot(AudioSnapshotId.Normal);
        }

        public static void HandlePauseChanged(bool paused)
        {
            AudioManager.Instance?.TransitionSnapshot(paused ? AudioSnapshotId.Pause : AudioSnapshotId.Normal);
        }

        public static void ApplySettings(SettingsManager settings)
        {
            AudioManager.Instance?.ApplyFromSettings(settings);
            AudioQualityController.Instance?.ApplyFromSettings(settings);
        }
    }
}
