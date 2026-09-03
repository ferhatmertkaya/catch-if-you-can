using CatchIfYouCan.UI;
using UnityEngine;

namespace CatchIfYouCan.Core.SceneSetup
{
    /// <summary>
    /// The cinematic main menu, and nothing else.
    ///
    /// Everything this holds is menu-only by construction. Once the lobby is its own scene
    /// there is no reference here that reaches into it, which is the whole point: the two
    /// halves could not be separated while one component held ten references across the
    /// boundary.
    /// </summary>
    [AddComponentMenu("Catch If You Can/Scene Setup/Main Menu Scene Installer")]
    public sealed class MainMenuSceneInstaller : SceneInstallerBase
    {
        [Header("Menu")]
        [Tooltip("Owns the hand-off to the lobby. Left empty the menu still renders; the " +
                 "tap simply does nothing, which is worth a warning rather than a crash.")]
        [SerializeField] private MainMenuModeController modeController;

        public override void Install()
        {
            fallbackLightIntensity = 0.15f;
            InstallSceneBasics();

            ShowScreenIfWeBuiltTheUi(UIScreen.MainMenu, true);

            if (modeController == null)
                modeController = Object.FindAnyObjectByType<MainMenuModeController>();

            if (modeController == null)
            {
                CIYCLog.Warn("No MainMenuModeController in the menu scene. The menu will " +
                             "render, but tapping it cannot start the lobby.");
            }
        }
    }
}
