using System.Collections;
using CatchIfYouCan.Audio;
using CatchIfYouCan.Content;
using CatchIfYouCan.Core;
using CatchIfYouCan.Equipment;
using CatchIfYouCan.Evidence;
using CatchIfYouCan.Ghost;
using CatchIfYouCan.Missions;
using CatchIfYouCan.Objectives;
using CatchIfYouCan.Player;
using CatchIfYouCan.UI;
using CatchIfYouCan.Weather;
using UnityEngine;
using UnityEngine.AI;

namespace CatchIfYouCan.Procedural
{
    public class InvestigationBootstrap : MonoBehaviour
    {
        [Header("Scene Roots")]
        [SerializeField] private Transform worldRoot;
        [SerializeField] private Transform vanAnchor;
        [SerializeField] private Transform houseAnchor;
        [SerializeField] private Transform playerPrefab;

        [Header("Systems")]
        [SerializeField] private ProceduralHouseGenerator houseGenerator;
        [SerializeField] private GhostSpawnManager ghostSpawnManager;
        [SerializeField] private GhostDefinition fallbackGhost;
        [SerializeField] private MissionDefinition fallbackMission;

        [Header("Intro")]
        [SerializeField] private CanvasGroup fadeOverlay;
        [SerializeField] private float introHoldSeconds = 3f;
        [SerializeField] private float fadeDuration = 1.2f;

        private GameObject _playerInstance;
        private GeneratedHouse _generatedHouse;
        private VanBuildResult _van;
        private CaseIntroPresenter _introPresenter;
        private GhostController _spawnedGhost;

        private void Start()
        {
            StartCoroutine(BootstrapSequence());
        }

        private IEnumerator BootstrapSequence()
        {
            EnsureManagers();
            EnsureRuntimeUi();
            yield return null;

            var mission = ResolveMission();
            if (mission == null)
            {
                CIYCLog.Error("InvestigationBootstrap: unable to resolve mission.");
                yield break;
            }

            if (fadeOverlay != null)
                fadeOverlay.alpha = 1f;

            BuildVan();
            GenerateHouse(mission.Seed);
            SpawnPlayer();
            SpawnGhost(mission);
            WireSystems(mission);
            InstallAudio();
            PlayIntro(mission);

            if (_introPresenter == null && fadeOverlay != null)
                yield return FadeIn();
            else
                yield return null;

            GameManager.Instance?.SetInvestigating();
            MissionManager.Instance?.MarkInvestigationEntered();
        }

        private void EnsureManagers()
        {
            // The persistent layer is not this scene's to own. This method used to create
            // its own set of ten, overlapping Bootstrap's eleven in two places, so which
            // services existed depended on how the scene had been entered.
            CiycServices.EnsureCore();
            CiycServices.EnsureMission();

            // Below this line is what genuinely belongs to the investigation scene: the
            // generator is parented into the world root, so it is scene content rather
            // than a service, and the spawn manager is created per mission.
            if (houseGenerator == null)
            {
                var go = new GameObject("ProceduralHouseGenerator");
                houseGenerator = go.AddComponent<ProceduralHouseGenerator>();
                go.transform.SetParent(worldRoot != null ? worldRoot : transform, false);
            }

            InvestigationContentLoader.ApplyToGenerator(houseGenerator);

            if (ghostSpawnManager == null)
            {
                var go = new GameObject("GhostSpawnManager");
                ghostSpawnManager = go.AddComponent<GhostSpawnManager>();
            }

            PlayerFactory.EnsureMobileInput();
        }

        private static void EnsureRuntimeUi()
        {
            // Whether the HUD is raised depends on whether this call is what created the
            // canvas, exactly as before: arriving from the boot flow the canvas already
            // exists and something else owns what is on screen, so raising the HUD here
            // would take the screen away from it.
            bool existedBefore = CiycServices.RuntimeUiRoot != null;

            CiycServices.EnsureCore();

            if (existedBefore)
                return;

            if (UIManager.Instance != null)
                UIManager.Instance.Show(UIScreen.HUD, false);
        }

        private MissionRuntime ResolveMission()
        {
            if (MissionManager.Instance?.ActiveMission != null)
                return MissionManager.Instance.ActiveMission;

            var missionDef = fallbackMission;
            if (MissionManager.Instance != null)
            {
                if (missionDef == null)
                    missionDef = MissionManager.Instance.SelectRandomMission();

                return MissionManager.Instance.StartInvestigation(missionDef);
            }

            return MissionRuntime.Create(
                missionDef,
                1001,
                SeedManager.KnownGoodSeed,
                fallbackGhost);
        }

        private void BuildVan()
        {
            Vector3 vanPos = vanAnchor != null ? vanAnchor.position : new Vector3(0f, 0f, -14f);
            Quaternion vanRot = vanAnchor != null ? vanAnchor.rotation : Quaternion.identity;
            _van = VanBuilder.Build(worldRoot != null ? worldRoot : transform, vanPos, vanRot);
        }

        private void GenerateHouse(int seed)
        {
            if (houseAnchor != null && houseGenerator != null)
                houseGenerator.transform.SetPositionAndRotation(houseAnchor.position, houseAnchor.rotation);

            _generatedHouse = houseGenerator.Generate(seed);
        }

        private void SpawnPlayer()
        {
            // Input is live immediately here, which is what this scene always did: it fades
            // in over a player that is already in control, rather than handing control over
            // at the end of a transition the way the menu does.
            var buildResult = PlayerSpawner.Spawn(
                _van?.PlayerSpawnPoint,
                enableInput: true,
                prefabOverride: playerPrefab != null ? playerPrefab.gameObject : null,
                contextForDiagnostics: name);

            if (buildResult == null)
                return;

            _playerInstance = buildResult.Root;
            WirePlayerEquipment(buildResult);
        }

        private void WirePlayerEquipment(PlayerBuildResult buildResult)
        {
            if (buildResult?.Root == null)
                return;

            Transform handAnchor = buildResult.HandAnchor != null
                ? buildResult.HandAnchor
                : buildResult.Root.transform;

            var inventory = buildResult.Root.GetComponent<PlayerInventory>();
            inventory?.SetHandAnchor(handAnchor);

            // Loadout only. What the player is holding is the inventory's answer, and the hand
            // anchor above is the only one there is now.
            EquipmentManager.Instance?.GiveStarterLoadout();

            // The fear system's light is wired where the torch is built, in PlayerFactory, so
            // every spawn path gets it. This used to be done here by reading a private field
            // off a second, parallel flashlight implementation that the player never carried,
            // which meant the "am I standing in my own light" check was wired to nothing.
        }

        private void SpawnGhost(MissionRuntime mission)
        {
            if (ghostSpawnManager == null || _generatedHouse == null)
                return;

            var anchors = new System.Collections.Generic.List<Transform>(_generatedHouse.GetRoomAnchors());
            if (_generatedHouse.GhostRoom?.Root != null)
                anchors.Add(_generatedHouse.GhostRoom.Root.transform);

            ghostSpawnManager.SetRoomAnchors(anchors.ToArray());

            var ghostDef = mission.AssignedGhost != null ? mission.AssignedGhost : fallbackGhost;
            if (ghostDef == null)
            {
                CIYCLog.Warn("InvestigationBootstrap: no ghost definition assigned.");
                return;
            }

            var ghost = ghostSpawnManager.SpawnGhost(ghostDef, true);
            _spawnedGhost = ghost;
            if (ghost != null && _generatedHouse.GhostRoom?.Root != null)
            {
                var ghostRoomPos = _generatedHouse.GhostRoom.Root.transform.position + Vector3.up * 0.2f;
                if (NavMesh.SamplePosition(ghostRoomPos, out NavMeshHit hit, 4f, NavMesh.AllAreas))
                    ghost.transform.position = hit.position;
            }

            if (ghost != null && _playerInstance != null)
            {
                var perception = ghost.GetComponent<GhostPerception>();
                perception?.SetPlayer(_playerInstance.transform);
            }
        }

        private void WireSystems(MissionRuntime mission)
        {
            EvidenceManager.Instance?.ResetMission();
            ObjectiveManager.Instance?.AssignMissionObjectives(mission);

            if (WeatherSystem.Instance != null)
            {
                Vector3 weatherPos = _generatedHouse?.Entrance?.Root != null
                    ? _generatedHouse.Entrance.Root.transform.position
                    : Vector3.zero;
                WeatherSystem.Instance.EnsureOutdoorParticles(weatherPos);

                // Weather is gameplay-affecting and therefore part of the layout, not a
                // cosmetic roll: every client derives the same value from the seed.
                if (_generatedHouse?.Layout != null)
                    WeatherSystem.Instance.ApplyLayoutWeather(_generatedHouse.Layout.WeatherIndex);
                else
                    WeatherSystem.Instance.SetSeededWeather();
            }
        }

        private void PlayIntro(MissionRuntime mission)
        {
            _introPresenter = CaseIntroPresenter.Ensure(fadeOverlay);
            if (_introPresenter != null)
            {
                _introPresenter.Present(mission, introHoldSeconds, fadeDuration);
            }
            else
            {
                // Wall clock, deliberately kept out of anything generation reads: this is
                // presentation-only flavour text on the case card.
                string timeText = System.DateTime.Now.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);
                string intro = $"CASE #{mission.CaseNumber}\n{mission.LocationName}\n{timeText}";
                GameEvents.TipRequested(intro);
            }

            CIYCLog.Info($"CASE #{mission.CaseNumber} {mission.LocationName}");
            GameEvents.InvestigationStarted();
        }

        private void InstallAudio()
        {
            var bootstrap = GetComponent<InvestigationAudioBootstrap>();
            if (bootstrap == null)
                bootstrap = gameObject.AddComponent<InvestigationAudioBootstrap>();
            bootstrap.InstallAfterHouseGeneration(_generatedHouse, _playerInstance, _spawnedGhost, _van);
        }

        private IEnumerator FadeIn()
        {
            if (fadeOverlay == null)
            {
                yield return new WaitForSeconds(introHoldSeconds);
                yield break;
            }

            yield return new WaitForSeconds(introHoldSeconds);

            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                fadeOverlay.alpha = 1f - Mathf.Clamp01(elapsed / fadeDuration);
                yield return null;
            }

            fadeOverlay.alpha = 0f;
        }
    }
}
