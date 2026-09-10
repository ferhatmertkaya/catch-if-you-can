using UnityEngine;

namespace CatchIfYouCan.Core.SceneSetup
{
    /// <summary>Boot: raise the services, then hand off to the menu.</summary>
    [AddComponentMenu("Catch If You Can/Scene Setup/Boot Scene Installer")]
    public sealed class BootSceneInstaller : SceneInstallerBase
    {
        [Tooltip("The object that starts the intro and loads the menu. Created if absent.")]
        [SerializeField] private Bootstrap bootstrap;

        public override void Install()
        {
            InstallSceneBasics();

            // The old splash text used to be built here. It is replaced by StartupIntroVideo,
            // which Bootstrap raises itself so the screen is black before anything else runs.
            if (bootstrap == null)
                bootstrap = Object.FindAnyObjectByType<Bootstrap>();

            if (bootstrap == null)
                bootstrap = FindOrCreate("BOOTSTRAP").AddComponent<Bootstrap>();
        }
    }
}
