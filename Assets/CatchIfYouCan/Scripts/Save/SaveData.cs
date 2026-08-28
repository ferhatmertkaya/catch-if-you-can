using System;
using System.Collections.Generic;
using CatchIfYouCan.Audio;

namespace CatchIfYouCan.Save
{
    [Serializable]
    public class SettingsSnapshot
    {
        public float LookSensitivity = 1.2f;
        public bool AutoSprint = false;
        public bool HoldToInteract = true;
        public bool CameraShake = true;
        public bool Haptics = true;
        public int QualityLevel = 2;
        public float ResolutionScale = 1f;
        public int TargetFps = 60;
        public bool Shadows = true;
        public bool PostProcessing = true;
        public float MasterVolume = 1f;
        public float MusicVolume = 0.8f;
        public float AmbientVolume = 1f;
        public float EffectsVolume = 1f;
        public float VoiceVolume = 1f;
        public float GhostVolume = 1f;
        public float EquipmentVolume = 1f;
        public float UIVolume = 1f;
        public DynamicRangeMode DynamicRangeMode = DynamicRangeMode.Normal;
        public HeadphoneMode HeadphoneMode = HeadphoneMode.Stereo;
        public float Brightness = 1f;
        public bool ReduceFlicker = false;
        public bool ReduceCameraMotion = false;
        public bool LargeButtons = false;
        public bool HighContrastEvidence = false;
        public bool Subtitles = true;
    }

    [Serializable]
    public class StatisticsData
    {
        public int Investigations;
        public int SuccessfulCases;
        public int Deaths;
        public int CorrectIdentifications;
        public int GhostPhotos;
        public int HuntsSurvived;
        public int EvidenceFound;
        public double TimePlayedSeconds;
    }

    [Serializable]
    public class EquipmentTierEntry
    {
        public string Id;
        public int Tier;
    }

    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 1;

        public int Version = CurrentVersion;
        public int Level = 1;
        public int Xp;
        public int Money = 500;
        public List<string> UnlockedEquipmentIds = new List<string>();
        public List<EquipmentTierEntry> EquipmentTierList = new List<EquipmentTierEntry>();
        public SettingsSnapshot Settings = new SettingsSnapshot();
        public StatisticsData Statistics = new StatisticsData();

        [NonSerialized] private Dictionary<string, int> _equipmentTiers;

        public Dictionary<string, int> EquipmentTiers
        {
            get
            {
                if (_equipmentTiers == null)
                    RebuildTierCache();
                return _equipmentTiers;
            }
        }

        public void RebuildTierCache()
        {
            _equipmentTiers = new Dictionary<string, int>();
            if (EquipmentTierList == null) return;
            foreach (var entry in EquipmentTierList)
            {
                if (entry != null && !string.IsNullOrEmpty(entry.Id))
                    _equipmentTiers[entry.Id] = entry.Tier;
            }
        }

        public void SyncTierListFromCache()
        {
            if (_equipmentTiers == null)
                RebuildTierCache();
            EquipmentTierList = new List<EquipmentTierEntry>();
            foreach (var pair in _equipmentTiers)
            {
                EquipmentTierList.Add(new EquipmentTierEntry { Id = pair.Key, Tier = pair.Value });
            }
        }

        public int XpToNextLevel => Level * 250 + 100;

        public void AddXp(int amount)
        {
            if (amount <= 0) return;
            Xp += amount;
            while (Xp >= XpToNextLevel)
            {
                Xp -= XpToNextLevel;
                Level++;
            }
        }

        public bool IsEquipmentUnlocked(string id) =>
            !string.IsNullOrEmpty(id) && UnlockedEquipmentIds.Contains(id);

        public void UnlockEquipment(string id)
        {
            if (string.IsNullOrEmpty(id) || UnlockedEquipmentIds.Contains(id))
                return;
            UnlockedEquipmentIds.Add(id);
        }

        public int GetEquipmentTier(string id)
        {
            if (string.IsNullOrEmpty(id)) return 0;
            return EquipmentTiers.TryGetValue(id, out int tier) ? tier : 0;
        }

        public void SetEquipmentTier(string id, int tier)
        {
            if (string.IsNullOrEmpty(id)) return;
            EquipmentTiers[id] = UnityEngine.Mathf.Max(0, tier);
        }

        public void MigrateIfNeeded()
        {
            if (Version >= CurrentVersion)
            {
                RebuildTierCache();
                return;
            }
            Version = CurrentVersion;
            RebuildTierCache();
        }
    }
}
