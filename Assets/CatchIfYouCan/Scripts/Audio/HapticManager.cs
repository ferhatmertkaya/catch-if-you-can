using CatchIfYouCan.Utilities;
using UnityEngine;

namespace CatchIfYouCan.Audio
{
    public enum HapticIntensity
    {
        Light,
        Medium,
        Heavy
    }

    public class HapticManager : SingletonBehaviour<HapticManager>
    {
        [SerializeField] private bool enabledByDefault = true;

        private bool _enabled;

        public bool Enabled
        {
            get => _enabled;
            set => _enabled = value;
        }

        protected override void Awake()
        {
            persist = true;
            base.Awake();
            _enabled = enabledByDefault;
        }

        public void SetEnabled(bool value) => _enabled = value;

        public void Play(HapticIntensity intensity)
        {
            if (!_enabled) return;

#if UNITY_EDITOR
            return;
#elif UNITY_IOS || UNITY_ANDROID
            switch (intensity)
            {
                case HapticIntensity.Light:
                    TriggerVibrate(15);
                    break;
                case HapticIntensity.Medium:
                    TriggerVibrate(35);
                    break;
                case HapticIntensity.Heavy:
                    Handheld.Vibrate();
                    break;
            }
#else
            if (intensity == HapticIntensity.Heavy && Application.isMobilePlatform)
                Handheld.Vibrate();
#endif
        }

        private static void TriggerVibrate(long milliseconds)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
                {
                    vibrator.Call("vibrate", milliseconds);
                }
            }
            catch
            {
                Handheld.Vibrate();
            }
#else
            Handheld.Vibrate();
#endif
        }

        public void Light() => Play(HapticIntensity.Light);
        public void Medium() => Play(HapticIntensity.Medium);
        public void Heavy() => Play(HapticIntensity.Heavy);
    }
}
