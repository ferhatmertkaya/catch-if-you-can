using System;
using System.IO;
using CatchIfYouCan.Utilities;
using UnityEngine;

namespace CatchIfYouCan.Save
{
    public class SaveManager : SingletonBehaviour<SaveManager>
    {
        private const string SaveFileName = "catchifyoucan_save.json";
        private const string BackupFileName = "catchifyoucan_save.bak.json";
        [SerializeField] private bool autoSaveOnPause = true;

        public SaveData Data { get; private set; } = new SaveData();
        public bool HasLoadedSave { get; private set; }

        private string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);
        private string BackupPath => Path.Combine(Application.persistentDataPath, BackupFileName);

        protected override void Awake()
        {
            persist = true;
            base.Awake();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && autoSaveOnPause)
                Save();
        }

        private void OnApplicationQuit()
        {
            Save();
        }

        public void Load()
        {
            Data = new SaveData();
            HasLoadedSave = false;

            if (TryReadFile(SavePath, out string json))
            {
                if (TryDeserialize(json, out SaveData loaded))
                {
                    Data = loaded;
                    Data.MigrateIfNeeded();
                    HasLoadedSave = true;
                    return;
                }
            }

            if (TryReadFile(BackupPath, out json) && TryDeserialize(json, out SaveData backup))
            {
                Data = backup;
                Data.MigrateIfNeeded();
                HasLoadedSave = true;
                Save();
            }
        }

        public void Save()
        {
            if (SettingsManager.Instance != null)
                Data.Settings = SettingsManager.Instance.CaptureSnapshot();
            Data.SyncTierListFromCache();

            try
            {
                string json = JsonUtility.ToJson(Data, true);
                if (File.Exists(SavePath))
                {
                    try { File.Copy(SavePath, BackupPath, true); }
                    catch (Exception ex) { Debug.LogWarning($"Save backup failed: {ex.Message}"); }
                }

                File.WriteAllText(SavePath, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Save failed: {ex.Message}");
            }
        }

        public void ResetProgress()
        {
            Data = new SaveData();
            Save();
        }

        public bool SpendMoney(int amount)
        {
            if (amount <= 0 || Data.Money < amount) return false;
            Data.Money -= amount;
            Save();
            return true;
        }

        public void AddMoney(int amount)
        {
            if (amount <= 0) return;
            Data.Money += amount;
            Save();
        }

        public void AddXp(int amount)
        {
            Data.AddXp(amount);
            Save();
        }

        private static bool TryReadFile(string path, out string json)
        {
            json = null;
            if (!File.Exists(path)) return false;
            try
            {
                json = File.ReadAllText(path);
                return !string.IsNullOrWhiteSpace(json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Read save failed ({path}): {ex.Message}");
                return false;
            }
        }

        private static bool TryDeserialize(string json, out SaveData data)
        {
            data = null;
            try
            {
                data = JsonUtility.FromJson<SaveData>(json);
                if (data == null) return false;
                data.UnlockedEquipmentIds ??= new System.Collections.Generic.List<string>();
                data.EquipmentTierList ??= new System.Collections.Generic.List<EquipmentTierEntry>();
                data.Settings ??= new SettingsSnapshot();
                data.Statistics ??= new StatisticsData();
                data.RebuildTierCache();
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Deserialize save failed: {ex.Message}");
                return false;
            }
        }
    }
}
