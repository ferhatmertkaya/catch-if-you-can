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
    /// <summary>How much of the investigation runs when its scene finishes loading.</summary>
    public enum InvestigationStartMode
    {
        /// <summary>
        /// Everything, on Start. What a scene entered directly has always done: build the
        /// world, spawn the player, spawn the ghost, play the intro, begin the mission.
        /// </summary>
        Immediate,

        /// <summary>
        /// The world only - the van and the house, generated from the mission's seed - and
        /// nothing that would make it a live game. No player, no ghost, no objectives, no
        /// audio, no intro, no state change.
        ///
        /// <para>
        /// This is the mode the lobby portal loads it in. The world has to exist while the
        /// player is still standing in the lobby, because a portal is a second camera
        /// rendering real geometry - but a world that is merely being looked at must not be
        /// hunting anybody. <see cref="InvestigationBootstrap.ActivateForEntry"/> runs the
        /// rest when the player actually walks through.
        /// </para>
        /// </summary>
        Deferred
    }

    public class InvestigationBootstrap : MonoBehaviour
    {
        /// <summary>
        /// Which mode the NEXT load of this scene runs in, read once by the instance that
        /// wakes up and immediately put back to <see cref="InvestigationStartMode.Immediate"/>.
        ///
        /// <para>
        /// A static set before the load, because a scene's components cannot be configured
        /// until they exist and by then Start has already run. It resets itself so that a
        /// deferred load which fails halfway cannot leave the next direct load inert.
        /// </para>
        /// </summary>
        public static InvestigationStartMode PendingStartMode = InvestigationStartMode.Immediate;

        /// <summary>
        /// The bootstrap of a world that has been prepared and not yet entered, or null.
        /// </summary>
        public static InvestigationBootstrap Prepared { get; private set; }

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

        private MissionRuntime _mission;
        private bool _worldPrepared;
        private bool _activated;

        /// <summary>True once the van and the house exist. The portal waits for this.</summary>
        public bool WorldPrepared => _worldPrepared;

        /// <summary>The mission this world was built for. Its seed is what built it.</summary>
        public MissionRuntime Mission => _mission;

        /// <summary>
        /// Where a player arriving from the lobby stands, and what the portal camera is aimed
        /// through: the van's own spawn point, which is where this mission has always begun.
        /// </summary>
        public Transform ArrivalPoint => _van != null ? _van.PlayerSpawnPoint : null;

        private void Start()
        {
            InvestigationStartMode mode = PendingStartMode;
            PendingStartMode = InvestigationStartMode.Immediate;

            if (mode == InvestigationStartMode.Deferred)
            {
                StartCoroutine(PrepareOnly());
                return;
            }

            StartCoroutine(BootstrapSequence());
        }

        private void OnDestroy()
        {
            if (Prepared == this)
                Prepared = null;
        }

        /// <summary>
        /// The whole thing, in the order it has always run. Prepare then activate, so a direct
        /// load and a portal entry go through exactly the same code rather than through two
        /// sequences that drift apart.
        /// </summary>
        private IEnumerator BootstrapSequence()
        {
            if (!PrepareWorld())
                yield break;

            yield return ActivateSequence();
        }

        /// <summary>
        /// Builds the world and stops. Nothing here spawns a player, wakes a ghost, starts an
        /// objective, installs audio or changes the game state - the world is scenery until
        /// somebody walks into it.
        /// </summary>
        private IEnumerator PrepareOnly()
        {
            // One frame so that the services this scene shares with the lobby have finished
            // their own Awake before the generator asks for them.
            yield return null;

            if (!PrepareWorld())
            {
                CIYCLog.Error("[CIYC][Portal] The mission world could not be prepared, so the " +
                              "portal has nothing to show. The lobby falls back to a direct " +
                              "scene load.");
                yield break;
            }

            Prepared = this;
            CIYCLog.Info("[CIYC][Portal] Mission world prepared for CASE #" +
                         _mission.CaseNumber + " at " + _mission.LocationName +
                         " (seed " + _mission.Seed + "). Not live until entered.");
        }

        /// <summary>
        /// The world: managers, the van, and the house generated from the mission's seed.
        ///
        /// <para>
        /// <b>The seed is not chosen here.</b> It was rolled once, in
        /// <see cref="MissionManager.StartInvestigation"/>, before the portal opened, and it
        /// travels on <see cref="MissionRuntime"/>, which outlives the scene load. Both the
        /// world the portal shows and the world the player ends up in are generated from that
        /// one number, which is what makes them the same world rather than two similar ones.
        /// </para>
        /// </summary>
        private bool PrepareWorld()
        {
            if (_worldPrepared)
                return true;

            EnsureManagers();
            EnsureRuntimeUi();

            _mission = ResolveMission();
            if (_mission == null)
            {
                CIYCLog.Error("InvestigationBootstrap: unable to resolve mission.");
                return false;
            }

            if (fadeOverlay != null)
                fadeOverlay.alpha = 1f;

            BuildVan();
            GenerateHouse(_mission.Seed);

            if (_generatedHouse == null)
            {
                CIYCLog.Error("InvestigationBootstrap: the generator returned no house for seed " +
                              _mission.Seed + ".");
                return false;
            }

            _worldPrepared = true;
            return true;
        }

        /// <summary>
        /// Turns a prepared world into a live one. Called by the portal when the player crosses,
        /// and by <see cref="BootstrapSequence"/> immediately when the scene was entered directly.
        /// </summary>
        public IEnumerator ActivateForEntry()
        {
            if (_activated)
                yield break;

            if (!_worldPrepared && !PrepareWorld())
            {
                CIYCLog.Error("[CIYC][Portal] Asked to enter a world that could not be built.");
                yield break;
            }

            yield return ActivateSequence();
        }

        private IEnumerator ActivateSequence()
        {
            _activated = true;
            if (Prepared == this)
                Prepared = null;

            SpawnPlayer();
            SpawnGhost(_mission);
            WireSystems(_mission);
            InstallAudio();
            PlayIntro(_mission);

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

        /// <summary>A fresh process has generated nothing.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetGenerationGuard() => _generatedFor = null;

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

        /// <summary>
        /// The mission runtime whose world has already been generated in this process.
        ///
        /// <para>
        /// Generating the same mission twice is the failure this pass exists to make
        /// impossible: it would mean a preview world and a played world that merely happen to
        /// match, which is a promise the code would then be relying on rather than enforcing.
        /// Held as a weak reference so a finished mission is not kept alive by the guard.
        /// </para>
        /// </summary>
        private static System.WeakReference<MissionRuntime> _generatedFor;

        private void GenerateHouse(int seed)
        {
            if (_mission != null && _generatedFor != null &&
                _generatedFor.TryGetTarget(out MissionRuntime already) &&
                ReferenceEquals(already, _mission))
            {
                CIYCLog.Error("InvestigationBootstrap: the world for CASE #" +
                              _mission.CaseNumber + " (seed " + _mission.Seed +
                              ") has already been generated in this process. Generating it a " +
                              "second time means the portal showed one world and the player is " +
                              "standing in another. Refusing.");
                return;
            }

            if (_mission != null)
                _generatedFor = new System.WeakReference<MissionRuntime>(_mission);

            GenerateHouseInternal(seed);
        }

        private void GenerateHouseInternal(int seed)
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
            SilenceSceneCameraAndListener(buildResult);
        }

        /// <summary>
        /// Switches off this scene's own camera and audio listener now that the player has
        /// brought their own.
        ///
        /// <para>
        /// The scene is authored with a Main Camera carrying an AudioListener, which is what
        /// lets it be opened and looked at in the editor. Once a player exists there are two of
        /// each: Unity picks one camera by depth and prints a warning about the listeners that
        /// is easy to never read, and the audio the player hears is then positioned at whichever
        /// listener won rather than at their head. One camera, one listener, from here on.
        /// </para>
        /// </summary>
        private void SilenceSceneCameraAndListener(PlayerBuildResult buildResult)
        {
            Camera playerCamera = buildResult.ViewCamera;

            foreach (Camera camera in FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                if (camera == null || camera == playerCamera || !camera.enabled)
                    continue;

                // The portal's own camera renders into a texture and is not a view of the game;
                // it manages its own enabled state and must not be touched here.
                if (camera.targetTexture != null)
                    continue;

                if (camera.gameObject.scene != gameObject.scene)
                    continue;

                camera.enabled = false;
                CIYCLog.Info("InvestigationBootstrap: switched off the scene camera '" +
                             camera.name + "'; the player brought their own.");
            }

            foreach (AudioListener listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
            {
                if (listener == null || !listener.enabled)
                    continue;
                if (buildResult.Root != null && listener.transform.IsChildOf(buildResult.Root.transform))
                    continue;
                if (listener.gameObject.scene != gameObject.scene)
                    continue;

                listener.enabled = false;
            }
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

            // The loadout is chosen here and INSTALLED here. Choosing it used to be the whole
            // of it: the list was filled and nothing ever turned a definition into an object,
            // so every item but the torch PlayerFactory builds was unreachable, and so was the
            // evidence they produce.
            EquipmentManager.Instance?.GiveStarterLoadout();
            MissionEquipmentInstaller.InstallLoadout(inventory);

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
