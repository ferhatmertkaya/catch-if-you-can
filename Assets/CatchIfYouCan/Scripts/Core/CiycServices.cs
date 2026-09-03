using CatchIfYouCan.Audio;
using CatchIfYouCan.Equipment;
using CatchIfYouCan.Evidence;
using CatchIfYouCan.Ghost;
using CatchIfYouCan.Graphics;
using CatchIfYouCan.Missions;
using CatchIfYouCan.Objectives;
using CatchIfYouCan.Save;
using CatchIfYouCan.UI;
using CatchIfYouCan.Weather;
using UnityEngine;

namespace CatchIfYouCan.Core
{
    /// <summary>
    /// The single owner of the persistent service layer.
    ///
    /// <para>
    /// There used to be two of these. <c>Bootstrap.EnsureManagers</c> created eleven
    /// managers for the boot scene; <c>InvestigationBootstrap.EnsureManagers</c> created a
    /// different ten for the mission scene, and only two names appeared in both. Neither
    /// list was wrong, but neither was complete either, so which services existed depended
    /// on which scene you had entered through - and a scene entered directly in the editor
    /// got whichever list happened to run, or none.
    /// </para>
    ///
    /// <para>
    /// Every service here is <c>DontDestroyOnLoad</c> and guards its own singleton, so
    /// calling this twice is a no-op rather than a duplicate. That is what lets the boot
    /// flow and a directly opened scene share one implementation instead of racing two.
    /// </para>
    ///
    /// <para>
    /// The split between <see cref="EnsureCore"/> and <see cref="EnsureMission"/> is about
    /// meaning, not lifetime: both sets survive a scene load. Core is what any scene needs
    /// to run at all. Mission is what only makes sense once an investigation exists, and
    /// creating it in the main menu would leave an empty MissionManager claiming to own a
    /// mission that was never started.
    /// </para>
    /// </summary>
    public static class CiycServices
    {
        /// <summary>
        /// Services every scene needs. Idempotent.
        ///
        /// Order matters in one place only: AudioBootstrap must run before the AudioManager
        /// check, because it creates the manager itself and subscribes the game events. The
        /// check after it is the belt to that braces.
        /// </summary>
        public static void EnsureCore()
        {
            if (GameManager.Instance == null)
                Create<GameManager>("GameManager");

            if (SceneLoader.Instance == null)
                Create<SceneLoader>("SceneLoader");

            if (SaveManager.Instance == null)
                Create<SaveManager>("SaveManager");

            if (SettingsManager.Instance == null)
                Create<SettingsManager>("SettingsManager");

            AudioBootstrap.Initialize();
            if (AudioManager.Instance == null)
                Create<AudioManager>("AudioManager");

            if (GraphicsManager.Instance == null)
                Create<GraphicsManager>("GraphicsManager");

            if (HapticManager.Instance == null)
                Create<HapticManager>("HapticManager");

            if (EquipmentManager.Instance == null)
                Create<EquipmentManager>("EquipmentManager");

            if (StatisticsTracker.Instance == null)
                Create<StatisticsTracker>("StatisticsTracker");

            if (UIManager.Instance == null)
                Create<UIManager>("UIManager");

            EnsureRuntimeUi();
        }

        /// <summary>
        /// Services that only mean anything inside an investigation. Idempotent.
        ///
        /// Deliberately not part of <see cref="EnsureCore"/>: the main menu and the lobby
        /// have no mission, and a MissionManager sitting there with a null ActiveMission is
        /// a state the rest of the code does not expect to see.
        /// </summary>
        public static void EnsureMission()
        {
            if (MissionManager.Instance == null)
                Create<MissionManager>("MissionManager");

            if (ObjectiveManager.Instance == null)
                Create<ObjectiveManager>("ObjectiveManager");

            if (EvidenceManager.Instance == null)
                Create<EvidenceManager>("EvidenceManager");

            if (WeatherSystem.Instance == null)
                Create<WeatherSystem>("WeatherSystem");

            if (GhostActivitySystem.Instance == null)
                Create<GhostActivitySystem>("GhostActivitySystem");
        }

        /// <summary>True once the core layer exists, used by scenes entered directly.</summary>
        public static bool CoreInstalled =>
            GameManager.Instance != null &&
            SceneLoader.Instance != null &&
            UIManager.Instance != null &&
            RuntimeUiRoot != null;

        public const string RuntimeUiName = "RuntimeUI";

        /// <summary>
        /// The runtime UI canvas, or null. Found by name because that is how the factory
        /// names it and how four separate call sites used to look for it; centralising the
        /// lookup is what stops a fifth one appearing with a different spelling.
        /// </summary>
        public static GameObject RuntimeUiRoot => GameObject.Find(RuntimeUiName);

        private static void EnsureRuntimeUi()
        {
            if (RuntimeUiRoot != null)
                return;

            // Builds the canvas and registers every screen with UIManager. Which screen is
            // then shown is the scene's decision, not this one's, so nothing is shown here.
            RuntimeUIFactory.BuildCompleteUI();
        }

        private static void Create<T>(string name) where T : MonoBehaviour
        {
            var go = new GameObject(name);
            go.AddComponent<T>();
        }
    }
}
