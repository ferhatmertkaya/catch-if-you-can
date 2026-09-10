using CatchIfYouCan.Art;
using CatchIfYouCan.Audio;
using CatchIfYouCan.Player;
using CatchIfYouCan.UI;
using UnityEngine;

namespace CatchIfYouCan.Core.SceneSetup
{
    /// <summary>
    /// The lobby, standing on its own.
    ///
    /// <para>
    /// Every reference here used to live on MainMenuModeController, on the far side of a
    /// scene boundary that did not exist yet. Unity cannot serialize a reference across
    /// scenes, so the split turns each of them into null - and each one fails quietly:
    /// a null spawn point drops the player outside the room, a null exterior leaves the
    /// window showing the camera's clear colour, a null ambience leaves the room silent.
    /// Holding them here is what makes the lobby a scene rather than a mode.
    /// </para>
    ///
    /// <para>
    /// There is deliberately no "activate the room" step. The lobby is active because it is
    /// the scene, which is the difference between opening it in Unity and being able to
    /// edit it, and opening it to find 41 disabled objects.
    /// </para>
    /// </summary>
    [AddComponentMenu("Catch If You Can/Scene Setup/Lobby Scene Installer")]
    public sealed class LobbySceneInstaller : SceneInstallerBase
    {
        [Header("Player")]
        [Tooltip("Where the player appears. The lobby's marker sits at (20, 0.05, -3.5); " +
                 "the room's floor spans x 14.7 to 25.3, so the world origin is outside it.")]
        [SerializeField] private Transform playerSpawn;

        [Tooltip("Whether the player can move the moment the scene starts. A scene entered " +
                 "from the menu fades in first and sets this from the transition instead.")]
        [SerializeField] private bool enableInputOnInstall = true;

        [Tooltip("On desktop, capture the cursor while the player has control.")]
        [SerializeField] private bool lockCursor = true;

        [Header("Room systems")]
        [Tooltip("Chooses the night outside the window and applies it to the player camera.")]
        [SerializeField] private LobbyExterior exterior;

        [Tooltip("The room's spatial soundscape. Silent until it is given a listener.")]
        [SerializeField] private LobbyAmbience ambience;

        public override void Install()
        {
            fallbackLightIntensity = 0.15f;
            InstallSceneBasics();

            ShowScreenIfWeBuiltTheUi(UIScreen.HUD, false);

            ResolveMissingReferences();

            var player = SpawnPlayer();
            BindRoomToPlayer(player);
        }

        /// <summary>
        /// Spawns the local player and shows the HUD. Public so the menu transition can do
        /// the same thing at the moment its fade finishes rather than on scene load.
        /// </summary>
        public PlayerBuildResult SpawnPlayer()
        {
            if (PlayerSpawner.HasPlayer)
                return PlayerSpawner.Current;

            PlayerSpawner.LockCursorWhenEnabled = lockCursor;

            var player = PlayerSpawner.Spawn(playerSpawn, enableInputOnInstall,
                                             contextForDiagnostics: name);
            PlayerSpawner.SetHudVisible(true);
            return player;
        }

        /// <summary>
        /// Points the room's per-camera and per-listener systems at the player. Separate
        /// from the spawn so the menu transition can spawn early, behind black, and bind
        /// at the same moment either way.
        /// </summary>
        public void BindRoomToPlayer(PlayerBuildResult player)
        {
            if (player == null)
                return;

            // The sky goes on the player's camera, never on RenderSettings: a global one
            // would feed ambient into a room lit without it.
            if (exterior != null && player.ViewCamera != null)
                exterior.ApplyTo(player.ViewCamera);

            if (ambience != null && player.Root != null)
                ambience.Begin(player.Root.transform);
        }

        /// <summary>
        /// Last-resort lookups, so a scene that has not been authored with its references
        /// yet still runs. Each one says what it did, because a silent search is how these
        /// references went missing in the first place.
        /// </summary>
        private void ResolveMissingReferences()
        {
            if (exterior == null)
            {
                exterior = Object.FindAnyObjectByType<LobbyExterior>();
                if (exterior != null)
                    CIYCLog.Warn("LobbySceneInstaller.exterior was not assigned; found " +
                                 exterior.name + " by search. Assign it on " + name + ".");
            }

            if (ambience == null)
            {
                ambience = Object.FindAnyObjectByType<LobbyAmbience>();
                if (ambience != null)
                    CIYCLog.Warn("LobbySceneInstaller.ambience was not assigned; found " +
                                 ambience.name + " by search. Assign it on " + name + ".");
            }
        }
    }
}
