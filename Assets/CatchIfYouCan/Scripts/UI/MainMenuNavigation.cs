using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CatchIfYouCan.UI
{
    /// <summary>
    /// The cinematic menu's three choices: PLAY, SETTINGS, CREDITS.
    ///
    /// <para>
    /// <b>PLAY does not load a scene.</b> It calls <see cref="MainMenuModeController.EnterLobby"/>,
    /// which is the route the menu already used through TAP ANYWHERE TO START. There is a second,
    /// older path in the project - <c>MainMenuController.OnPlay</c> reaching for
    /// <c>SceneLoader.LoadInvestigation()</c> - and it is NOT used here: that component is not in
    /// this scene at all, and neither is the <c>UIManager</c> it talks to. Wiring PLAY to it would
    /// have been a second implementation of a flow that already works, which is this project's
    /// first recorded mistake.
    /// </para>
    ///
    /// <para>
    /// <b>Selection is one index.</b> The white brush stroke is a single Image that MOVES to the
    /// selected row rather than one stroke per button switched on and off: three strokes would be
    /// three things that can disagree about which row is selected, and the disagreement would be a
    /// menu with two highlights.
    /// </para>
    ///
    /// <para>
    /// <b>It does not read a key of its own for anything gameplay owns.</b> The arrows, W/S and
    /// Submit are read here because this screen has no other input path - the gameplay router and
    /// <c>MobileInputController</c> are not running in the cinematic menu. Pointer and touch go
    /// through the normal UI event system, which needs a GraphicRaycaster on the canvas and an
    /// EventSystem in the scene; the baker adds the first and this component ensures the second
    /// through <see cref="EventSystemUtil"/>, the one place that decision is made.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Catch If You Can/Main Menu Navigation")]
    public sealed class MainMenuNavigation : MonoBehaviour
    {
        /// <summary>One row: the button, its label, and the rect the brush lines up with.</summary>
        [Serializable]
        public sealed class Row
        {
            [Tooltip("The clickable control. Mouse and touch arrive through this.")]
            public Button button;

            [Tooltip("The caption. Held as a Component because the project builds either a " +
                     "TextMeshPro label or a legacy Text, decided in one place by " +
                     "RuntimeUIFactory - this must not care which it got.")]
            public Component label;

            [Tooltip("What the brush stroke lines up with when this row is selected.")]
            public RectTransform rect;
        }

        [Header("Flow")]
        [Tooltip("The controller that performs the handover into the lobby. PLAY calls its " +
                 "EnterLobby(), which is the same route TAP ANYWHERE TO START used.")]
        [SerializeField] private MainMenuModeController modeController;

        [Header("Rows")]
        [Tooltip("PLAY, SETTINGS, CREDITS, in the order they appear.")]
        [SerializeField] private Row[] rows = new Row[0];

        [Tooltip("The white brush stroke behind the selected row. ONE image that moves, not one " +
                 "per row.")]
        [SerializeField] private RectTransform selectionBrush;

        [Header("Panels")]
        [Tooltip("Opened by SETTINGS. A shell for now; the settings system itself is not built " +
                 "here.")]
        [SerializeField] private GameObject settingsPanel;

        [Tooltip("Opened by CREDITS.")]
        [SerializeField] private GameObject creditsPanel;

        [Header("Feel")]
        [Tooltip("How far the selected caption slides to the right, in reference pixels.")]
        [SerializeField, Range(0f, 24f)] private float selectedNudge = 6f;

        [Tooltip("How much larger the selected caption is drawn.")]
        [SerializeField, Range(1f, 1.4f)] private float selectedScale = 1.12f;

        [Tooltip("Seconds the brush takes to reach a newly selected row, and the caption to " +
                 "settle. Short on purpose: a menu that answers late reads as a menu that did " +
                 "not hear you.")]
        [SerializeField, Range(0f, 0.4f)] private float settleTime = 0.12f;

        [Tooltip("Ignore input for this long after the menu appears, so a stray touch left over " +
                 "from the startup intro cannot press PLAY on its own.")]
        [SerializeField, Min(0f)] private float inputArmDelay = 0.5f;

        private int _selected;
        private float _armedAt;
        private bool _handedOver;

        private Vector2 _brushFrom, _brushTo;
        private float _brushT = 1f;
        private Vector2[] _labelHome;

        /// <summary>Which row is highlighted. PLAY is 0.</summary>
        public int SelectedIndex => _selected;

        private void Awake()
        {
            Wire();
        }

        /// <summary>
        /// Binds every listener this component needs from its current fields.
        ///
        /// <para>
        /// <b>Called from Awake AND from <see cref="Bind"/>, and that is the whole point.</b>
        /// When <see cref="MainMenuScreenBuilder"/> adds this component at runtime, Awake runs
        /// INSIDE that <c>AddComponent</c> - one line before the builder can hand over the rows -
        /// so Awake sees an empty array and wires nothing. That is CLAUDE.md mistake 25 and 27,
        /// the same trap twice, and the symptom here is the expensive one: a menu that draws
        /// perfectly and answers nothing. Binding therefore re-runs this rather than only writing
        /// the fields.
        /// </para>
        /// <para>
        /// Listeners are CLEARED first. This runs at least twice on the runtime path, and a
        /// second <c>AddListener</c> on the same button is a single click that activates the row
        /// twice. Clearing is safe because this component builds and owns these buttons - nothing
        /// else adds a listener to them.
        /// </para>
        /// </summary>
        private void Wire()
        {
            if (modeController == null)
                modeController = GetComponentInParent<MainMenuModeController>();

            if (modeController == null)
                modeController = FindAnyObjectByType<MainMenuModeController>();

            // Captured before anything moves them, so the nudge is measured from where the
            // builder put each caption rather than from wherever the last selection left it.
            _labelHome = new Vector2[rows.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                RectTransform lr = LabelRect(i);
                _labelHome[i] = lr != null ? lr.anchoredPosition : Vector2.zero;
            }

            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i] == null || rows[i].button == null)
                    continue;

                int index = i;
                rows[i].button.onClick.RemoveAllListeners();
                rows[i].button.onClick.AddListener(() => Activate(index));
            }

            // Every button inside a panel closes it. Wired here rather than when the panel is
            // built: a listener added in the editor is either a lambda, which Unity does not
            // serialise, or a persistent listener through an API this project cannot verify
            // offline. This runs.
            WirePanelExit(settingsPanel);
            WirePanelExit(creditsPanel);
        }

        private void WirePanelExit(GameObject panel)
        {
            if (panel == null)
                return;

            var buttons = panel.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null)
                    continue;

                buttons[i].onClick.RemoveAllListeners();
                buttons[i].onClick.AddListener(CloseOpenPanel);
            }
        }

        /// <summary>Closes whichever panel is open. Public so a back button can reach it.</summary>
        public void CloseOpenPanel()
        {
            ClosePanels();
        }

        private void OnEnable()
        {
            // Without one, every button on this canvas is visible and dead - and a menu that
            // draws correctly and answers nothing is this project's most expensive shape.
            EventSystemUtil.EnsureEventSystem();

            _armedAt = Time.unscaledTime + inputArmDelay;
            _handedOver = false;

            ClosePanels();
            Select(0, instant: true);
        }

        private void Update()
        {
            if (_handedOver)
                return;

            AnimateSelection();

            // The intro owns the screen first; input during it is not a menu press.
            if (StartupIntroVideo.IsIntroPlaying)
            {
                _armedAt = Time.unscaledTime + inputArmDelay;
                return;
            }

            if (Time.unscaledTime < _armedAt)
                return;

            if (PanelOpen())
            {
                if (CancelPressed())
                    ClosePanels();
                return;
            }

            int step = StepPressed();
            if (step != 0)
                Select(Wrap(_selected + step), instant: false);

            if (SubmitPressed())
                Activate(_selected);
        }

        /// <summary>Moves the highlight. Called by the keyboard, and by a row under the pointer.</summary>
        public void Select(int index, bool instant)
        {
            if (rows.Length == 0)
                return;

            _selected = Wrap(index);

            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i] == null || rows[i].label == null)
                    continue;

                bool on = i == _selected;

                // Black on the white stroke, grey off it. Through UITheme rather than by hand:
                // it already knows whether this label is TextMeshPro or a legacy Text, and
                // deciding that a second time here is how a project ends up with two answers.
                UITheme.SetTextColor(rows[i].label,
                    on ? UITheme.BackgroundDark : UITheme.TextMuted);

                RectTransform lr = LabelRect(i);
                if (lr == null)
                    continue;

                Vector2 home = i < _labelHome.Length ? _labelHome[i] : lr.anchoredPosition;
                Vector2 want = on ? home + new Vector2(selectedNudge, 0f) : home;
                float scale = on ? selectedScale : 1f;

                if (instant)
                {
                    lr.anchoredPosition = want;
                    lr.localScale = Vector3.one * scale;
                }
            }

            if (selectionBrush == null)
                return;

            RectTransform target = rows[_selected] != null ? rows[_selected].rect : null;
            if (target == null)
                return;

            _brushFrom = selectionBrush.anchoredPosition;
            _brushTo = target.anchoredPosition;
            _brushT = instant ? 1f : 0f;

            if (instant)
                selectionBrush.anchoredPosition = _brushTo;
        }

        /// <summary>
        /// Eases the brush and the captions towards the current selection.
        ///
        /// <para>
        /// Unscaled time throughout: the cinematic menu can sit over a paused timescale, and a
        /// highlight that stops moving there reads as a frozen menu.
        /// </para>
        /// </summary>
        private void AnimateSelection()
        {
            float rate = settleTime <= 0.0001f
                ? 1f
                : Mathf.Clamp01(Time.unscaledDeltaTime / settleTime);

            if (selectionBrush != null && _brushT < 1f)
            {
                _brushT = Mathf.Clamp01(_brushT + rate);
                float e = _brushT * _brushT * (3f - 2f * _brushT);
                selectionBrush.anchoredPosition = Vector2.Lerp(_brushFrom, _brushTo, e);
            }

            for (int i = 0; i < rows.Length; i++)
            {
                RectTransform lr = LabelRect(i);
                if (lr == null)
                    continue;

                bool on = i == _selected;
                Vector2 home = i < _labelHome.Length ? _labelHome[i] : lr.anchoredPosition;
                Vector2 want = on ? home + new Vector2(selectedNudge, 0f) : home;
                float scale = on ? selectedScale : 1f;

                lr.anchoredPosition = Vector2.Lerp(lr.anchoredPosition, want, rate);
                lr.localScale = Vector3.Lerp(lr.localScale, Vector3.one * scale, rate);
            }
        }

        private void Activate(int index)
        {
            if (_handedOver || Time.unscaledTime < _armedAt)
                return;

            Select(index, instant: true);

            switch (index)
            {
                case 0:
                    EnterLobby();
                    break;
                case 1:
                    OpenPanel(settingsPanel);
                    break;
                case 2:
                    OpenPanel(creditsPanel);
                    break;
                case 3:
                    QuitGame();
                    break;
            }
        }

        /// <summary>
        /// The handover, through the controller that owns the once-only guard.
        ///
        /// <para>
        /// Latched here as well, so a second click during the fade does no further work at all -
        /// the same belt and braces the tap label used.
        /// </para>
        /// </summary>
        private void EnterLobby()
        {
            if (modeController == null)
            {
                Core.CIYCLog.Error("[CIYC][MENU] PLAY has no MainMenuModeController, so there is " +
                                   "nothing to hand over to. The button is wired and the flow " +
                                   "behind it is missing.");
                return;
            }

            if (!modeController.CanEnterLobby)
                return;

            _handedOver = true;
            modeController.EnterLobby();
        }

        /// <summary>
        /// Leaves the game.
        ///
        /// <para>
        /// <c>Application.Quit</c> does nothing in the editor, so the editor branch says so out
        /// loud rather than looking like a dead button - "I pressed QUIT and nothing happened" is
        /// a bug report this project would otherwise have earned honestly.
        /// </para>
        /// <para>
        /// Worth knowing before this ships: iOS's guidelines discourage an in-app quit, and a
        /// button that terminates the app has been a review rejection there. It is here because
        /// the design reference has it; whether it survives on mobile is a design call, not this
        /// component's.
        /// </para>
        /// </summary>
        private void QuitGame()
        {
            _handedOver = true;
#if UNITY_EDITOR
            Core.CIYCLog.Info("[CIYC][MENU] QUIT pressed. Application.Quit does nothing in the " +
                              "editor; in a build this closes the game.");
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OpenPanel(GameObject panel)
        {
            ClosePanels();

            if (panel == null)
                return;

            panel.SetActive(true);
        }

        private void ClosePanels()
        {
            if (settingsPanel != null)
                settingsPanel.SetActive(false);
            if (creditsPanel != null)
                creditsPanel.SetActive(false);
        }

        private bool PanelOpen()
        {
            return (settingsPanel != null && settingsPanel.activeSelf) ||
                   (creditsPanel != null && creditsPanel.activeSelf);
        }

        private RectTransform LabelRect(int index)
        {
            if (index < 0 || index >= rows.Length || rows[index] == null || rows[index].label == null)
                return null;

            return rows[index].label.transform as RectTransform;
        }

        private int Wrap(int index)
        {
            if (rows.Length == 0)
                return 0;

            int n = rows.Length;
            return ((index % n) + n) % n;
        }

        // Fully qualified on purpose: inside this namespace a bare "Input" binds to the
        // project's own CatchIfYouCan.Input namespace, not to UnityEngine.Input.
        private static int StepPressed()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.DownArrow) ||
                UnityEngine.Input.GetKeyDown(KeyCode.S))
                return 1;

            if (UnityEngine.Input.GetKeyDown(KeyCode.UpArrow) ||
                UnityEngine.Input.GetKeyDown(KeyCode.W))
                return -1;

            return 0;
        }

        private static bool SubmitPressed()
        {
            return UnityEngine.Input.GetKeyDown(KeyCode.Return) ||
                   UnityEngine.Input.GetKeyDown(KeyCode.KeypadEnter) ||
                   UnityEngine.Input.GetKeyDown(KeyCode.Space);
        }

        private static bool CancelPressed()
        {
            return UnityEngine.Input.GetKeyDown(KeyCode.Escape) ||
                   UnityEngine.Input.GetKeyDown(KeyCode.Backspace);
        }

        /// <summary>
        /// Hands this component its rows, so <see cref="MainMenuScreenBuilder"/> never reaches
        /// into a private field.
        ///
        /// <para>
        /// It does not only WRITE the fields - it re-runs the wiring and re-arms the screen.
        /// Awake has already been and gone by the time this is called on the runtime path (it
        /// runs inside <c>AddComponent</c>), and <c>OnEnable</c>'s <c>Select(0)</c> returned at
        /// its first line because there were no rows yet. Setting the fields alone would leave
        /// every button unlistened and no row highlighted.
        /// </para>
        /// </summary>
        public void Bind(MainMenuModeController controller, Row[] built,
                         RectTransform brush, GameObject settings, GameObject credits)
        {
            modeController = controller;
            rows = built ?? new Row[0];
            selectionBrush = brush;
            settingsPanel = settings;
            creditsPanel = credits;

            Wire();

            _armedAt = Time.unscaledTime + inputArmDelay;
            _handedOver = false;
            ClosePanels();
            Select(0, instant: true);
        }
    }
}
