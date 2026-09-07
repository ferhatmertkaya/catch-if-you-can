using CatchIfYouCan.Core;
using CatchIfYouCan.Equipment;
using CatchIfYouCan.Ghost;
using CatchIfYouCan.Interaction;
using CatchIfYouCan.Player;
using CatchIfYouCan.Procedural;
using CatchIfYouCan.Weather;
using UnityEngine;

namespace CatchIfYouCan.Audio
{
    public class InvestigationAudioBootstrap : MonoBehaviour
    {
        [SerializeField] private RoomAudioProfile roomProfile;
        [SerializeField] private SurfaceAudioProfile surfaceProfile;

        private bool _installed;

        private void OnEnable()
        {
            GameEvents.OnInvestigationStarted += HandleInvestigationStarted;
        }

        private void OnDisable()
        {
            GameEvents.OnInvestigationStarted -= HandleInvestigationStarted;
        }

        public void InstallAfterHouseGeneration(GeneratedHouse house, GameObject player, GhostController ghost, VanBuildResult van)
        {
            if (_installed) return;
            _installed = true;

            EnsureCoreServices();
            InstallRoomAudio(house);
            InstallPlayerAudio(player);
            InstallGhostAudio(ghost);
            InstallDoors(house);
            InstallFurniture();
            InstallVan(van);
            InstallDirectors();
            WireWeather();
            WireEquipment();
            WireReverbListener();
        }

        private void HandleInvestigationStarted()
        {
            if (!_installed)
                TryLateInstall();
        }

        /// <summary>
        /// The safety net for an investigation that started without anybody calling
        /// <see cref="InstallAfterHouseGeneration"/>. It is not the route, and it must not
        /// pre-empt the route.
        ///
        /// <para>
        /// <b>It did, and it cost the whole audio layer.</b>
        /// <c>GameManager.BeginMission</c> raises <c>InvestigationStarted</c> while the mission
        /// is being RESOLVED - which in <c>InvestigationBootstrap.PrepareWorld</c> is before the
        /// van is built, before the house is generated and before the player exists. This ran
        /// there, found nothing, installed nothing against nothing, and set <c>_installed</c>.
        /// The real call a few seconds later then returned on its first line. No footsteps, no
        /// heartbeat, no sanity audio, no room tone, no door audio, no ghost audio - and a
        /// <see cref="VanAudioController"/> that had been handed a null van and threw once per
        /// frame for the rest of the mission.
        /// </para>
        ///
        /// <para>
        /// So it stands down while the world it would install against does not exist yet, and
        /// leaves <c>_installed</c> alone, so the authoritative call still counts.
        /// </para>
        /// </summary>
        private void TryLateInstall()
        {
            var player = Core.LocalPlayerService.Root;
            var ghost = FindAnyObjectByType<GhostController>();
            var vanRoot = GameObject.Find("InvestigationVan");

            if (player == null && ghost == null && vanRoot == null)
            {
                CIYCLog.Warn("[CIYC][Audio] InvestigationStarted arrived before the world " +
                             "existed - no player, no van, no ghost - so the late audio install " +
                             "stood down instead of installing against nothing and locking " +
                             "itself in. InvestigationBootstrap installs the audio properly once " +
                             "the world is built.");
                return;
            }

            VanBuildResult van = vanRoot != null ? new VanBuildResult { Root = vanRoot } : null;

            // House reference unavailable post-hoc; room zones may already exist from installer on generator object.
            InstallAfterHouseGeneration(null, player, ghost, van);
        }

        private void EnsureCoreServices()
        {
            AudioBootstrap.Initialize();
            if (FindAnyObjectByType<AudioOcclusionController>() == null)
            {
                var go = new GameObject("AudioOcclusionController");
                go.AddComponent<AudioOcclusionController>();
            }
            if (FindAnyObjectByType<UiAudioService>() == null)
            {
                var go = new GameObject("UiAudioService");
                go.AddComponent<UiAudioService>();
            }
        }

        private void InstallRoomAudio(GeneratedHouse house)
        {
            var installer = GetComponent<RoomAudioInstaller>();
            if (installer == null)
                installer = gameObject.AddComponent<RoomAudioInstaller>();
            if (house != null)
                installer.Install(house, roomProfile ?? RoomAudioProfile.CreateDefaultRuntime());

            if (FindAnyObjectByType<RoomToneController>() == null)
            {
                var go = new GameObject("RoomToneController");
                go.AddComponent<RoomToneController>();
            }
        }

        private void InstallPlayerAudio(GameObject player)
        {
            if (player == null) return;
            if (player.GetComponent<FootstepController>() == null)
            {
                var fs = player.AddComponent<FootstepController>();
                if (surfaceProfile != null)
                {
                    var field = typeof(FootstepController).GetField("profile",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    field?.SetValue(fs, surfaceProfile);
                }
            }
            if (player.GetComponent<PlayerHeartbeatAudio>() == null)
                player.AddComponent<PlayerHeartbeatAudio>();
            if (player.GetComponent<SanityAudioController>() == null)
                player.AddComponent<SanityAudioController>();
        }

        private void InstallGhostAudio(GhostController ghost)
        {
            if (ghost == null) return;
            if (ghost.GetComponent<GhostAudioController>() == null)
                ghost.gameObject.AddComponent<GhostAudioController>();
            if (ghost.GetComponent<GhostVoiceController>() == null)
                ghost.gameObject.AddComponent<GhostVoiceController>();
            if (ghost.GetComponent<GhostHuntAudioController>() == null)
                ghost.gameObject.AddComponent<GhostHuntAudioController>();
        }

        private void InstallDoors(GeneratedHouse house)
        {
            if (house?.Doors != null)
            {
                for (int i = 0; i < house.Doors.Count; i++)
                {
                    var door = house.Doors[i]?.Door;
                    if (door == null) continue;
                    if (door.GetComponent<DoorAudioController>() == null)
                        door.gameObject.AddComponent<DoorAudioController>();
                }
                return;
            }

            var doors = FindObjectsByType<InteractiveDoor>();
            for (int i = 0; i < doors.Length; i++)
            {
                if (doors[i].GetComponent<DoorAudioController>() == null)
                    doors[i].gameObject.AddComponent<DoorAudioController>();
            }
        }

        private void InstallFurniture()
        {
            var drawers = FindObjectsByType<InteractiveDrawer>();
            for (int i = 0; i < drawers.Length; i++)
            {
                if (drawers[i].GetComponent<FurnitureAudioRelay>() == null)
                    drawers[i].gameObject.AddComponent<FurnitureAudioRelay>();
            }

            var hides = FindObjectsByType<HideSpot>();
            for (int i = 0; i < hides.Length; i++)
            {
                if (hides[i].GetComponent<HideSpotAudio>() == null)
                    hides[i].gameObject.AddComponent<HideSpotAudio>();
            }
        }

        private void InstallVan(VanBuildResult van)
        {
            var weather = FindAnyObjectByType<WeatherAudioController>();
            if (weather == null)
            {
                var go = new GameObject("WeatherAudioController");
                weather = go.AddComponent<WeatherAudioController>();
            }

            if (FindAnyObjectByType<VanAudioController>() == null)
            {
                var go = new GameObject("VanAudioController");
                var vanAudio = go.AddComponent<VanAudioController>();
                vanAudio.Initialize(van, weather);
            }
        }

        private void InstallDirectors()
        {
            if (FindAnyObjectByType<TensionAudioDirector>() == null)
            {
                var go = new GameObject("TensionAudioDirector");
                go.AddComponent<TensionAudioDirector>();
            }
            if (FindAnyObjectByType<HorrorSilenceSystem>() == null)
            {
                var go = new GameObject("HorrorSilenceSystem");
                go.AddComponent<HorrorSilenceSystem>();
            }
            if (FindAnyObjectByType<PsychologicalAudioDirector>() == null)
            {
                var go = new GameObject("PsychologicalAudioDirector");
                go.AddComponent<PsychologicalAudioDirector>();
            }
        }

        private void WireWeather()
        {
            var weatherAudio = FindAnyObjectByType<WeatherAudioController>();
            if (WeatherSystem.Instance != null && weatherAudio != null)
                weatherAudio.ApplyFromSystem(WeatherSystem.Instance.CurrentWeather);
        }

        private void WireEquipment()
        {
            if (FindAnyObjectByType<EquipmentAudioController>() == null)
            {
                var go = new GameObject("EquipmentAudioController");
                go.AddComponent<EquipmentAudioController>();
            }
            EquipmentAudioController.Instance?.WireAllEquipment();
        }

        private void WireReverbListener()
        {
            if (FindAnyObjectByType<ReverbZoneController>() != null) return;
            var cam = Core.LocalPlayerService.ResolveViewCamera();
            if (cam == null) return;
            cam.gameObject.AddComponent<ReverbZoneController>();
        }
    }
}
