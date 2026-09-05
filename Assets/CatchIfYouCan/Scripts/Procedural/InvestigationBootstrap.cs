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

        [Header("World")]
        [Tooltip("AUS: START INVESTIGATION baut kein Haus, keinen Van und keine Props - nur " +
                 "eine leere Ebene, auf der der Spieler steht. Zum Untersuchen des " +
                 "Spielercharakters, der Ausruestung und des Portals, ohne dass eine halb " +
                 "richtige Welt drumherum davon ablenkt.\n\n" +
                 "AN: der normale Ablauf - deterministisch aus dem Missions-Seed erzeugt.\n\n" +
                 "Nichts an der Generierung ist geloescht: der Schalter fuehrt sie nur nicht " +
                 "aus. Wieder anschalten stellt genau denselben Ablauf wieder her, und der " +
                 "Seed bestimmt weiterhin dasselbe Haus wie vorher.")]
        // Auf AN. Ausgeschaltet war er, weil der Kenney-Bestand entfernt wurde und die
        // Generierung nur noch graue Primitivkisten baute. Sie baut jetzt erzeugte Geometrie
        // in exakt der Groesse, die das Layout nennt, also gibt es wieder etwas zu sehen.
        // InvestigationSceneInstaller haengt diese Komponente per AddComponent an, es gibt sie
        // in keiner Szene - dieser Anfangswert IST der wirksame Wert.
        [SerializeField] private bool generateWorld = true;

        [Tooltip("Groesse der leeren Ebene in Metern, wenn oben nichts generiert wird. Sie wird " +
                 "erst beim Betreten gebaut, nicht schon beim Vorbereiten - sonst haengt sie als " +
                 "grosse leere Flaeche hinter dem Portal. Ohne Boden faellt der Spieler beim " +
                 "Betreten durch die Welt.")]
        [SerializeField, Min(4f)] private float emptyFloorSize = 16f;

        [Header("Systems")]
        [SerializeField] private ProceduralHouseGenerator houseGenerator;
        [SerializeField] private GhostSpawnManager ghostSpawnManager;
        [SerializeField] private GhostDefinition fallbackGhost;
        [SerializeField] private MissionDefinition fallbackMission;

        [Header("Intro")]
        [SerializeField] private CanvasGroup fadeOverlay;

        /// <summary>
        /// True when this world was prepared for a portal to walk somebody into, rather than
        /// loaded directly. The difference is one thing only: no curtain, ever.
        /// </summary>
        private bool _seamlessEntry;
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
        private Transform _fallbackArrival;

        /// <summary>
        /// Wo der Spieler ankommt, und wohin die Portalkamera schaut.
        ///
        /// <para>
        /// Normalerweise der Spawnpunkt am Van. Ohne Van gibt es einen Ersatzpunkt, und das ist
        /// keine Kosmetik: das Portal bindet seine Ansicht an DIESEN Transform, und solange er
        /// null ist, wird es nie gebunden - dann laeuft die Oeffnungsroutine ab, haelt das fuer
        /// eine fehlgeschlagene Vorbereitung und laesst die Tuer nach gut einer Sekunde wieder
        /// zusammenfallen, waehrend der Diagnoseraum durchscheint. Genau das ist passiert, als
        /// generateWorld ausgeschaltet wurde und mit dem Van auch der Ankunftspunkt wegfiel.
        /// </para>
        /// </summary>
        public Transform ArrivalPoint =>
            _van != null && _van.PlayerSpawnPoint != null ? _van.PlayerSpawnPoint : _fallbackArrival;

        /// <summary>
        /// Hands this bootstrap the anchors its scene already carries.
        ///
        /// <para>
        /// Needed because a bootstrap ATTACHED AT RUNTIME has every serialized field null, and
        /// then every fallback fires at once: the van is built at a hard-coded (0, 0, -14)
        /// instead of at VanAnchor, the house at the origin instead of at HouseAnchor, and the
        /// world is parented to this object instead of to WORLD. The scene names all three; it
        /// simply never had the component that reads them.
        /// </para>
        ///
        /// <para>
        /// A public method rather than reflection into the private fields (CLAUDE.md mistake 4):
        /// reflection compiles, reviews clean and dies silently on the next rename.
        /// </para>
        /// </summary>
        public void BindSceneAnchors(Transform world, Transform van, Transform house)
        {
            if (_worldPrepared)
            {
                CIYCLog.Warn("[CIYC][Investigation] BindSceneAnchors after the world was built " +
                             "is ignored; the van and the house are already placed.");
                return;
            }

            if (world != null) worldRoot = world;
            if (van != null) vanAnchor = van;
            if (house != null) houseAnchor = house;
        }

        private void Start()
        {
            InvestigationStartMode mode = PendingStartMode;
            PendingStartMode = InvestigationStartMode.Immediate;

            if (mode == InvestigationStartMode.Deferred)
            {
                // Deferred means a portal is going to walk somebody in through a doorway that is
                // already showing them this world. Recorded here rather than inferred later,
                // because the one thing that must NOT happen on that route is a curtain.
                _seamlessEntry = true;
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

            // The overlay is a FULL-SCREEN OPAQUE CanvasGroup, raised here so the direct load
            // has something to fade in from. On the portal route it is the black frame: the
            // world is prepared while the player is still in the lobby, so by the time they
            // walk through, this has been sitting at 1 for seconds and the mission scene turns
            // active behind it. A doorway you can see through does not need a curtain.
            if (fadeOverlay != null)
                fadeOverlay.alpha = _seamlessEntry ? 0f : 1f;

            if (!generateWorld)
            {
                // Nichts wird gebaut - kein Van, kein Haus, keine Props, kein NavMesh, und
                // auch KEIN Boden.
                //
                // Der Boden entsteht erst beim tatsaechlichen Betreten. Waehrend die Welt nur
                // Kulisse hinter dem Portal ist, schaut der Spieler sonst aus der Lobby auf eine
                // Platte, die er weder braucht noch sehen soll - und die als riesige leere
                // Flaeche unter allem haengt.
                //
                // _generatedHouse bleibt null, und das ist in Ordnung: die Schritte danach - der
                // Geist, die Raumanker, das Hauslicht, das Wetter - pruefen alle darauf und
                // ueberspringen sich selbst. Sie starten also nicht halb.
                EnsureFallbackArrival();

                // Nicht nur nichts bauen, sondern auch nichts stehen lassen. Die Raeume und
                // Waende sind Laufzeitobjekte - Room_Storage(Clone) und so weiter - und sie
                // stehen in keiner Szenendatei; was von einer frueheren Vorbereitung uebrig
                // ist, verschwindet also nur, wenn es hier wirklich zerstoert wird. Destroy,
                // nicht SetActive(false): ein abgeschaltetes Zimmer ist immer noch ein Zimmer.
                ClearGeneratedWorld();

                CIYCLog.Warn("[CIYC][Investigation] generateWorld ist AUS: es wird keine Welt " +
                             "gebaut. Kein Haus, kein Van, keine Props, kein Geist. Der Boden " +
                             "entsteht erst beim Betreten. Wieder anschalten am " +
                             "InvestigationBootstrap in 03_Investigation.");

                _worldPrepared = true;
                return true;
            }

            BuildVan();
            GenerateHouse(_mission.Seed);

            if (_generatedHouse == null)
            {
                CIYCLog.Error("InvestigationBootstrap: the generator returned no house for seed " +
                              _mission.Seed + ".");
                return false;
            }

            // Auch im normalen Pfad: wenn der Van keinen Spawnpunkt geliefert hat, faellt das
            // Portal sonst genauso zusammen, nur seltener und schwerer zu finden.
            EnsureFallbackArrival();

            _worldPrepared = true;
            return true;
        }

        /// <summary>
        /// Loescht alles, was unter der Weltwurzel steht.
        ///
        /// <para>
        /// Zerstoert wird, nicht abgeschaltet. Der Ankunftspunkt ueberlebt, weil das Portal ihn
        /// braucht - ohne ihn bindet es nicht und faellt nach seiner Oeffnungsdauer zusammen.
        /// </para>
        /// </summary>
        private void ClearGeneratedWorld()
        {
            Transform root = worldRoot != null ? worldRoot : transform;
            int removed = 0;

            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
                if (child == null || child == _fallbackArrival)
                    continue;

                Destroy(child.gameObject);
                removed++;
            }

            if (removed > 0)
                CIYCLog.Info("[CIYC][Investigation] " + removed + " Objekte unter '" + root.name +
                             "' geloescht, weil keine Welt gebaut wird.");
        }

        /// <summary>
        /// Ein Ankunftspunkt, falls keiner aus der Welt kommt. Am Hausanker, sonst am Ursprung.
        /// </summary>
        private void EnsureFallbackArrival()
        {
            if (_fallbackArrival != null)
                return;

            var go = new GameObject("ArrivalPoint_Fallback");
            go.transform.SetParent(worldRoot != null ? worldRoot : transform, false);
            go.transform.position = houseAnchor != null
                ? houseAnchor.position + Vector3.up * 0.05f
                : new Vector3(0f, 0.05f, 0f);
            go.transform.rotation = houseAnchor != null ? houseAnchor.rotation : Quaternion.identity;
            _fallbackArrival = go.transform;
        }

        /// <summary>
        /// Eine Ebene und sonst nichts, fuer den Fall, dass nicht generiert wird.
        ///
        /// <para>
        /// Bewusst ein Primitive und kein Stueck der Generierung: was hier steht, soll nicht so
        /// aussehen, als waere es die Welt, und es soll nichts von dem anfassen, was
        /// abgeschaltet wurde.
        /// </para>
        /// </summary>
        private void BuildEmptyFloor()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "EmptyFloor_NoWorldGenerated";
            floor.transform.SetParent(worldRoot != null ? worldRoot : transform, false);
            floor.transform.localPosition = Vector3.zero;

            // Ein Plane-Primitive ist 10 m gross, die Skalierung ist also ein Zehntel der
            // gewuenschten Kantenlaenge.
            floor.transform.localScale = Vector3.one * (emptyFloorSize * 0.1f);
            floor.tag = "Environment";
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

            // Vor dem Spawn, nicht danach: der Spawn rastet den Spieler mit einem Strahl nach
            // unten auf den Boden, und ohne Boden faellt er stattdessen.
            if (!generateWorld)
                BuildEmptyFloor();

            SpawnPlayer();
            SpawnGhost(_mission);
            WireSystems(_mission);
            InstallAudio();
            PlayIntro(_mission);

            if (_seamlessEntry)
            {
                // Nothing was covered, so there is nothing to uncover. A FadeIn here would be a
                // fade UP from transparent - a black flash on the frame it starts.
                yield return null;
            }
            else if (_introPresenter == null && fadeOverlay != null)
            {
                yield return FadeIn();
            }
            else
            {
                yield return null;
            }

            // The last word on the overlay, whoever was meant to clear it.
            //
            // PrepareWorld sets fadeOverlay.alpha to 1 - a FULL-SCREEN OPAQUE CANVAS GROUP -
            // and the only thing that ever took it back down was FadeIn(), which the branch
            // above skips whenever an intro presenter exists. If that presenter's routine does
            // not finish (its object destroyed with the lobby, an exception, a scene swap
            // mid-fade) the overlay stays at 1 and the player is left looking at a black
            // rectangle with the HUD drawn on top of it - world rendering perfectly behind it,
            // lighting fine, camera fine, and nothing in any log to say so.
            //
            // This is not a fade. It is the guarantee that the screen is never handed to the
            // player still covered.
            if (fadeOverlay != null && fadeOverlay.alpha > 0.001f)
            {
                CIYCLog.Warn("[CIYC][Investigation] The intro overlay was still at alpha " +
                             fadeOverlay.alpha.ToString("F2") + " when the mission went live. " +
                             "Clearing it - the world was behind it the whole time.");
                fadeOverlay.alpha = 0f;
            }

            GameManager.Instance?.SetInvestigating();
            MissionManager.Instance?.MarkInvestigationEntered();

            ReportEntry();
        }

        /// <summary>
        /// One block, once, the moment the mission goes live.
        ///
        /// <para>
        /// Every field here is one of the things that was individually invisible when this
        /// handover broke: a black screen tells you nothing about whether the camera was off,
        /// the overlay was up, the HUD was suppressed or the equipment went to a player that no
        /// longer exists. Reported together they say which.
        /// </para>
        /// </summary>
        private void ReportEntry()
        {
            var camera = Core.LocalPlayerService.ResolveViewCamera();
            int listeners = 0;
            var listenerBuffer = Object.FindObjectsByType<AudioListener>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < listenerBuffer.Length; i++)
                if (listenerBuffer[i].enabled)
                    listeners++;

            var inventory = _playerInstance != null
                ? _playerInstance.GetComponentInChildren<Player.PlayerInventory>()
                : null;

            CIYCLog.Info("[CIYC][PortalEntry] " +
                "activeScene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name +
                " entryAnchor=" + (ArrivalPoint != null ? ArrivalPoint.name : "<none>") +
                " newPlayer=" + (_playerInstance != null ? _playerInstance.name : "<none>") +
                " playerAt=" + (_playerInstance != null
                    ? _playerInstance.transform.position.ToString("F1") : "<none>") +
                " localPlayerRegistered=" + (Core.LocalPlayerService.Root != null) +
                " camera=" + (camera != null ? camera.name : "<none>") +
                " cameraEnabled=" + (camera != null && camera.enabled) +
                " listenerCount=" + listeners +
                " menuGateHolds=" + UI.MenuInputGate.HolderCount +
                " hudVisible=" + Player.PlayerSpawner.IsHudVisible +
                " inputEnabled=" + Player.PlayerSpawner.IsInputEnabled +
                " inventoryFreeSlot=" + (inventory != null
                    ? inventory.HasFreeSlot.ToString() : "<none>") +
                " torch=" + (inventory != null ? inventory.HasTorch.ToString() : "<none>") +
                " fadeOverlayAlpha=" + (fadeOverlay != null
                    ? fadeOverlay.alpha.ToString("F2") : "<none>") +
                " ambient=" + RenderSettings.ambientIntensity.ToString("F2") +
                " ghost=" + (_spawnedGhost != null ? _spawnedGhost.name : "<none>"));
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
            Transform arrival = _van?.PlayerSpawnPoint;

            var buildResult = arrival != null
                ? PlayerSpawner.Spawn(GroundedSpawn(arrival), arrival.rotation, true,
                                      playerPrefab != null ? playerPrefab.gameObject : null)
                : PlayerSpawner.Spawn(null, enableInput: true,
                                      prefabOverride: playerPrefab != null ? playerPrefab.gameObject : null,
                                      contextForDiagnostics: name);

            if (buildResult == null)
                return;

            _playerInstance = buildResult.Root;
            WirePlayerEquipment(buildResult);
            SilenceSceneCameraAndListener(buildResult);
        }

        /// <summary>
        /// The arrival point, dropped onto whatever floor is actually under it.
        ///
        /// <para>
        /// The van's spawn point sits at its local Y of zero, while the van's own floor is a
        /// slab whose top is 8 cm above that - and nothing in the spawn path has ever put the
        /// player on a surface. They are placed at the anchor's exact height and gravity then
        /// deals with the difference, which is a drop the player sees on the frame they arrive.
        /// </para>
        ///
        /// <para>
        /// Cast from two metres above and accept the first thing hit that is not the player
        /// themselves - there is no player yet at this point, so a plain cast is enough. If
        /// nothing is under the anchor at all the anchor's own position is used unchanged and
        /// the miss is reported, because an arrival point suspended over nothing is a content
        /// bug that should be seen rather than quietly patched.
        /// </para>
        /// </summary>
        private static Vector3 GroundedSpawn(Transform arrival)
        {
            Vector3 from = arrival.position + Vector3.up * 2f;

            if (Physics.Raycast(from, Vector3.down, out RaycastHit hit, 6f,
                                ~0, QueryTriggerInteraction.Ignore))
            {
                return hit.point;
            }

            CIYCLog.Warn("[CIYC][Investigation] Nothing under the arrival point at " +
                         arrival.position.ToString("F1") + ", so the player is placed at it " +
                         "exactly and will fall until something catches them.");
            return arrival.position;
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
            // Lighting runs here rather than in PrepareWorld: ambient and fog are written to
            // RenderSettings, which belongs to the ACTIVE scene, and a world being looked at
            // through a portal is not the active one. Applying it early would repaint the lobby.
            Environment.HouseLightingDirector.Apply(_generatedHouse, mission != null ? mission.Seed : 0);

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
