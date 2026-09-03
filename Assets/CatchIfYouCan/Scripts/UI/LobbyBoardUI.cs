using System;
using CatchIfYouCan.Core;
using CatchIfYouCan.Session;
using UnityEngine;
using UnityEngine.UI;

namespace CatchIfYouCan.UI
{
    /// <summary>
    /// The panel the lobby investigation board opens: single player, online co-op, settings.
    ///
    /// <para>
    /// <b>This is not a second main menu.</b> It is a lobby interaction surface onto systems
    /// that already exist - <see cref="SessionLauncher"/> chooses the session mode,
    /// <see cref="UIManager"/> owns the screen registry, <see cref="SettingsUI"/> is the one
    /// settings screen, and <see cref="RuntimeUIFactory"/> builds every widget. Nothing here
    /// re-implements any of them.
    /// </para>
    ///
    /// <para>
    /// Built once and then shown and hidden. A panel rebuilt per interaction is a second
    /// canvas, a second EventSystem argument and a leak; the board asks
    /// <see cref="IsOpen"/> before it offers to open anything.
    /// </para>
    ///
    /// <para>
    /// <b>There is one logical Back action</b> - <see cref="RequestClose"/> - and three input
    /// routes into it: the on-screen button, Escape, and the gamepad B/Circle button. Escape
    /// is also what Unity reports for Android's system back, so the mobile hardware button
    /// costs no extra code and cannot drift out of step with the on-screen one.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LobbyBoardUI : MonoBehaviour
    {
        /// <summary>Gamepad B on Xbox layouts, Circle on PlayStation. The conventional cancel.</summary>
        private const KeyCode GamepadCancel = KeyCode.JoystickButton1;

        private static LobbyBoardUI _instance;

        private GameObject _root;
        private Component _statusText;
        private Button _multiplayerButton;

        /// <summary>Whether the panel is up. The board asks this before offering to open it.</summary>
        public static bool IsOpen =>
            _instance != null && _instance._root != null && _instance._root.activeSelf;

        /// <summary>
        /// Opens the panel, building it the first time. Takes the player's controls; they are
        /// given back by <see cref="Close"/> and by nothing else.
        /// </summary>
        public static void Open()
        {
            if (IsOpen)
                return;

            EnsureBuilt();
            if (_instance == null)
                return;

            _instance.RefreshOnlineAvailability();
            _instance._root.SetActive(true);

            if (UIManager.Instance != null)
                UIManager.Instance.Show(UIScreen.LobbyBoard, false);

            // The lobby keeps running behind the panel - this is a board on a wall, not a
            // pause. What stops is the player driving into it while reading.
            Player.PlayerSpawner.SetInputEnabled(false);
            Player.PlayerSpawner.SetHudVisible(false);
        }

        /// <summary>Closes the panel and gives the player back their controls.</summary>
        public static void Close()
        {
            if (_instance == null || _instance._root == null)
                return;

            _instance._root.SetActive(false);

            if (UIManager.Instance != null)
                UIManager.Instance.Hide(UIScreen.LobbyBoard);

            // SetInputEnabled also restores the cursor on desktop, which is why the cursor is
            // not handled separately here: two places deciding where the cursor is, is how it
            // ends up locked with a menu open.
            Player.PlayerSpawner.SetInputEnabled(true);
            Player.PlayerSpawner.SetHudVisible(true);
        }

        private static void EnsureBuilt()
        {
            if (_instance != null)
                return;

            var go = new GameObject("LobbyBoardUI");
            _instance = go.AddComponent<LobbyBoardUI>();
            _instance.Build();
        }

        // ---- construction ---------------------------------------------------------------

        private void Build()
        {
            // Its own canvas, above the HUD's 100. Deliberately not a second RuntimeUI: this
            // is one purpose-named overlay built through the shared factory, so it inherits the
            // project's scaler, its reference resolution and its EventSystem.
            Canvas canvas = RuntimeUIFactory.BuildRootCanvas("LobbyBoardCanvas", out _);
            canvas.sortingOrder = 200;
            canvas.transform.SetParent(transform, false);

            _root = RuntimeUIFactory.CreatePanel(canvas.transform, "LobbyBoardRoot");
            _root.GetComponent<Image>().color = UITheme.Overlay;

            // The safe-area container. BuildRootCanvas puts a SafeAreaFitter on the canvas
            // object itself, where the RectTransform is driven by the Canvas and the anchors
            // it writes are overwritten - so on a notched phone that fitter does nothing. A
            // fitter on a CHILD rect works, which is what this is, and it is why the Back
            // button below can be trusted to sit inside the safe area.
            var safe = RuntimeUIFactory.CreatePanel(_root.transform, "SafeArea");
            safe.GetComponent<Image>().enabled = false;
            safe.AddComponent<SafeAreaFitter>();

            BuildPanel(safe.transform);
            BuildBackButton(safe.transform);

            _root.SetActive(false);
        }

        private void BuildPanel(Transform parent)
        {
            var panel = RuntimeUIFactory.CreatePanel(parent, "BoardPanel", false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(760f, 700f);
            UITheme.ApplyBorder(panel);

            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(48, 48, 44, 44);
            layout.spacing = 18f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = true;
            layout.childControlWidth = true;

            var title = RuntimeUIFactory.CreateText(panel.transform, "Title",
                "CATCH IF YOU CAN", 30, TextAnchor.MiddleCenter, true);
            UITheme.SetTextColor(title, UITheme.TextPrimary);
            SetPreferredHeight(title.gameObject, 44f);

            var subtitle = RuntimeUIFactory.CreateText(panel.transform, "Subtitle",
                "INVESTIGATION BOARD", 20, TextAnchor.MiddleCenter, true);
            UITheme.SetTextColor(subtitle, UITheme.Primary);
            SetPreferredHeight(subtitle.gameObject, 32f);

            SetPreferredHeight(Spacer(panel.transform, "TitleGap").gameObject, 18f);

            AddEntry(panel.transform, "SINGLEPLAYER", StartSinglePlayer, true);
            _multiplayerButton = AddEntry(panel.transform, "MULTIPLAYER", StartMultiplayer, false);
            AddEntry(panel.transform, "SETTINGS", OpenSettings, false);

            SetPreferredHeight(Spacer(panel.transform, "StatusGap").gameObject, 10f);

            _statusText = RuntimeUIFactory.CreateText(panel.transform, "Status",
                string.Empty, 16, TextAnchor.MiddleCenter);
            UITheme.SetTextColor(_statusText, UITheme.TextMuted);
            SetPreferredHeight(_statusText.gameObject, 60f);
        }

        private static Button AddEntry(Transform parent, string label, Action onClick, bool primary)
        {
            Button button = RuntimeUIFactory.CreateButton(parent, label, onClick, primary, 68f);
            SetPreferredHeight(button.gameObject, 68f);
            return button;
        }

        /// <summary>
        /// Back, bottom-left, inside the safe area, with an arrow so it reads without the word.
        /// Bottom-left because that is where a thumb is on a phone and where every platform's
        /// own back control sits.
        /// </summary>
        private void BuildBackButton(Transform parent)
        {
            Button back = RuntimeUIFactory.CreateButton(parent, "←  BACK", RequestClose, false, 64f);
            var rect = back.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.sizeDelta = new Vector2(220f, 64f);
            rect.anchoredPosition = new Vector2(36f, 36f);
            back.gameObject.name = "BackButton";
        }

        private static Component Spacer(Transform parent, string name) =>
            RuntimeUIFactory.CreateText(parent, name, string.Empty, 1);

        private static void SetPreferredHeight(GameObject go, float height)
        {
            var element = go.GetComponent<LayoutElement>();
            if (element == null)
                element = go.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.minHeight = height;
        }

        // ---- the three entries -----------------------------------------------------------

        /// <summary>
        /// Offline solo, through the one explicit path. No Authentication, no Lobby, no Relay,
        /// no transport and no NetworkManager: <see cref="SessionLauncher.BeginOfflineSolo"/>
        /// installs an offline session with local authority and touches nothing outside this
        /// device, which is what makes airplane mode a non-event.
        /// </summary>
        private void StartSinglePlayer()
        {
            LaunchResult result = SessionLauncher.BeginOfflineSolo();

            if (!result.Started)
            {
                SetStatus("Could not start a single player session: " + result.Detail, UITheme.Warning);
                return;
            }

            Close();

            // Toward mission and loadout selection, which is an existing screen. This does not
            // load a scene; picking the mission does.
            if (UIManager.Instance != null)
                UIManager.Instance.Show(UIScreen.MissionSelect, false);
        }

        /// <summary>
        /// Online, through the same explicit path - and honestly when it cannot be served.
        ///
        /// <para>
        /// <b>It never falls back to offline.</b> With no networking layer registered this
        /// says so and changes nothing; a player who chose online and silently got a
        /// single-player mission is the failure
        /// <c>Docs/MULTIPLAYER_RUNTIME_ARCHITECTURE.md</c> §7b exists to forbid. The provider
        /// is asked BEFORE the launch, because offering a button that can only fail is worse
        /// than saying why it cannot run.
        /// </para>
        /// </summary>
        private void StartMultiplayer()
        {
            if (!SessionLauncher.HasOnlineProvider)
            {
                SetStatus("Online multiplayer currently unavailable.\n" +
                          "Networking provider not installed in this build.", UITheme.Warning);
                return;
            }

            LaunchResult result = SessionLauncher.BeginOnline(SessionChoice.OnlineHost, null);

            if (!result.Started)
            {
                SetStatus("Online session could not be started: " + result.Detail, UITheme.Warning);
                return;
            }

            Close();

            if (UIManager.Instance != null)
                UIManager.Instance.Show(UIScreen.MissionSelect, false);
        }

        /// <summary>
        /// The existing settings screen, shown over this one rather than instead of it, so the
        /// board is still here when settings closes itself. There is one settings system and
        /// this is not a second one.
        /// </summary>
        private void OpenSettings()
        {
            if (UIManager.Instance == null)
            {
                SetStatus("Settings are not available in this scene.", UITheme.Warning);
                return;
            }

            if (!UIManager.Instance.TryGetScreen(UIScreen.Settings, out _))
            {
                SetStatus("Settings screen is not registered in this scene.", UITheme.Warning);
                return;
            }

            UIManager.Instance.Show(UIScreen.Settings, false);
        }

        // ---- one Back action, three input routes -----------------------------------------

        /// <summary>
        /// The one logical Back. The on-screen button calls it, so does Escape, so does the
        /// gamepad cancel button, and on Android so does the system back key - Unity reports
        /// that as Escape.
        /// </summary>
        public void RequestClose() => Close();

        private void Update()
        {
            if (_root == null || !_root.activeSelf)
                return;

            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape) ||
                UnityEngine.Input.GetKeyDown(GamepadCancel))
            {
                RequestClose();
            }
        }

        // ---- state -----------------------------------------------------------------------

        /// <summary>
        /// Asked every time the panel opens rather than once at build, because a networking
        /// layer can register itself at any point in a session's life.
        /// </summary>
        private void RefreshOnlineAvailability()
        {
            bool online = SessionLauncher.HasOnlineProvider;

            if (_multiplayerButton != null)
            {
                // Left interactive on purpose. A dead grey button tells the player nothing;
                // pressing it and being told why is an answer.
                var colors = _multiplayerButton.colors;
                colors.normalColor = online ? UITheme.BackgroundPanel : UITheme.Hex("#0C0F0E");
                _multiplayerButton.colors = colors;
            }

            SetStatus(online
                    ? string.Empty
                    : "Online multiplayer is not available in this build.",
                online ? UITheme.TextMuted : UITheme.TextMuted);
        }

        private void SetStatus(string message, Color color)
        {
            if (_statusText == null)
                return;

            UITheme.SetText(_statusText, message);
            UITheme.SetTextColor(_statusText, color);
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        /// <summary>A fresh process holds no panel from the last one.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => _instance = null;
    }
}
