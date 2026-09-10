using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatchIfYouCan.Core.SceneSetup
{
    /// <summary>
    /// The one thing every scene runs on entry: make sure the persistent services exist,
    /// then let the scene install itself.
    ///
    /// <para>
    /// This replaces a static switch on the scene's name. That switch was the reason a
    /// scene it had not heard of received no camera, no event system, no runtime UI and no
    /// managers - and received them silently, because none of those absences throw. Adding
    /// a scene meant remembering to add a case to a file in another folder.
    /// </para>
    ///
    /// <para>
    /// A scene declares what it needs by carrying this component and an installer. Until a
    /// scene has been authored that way, <see cref="EnsureForActiveScene"/> builds the pair
    /// for it and says so. That fallback is a migration aid with a finite life, not a second
    /// way of setting scenes up: it creates exactly the same installer the scene should
    /// carry, and everything it then does runs through that installer.
    /// </para>
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    [AddComponentMenu("Catch If You Can/Scene Setup/Scene Bootstrapper")]
    public sealed class SceneBootstrapper : MonoBehaviour
    {
        [Tooltip("This scene's installer. Falls back to one on the same object.")]
        [SerializeField] private SceneInstallerBase installer;

        private bool _installed;

        private void Awake()
        {
            // Before anything else in the scene, so a component that asks for a service in
            // its own Awake finds one. Execution order -1000 is what buys that.
            CiycServices.EnsureCore();
        }

        private void Start() => Run();

        /// <summary>Installs the scene. Safe to call twice; the second call does nothing.</summary>
        public void Run()
        {
            if (_installed)
                return;

            _installed = true;

            if (installer == null)
                installer = GetComponent<SceneInstallerBase>();

            if (installer == null)
            {
                CIYCLog.Error("SceneBootstrapper on '" + name + "' has no installer, so scene '" +
                              gameObject.scene.name + "' will not set itself up. Add the " +
                              "installer component for this scene to the same object.");
                return;
            }

            installer.Install();
        }

        /// <summary>
        /// Builds the bootstrapper for a scene that does not carry one yet.
        ///
        /// Runs after every scene object's Awake and before the first Start, which is where
        /// the old per-scene switch ran, so the ordering scenes were authored against is
        /// unchanged.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureForActiveScene()
        {
            var existing = Object.FindAnyObjectByType<SceneBootstrapper>();
            if (existing != null)
            {
                existing.Run();
                return;
            }

            var scene = SceneManager.GetActiveScene();
            var installerType = DefaultInstallerFor(scene.name);
            if (installerType == null)
            {
                // A scene with no bootstrapper and no known identity is either a test scene
                // or a mistake. Saying nothing is what made the second case hard to find.
                CIYCLog.Warn("Scene '" + scene.name + "' has no SceneBootstrapper and is not a " +
                             "known production scene, so nothing set it up. Add a " +
                             "SceneBootstrapper and an installer if it is meant to run.");
                return;
            }

            CIYCLog.Warn("Scene '" + scene.name + "' has no SceneBootstrapper, so one was " +
                         "created with a " + installerType.Name + ". Add both to the scene " +
                         "and assign their references, so the scene declares what it needs " +
                         "instead of being guessed at from its name.");

            var go = new GameObject("SCENE_BOOTSTRAP");
            var installer = (SceneInstallerBase)go.AddComponent(installerType);
            var bootstrapper = go.AddComponent<SceneBootstrapper>();
            bootstrapper.installer = installer;
            bootstrapper.Run();
        }

        /// <summary>
        /// Which installer a production scene would carry. Used only to build the fallback
        /// above - nothing in the running game branches on a scene name.
        /// </summary>
        private static System.Type DefaultInstallerFor(string sceneName)
        {
            if (!CiycScenes.TryParse(sceneName, out var scene))
                return null;

            switch (scene)
            {
                case CiycScene.Boot: return typeof(BootSceneInstaller);
                case CiycScene.MainMenu: return typeof(MainMenuSceneInstaller);
                case CiycScene.Lobby: return typeof(LobbySceneInstaller);
                case CiycScene.Training: return typeof(TrainingSceneInstaller);
                case CiycScene.Investigation: return typeof(InvestigationSceneInstaller);
                default: return null;
            }
        }
    }
}
