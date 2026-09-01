using UnityEngine;

namespace CatchIfYouCan.UI
{
    /// <summary>
    /// Watches for the tap or click that ends the cinematic menu.
    ///
    /// <para>
    /// The TAP TO START label in the scene is a plain Text with no button behind it, so the
    /// input is read here rather than through the UI event system — a full-screen invisible
    /// button would sit over the menu and swallow anything else the canvas might want later.
    /// </para>
    ///
    /// <para>
    /// This component decides nothing about the handover. It asks
    /// <see cref="MainMenuModeController"/>, which owns the once-only guard, so holding the
    /// screen or tapping repeatedly during the fade cannot start a second transition.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Catch If You Can/Main Menu Tap To Start")]
    public sealed class MainMenuTapToStart : MonoBehaviour
    {
        [Tooltip("The controller that performs the handover. Falls back to one on this object.")]
        [SerializeField] private MainMenuModeController modeController;

        [Tooltip("Ignore taps for this long after the menu appears, so a stray touch left over " +
                 "from the startup intro cannot skip straight past the menu.")]
        [SerializeField, Min(0f)] private float inputArmDelay = 0.5f;

        private float _armedAt;
        private bool _consumed;

        private void Awake()
        {
            if (modeController == null)
                modeController = GetComponent<MainMenuModeController>();
        }

        private void OnEnable()
        {
            _armedAt = Time.unscaledTime + inputArmDelay;
        }

        private void Update()
        {
            if (_consumed || modeController == null || !modeController.CanEnterInteractiveRoom)
                return;

            // The intro owns the screen first; a tap during it is not a menu tap.
            if (StartupIntroVideo.IsIntroPlaying)
            {
                _armedAt = Time.unscaledTime + inputArmDelay;
                return;
            }

            if (Time.unscaledTime < _armedAt)
                return;

            if (!TapDetected())
                return;

            // Latched here as well as in the controller: this stops Update doing any further
            // work at all once the handover is under way.
            _consumed = true;
            modeController.EnterInteractiveRoom();
        }

        private static bool TapDetected()
        {
            // Fully qualified on purpose: inside this namespace a bare "Input" binds to the
            // project's own CatchIfYouCan.Input namespace, not to UnityEngine.Input.
            if (UnityEngine.Input.GetMouseButtonDown(0))
                return true;

            if (UnityEngine.Input.touchCount > 0 &&
                UnityEngine.Input.GetTouch(0).phase == TouchPhase.Began)
                return true;

            // Submit covers a gamepad or keyboard without adding a second control scheme.
            return UnityEngine.Input.GetKeyDown(KeyCode.Space) ||
                   UnityEngine.Input.GetKeyDown(KeyCode.Return) ||
                   UnityEngine.Input.GetKeyDown(KeyCode.KeypadEnter);
        }
    }
}
