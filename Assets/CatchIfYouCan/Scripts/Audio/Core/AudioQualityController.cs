using CatchIfYouCan.Graphics;
using CatchIfYouCan.Save;
using CatchIfYouCan.Utilities;
using UnityEngine;

namespace CatchIfYouCan.Audio
{
    public enum AudioQualitySetting
    {
        Low,
        Medium,
        High
    }

    public class AudioQualityController : SingletonBehaviour<AudioQualityController>
    {
        [SerializeField] private AudioQualitySetting defaultQuality = AudioQualitySetting.Medium;

        public AudioQualitySetting CurrentQuality { get; private set; }
        public int MaxSimultaneousSources { get; private set; } = 40;
        public int AmbientLayers { get; private set; } = 2;
        public float ReverbQuality { get; private set; } = 0.75f;

        protected override void Awake()
        {
            persist = true;
            base.Awake();
            ApplyQuality(defaultQuality);
        }

        public void ApplyFromSettings(SettingsManager settings)
        {
            if (settings == null)
                return;

            var quality = settings.QualityLevel switch
            {
                <= 0 => AudioQualitySetting.Low,
                1 => AudioQualitySetting.Medium,
                _ => AudioQualitySetting.High
            };
            ApplyQuality(quality);
        }

        public void ApplyFromGraphicsProfile(GraphicsProfile profile)
        {
            var quality = profile switch
            {
                GraphicsProfile.Low => AudioQualitySetting.Low,
                GraphicsProfile.High => AudioQualitySetting.High,
                _ => AudioQualitySetting.Medium
            };
            ApplyQuality(quality);
        }

        public void ApplyQuality(AudioQualitySetting quality)
        {
            CurrentQuality = quality;
            switch (quality)
            {
                case AudioQualitySetting.Low:
                    MaxSimultaneousSources = 24;
                    AmbientLayers = 1;
                    ReverbQuality = 0.35f;
                    break;
                case AudioQualitySetting.High:
                    MaxSimultaneousSources = 56;
                    AmbientLayers = 3;
                    ReverbQuality = 1f;
                    break;
                default:
                    MaxSimultaneousSources = 40;
                    AmbientLayers = 2;
                    ReverbQuality = 0.75f;
                    break;
            }

            if (AudioEmitterPool.Instance != null)
                AudioEmitterPool.Instance.RefreshBudgetFromQuality();

            AudioListener.volume = 1f;
        }
    }
}
