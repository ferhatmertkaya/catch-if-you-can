using System;
using System.Collections.Generic;
using CatchIfYouCan.Audio;
using CatchIfYouCan.Core;
using CatchIfYouCan.Input;
using CatchIfYouCan.Save;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if TMP_PRESENT || UNITY_TEXTMESHPRO
using TMPro;
#endif

namespace CatchIfYouCan.UI
{
    public static class RuntimeUIFactory
    {
        private static Font _defaultFont;
        private static bool _tmpAvailable;

        static RuntimeUIFactory()
        {
#if TMP_PRESENT || UNITY_TEXTMESHPRO
            _tmpAvailable = true;
#else
            _tmpAvailable = Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro") != null;
#endif
            _defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_defaultFont == null)
                _defaultFont = Font.CreateDynamicFontFromOSFont("Arial", 16);
        }

        public static Canvas BuildRootCanvas(string name, out EventSystem eventSystem)
        {
            eventSystem = EventSystemUtil.EnsureEventSystem();

            var canvasGo = new GameObject(name);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var safe = canvasGo.AddComponent<SafeAreaFitter>();
            safe.enabled = true;

            return canvas;
        }

        public static GameObject CreatePanel(Transform parent, string name, bool stretch = true)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            if (stretch)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
            var img = go.GetComponent<Image>();
            UITheme.ApplyPanel(img);
            return go;
        }

        public static Component CreateText(Transform parent, string name, string text, int fontSize,
            TextAnchor alignment = TextAnchor.MiddleCenter, bool bold = false)
        {
            GameObject go;
            Component comp;
#if TMP_PRESENT || UNITY_TEXTMESHPRO
            if (_tmpAvailable)
            {
                go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(parent, false);
                var tmp = go.AddComponent<TextMeshProUGUI>();
                tmp.text = text;
                tmp.fontSize = fontSize;
                tmp.alignment = AlignmentFromAnchor(alignment);
                tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
                tmp.color = UITheme.TextPrimary;
                tmp.raycastTarget = false;
                comp = tmp;
            }
            else
#endif
            {
                go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(parent, false);
                var legacy = go.AddComponent<Text>();
                legacy.text = text;
                legacy.fontSize = fontSize;
                legacy.alignment = alignment;
                legacy.font = _defaultFont;
                legacy.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
                legacy.color = UITheme.TextPrimary;
                legacy.raycastTarget = false;
                comp = legacy;
            }

            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(400, fontSize + 12);
            return comp;
        }

        public static Button CreateButton(Transform parent, string label, Action onClick, bool primary = false,
            float height = 0f)
        {
            height = height <= 0f ? UITheme.ButtonHeight : height;
            var go = new GameObject(label + "Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(420, height);

            var img = go.GetComponent<Image>();
            img.color = primary ? UITheme.Secondary : UITheme.BackgroundPanel;
            UITheme.ApplyBorder(go);

            var btn = go.GetComponent<Button>();
            UITheme.ApplyButtonColors(btn, primary);
            btn.onClick.AddListener(() =>
            {
                UiAudioService.Instance?.PlayButton();
                onClick?.Invoke();
            });
            go.AddComponent<UIButtonFeedback>();

            var text = CreateText(go.transform, "Label", label, 22, TextAnchor.MiddleCenter, true);
            Stretch(text.gameObject);

            return btn;
        }

        public static Slider CreateSlider(Transform parent, string name, float min, float max, float value)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Slider));
            go.transform.SetParent(parent, false);
            var slider = go.GetComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;

            var bg = CreatePanel(go.transform, "Background", false);
            var bgRect = bg.GetComponent<RectTransform>();
            bgRect.sizeDelta = new Vector2(300, 16);
            bg.GetComponent<Image>().color = UITheme.BackgroundDark;

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(bg.transform, false);
            Stretch(fillArea);

            var fill = CreatePanel(fillArea.transform, "Fill", true);
            fill.GetComponent<Image>().color = UITheme.Primary;

            var handle = CreatePanel(go.transform, "Handle", false);
            handle.GetComponent<RectTransform>().sizeDelta = new Vector2(24, 24);
            handle.GetComponent<Image>().color = UITheme.Secondary;

            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.handleRect = handle.GetComponent<RectTransform>();
            slider.targetGraphic = handle.GetComponent<Image>();
            return slider;
        }

        public static Toggle CreateToggle(Transform parent, string label, bool value)
        {
            var go = new GameObject(label + "Toggle", typeof(RectTransform), typeof(Toggle));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(420, 44);

            var bg = CreatePanel(go.transform, "Background", false);
            bg.GetComponent<RectTransform>().sizeDelta = new Vector2(36, 36);
            bg.GetComponent<RectTransform>().anchorMin = new Vector2(0, 0.5f);
            bg.GetComponent<RectTransform>().anchorMax = new Vector2(0, 0.5f);
            bg.GetComponent<RectTransform>().pivot = new Vector2(0, 0.5f);
            bg.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;

            var check = CreatePanel(bg.transform, "Checkmark", true);
            check.GetComponent<Image>().color = UITheme.Primary;

            var text = CreateText(go.transform, "Label", label, 20, TextAnchor.MiddleLeft);
            var textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0, 0);
            textRect.anchorMax = new Vector2(1, 1);
            textRect.offsetMin = new Vector2(48, 0);
            textRect.offsetMax = Vector2.zero;

            var toggle = go.GetComponent<Toggle>();
            toggle.isOn = value;
            toggle.targetGraphic = bg.GetComponent<Image>();
            toggle.graphic = check.GetComponent<Image>();
            return toggle;
        }

        public static void Stretch(GameObject go)
        {
            var rect = go.GetComponent<RectTransform>();
            if (rect == null) return;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public static RuntimeUIBuildResult BuildCompleteUI()
        {
            var canvas = BuildRootCanvas("RuntimeUI", out _);
            UnityEngine.Object.DontDestroyOnLoad(canvas.gameObject);

            var result = new RuntimeUIBuildResult { Canvas = canvas };

            result.MainMenuRoot = CreatePanel(canvas.transform, "MainMenu");
            var mainMenuBackground = result.MainMenuRoot.GetComponent<Image>();
            if (mainMenuBackground != null)
            {
                mainMenuBackground.color = Color.clear;
                mainMenuBackground.raycastTarget = false;
            }
            var mainMenu = result.MainMenuRoot.AddComponent<MainMenuController>();
            WireMainMenu(mainMenu, result.MainMenuRoot.transform);

            result.HudRoot = CreatePanel(canvas.transform, "HUD");
            var hud = result.HudRoot.AddComponent<MobileHUDController>();
            WireHUD(hud, result.HudRoot.transform);

            result.MissionSelectRoot = CreatePanel(canvas.transform, "MissionSelect");
            var missionSelect = result.MissionSelectRoot.AddComponent<MissionSelectUI>();
            WireMissionSelect(missionSelect, result.MissionSelectRoot.transform);

            result.JournalRoot = CreatePanel(canvas.transform, "Journal");
            var journal = result.JournalRoot.AddComponent<JournalController>();
            WireJournal(journal, result.JournalRoot.transform);

            result.PauseRoot = CreatePanel(canvas.transform, "Pause");
            var pause = result.PauseRoot.AddComponent<PauseMenuUI>();
            WirePause(pause, result.PauseRoot.transform);

            result.SettingsRoot = CreatePanel(canvas.transform, "Settings");
            var settings = result.SettingsRoot.AddComponent<SettingsUI>();
            WireSettings(settings, result.SettingsRoot.transform);

            result.MissionResultRoot = CreatePanel(canvas.transform, "MissionResult");
            var missionResult = result.MissionResultRoot.AddComponent<MissionResultUI>();
            WireMissionResult(missionResult, result.MissionResultRoot.transform);

            result.EquipmentShopRoot = CreatePanel(canvas.transform, "EquipmentShop");
            var shop = result.EquipmentShopRoot.AddComponent<EquipmentShopUI>();
            WireEquipmentShop(shop, result.EquipmentShopRoot.transform);

            result.LoadingRoot = CreatePanel(canvas.transform, "Loading");
            var loading = result.LoadingRoot.AddComponent<LoadingUI>();
            WireLoading(loading, result.LoadingRoot.transform);

            result.InteractionRoot = CreatePanel(canvas.transform, "InteractionPrompt");
            var interaction = result.InteractionRoot.AddComponent<InteractionPromptUI>();
            WireInteractionPrompt(interaction, result.InteractionRoot.transform);

            result.EntityDiscoveredRoot = CreatePanel(canvas.transform, "EntityDiscovered");
            var entityDisc = result.EntityDiscoveredRoot.AddComponent<EntityDiscoveredUI>();
            WireEntityDiscovered(entityDisc, result.EntityDiscoveredRoot.transform);

            result.CameraMonitorRoot = CreatePanel(canvas.transform, "CameraMonitor");
            var camMon = result.CameraMonitorRoot.AddComponent<CameraMonitorUI>();
            WireCameraMonitor(camMon, result.CameraMonitorRoot.transform);

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            result.DebugRoot = CreatePanel(canvas.transform, "DebugMenu");
            var debug = result.DebugRoot.AddComponent<DebugMenuUI>();
            WireDebugMenu(debug, result.DebugRoot.transform);
#endif

            if (UIManager.Instance != null)
            {
                UIManager.Instance.RegisterScreen(UIScreen.MainMenu, result.MainMenuRoot);
                UIManager.Instance.RegisterScreen(UIScreen.HUD, result.HudRoot);
                UIManager.Instance.RegisterScreen(UIScreen.MissionSelect, result.MissionSelectRoot);
                UIManager.Instance.RegisterScreen(UIScreen.Journal, result.JournalRoot);
                UIManager.Instance.RegisterScreen(UIScreen.Pause, result.PauseRoot);
                UIManager.Instance.RegisterScreen(UIScreen.Settings, result.SettingsRoot);
                UIManager.Instance.RegisterScreen(UIScreen.MissionComplete, result.MissionResultRoot);
                UIManager.Instance.RegisterScreen(UIScreen.MissionFailed, result.MissionResultRoot);
                UIManager.Instance.RegisterScreen(UIScreen.EquipmentShop, result.EquipmentShopRoot);
                UIManager.Instance.RegisterScreen(UIScreen.Loading, result.LoadingRoot);
                UIManager.Instance.RegisterScreen(UIScreen.InteractionPrompt, result.InteractionRoot);
                UIManager.Instance.RegisterScreen(UIScreen.EntityDiscovered, result.EntityDiscoveredRoot);
                UIManager.Instance.RegisterScreen(UIScreen.CameraMonitor, result.CameraMonitorRoot);
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                UIManager.Instance.RegisterScreen(UIScreen.Debug, result.DebugRoot);
#endif
            }

            result.MainMenuRoot.SetActive(false);
            result.HudRoot.SetActive(false);
            result.MissionSelectRoot.SetActive(false);
            result.JournalRoot.SetActive(false);
            result.PauseRoot.SetActive(false);
            result.SettingsRoot.SetActive(false);
            result.MissionResultRoot.SetActive(false);
            result.EquipmentShopRoot.SetActive(false);
            result.LoadingRoot.SetActive(false);
            result.InteractionRoot.SetActive(false);
            result.EntityDiscoveredRoot.SetActive(false);
            result.CameraMonitorRoot.SetActive(false);
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            result.DebugRoot.SetActive(false);
#endif

            return result;
        }

        private static void WireMainMenu(MainMenuController ctrl, Transform root)
        {
            var left = CreatePanel(root, "LeftColumn", false);
            var leftRect = left.GetComponent<RectTransform>();
            leftRect.anchorMin = new Vector2(0.05f, 0.15f);
            leftRect.anchorMax = new Vector2(0.35f, 0.85f);
            leftRect.offsetMin = leftRect.offsetMax = Vector2.zero;

            var leftBackground = left.GetComponent<Image>();
            if (leftBackground != null)
            {
                leftBackground.color = Color.clear;
                leftBackground.raycastTarget = false;
            }

            var gameLogo = new GameObject(
                "GameLogo",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));

            gameLogo.transform.SetParent(root, false);

            var gameLogoImage = gameLogo.GetComponent<Image>();
            gameLogoImage.sprite = Resources.Load<Sprite>("UI/Branding/CatchIfYouCan_Logo");
            gameLogoImage.color = Color.white;
            gameLogoImage.preserveAspect = true;
            gameLogoImage.raycastTarget = false;

            Position(gameLogo, -0.15f, 0.16f, 0.45f, 0.88f);

            var layout = left.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ctrl.BindRuntime(
                levelText: CreateText(root, "LevelText", "LV 1", 24, TextAnchor.UpperRight),
                moneyText: CreateText(root, "MoneyText", "$500", 24, TextAnchor.UpperRight),
                versionText: CreateText(root, "VersionText", "v1.0", 16, TextAnchor.LowerRight),
                flickerOverlay: CreatePanel(root, "FlickerOverlay").GetComponent<Image>(),
                playButton: CreateButton(left.transform, "PLAY", null, true),
                equipmentButton: CreateButton(left.transform, "EQUIPMENT", null),
                journalButton: CreateButton(left.transform, "JOURNAL", null),
                trainingButton: CreateButton(left.transform, "TRAINING", null),
                settingsButton: CreateButton(left.transform, "SETTINGS", null),
                creditsButton: CreateButton(left.transform, "CREDITS", null));

            Position(ctrl.LevelText.gameObject, 0.62f, 0.88f, 0.95f, 0.96f);
            Position(ctrl.MoneyText.gameObject, 0.62f, 0.82f, 0.95f, 0.88f);
            Position(ctrl.VersionText.gameObject, 0.75f, 0.02f, 0.98f, 0.08f);
            UITheme.StyleMuted(ctrl.VersionText);

            var flickerRect = ctrl.FlickerOverlay.rectTransform;
            Stretch(ctrl.FlickerOverlay.gameObject);
            ctrl.FlickerOverlay.color = new Color(0, 0, 0, 0);
            ctrl.FlickerOverlay.raycastTarget = false;
        }

        private static void WireHUD(MobileHUDController hud, Transform root)
        {
            var topBar = CreatePanel(root, "TopBar", false);
            Position(topBar, 0, 0.88f, 1, 1);
            var caseIcon = CreatePanel(topBar.transform, "CaseIcon", false);
            caseIcon.GetComponent<RectTransform>().sizeDelta = new Vector2(64, 64);
            var journalBtn = CreateButton(topBar.transform, "JOURNAL", null, false, 48);

            var joystickArea = CreatePanel(root, "JoystickArea", false);
            Position(joystickArea, 0.02f, 0.05f, 0.28f, 0.45f);
            joystickArea.GetComponent<Image>().color = new Color(1, 1, 1, 0.05f);

            var interactBtn = CreateButton(root, "INTERACT", null, true, 64);
            Position(interactBtn.gameObject, 0.72f, 0.12f, 0.92f, 0.22f);

            var crouchBtn = CreateButton(root, "CROUCH", null, false, 52);
            Position(crouchBtn.gameObject, 0.55f, 0.05f, 0.68f, 0.14f);

            var sprintBtn = CreateButton(root, "SPRINT", null, false, 52);
            Position(sprintBtn.gameObject, 0.7f, 0.05f, 0.83f, 0.14f);

            var useBtn = CreateButton(root, "USE", null, true, 52);
            Position(useBtn.gameObject, 0.85f, 0.05f, 0.98f, 0.14f);

            var slots = new Image[3];
            var slotRow = CreatePanel(root, "EquipmentSlots", false);
            Position(slotRow, 0.35f, 0.02f, 0.65f, 0.12f);
            var hlg = slotRow.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            for (int i = 0; i < 3; i++)
            {
                var slot = CreatePanel(slotRow.transform, "Slot" + (i + 1), false);
                slot.GetComponent<RectTransform>().sizeDelta = new Vector2(72, 72);
                slots[i] = slot.GetComponent<Image>();
            }

            hud.BindRuntime(
                caseIcon: caseIcon.GetComponent<Image>(),
                journalButton: journalBtn,
                joystickArea: joystickArea.GetComponent<RectTransform>(),
                interactButton: interactBtn,
                crouchButton: crouchBtn,
                sprintButton: sprintBtn,
                equipmentSlots: slots,
                useButton: useBtn);

            EnsureMobileInput(joystickArea.transform);
        }

        private static void EnsureMobileInput(Transform joystickParent)
        {
            if (MobileInputController.Instance != null)
                return;

            var inputGo = new GameObject("MobileInputController");
            UnityEngine.Object.DontDestroyOnLoad(inputGo);
            var input = inputGo.AddComponent<MobileInputController>();

            var joystickGo = new GameObject("MoveJoystick", typeof(RectTransform));
            joystickGo.transform.SetParent(joystickParent, false);
            Stretch(joystickGo);
            var bg = CreatePanel(joystickGo.transform, "Background", true);
            bg.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f);
            var handle = CreatePanel(joystickGo.transform, "Handle", false);
            handle.GetComponent<RectTransform>().sizeDelta = new Vector2(72, 72);
            handle.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.35f);

            var joystick = joystickGo.AddComponent<VirtualJoystick>();
            SetPrivateField(joystick, "background", bg.GetComponent<RectTransform>());
            SetPrivateField(joystick, "handle", handle.GetComponent<RectTransform>());
            input.BindJoystick(joystick);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            if (target == null)
                return;

            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(target, value);
        }

        private static void WireMissionSelect(MissionSelectUI ui, Transform root)
        {
            var title = CreateText(root, "Title", "SELECT MISSION", 42, TextAnchor.UpperCenter, true);
            Position(title.gameObject, 0.1f, 0.85f, 0.9f, 0.95f);
            UITheme.StyleTitle(title);

            var list = CreatePanel(root, "MissionList", false);
            Position(list, 0.05f, 0.2f, 0.55f, 0.82f);
            var vlg = list.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8;
            vlg.padding = new RectOffset(8, 8, 8, 8);

            var detail = CreatePanel(root, "DetailPanel", false);
            Position(detail, 0.58f, 0.2f, 0.95f, 0.82f);

            ui.BindRuntime(
                missionListParent: list.transform,
                detailTitle: CreateText(detail.transform, "DetailTitle", "", 32, TextAnchor.UpperLeft, true),
                detailBody: CreateText(detail.transform, "DetailBody", "", 20, TextAnchor.UpperLeft),
                startButton: CreateButton(root, "START INVESTIGATION", null, true, 64),
                backButton: CreateButton(root, "BACK", null, false, 48));

            Position(ui.StartButton.gameObject, 0.58f, 0.08f, 0.95f, 0.16f);
            Position(ui.BackButton.gameObject, 0.05f, 0.08f, 0.2f, 0.16f);
        }

        private static void WireJournal(JournalController journal, Transform root)
        {
            var panel = CreatePanel(root, "SlidePanel", false);
            Position(panel, 0.55f, 0, 1, 1);
            panel.GetComponent<Image>().color = UITheme.Overlay;

            var tabs = CreatePanel(panel.transform, "Tabs", false);
            Position(tabs, 0, 0.88f, 1, 1);
            var tabLayout = tabs.AddComponent<HorizontalLayoutGroup>();
            tabLayout.spacing = 4;
            tabLayout.padding = new RectOffset(8, 8, 4, 4);

            var content = CreatePanel(panel.transform, "Content", true);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.offsetMax = new Vector2(0, -80);

            var closeButton = CreateButton(panel.transform, "CLOSE", null, false, 44);

            journal.BindRuntime(
                slidePanel: panel.GetComponent<RectTransform>(),
                tabButtons: new[]
                {
                    CreateButton(tabs.transform, "CASE", null, false, 40),
                    CreateButton(tabs.transform, "EVIDENCE", null, false, 40),
                    CreateButton(tabs.transform, "ENTITIES", null, false, 40),
                    CreateButton(tabs.transform, "PHOTOS", null, false, 40),
                    CreateButton(tabs.transform, "OBJECTIVES", null, false, 40)
                },
                contentParent: content.transform,
                closeButton: closeButton);

            if (journal.GetComponent<JournalAudio>() == null)
                journal.gameObject.AddComponent<JournalAudio>();

            Position(closeButton.gameObject, 0.02f, 0.02f, 0.2f, 0.08f);
        }

        private static void WirePause(PauseMenuUI pause, Transform root)
        {
            root.GetComponent<Image>().color = UITheme.Overlay;
            var center = CreatePanel(root, "Center", false);
            Position(center, 0.3f, 0.2f, 0.7f, 0.8f);
            var layout = center.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12;
            layout.padding = new RectOffset(24, 24, 24, 24);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;

            pause.BindRuntime(
                continueButton: CreateButton(center.transform, "CONTINUE", null, true),
                settingsButton: CreateButton(center.transform, "SETTINGS", null),
                restartButton: CreateButton(center.transform, "RESTART", null),
                returnVanButton: CreateButton(center.transform, "RETURN TO VAN", null),
                exitButton: CreateButton(center.transform, "EXIT MISSION", null));
        }

        private static void WireSettings(SettingsUI settings, Transform root)
        {
            var title = CreateText(root, "Title", "SETTINGS", 40, TextAnchor.UpperCenter, true);
            Position(title.gameObject, 0.1f, 0.88f, 0.9f, 0.98f);

            var tabs = CreatePanel(root, "Tabs", false);
            Position(tabs, 0.05f, 0.8f, 0.95f, 0.87f);
            var tabLayout = tabs.AddComponent<HorizontalLayoutGroup>();
            tabLayout.spacing = 6;

            var content = CreatePanel(root, "TabContent", false);
            Position(content, 0.05f, 0.12f, 0.95f, 0.78f);
            var scroll = content.AddComponent<ScrollRect>();
            var viewport = CreatePanel(content.transform, "Viewport", true);
            scroll.viewport = viewport.GetComponent<RectTransform>();
            var inner = CreatePanel(viewport.transform, "Inner", false);
            var innerLayout = inner.AddComponent<VerticalLayoutGroup>();
            innerLayout.spacing = 10;
            innerLayout.padding = new RectOffset(12, 12, 12, 12);
            innerLayout.childControlWidth = true;
            innerLayout.childForceExpandWidth = true;
            scroll.content = inner.GetComponent<RectTransform>();
            scroll.horizontal = false;

            var closeButton = CreateButton(root, "CLOSE", null, false, 48);

            settings.BindRuntime(
                gameplayTab: CreateButton(tabs.transform, "GAMEPLAY", null, false, 40),
                graphicsTab: CreateButton(tabs.transform, "GRAPHICS", null, false, 40),
                audioTab: CreateButton(tabs.transform, "AUDIO", null, false, 40),
                accessibilityTab: CreateButton(tabs.transform, "ACCESSIBILITY", null, false, 40),
                contentParent: inner.transform,
                closeButton: closeButton);

            Position(closeButton.gameObject, 0.4f, 0.02f, 0.6f, 0.1f);
        }

        private static void WireMissionResult(MissionResultUI ui, Transform root)
        {
            root.GetComponent<Image>().color = UITheme.Overlay;
            var panel = CreatePanel(root, "Panel", false);
            Position(panel, 0.25f, 0.15f, 0.75f, 0.85f);
            ui.BindRuntime(
                titleText: CreateText(panel.transform, "Title", "MISSION COMPLETE", 44, TextAnchor.UpperCenter, true),
                breakdownText: CreateText(panel.transform, "Breakdown", "", 22, TextAnchor.UpperLeft),
                continueButton: CreateButton(panel.transform, "CONTINUE", null, true));
            Position(ui.BreakdownText.gameObject, 0.05f, 0.2f, 0.95f, 0.75f);
            Position(ui.ContinueButton.gameObject, 0.2f, 0.05f, 0.8f, 0.15f);
        }

        private static void WireEquipmentShop(EquipmentShopUI shop, Transform root)
        {
            var title = CreateText(root, "Title", "EQUIPMENT", 40, TextAnchor.UpperCenter, true);
            Position(title.gameObject, 0.1f, 0.88f, 0.9f, 0.98f);

            var cats = CreatePanel(root, "Categories", false);
            Position(cats, 0.05f, 0.75f, 0.95f, 0.86f);
            var catLayout = cats.AddComponent<HorizontalLayoutGroup>();
            catLayout.spacing = 6;

            var list = CreatePanel(root, "ItemList", false);
            Position(list, 0.05f, 0.15f, 0.6f, 0.72f);
            list.AddComponent<VerticalLayoutGroup>().spacing = 6;

            var detail = CreatePanel(root, "Detail", false);
            Position(detail, 0.62f, 0.15f, 0.95f, 0.72f);

            shop.BindRuntime(
                categoryButtons: new[]
                {
                    CreateButton(cats.transform, "Detection", null, false, 40),
                    CreateButton(cats.transform, "Visual", null, false, 40),
                    CreateButton(cats.transform, "Audio", null, false, 40),
                    CreateButton(cats.transform, "Protection", null, false, 40),
                    CreateButton(cats.transform, "Utility", null, false, 40)
                },
                itemListParent: list.transform,
                detailText: CreateText(detail.transform, "Detail", "", 20, TextAnchor.UpperLeft),
                buyButton: CreateButton(detail.transform, "BUY / UPGRADE", null, true),
                backButton: CreateButton(root, "BACK", null, false, 48));

            Position(shop.BackButton.gameObject, 0.05f, 0.05f, 0.2f, 0.12f);
        }

        private static void WireLoading(LoadingUI loading, Transform root)
        {
            root.GetComponent<Image>().color = UITheme.Overlay;
            loading.BindRuntime(
                progressSlider: CreateSlider(root, "Progress", 0, 1, 0),
                tipText: CreateText(root, "Tip", "Loading...", 24, TextAnchor.MiddleCenter),
                logoText: CreateText(root, "Logo", "CATCH IF YOU CAN", 48, TextAnchor.UpperCenter, true));
            Position(loading.ProgressSlider.gameObject, 0.15f, 0.35f, 0.85f, 0.42f);
            Position(loading.TipText.gameObject, 0.1f, 0.22f, 0.9f, 0.32f);
            Position(loading.LogoText.gameObject, 0.1f, 0.55f, 0.9f, 0.7f);
        }

        private static void WireInteractionPrompt(InteractionPromptUI ui, Transform root)
        {
            root.GetComponent<Image>().raycastTarget = false;
            var icon = CreatePanel(root, "HandIcon", false);
            icon.GetComponent<RectTransform>().sizeDelta = new Vector2(48, 48);
            icon.GetComponent<Image>().color = UITheme.Primary;
            ui.BindRuntime(
                handIcon: icon.GetComponent<Image>(),
                promptText: CreateText(root, "Prompt", "", 24, TextAnchor.MiddleCenter));
            Position(ui.RootRect != null ? ui.RootRect.gameObject : root.gameObject, 0.35f, 0.08f, 0.65f, 0.16f);
        }

        private static void WireEntityDiscovered(EntityDiscoveredUI ui, Transform root)
        {
            root.GetComponent<Image>().color = UITheme.Overlay;
            var panel = CreatePanel(root, "Panel", false);
            Position(panel, 0.2f, 0.3f, 0.8f, 0.7f);
            ui.BindRuntime(
                titleText: CreateText(panel.transform, "Title", "ENTITY DISCOVERED", 48, TextAnchor.UpperCenter, true),
                nameText: CreateText(panel.transform, "Name", "", 36, TextAnchor.MiddleCenter, true),
                descText: CreateText(panel.transform, "Desc", "", 20, TextAnchor.LowerCenter));
        }

        private static void WireCameraMonitor(CameraMonitorUI ui, Transform root)
        {
            Position(root.gameObject, 0.05f, 0.55f, 0.45f, 0.95f);
            ui.BindRuntime(
                cameraNameText: CreateText(root, "CamName", "CAM 01", 24, TextAnchor.UpperLeft, true),
                prevButton: CreateButton(root, "PREV", null, false, 40),
                nextButton: CreateButton(root, "NEXT", null, false, 40),
                nightVisionToggle: CreateToggle(root, "Night Vision", false),
                distortionOverlay: CreatePanel(root, "Distortion").GetComponent<Image>());
            Position(ui.PrevButton.gameObject, 0.05f, 0.05f, 0.25f, 0.12f);
            Position(ui.NextButton.gameObject, 0.28f, 0.05f, 0.48f, 0.12f);
            ui.DistortionOverlay.color = new Color(0.2f, 1f, 0.4f, 0);
            ui.DistortionOverlay.raycastTarget = false;
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private static void WireDebugMenu(DebugMenuUI ui, Transform root)
        {
            Position(root.gameObject, 0.02f, 0.02f, 0.35f, 0.55f);
            var layout = root.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 4;
            layout.padding = new RectOffset(8, 8, 8, 8);
            ui.BindRuntime(
                fpsText: CreateText(root, "FPS", "FPS: 0", 18, TextAnchor.UpperLeft),
                ghostStateText: CreateText(root, "GhostState", "", 16, TextAnchor.UpperLeft),
                forceEventButton: CreateButton(root, "Force Event", null, false, 36),
                forceHuntButton: CreateButton(root, "Force Hunt", null, false, 36),
                teleportButton: CreateButton(root, "Teleport Player", null, false, 36),
                giveEquipmentButton: CreateButton(root, "Give Equipment", null, false, 36),
                invincibilityToggle: CreateToggle(root, "Invincibility", false));
        }
#endif

        private static void Position(GameObject go, float xMin, float yMin, float xMax, float yMax)
        {
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(xMin, yMin);
            rect.anchorMax = new Vector2(xMax, yMax);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

#if TMP_PRESENT || UNITY_TEXTMESHPRO
        private static TextAlignmentOptions AlignmentFromAnchor(TextAnchor anchor)
        {
            return anchor switch
            {
                TextAnchor.UpperLeft => TextAlignmentOptions.TopLeft,
                TextAnchor.UpperCenter => TextAlignmentOptions.Top,
                TextAnchor.UpperRight => TextAlignmentOptions.TopRight,
                TextAnchor.MiddleLeft => TextAlignmentOptions.Left,
                TextAnchor.MiddleCenter => TextAlignmentOptions.Center,
                TextAnchor.MiddleRight => TextAlignmentOptions.Right,
                TextAnchor.LowerLeft => TextAlignmentOptions.BottomLeft,
                TextAnchor.LowerCenter => TextAlignmentOptions.Bottom,
                TextAnchor.LowerRight => TextAlignmentOptions.BottomRight,
                _ => TextAlignmentOptions.Center
            };
        }
#endif
    }

    public class RuntimeUIBuildResult
    {
        public Canvas Canvas;
        public GameObject MainMenuRoot;
        public GameObject HudRoot;
        public GameObject MissionSelectRoot;
        public GameObject JournalRoot;
        public GameObject PauseRoot;
        public GameObject SettingsRoot;
        public GameObject MissionResultRoot;
        public GameObject EquipmentShopRoot;
        public GameObject LoadingRoot;
        public GameObject InteractionRoot;
        public GameObject EntityDiscoveredRoot;
        public GameObject CameraMonitorRoot;
        public GameObject DebugRoot;
    }
}
