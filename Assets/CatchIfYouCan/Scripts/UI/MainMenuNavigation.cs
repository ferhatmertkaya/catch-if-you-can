using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CatchIfYouCan.UI
{
    /// <summary>
    /// Behaviour for the cinematic menu's three choices: PLAY, SETTINGS, CREDITS.
    ///
    /// <para>
    /// <b>This component authors NOTHING.</b> Every button, label, brush stroke and background it
    /// works with is a real GameObject saved in <c>01_MainMenu.unity</c>, put there by
    /// <c>Catch If You Can &gt; 1. LOBBY &gt; Hauptmenue in die Szene schreiben</c>. What this class
    /// does is decide which row is selected and what happens when one is activated. It never
    /// writes a label's text, its font, its font size, its RectTransform or its scale, so
    /// anything set by hand in the Inspector survives Play Mode untouched. The scene is the
    /// design; this file is the behaviour.
    /// </para>
    ///
    /// <para>
    /// The one visual property it DOES write is the caption COLOUR, because that is genuinely
    /// runtime state: the selected row is dark text on the white brush, the others are light text
    /// on the corridor. Both colours are serialized fields, so they are still yours to change.
    /// </para>
    ///
    /// <para>
    /// <b>PLAY does not load a scene.</b> It calls <see cref="MainMenuModeController.EnterLobby"/>,
    /// the route the menu already used through TAP ANYWHERE TO START. There is a second, older
    /// path in the project - <c>MainMenuController.OnPlay</c> reaching for
    /// <c>SceneLoader.LoadInvestigation()</c> - and it is NOT used here: that component is not in
    /// this scene at all, and neither is the <c>UIManager</c> it talks to. Wiring PLAY to it would
    /// be a second implementation of a flow that already works, which is this project's first
    /// recorded mistake.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Catch If You Can/Main Menu Navigation")]
    public sealed class MainMenuNavigation : MonoBehaviour
    {
        /// <summary>
        /// One authored row. Every field points at an object that already exists in the scene;
        /// none of them is created here.
        /// </summary>
        [Serializable]
        public sealed class Row
        {
            [Tooltip("The authored Button. Mouse, touch and Submit all arrive through this.")]
            public Button button;

            [Tooltip("The authored caption. Its TEXT, FONT and SIZE are yours - this component " +
                     "only ever changes its colour, and only to show selection.")]
            public TMP_Text label;

            [Tooltip("The authored white brush stroke behind this row. Switched on for the " +
                     "selected row and off for the others. Never created, never moved, never " +
                     "resized - position and size are yours in the Inspector.")]
            public GameObject selectionBrush;
        }

        [Header("Flow")]
        [Tooltip("The controller that performs the handover into the lobby. PLAY calls its " +
                 "EnterLobby(), the same route TAP ANYWHERE TO START used.")]
        [SerializeField] private MainMenuModeController modeController;

        [Header("Authored rows")]
        [Tooltip("PLAY, SETTINGS, CREDITS, in the order they appear on screen. The ORDER IS THE " +
                 "MEANING: this component activates by index, so swapping two entries here is a " +
                 "PLAY that opens the credits.")]
        [SerializeField] private Row[] rows = new Row[0];

        [Header("Panels")]
        [Tooltip("Opened by SETTINGS. Authored in the scene and inactive; Escape closes it.")]
        [SerializeField] private GameObject settingsPanel;

        [Tooltip("Opened by CREDITS.")]
        [SerializeField] private GameObject creditsPanel;

        [Header("Selection colours")]
        [Tooltip("The selected caption. Dark, because it is read against the white brush stroke.")]
        [SerializeField] private Color selectedColor = new Color(0.055f, 0.055f, 0.067f, 1f);

        [Tooltip("The captions that are not selected. Off-white against the dark corridor.")]
        [SerializeField] private Color idleColor = new Color(0.78f, 0.78f, 0.80f, 1f);

        [Header("Feel")]
        [Tooltip("Ignore input for this long after the menu appears, so a stray touch left over " +
                 "from the startup intro cannot press PLAY on its own.")]
        [SerializeField, Min(0f)] private float inputArmDelay = 0.5f;

        private int _selected;
        private float _armedAt;
        private bool _handedOver;

        /// <summary>Which row is highlighted. PLAY is 0.</summary>
        public int SelectedIndex => _selected;

        /// <summary>How many authored rows this component found. Read by the scene verifier.</summary>
        public int RowCount => rows != null ? rows.Length : 0;

        private void Awake()
        {
            if (modeController == null)
                modeController = GetComponentInParent<MainMenuModeController>();

            if (modeController == null)
                modeController = FindAnyObjectByType<MainMenuModeController>();

            WireButtons();
        }

        /// <summary>
        /// Attaches the click handlers to the AUTHORED buttons.
        ///
        /// <para>
        /// A listener is the one part of this that cannot be authored: a lambda is not
        /// serialised, and a persistent listener added from an editor tool needs an API this
        /// project cannot verify offline. So the buttons are yours and the wiring is code's.
        /// Cleared before adding, because this runs again whenever the menu is re-armed and a
        /// second listener on the same button is one click that activates the row twice.
        /// </para>
        /// </summary>
        private void WireButtons()
        {
            if (rows == null)
                return;

            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i] == null || rows[i].button == null)
                    continue;

                int index = i;
                rows[i].button.onClick.RemoveAllListeners();
                rows[i].button.onClick.AddListener(() => Activate(index));

                var hover = rows[i].button.GetComponent<MainMenuNavHover>();
                if (hover != null)
                    hover.Bind(this, index);
            }

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

        /// <summary>
        /// Moves the highlight. Called by the keyboard, and by a row under the pointer.
        ///
        /// <para>
        /// All it changes is which brush stroke is active and what colour each caption is. It
        /// does not move, scale or resize anything: an earlier version slid the selected caption
        /// sideways and grew it, which meant writing <c>anchoredPosition</c> and
        /// <c>localScale</c> on objects whose layout is meant to be authored by hand.
        /// </para>
        /// </summary>
        public void Select(int index, bool instant)
        {
            if (rows == null || rows.Length == 0)
                return;

            _selected = Wrap(index);

            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i] == null)
                    continue;

                bool on = i == _selected;

                if (rows[i].selectionBrush != null && rows[i].selectionBrush.activeSelf != on)
                    rows[i].selectionBrush.SetActive(on);

                if (rows[i].label != null)
                    rows[i].label.color = on ? selectedColor : idleColor;
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
            }
        }

        private void EnterLobby()
        {
            if (modeController == null)
            {
                Core.CIYCLog.Error("[CIYC][MENU] PLAY pressed with no MainMenuModeController. " +
                                   "Nothing can hand the player over.");
                return;
            }

            if (!modeController.CanEnterLobby)
                return;

            _handedOver = true;
            modeController.EnterLobby();
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
        /// Hands this component its authored rows. Called by the editor tool that writes them
        /// into the scene, so the wiring is saved rather than rebuilt on every load.
        /// </summary>
        public void Bind(MainMenuModeController controller, Row[] authored,
                         GameObject settings, GameObject credits)
        {
            modeController = controller;
            rows = authored ?? new Row[0];
            settingsPanel = settings;
            creditsPanel = credits;
        }
    }
}
