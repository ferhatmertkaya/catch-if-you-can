using CatchIfYouCan.Audio;
using CatchIfYouCan.Core;
using CatchIfYouCan.Graphics;
using CatchIfYouCan.Utilities;
using UnityEngine;

namespace CatchIfYouCan.Save
{
    public class SettingsManager : SingletonBehaviour<SettingsManager>
    {
        private const string Prefix = "ciyc_";

        public float LookSensitivity { get; set; } = 1.2f;
        public bool AutoSprint { get; set; }
        public bool HoldToInteract { get; set; } = true;
        public bool CameraShake { get; set; } = true;
        public bool Haptics { get; set; } = true;
        public int QualityLevel { get; set; } = 2;
        public float ResolutionScale { get; set; } = 1f;
        public int TargetFps { get; set; } = 60;
        public bool Shadows { get; set; } = true;
        public bool PostProcessing { get; set; } = true;
        public float MasterVolume { get; set; } = 1f;
        public float MusicVolume { get; set; } = 0.8f;
        public float AmbientVolume { get; set; } = 1f;
        public float EffectsVolume { get; set; } = 1f;
        public float VoiceVolume { get; set; } = 1f;
        public float GhostVolume { get; set; } = 1f;
        public float EquipmentVolume { get; set; } = 1f;
        public float UIVolume { get; set; } = 1f;
        public DynamicRangeMode DynamicRangeMode { get; set; } = DynamicRangeMode.Normal;
        public HeadphoneMode HeadphoneMode { get; set; } = HeadphoneMode.Stereo;
        public float Brightness { get; set; } = 1f;
        public bool ReduceFlicker { get; set; }
        public bool ReduceCameraMotion { get; set; }
        public bool LargeButtons { get; set; }
        public bool HighContrastEvidence { get; set; }
        public bool Subtitles { get; set; } = true;

        protected override void Awake()
        {
            persist = true;
            base.Awake();
            LoadFromPrefs();
        }

        public void LoadFromPrefs()
        {
            LookSensitivity = PlayerPrefs.GetFloat(Prefix + "look_sens", 1.2f);
            AutoSprint = PlayerPrefs.GetInt(Prefix + "auto_sprint", 0) == 1;
            HoldToInteract = PlayerPrefs.GetInt(Prefix + "hold_interact", 1) == 1;
            CameraShake = PlayerPrefs.GetInt(Prefix + "camera_shake", 1) == 1;
            Haptics = PlayerPrefs.GetInt(Prefix + "haptics", 1) == 1;
            QualityLevel = PlayerPrefs.GetInt(Prefix + "quality", 2);
            ResolutionScale = PlayerPrefs.GetFloat(Prefix + "res_scale", 1f);
            TargetFps = PlayerPrefs.GetInt(Prefix + "fps", 60);
            Shadows = PlayerPrefs.GetInt(Prefix + "shadows", 1) == 1;
            PostProcessing = PlayerPrefs.GetInt(Prefix + "post", 1) == 1;
            MasterVolume = PlayerPrefs.GetFloat(Prefix + "vol_master", 1f);
            MusicVolume = PlayerPrefs.GetFloat(Prefix + "vol_music", 0.8f);
            AmbientVolume = PlayerPrefs.GetFloat(Prefix + "vol_ambient", 1f);
            EffectsVolume = PlayerPrefs.GetFloat(Prefix + "vol_effects", 1f);
            VoiceVolume = PlayerPrefs.GetFloat(Prefix + "vol_voice", 1f);
            GhostVolume = PlayerPrefs.GetFloat(Prefix + "vol_ghost", PlayerPrefs.GetFloat(Prefix + "vol_ambient", 1f));
            EquipmentVolume = PlayerPrefs.GetFloat(Prefix + "vol_equipment", PlayerPrefs.GetFloat(Prefix + "vol_effects", 1f));
            UIVolume = PlayerPrefs.GetFloat(Prefix + "vol_ui", 1f);
            DynamicRangeMode = (DynamicRangeMode)PlayerPrefs.GetInt(Prefix + "audio_dynamic_range", (int)DynamicRangeMode.Normal);
            HeadphoneMode = (HeadphoneMode)PlayerPrefs.GetInt(Prefix + "audio_headphone_mode", (int)HeadphoneMode.Stereo);
            Brightness = PlayerPrefs.GetFloat(Prefix + "brightness", 1f);
            ReduceFlicker = PlayerPrefs.GetInt(Prefix + "reduce_flicker", 0) == 1;
            ReduceCameraMotion = PlayerPrefs.GetInt(Prefix + "reduce_motion", 0) == 1;
            LargeButtons = PlayerPrefs.GetInt(Prefix + "large_buttons", 0) == 1;
            HighContrastEvidence = PlayerPrefs.GetInt(Prefix + "high_contrast", 0) == 1;
            Subtitles = PlayerPrefs.GetInt(Prefix + "subtitles", 1) == 1;

            if (SaveManager.Instance != null && SaveManager.Instance.HasLoadedSave && SaveManager.Instance.Data.Settings != null)
                ApplySnapshot(SaveManager.Instance.Data.Settings);
        }

        public void SaveToPrefs()
        {
            PlayerPrefs.SetFloat(Prefix + "look_sens", LookSensitivity);
            PlayerPrefs.SetInt(Prefix + "auto_sprint", AutoSprint ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "hold_interact", HoldToInteract ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "camera_shake", CameraShake ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "haptics", Haptics ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "quality", QualityLevel);
            PlayerPrefs.SetFloat(Prefix + "res_scale", ResolutionScale);
            PlayerPrefs.SetInt(Prefix + "fps", TargetFps);
            PlayerPrefs.SetInt(Prefix + "shadows", Shadows ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "post", PostProcessing ? 1 : 0);
            PlayerPrefs.SetFloat(Prefix + "vol_master", MasterVolume);
            PlayerPrefs.SetFloat(Prefix + "vol_music", MusicVolume);
            PlayerPrefs.SetFloat(Prefix + "vol_ambient", AmbientVolume);
            PlayerPrefs.SetFloat(Prefix + "vol_effects", EffectsVolume);
            PlayerPrefs.SetFloat(Prefix + "vol_voice", VoiceVolume);
            PlayerPrefs.SetFloat(Prefix + "vol_ghost", GhostVolume);
            PlayerPrefs.SetFloat(Prefix + "vol_equipment", EquipmentVolume);
            PlayerPrefs.SetFloat(Prefix + "vol_ui", UIVolume);
            PlayerPrefs.SetInt(Prefix + "audio_dynamic_range", (int)DynamicRangeMode);
            PlayerPrefs.SetInt(Prefix + "audio_headphone_mode", (int)HeadphoneMode);
            PlayerPrefs.SetFloat(Prefix + "brightness", Brightness);
            PlayerPrefs.SetInt(Prefix + "reduce_flicker", ReduceFlicker ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "reduce_motion", ReduceCameraMotion ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "large_buttons", LargeButtons ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "high_contrast", HighContrastEvidence ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "subtitles", Subtitles ? 1 : 0);
            PlayerPrefs.Save();
        }

        public SettingsSnapshot CaptureSnapshot()
        {
            return new SettingsSnapshot
            {
                LookSensitivity = LookSensitivity,
                AutoSprint = AutoSprint,
                HoldToInteract = HoldToInteract,
                CameraShake = CameraShake,
                Haptics = Haptics,
                QualityLevel = QualityLevel,
                ResolutionScale = ResolutionScale,
                TargetFps = TargetFps,
                Shadows = Shadows,
                PostProcessing = PostProcessing,
                MasterVolume = MasterVolume,
                MusicVolume = MusicVolume,
                AmbientVolume = AmbientVolume,
                EffectsVolume = EffectsVolume,
                VoiceVolume = VoiceVolume,
                GhostVolume = GhostVolume,
                EquipmentVolume = EquipmentVolume,
                UIVolume = UIVolume,
                DynamicRangeMode = DynamicRangeMode,
                HeadphoneMode = HeadphoneMode,
                Brightness = Brightness,
                ReduceFlicker = ReduceFlicker,
                ReduceCameraMotion = ReduceCameraMotion,
                LargeButtons = LargeButtons,
                HighContrastEvidence = HighContrastEvidence,
                Subtitles = Subtitles
            };
        }

        public void ApplySnapshot(SettingsSnapshot snapshot)
        {
            if (snapshot == null) return;
            LookSensitivity = snapshot.LookSensitivity;
            AutoSprint = snapshot.AutoSprint;
            HoldToInteract = snapshot.HoldToInteract;
            CameraShake = snapshot.CameraShake;
            Haptics = snapshot.Haptics;
            QualityLevel = snapshot.QualityLevel;
            ResolutionScale = snapshot.ResolutionScale;
            TargetFps = snapshot.TargetFps;
            Shadows = snapshot.Shadows;
            PostProcessing = snapshot.PostProcessing;
            MasterVolume = snapshot.MasterVolume;
            MusicVolume = snapshot.MusicVolume;
            AmbientVolume = snapshot.AmbientVolume;
            EffectsVolume = snapshot.EffectsVolume;
            VoiceVolume = snapshot.VoiceVolume;
            GhostVolume = snapshot.GhostVolume;
            EquipmentVolume = snapshot.EquipmentVolume;
            UIVolume = snapshot.UIVolume;
            DynamicRangeMode = snapshot.DynamicRangeMode;
            HeadphoneMode = snapshot.HeadphoneMode;
            Brightness = snapshot.Brightness;
            ReduceFlicker = snapshot.ReduceFlicker;
            ReduceCameraMotion = snapshot.ReduceCameraMotion;
            LargeButtons = snapshot.LargeButtons;
            HighContrastEvidence = snapshot.HighContrastEvidence;
            Subtitles = snapshot.Subtitles;
        }

        public void ApplyAll()
        {
            SaveToPrefs();
            if (GameManager.Instance != null)
                GameManager.Instance.SetTargetFramerate(TargetFps);
            if (GraphicsManager.Instance != null)
                GraphicsManager.Instance.ApplyFromSettings(this);
            if (AudioManager.Instance != null)
                AudioManager.Instance.ApplyFromSettings(this);
            if (HapticManager.Instance != null)
                HapticManager.Instance.SetEnabled(Haptics);
            ApplyPlayerLookSettings();
        }

        private void ApplyPlayerLookSettings()
        {
            var look = Object.FindFirstObjectByType<Player.PlayerLook>();
            if (look != null)
            {
                look.Sensitivity = LookSensitivity;
                look.AllowLook = !ReduceCameraMotion || GameManager.Instance == null ||
                                 GameManager.Instance.State != GameState.Paused;
            }
        }
    }
}
