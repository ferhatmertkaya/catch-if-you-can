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

        /// <summary>This panel's name in <see cref="MenuInputGate"/>. One name, one hold.</summary>
        private const string GateOwner = "LobbyBoardUI";

        private static LobbyBoardUI _instance;

        private GameObject _root;
        private Component _statusText;
        private Button _multiplayerButton;

        /// <summary>
        /// The frame this panel came up on.
        ///
        /// <para>
        /// Mission select also closes on Escape, and closing it opens this panel - all within
        /// one frame. <c>GetKeyDown</c> stays true for the whole of that frame and Unity does
        /// not promise which component's Update runs first, so without this the single press
        /// that backs out of mission select could also close the board it just opened. A frame
        /// number, not a timer: it costs nothing and adds no latency.
        /// </para>
        /// </summary>
        private int _openedFrame = -1;

        /// <summary>
        /// Whether this lobby has a board at all - that is, whether the player reached the game
        /// through it. Mission select asks this to decide where Back goes: to the board they
        /// came from, or, in a scene with no lobby, to the main menu.
        /// </summary>
        public static bool Exists => _instance != null;

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
            _instance._openedFrame = Time.frameCount;

            if (UIManager.Instance != null)
                UIManager.Instance.Show(UIScreen.LobbyBoard, false);

            // The lobby keeps running behind the panel - this is a board on a wall, not a
            // pause. What stops is the player driving into it while reading, and the on-screen
            // controls, which would otherwise sit on top of the menu and eat its touches.
            MenuInputGate.Push(GateOwner);
        }

        /// <summary>Closes the panel and gives the player back their controls.</summary>
        public static void Close()
        {
            if (_instance == null || _instance._root == null)
                return;

            _instance._root.SetActive(false);

            if (UIManager.Instance != null)
                UIManager.Instance.Hide(UIScreen.LobbyBoard);

            // Released, not restored. If mission select is already up, it is holding the gate
            // too and the controls stay away until it closes - which is exactly the sequencing
            // this panel used to get wrong by handing them back here unconditionally.
            MenuInputGate.Pop(GateOwner);
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

        /// <summary>
        /// One centred column on black. No frame, no boxes.
        ///
        /// <para>
        /// It used to be a 760x700 panel with a green outline holding three green-filled
        /// buttons and a green subtitle - four separate green things on one screen, which is
        /// what made it read as a tool rather than as part of the game. What is left is the
        /// title in the display face, a hairline of brand green under it, three dark buttons
        /// with a hairline each, and a small line of status text.
        /// </para>
        /// </summary>
        private void BuildPanel(Transform parent)
        {
            var column = RuntimeUIFactory.CreatePanel(parent, "BoardColumn", false);
            var rect = column.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(680f, 640f);

            // The column is a layout, not a surface. Nothing to see, nothing to raycast.
            var columnImage = column.GetComponent<Image>();
            columnImage.enabled = false;

            var layout = column.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = true;
            layout.childControlWidth = true;

            var title = RuntimeUIFactory.CreateText(column.transform, "Title",
                "CATCH IF YOU CAN", 54, TextAnchor.MiddleCenter, true,
                UITheme.FontRole.Title);
            UITheme.StyleTitle(title);
            SetPreferredHeight(title.gameObject, 66f);

            BuildAccentRule(column.transform);

            var subtitle = RuntimeUIFactory.CreateText(column.transform, "Subtitle",
                "INVESTIGATION BOARD", 22, TextAnchor.MiddleCenter, true,
                UITheme.FontRole.Header);
            UITheme.SetTextColor(subtitle, UITheme.TextMuted);
            SetPreferredHeight(subtitle.gameObject, 30f);

            SetPreferredHeight(Spacer(column.transform, "TitleGap").gameObject, 26f);

            AddEntry(column.transform, "SINGLEPLAYER", StartSinglePlayer, true);
            _multiplayerButton = AddEntry(column.transform, "MULTIPLAYER", StartMultiplayer, false);
            AddEntry(column.transform, "SETTINGS", OpenSettings, false);

            SetPreferredHeight(Spacer(column.transform, "StatusGap").gameObject, 12f);

            _statusText = RuntimeUIFactory.CreateText(column.transform, "Status",
                string.Empty, 17, TextAnchor.UpperCenter, false, UITheme.FontRole.Body);
            UITheme.SetTextColor(_statusText, UITheme.TextMuted);
            SetPreferredHeight(_statusText.gameObject, 56f);
        }

        /// <summary>
        /// The one piece of brand colour on this screen: a 110 x 2 line under the title.
        /// </summary>
        private static void BuildAccentRule(Transform parent)
        {
            var rule = RuntimeUIFactory.CreatePanel(parent, "AccentRule", false);
            rule.GetComponent<Image>().color = UITheme.Secondary;

            // flexibleWidth 0 is what keeps this a short mark rather than a bar across the
            // screen: the column expands its children to full width, and a child that asks for
            // no flexible width keeps its preferred one instead.
            var element = rule.AddComponent<LayoutElement>();
            element.preferredHeight = 2f;
            element.minHeight = 2f;
            element.preferredWidth = 110f;
            element.flexibleWidth = 0f;
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
            // Checked BEFORE the launch, not after. Installing a session and then discovering
            // there is nowhere to send the player leaves a live session behind a closed panel,
            // with the controls handed back and nothing on screen to say what happened.
            if (!CanReach(UIScreen.MissionSelect, out string why))
            {
                SetStatus(why, UITheme.Warning);
                return;
            }

            LaunchResult result = SessionLauncher.BeginOfflineSolo();

            if (!result.Started)
            {
                SetStatus("Could not start a single player session: " + result.Detail, UITheme.Warning);
                return;
            }

            // Shown BEFORE this panel closes. Mission select takes the input gate in its own
            // OnEnable, so with this order the gate is never empty between the two screens and
            // the touch HUD cannot flash back for a frame underneath the menu.
            UIManager.Instance.Show(UIScreen.MissionSelect, false);

            Close();
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

            // Same order as offline, and it matters more here: an online session that is
            // installed and then stranded is a LIVE session with no way forward, and the next
            // press of the board is refused with SessionAlreadyLive.
            if (!CanReach(UIScreen.MissionSelect, out string why))
            {
                SetStatus(why, UITheme.Warning);
                return;
            }

            LaunchResult result = SessionLauncher.BeginOnline(SessionChoice.OnlineHost, null);

            if (!result.Started)
            {
                SetStatus("Online session could not be started: " + result.Detail, UITheme.Warning);
                return;
            }

            UIManager.Instance.Show(UIScreen.MissionSelect, false);

            Close();
        }

        /// <summary>
        /// The existing settings screen, shown over this one rather than instead of it, so the
        /// board is still here when settings closes itself. There is one settings system and
        /// this is not a second one.
        /// </summary>
        private void OpenSettings()
        {
            if (!CanReach(UIScreen.Settings, out string why))
            {
                SetStatus(why, UITheme.Warning);
                return;
            }

            UIManager.Instance.Show(UIScreen.Settings, false);
        }

        /// <summary>
        /// Whether a screen this panel wants to hand the player to actually exists here.
        ///
        /// <para>
        /// <see cref="UIManager.Show"/> on an unregistered screen is silent: it sets the
        /// current screen and activates nothing, so the caller believes it succeeded and the
        /// player is left looking at the room. Every destination goes through this, and it is
        /// asked before anything irreversible happens.
        /// </para>
        /// </summary>
        private static bool CanReach(UIScreen screen, out string why)
        {
            if (UIManager.Instance == null)
            {
                why = "The interface is not available in this scene.";
                return false;
            }

            if (!UIManager.Instance.TryGetScreen(screen, out GameObject root) || root == null)
            {
                why = screen + " is not registered in this scene.";
                return false;
            }

            why = null;
            return true;
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

            if (Time.frameCount == _openedFrame)
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
                // Read back and written back, so the fade duration UITheme set to zero is
                // preserved. Assigning a fresh ColorBlock here would silently reintroduce
                // Unity's 0.1 s tint fade on this one button.
                var colors = _multiplayerButton.colors;
                colors.normalColor = online ? UITheme.BackgroundPanel : UITheme.BackgroundDark;
                colors.selectedColor = colors.normalColor;
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
