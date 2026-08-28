using System.Collections.Generic;
using CatchIfYouCan.Audio;
using CatchIfYouCan.Core;
using CatchIfYouCan.Graphics;
using CatchIfYouCan.Save;
using UnityEngine;
using UnityEngine.UI;

namespace CatchIfYouCan.UI
{
    public enum SettingsTab
    {
        Gameplay,
        Graphics,
        Audio,
        Accessibility
    }

    public class SettingsUI : MonoBehaviour
    {
        [SerializeField] private Button gameplayTab;
        [SerializeField] private Button graphicsTab;
        [SerializeField] private Button audioTab;
        [SerializeField] private Button accessibilityTab;
        [SerializeField] private Transform contentParent;
        [SerializeField] private Button closeButton;

        private SettingsTab _activeTab = SettingsTab.Gameplay;
        private readonly List<GameObject> _rows = new List<GameObject>();
        private SettingsManager _settings;

        public void BindRuntime(
            Button gameplayTab,
            Button graphicsTab,
            Button audioTab,
            Button accessibilityTab,
            Transform contentParent,
            Button closeButton)
        {
            this.gameplayTab = gameplayTab;
            this.graphicsTab = graphicsTab;
            this.audioTab = audioTab;
            this.accessibilityTab = accessibilityTab;
            this.contentParent = contentParent;
            this.closeButton = closeButton;
            WireTabs();
        }

        private void OnEnable()
        {
            _settings = SettingsManager.Instance;
            WireTabs();
            BuildTab(SettingsTab.Gameplay);
            if (UIManager.Instance != null)
                UIManager.Instance.Show(UIScreen.Settings, false);
        }

        private void Start()
        {
            _settings = SettingsManager.Instance;
            WireTabs();
        }

        private void WireTabs()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(CloseSettings);
            }

            BindTab(gameplayTab, SettingsTab.Gameplay);
            BindTab(graphicsTab, SettingsTab.Graphics);
            BindTab(audioTab, SettingsTab.Audio);
            BindTab(accessibilityTab, SettingsTab.Accessibility);
        }

        private void BindTab(Button btn, SettingsTab tab)
        {
            if (btn == null) return;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => BuildTab(tab));
        }

        private void CloseSettings()
        {
            _settings?.ApplyAll();
            if (UIManager.Instance != null)
            {
                UIManager.Instance.Hide(UIScreen.Settings);
                if (GameManager.Instance != null && GameManager.Instance.IsPaused)
                    UIManager.Instance.Show(UIScreen.Pause, false);
                else if (GameManager.Instance != null && GameManager.Instance.State == GameState.MainMenu)
                    UIManager.Instance.Show(UIScreen.MainMenu, false);
            }
        }

        private void BuildTab(SettingsTab tab)
        {
            _activeTab = tab;
            ClearRows();
            if (_settings == null) _settings = SettingsManager.Instance;
            if (_settings == null) return;

            switch (tab)
            {
                case SettingsTab.Gameplay:
                    AddSlider("Look Sensitivity", 0.2f, 3f, _settings.LookSensitivity, v => _settings.LookSensitivity = v);
                    AddToggle("Auto Sprint", _settings.AutoSprint, v => _settings.AutoSprint = v);
                    AddToggle("Hold To Interact", _settings.HoldToInteract, v => _settings.HoldToInteract = v);
                    AddToggle("Camera Shake", _settings.CameraShake, v => _settings.CameraShake = v);
                    AddToggle("Haptics", _settings.Haptics, v => _settings.Haptics = v);
                    break;
                case SettingsTab.Graphics:
                    AddChoice("Quality", new[] { "LOW", "MEDIUM", "HIGH" }, _settings.QualityLevel,
                        i => _settings.QualityLevel = i);
                    AddSlider("Resolution Scale", 0.5f, 1.5f, _settings.ResolutionScale, v => _settings.ResolutionScale = v);
                    AddChoice("Target FPS", new[] { "30", "60" }, _settings.TargetFps == 30 ? 0 : 1,
                        i => _settings.TargetFps = i == 0 ? 30 : 60);
                    AddToggle("Shadows", _settings.Shadows, v => _settings.Shadows = v);
                    AddToggle("Post Processing", _settings.PostProcessing, v => _settings.PostProcessing = v);
                    break;
                case SettingsTab.Audio:
                    AddSlider("Master Volume", 0f, 1f, _settings.MasterVolume, v => _settings.MasterVolume = v);
                    AddSlider("Music Volume", 0f, 1f, _settings.MusicVolume, v => _settings.MusicVolume = v);
                    AddSlider("Ambient Volume", 0f, 1f, _settings.AmbientVolume, v => _settings.AmbientVolume = v);
                    AddSlider("Effects Volume", 0f, 1f, _settings.EffectsVolume, v => _settings.EffectsVolume = v);
                    AddSlider("Voice Volume", 0f, 1f, _settings.VoiceVolume, v => _settings.VoiceVolume = v);
                    AddSlider("Ghost Volume", 0f, 1f, _settings.GhostVolume, v => _settings.GhostVolume = v);
                    AddSlider("Equipment Volume", 0f, 1f, _settings.EquipmentVolume, v => _settings.EquipmentVolume = v);
                    AddSlider("UI Volume", 0f, 1f, _settings.UIVolume, v => _settings.UIVolume = v);
                    AddChoice("Dynamic Range", new[] { "Night", "Normal", "Wide" }, (int)_settings.DynamicRangeMode,
                        i => _settings.DynamicRangeMode = (DynamicRangeMode)i);
                    AddChoice("Headphone Mode", new[] { "Off", "Stereo", "Spatial" }, (int)_settings.HeadphoneMode,
                        i => _settings.HeadphoneMode = (HeadphoneMode)i);
                    break;
                case SettingsTab.Accessibility:
                    AddSlider("Brightness", 0.5f, 1.5f, _settings.Brightness, v => _settings.Brightness = v);
                    AddToggle("Reduce Flicker", _settings.ReduceFlicker, v => _settings.ReduceFlicker = v);
                    AddToggle("Reduce Camera Motion", _settings.ReduceCameraMotion, v => _settings.ReduceCameraMotion = v);
                    AddToggle("Large Buttons", _settings.LargeButtons, v => _settings.LargeButtons = v);
                    AddToggle("High Contrast Evidence", _settings.HighContrastEvidence, v => _settings.HighContrastEvidence = v);
                    AddToggle("Subtitles", _settings.Subtitles, v => _settings.Subtitles = v);
                    break;
            }

            AddApplyButton();
        }

        private void AddSlider(string label, float min, float max, float value, System.Action<float> setter)
        {
            if (contentParent == null) return;
            var row = new GameObject(label, typeof(RectTransform));
            row.transform.SetParent(contentParent, false);
            row.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 56);
            _rows.Add(row);

            var text = RuntimeUIFactory.CreateText(row.transform, "Label", label, 18, TextAnchor.MiddleLeft);
            var textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0, 0);
            textRect.anchorMax = new Vector2(0.55f, 1);
            textRect.offsetMin = textRect.offsetMax = Vector2.zero;

            var slider = RuntimeUIFactory.CreateSlider(row.transform, "Slider", min, max, value);
            var sliderRect = slider.GetComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0.58f, 0.2f);
            sliderRect.anchorMax = new Vector2(1f, 0.8f);
            sliderRect.offsetMin = sliderRect.offsetMax = Vector2.zero;
            slider.onValueChanged.AddListener(v => setter(v));
        }

        private void AddToggle(string label, bool value, System.Action<bool> setter)
        {
            if (contentParent == null) return;
            var toggle = RuntimeUIFactory.CreateToggle(contentParent, label, value);
            toggle.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 44);
            toggle.onValueChanged.AddListener(v => setter(v));
            _rows.Add(toggle.gameObject);
        }

        private void AddChoice(string label, string[] options, int selectedIndex, System.Action<int> setter)
        {
            if (contentParent == null) return;
            var row = new GameObject(label, typeof(RectTransform));
            row.transform.SetParent(contentParent, false);
            row.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 48);
            _rows.Add(row);

            RuntimeUIFactory.CreateText(row.transform, "Label", label, 18, TextAnchor.MiddleLeft);

            for (int i = 0; i < options.Length; i++)
            {
                int index = i;
                bool primary = i == selectedIndex;
                var btn = RuntimeUIFactory.CreateButton(row.transform, options[i], () => setter(index), primary, 36);
                var rect = btn.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.55f + i * 0.2f, 0.1f);
                rect.anchorMax = new Vector2(0.73f + i * 0.2f, 0.9f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
            }
        }

        private void AddApplyButton()
        {
            var btn = RuntimeUIFactory.CreateButton(contentParent, "APPLY", () =>
            {
                _settings?.ApplyAll();
                if (SaveManager.Instance != null)
                    SaveManager.Instance.Save();
            }, true, 48);
            _rows.Add(btn.gameObject);
        }

        private void ClearRows()
        {
            foreach (var row in _rows)
            {
                if (row != null)
                    Destroy(row);
            }
            _rows.Clear();
        }
    }
}
