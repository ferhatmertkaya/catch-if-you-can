using System;
using System.Collections;
using System.Collections.Generic;
using CatchIfYouCan.Core;
using CatchIfYouCan.Procedural;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace CatchIfYouCan.Missions
{
    /// <summary>
    /// Builds the mission world while the player is still in the lobby, and hands it over when
    /// they walk through the door.
    ///
    /// <para>
    /// <b>One world, one seed, generated once.</b> The seed is rolled in
    /// <see cref="MissionManager.StartInvestigation"/> before the portal opens and travels on
    /// the <see cref="MissionRuntime"/>, which is held by a persistent singleton and outlives
    /// every scene load. The investigation scene is loaded <em>additively</em>, in
    /// <see cref="InvestigationStartMode.Deferred"/>, and generates the house from that seed.
    /// The portal camera renders that geometry; when the player crosses, that same scene becomes
    /// the active one. Nothing is generated twice and there is no preview copy to drift from the
    /// real thing.
    /// </para>
    ///
    /// <para>
    /// <b>A prepared world is not a running one.</b> Deferred mode builds the van and the house
    /// and stops - no player, no ghost, no objectives, no audio, no state change - so nothing is
    /// hunting anybody while they are still reading a noticeboard. The scene's own camera,
    /// listener and event system are switched off the moment it loads, because two of any of
    /// those is a bug that only shows up as a warning nobody reads.
    /// </para>
    ///
    /// <para>
    /// <b>The direct load survives as a named alternative.</b> If the additive load cannot be
    /// done - the scene is not in the build list, Unity refuses the load, the world fails to
    /// build - the lobby says so at error level and falls back to
    /// <see cref="SceneLoader.LoadInvestigation"/>, which is the path this game shipped with.
    /// A player who reaches their mission down the older route is a degraded experience; one who
    /// presses START and watches nothing happen is a broken game.
    /// </para>
    /// </summary>
    public static class MissionWorldLoader
    {
        private const string LogTag = "[CIYC][Portal] ";

        /// <summary>How long to wait for a deferred world to finish building, in frames.</summary>
        private const int PrepareFrameBudget = 600;

        private static readonly List<Camera> CameraBuffer = new List<Camera>();
        private static readonly List<AudioListener> ListenerBuffer = new List<AudioListener>();
        private static readonly List<EventSystem> EventSystemBuffer = new List<EventSystem>();

        /// <summary>The world that has been built and not yet entered, or null.</summary>
        public static InvestigationBootstrap PreparedWorld => InvestigationBootstrap.Prepared;

        /// <summary>True while a world is standing by behind the portal.</summary>
        public static bool WorldReady =>
            InvestigationBootstrap.Prepared != null &&
            InvestigationBootstrap.Prepared.WorldPrepared;

        // ---- preparing ------------------------------------------------------------------------

        /// <summary>
        /// Loads the investigation additively and builds its world from the active mission's
        /// seed. Reports the bootstrap when it is ready, or null when it is not - the caller
        /// decides what to do about that, and is expected to say something either way.
        /// </summary>
        public static IEnumerator PrepareAsync(Action<InvestigationBootstrap> onDone)
        {
            MissionRuntime mission = MissionManager.Instance != null
                ? MissionManager.Instance.ActiveMission
                : null;

            if (WorldReady)
            {
                // A world is standing by - but is it THIS mission's? Every press of START
                // INVESTIGATION creates a new MissionRuntime with a freshly rolled seed, so a
                // player who backs out and picks a different house would otherwise walk through
                // the door into the one they rejected. Reference equality is exactly right
                // here: same runtime object, same seed, same world.
                if (mission != null && ReferenceEquals(InvestigationBootstrap.Prepared.Mission, mission))
                {
                    onDone?.Invoke(InvestigationBootstrap.Prepared);
                    yield break;
                }

                CIYCLog.Info(LogTag + "A world is prepared for a different mission; replacing it.");
                yield return DiscardAsync("a different mission was accepted");
            }

            if (mission == null)
            {
                CIYCLog.Error(LogTag + "No active mission, so there is no seed to build a world " +
                              "from. MissionManager.StartInvestigation must run before the " +
                              "portal opens.");
                onDone?.Invoke(null);
                yield break;
            }

            string sceneName = CiycScenes.Investigation;
            if (!CiycScenes.IsRegisteredInBuild(sceneName))
            {
                CIYCLog.Error(LogTag + "Cannot prepare the mission world: '" + sceneName +
                              "' is not in the build settings scene list. Add " +
                              CiycScenes.PathOf(sceneName) + " under File > Build Settings, or " +
                              "run Catch If You Can > Setup Project.");
                onDone?.Invoke(null);
                yield break;
            }

            CIYCLog.Info(LogTag + "mission accepted seed=" + mission.Seed +
                         " case=" + mission.CaseNumber + " location=" + mission.LocationName);

            // Set before the load, because a scene's components cannot be configured until they
            // exist and by then their Start has already decided what to do.
            InvestigationBootstrap.PendingStartMode = InvestigationStartMode.Deferred;

            // Subscribed rather than done after the load completes: sceneLoaded fires once every
            // object in the scene has woken and before any of their Start methods run, which is
            // the earliest moment a second camera or a second audio listener can be switched off.
            SceneManager.sceneLoaded += OnSceneLoaded;

            AsyncOperation load;
            try
            {
                load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            }
            catch (Exception e)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                InvestigationBootstrap.PendingStartMode = InvestigationStartMode.Immediate;
                CIYCLog.Error(LogTag + "Additive load of '" + sceneName + "' threw: " + e.Message);
                onDone?.Invoke(null);
                yield break;
            }

            if (load == null)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                InvestigationBootstrap.PendingStartMode = InvestigationStartMode.Immediate;
                CIYCLog.Error(LogTag + "Additive load of '" + sceneName + "' returned nothing.");
                onDone?.Invoke(null);
                yield break;
            }

            while (!load.isDone)
                yield return null;

            SceneManager.sceneLoaded -= OnSceneLoaded;
            CIYCLog.Info(LogTag + "investigation scene loaded");

            // The bootstrap builds the world on its own first frame; this waits for it rather
            // than assuming a frame count. The budget exists so a world that never finishes
            // reports a failure instead of hanging the lobby forever.
            int frames = 0;
            while (!WorldReady && frames < PrepareFrameBudget)
            {
                frames++;
                yield return null;
            }

            if (!WorldReady)
            {
                CIYCLog.Error(LogTag + "The mission world did not finish building within " +
                              PrepareFrameBudget + " frames. Unloading it rather than leaving " +
                              "half a scene resident.");
                yield return DiscardAsync("preparation timed out");
                onDone?.Invoke(null);
                yield break;
            }

            CIYCLog.Info(LogTag + "world prepared after " + frames + " frame(s)");
            onDone?.Invoke(InvestigationBootstrap.Prepared);
        }

        /// <summary>
        /// Silences a freshly loaded world: its camera, its listener and its event system. The
        /// lobby owns all three until the player crosses, and the mission's own player brings a
        /// camera and a listener of its own when it spawns.
        /// </summary>
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Additive || scene.name != CiycScenes.Investigation)
                return;

            int cameras = 0, listeners = 0, eventSystems = 0;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                root.GetComponentsInChildren(true, CameraBuffer);
                foreach (Camera c in CameraBuffer)
                {
                    if (!c.enabled) continue;
                    c.enabled = false;
                    cameras++;
                }

                root.GetComponentsInChildren(true, ListenerBuffer);
                foreach (AudioListener l in ListenerBuffer)
                {
                    if (!l.enabled) continue;
                    l.enabled = false;
                    listeners++;
                }

                root.GetComponentsInChildren(true, EventSystemBuffer);
                foreach (EventSystem e in EventSystemBuffer)
                {
                    if (!e.enabled) continue;
                    e.enabled = false;
                    eventSystems++;
                }
            }

            CIYCLog.Info(LogTag + "Mission world loaded behind the lobby; silenced " + cameras +
                         " camera(s), " + listeners + " listener(s), " + eventSystems +
                         " event system(s).");

            EnsureBootstrap(scene);
        }

        /// <summary>
        /// Makes sure the loaded investigation actually carries the component that builds it.
        ///
        /// <para>
        /// <b>03_Investigation.unity contains ZERO MonoBehaviours.</b> It has a GameObject named
        /// INVESTIGATION_BOOTSTRAP, and another named ProceduralHouseGenerator, and one called
        /// MissionManager - and every one of them is a bare Transform with a name and nothing
        /// attached. The scene was authored as YAML and the scripts were never put on it.
        /// </para>
        ///
        /// <para>
        /// So <c>InvestigationBootstrap.Start</c> never ran, <c>Prepared</c> stayed null,
        /// <c>WorldReady</c> was never true, and PrepareAsync sat there until its 600-frame
        /// budget expired - which is the timeout in the log, and the reason the portal has been
        /// collapsing. It is also why entering by the old direct-load fallback dropped the
        /// player into an empty dark scene with no house, no spawn point, no equipment and no
        /// lighting: there was nothing in that scene to build any of it.
        /// </para>
        ///
        /// <para>
        /// Attached here rather than left to fail, on the same principle as
        /// <c>SceneBootstrapper.EnsureForActiveScene</c>, and reported at error level because a
        /// scene that has to be repaired on load is a scene that should be fixed in the editor.
        /// </para>
        /// </summary>
        public static void EnsureBootstrap(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.GetComponentInChildren<InvestigationBootstrap>() != null)
                    return;
            }

            // Prefer the object the scene already names for the job, so the repaired scene has
            // the shape the authored one was meant to have.
            GameObject host = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "INVESTIGATION_BOOTSTRAP")
                {
                    host = root;
                    break;
                }
            }

            if (host == null)
            {
                host = new GameObject("INVESTIGATION_BOOTSTRAP");
                SceneManager.MoveGameObjectToScene(host, scene);
            }

            var bootstrap = host.AddComponent<InvestigationBootstrap>();

            // A component attached here has every serialized field null, so without this the
            // van lands on a hard-coded (0, 0, -14), the house at the origin and the world root
            // on the bootstrap object - none of which is what the scene says. It names all
            // three; it simply never had the component that reads them.
            bootstrap.BindSceneAnchors(FindInScene(scene, "WORLD"),
                                       FindInScene(scene, "VanAnchor"),
                                       FindInScene(scene, "HouseAnchor"));

            CIYCLog.Error(LogTag + "'" + scene.name + "' carried no InvestigationBootstrap, so " +
                          "one was attached to '" + host.name + "' at load. NOTHING in that " +
                          "scene builds the world without it - no house, no van, no spawn " +
                          "point, no lighting - which is why preparation timed out. Open the " +
                          "scene and add the component properly, so the scene declares what it " +
                          "needs instead of being repaired every time it loads.");
        }

        /// <summary>One named transform anywhere in a scene, or null. Depth-first by root.</summary>
        private static Transform FindInScene(Scene scene, string objectName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == objectName)
                    return root.transform;

                Transform found = FindInChildren(root.transform, objectName);
                if (found != null)
                    return found;
            }

            return null;
        }

        private static Transform FindInChildren(Transform parent, string objectName)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == objectName)
                    return child;

                Transform found = FindInChildren(child, objectName);
                if (found != null)
                    return found;
            }

            return null;
        }

        // ---- entering --------------------------------------------------------------------------

        /// <summary>
        /// Hands the game over to the prepared world: black, swap, activate, unload the lobby,
        /// reveal.
        ///
        /// <para>
        /// The order is the whole point. The active scene changes first, so everything spawned
        /// afterwards belongs to the mission and inherits its lighting. The lobby's player is
        /// despawned <b>before</b> the mission's is created, so there is never a moment with two
        /// players, two cameras or two audio listeners. The lobby is unloaded last, after the
        /// mission is already live, so there is no frame with nothing to render.
        /// </para>
        /// </summary>
        /// <summary>
        /// Walks the player through the portal into the world that is already standing behind it.
        ///
        /// <para>
        /// <b>This is not <see cref="EnterAsync"/> with the fade removed.</b> That path fades to
        /// black, DESTROYS the lobby player and builds a new one at the van's spawn point - which
        /// is a teleport with a curtain drawn over it, and it is why crossing produced a black
        /// frame and then a fall. Here the player is the same object throughout: they are moved
        /// through the portal pair by the same matrix the portal camera uses, so what they walked
        /// into is where they come out.
        /// </para>
        ///
        /// <para>
        /// <b>The ground is checked before anything is committed.</b> If nothing is under the
        /// mapped arrival the crossing is refused and the player keeps their controls in the
        /// lobby, because arriving in a world with no floor is worse than not arriving.
        /// </para>
        /// </summary>
        /// <param name="sourcePlane">The portal surface the player walked into.</param>
        /// <param name="destinationAnchor">Its counterpart in the prepared world.</param>
        /// <param name="onResult">True when the player was handed over; false when it refused.</param>
        public static IEnumerator EnterSeamlessAsync(Transform sourcePlane,
                                                     Transform destinationAnchor,
                                                     Action<bool> onResult = null)
        {
            const string Diag = "[CIYC][Portal][SeamlessEntry] ";

            InvestigationBootstrap world = InvestigationBootstrap.Prepared;
            if (world == null || sourcePlane == null || destinationAnchor == null)
            {
                CIYCLog.Error(Diag + "refused: worldAlreadyPrepared=" + (world != null) +
                              " sourcePlane=" + (sourcePlane != null) +
                              " destinationAnchor=" + (destinationAnchor != null) +
                              " success=false failureReason=MISSING_PAIR");
                onResult?.Invoke(false);
                yield break;
            }

            GameObject playerRoot = Player.PlayerSpawner.Current?.Root;
            var motor = playerRoot != null ? playerRoot.GetComponent<Player.PlayerController>() : null;
            if (playerRoot == null || motor == null)
            {
                CIYCLog.Error(Diag + "refused: no local player to carry through. " +
                              "success=false failureReason=NO_PLAYER");
                onResult?.Invoke(false);
                yield break;
            }

            // ---- the mapping ---------------------------------------------------------------
            // The SAME matrix the portal camera is posed with, so the player arrives where the
            // view they walked into said they would. The half turn is what makes walking in one
            // side come out forwards rather than backwards.
            Matrix4x4 flip = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, 180f, 0f), Vector3.one);
            Matrix4x4 through = destinationAnchor.localToWorldMatrix *
                                flip *
                                sourcePlane.worldToLocalMatrix *
                                playerRoot.transform.localToWorldMatrix;

            Vector3 mapped = through.GetColumn(3);
            Vector3 mappedForward = through.GetColumn(2);

            // Yaw only. A CharacterController stands up, and carrying the pitch through would
            // arrive with the body tilted while the camera sorted itself out.
            Vector3 flat = new Vector3(mappedForward.x, 0f, mappedForward.z);
            Quaternion mappedRotation = flat.sqrMagnitude > 1e-6f
                ? Quaternion.LookRotation(flat.normalized, Vector3.up)
                : playerRoot.transform.rotation;

            // ---- the ground -----------------------------------------------------------------
            // Checked BEFORE the scene is switched, so a refusal costs nothing. The prepared
            // world is additively loaded and its colliders are live, but the physics scene is
            // only synced at the fixed step - so ask for the sync rather than hope for it.
            Physics.SyncTransforms();

            Vector3 probeFrom = mapped + Vector3.up * 2f;
            if (!Physics.Raycast(probeFrom, Vector3.down, out RaycastHit ground, 8f, ~0,
                                 QueryTriggerInteraction.Ignore))
            {
                CIYCLog.Error(Diag + "refused: mappedDestination=" + mapped.ToString("F2") +
                              " groundHit=<none> - nothing solid within 8 m below the arrival. " +
                              "The player stays in the lobby with their controls. " +
                              "success=false failureReason=NO_GROUND");
                onResult?.Invoke(false);
                yield break;
            }

            // The player's root IS their feet: PlayerRigBuilder sets the controller's centre to
            // half its height, so the capsule's bottom sits exactly on the root. No offset, and
            // none invented - the convention is read from the rig rather than guessed at.
            Vector3 finalPosition = ground.point;

            LogHandoff("DESTINATION_VALID", playerRoot, motor);

            float verticalBefore = motor.CurrentSpeed;
            Camera cameraBefore = Core.LocalPlayerService.ResolveViewCamera();

            // ---- the handover ---------------------------------------------------------------
            // No fade anywhere in here. The player's own camera renders every frame of this, so
            // there is never a frame without a view.
            LogHandoff("PLAYER_TRANSFER_BEGIN", playerRoot, motor);

            Scene missionScene = world.gameObject.scene;
            Scene lobbyScene = SceneManager.GetActiveScene();

            if (missionScene.IsValid() && missionScene.isLoaded)
                SceneManager.SetActiveScene(missionScene);

            // ---- the player has to CHANGE SCENE, not just position -------------------------
            //
            // PlayerRigBuilder creates the rig as `new GameObject("Player")` with no parent, so
            // the player is a ROOT OBJECT of whatever scene was active when they were built -
            // the lobby. Unloading a scene destroys every root object in it, so carrying the
            // lobby player through and then unloading the lobby destroys the player, their
            // CharacterController and their camera a few lines later. That is precisely
            // "Display 1 - No cameras rendering" with the last rendered frame still on screen,
            // and controls that stopped responding because there is nothing left to control.
            //
            // The old EnterAsync never met this because it despawned the lobby player and built
            // a new one after the mission scene was active. Reusing the player is the better
            // architecture - one camera, one input owner, no gap - but it makes the move
            // explicit rather than incidental.
            if (missionScene.IsValid() && missionScene.isLoaded)
            {
                if (playerRoot.transform.parent != null)
                {
                    CIYCLog.Error(Diag + "the player is parented to '" +
                                  playerRoot.transform.parent.name + "', so it cannot be moved " +
                                  "between scenes and will be destroyed with the lobby. " +
                                  "success=false failureReason=PLAYER_NOT_A_SCENE_ROOT");
                    onResult?.Invoke(false);
                    yield break;
                }

                SceneManager.MoveGameObjectToScene(playerRoot, missionScene);
            }

            // Teleport is the rig's own move: it disables the controller, sets the pose, enables
            // it again and zeroes the velocity - which is the fall accumulator, so there is
            // nothing left over to drop with.
            motor.Teleport(finalPosition, mappedRotation);
            Physics.SyncTransforms();

            LogHandoff("PLAYER_MOVED", playerRoot, motor);

            // The world goes live around a player who is already standing in it. PlayerSpawner
            // returns the existing one rather than building a second, so nothing is duplicated
            // and nothing is moved again.
            yield return world.ActivateForEntry();
            LogHandoff("INVESTIGATION_LIVE", playerRoot, motor);

            // Given back HERE rather than left to the lobby portal's OnDestroy. The portal is
            // about to be destroyed with its scene, and relying on a destruction order to
            // restore the player's controls is how a player ends up standing in a finished
            // world unable to move.
            UI.MenuInputGate.Pop("LobbyPortal");

            if (lobbyScene.IsValid() && lobbyScene.isLoaded && lobbyScene != missionScene)
            {
                AsyncOperation unload = SceneManager.UnloadSceneAsync(lobbyScene);
                while (unload != null && !unload.isDone)
                    yield return null;
            }

            LogHandoff("LOBBY_CLEANUP", playerRoot, motor);

            Camera cameraAfter = Core.LocalPlayerService.ResolveViewCamera();

            CIYCLog.Info(Diag +
                "sourcePos=" + playerRoot.transform.position.ToString("F2") +
                " mappedDestination=" + mapped.ToString("F2") +
                " groundHit=" + ground.point.ToString("F2") +
                " groundNormal=" + ground.normal.ToString("F2") +
                " ground='" + ground.collider.name + "'" +
                " finalPlayerPos=" + finalPosition.ToString("F2") +
                " speedBefore=" + verticalBefore.ToString("F2") +
                " speedAfter=" + motor.CurrentSpeed.ToString("F2") +
                " cameraBefore=" + (cameraBefore != null ? cameraBefore.name : "<none>") +
                " cameraAfter=" + (cameraAfter != null ? cameraAfter.name : "<none>") +
                " worldAlreadyPrepared=true sceneReloaded=false houseRegenerated=false" +
                " blackFadeTriggered=false" +
                " duplicatePlayer=" + (Player.PlayerSpawner.Current?.Root != playerRoot) +
                " success=true");

            LogHandoff("COMPLETE", playerRoot, motor);
            onResult?.Invoke(true);
        }

        /// <summary>
        /// One line per stage of the handover, and the one invariant that matters checked rather
        /// than described: how many cameras are actually able to render Display 1.
        ///
        /// <para>
        /// Zero of them is "Display 1 - No cameras rendering", and it is logged at ERROR with
        /// every camera in the run listed, because by the time a human sees that overlay the
        /// frame that caused it is long gone.
        /// </para>
        /// </summary>
        private static void LogHandoff(string stage, GameObject playerRoot,
                                       Player.PlayerController motor)
        {
            int display1 = 0;
            var names = new System.Text.StringBuilder();

            foreach (Camera camera in
                     UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                if (camera == null)
                    continue;

                // A camera drawing into a texture - the portal's own - is not a view of the game
                // and cannot satisfy Display 1.
                bool live = camera.enabled && camera.gameObject.activeInHierarchy &&
                            camera.targetTexture == null && camera.targetDisplay == 0;
                if (live)
                    display1++;

                names.Append(" ").Append(camera.name)
                     .Append("[scene=").Append(camera.gameObject.scene.name)
                     .Append(" enabled=").Append(camera.enabled)
                     .Append(" active=").Append(camera.gameObject.activeInHierarchy)
                     .Append(" display=").Append(camera.targetDisplay)
                     .Append(camera.targetTexture != null ? " toTexture" : "")
                     .Append("]");
            }

            string line = "[CIYC][Portal][Handoff] " + stage +
                " frame=" + Time.frameCount +
                " player=" + (playerRoot != null ? playerRoot.GetEntityId().ToString() : "<none>") +
                " playerScene=" + (playerRoot != null ? playerRoot.scene.name : "<none>") +
                " playerPos=" + (playerRoot != null
                    ? playerRoot.transform.position.ToString("F2") : "<none>") +
                " playerActive=" + (playerRoot != null && playerRoot.activeInHierarchy) +
                " movementEnabled=" + (motor != null && motor.MovementEnabled) +
                " inputEnabled=" + Player.PlayerSpawner.IsInputEnabled +
                " timeScale=" + Time.timeScale.ToString("F2") +
                " cameraCountDisplay1=" + display1 +
                " cameras=" + names;

            if (display1 == 0)
                CIYCLog.Error(line + "  <-- NO CAMERA CAN RENDER DISPLAY 1 AT THIS STAGE");
            else
                CIYCLog.Info(line);
        }

        public static IEnumerator EnterAsync(float fadeOut = 0.28f, float fadeIn = 0.4f)
        {
            InvestigationBootstrap world = InvestigationBootstrap.Prepared;
            if (world == null)
            {
                CIYCLog.Error(LogTag + "Asked to enter a mission world that was never prepared.");
                yield break;
            }

            Scene missionScene = world.gameObject.scene;
            Scene lobbyScene = SceneManager.GetActiveScene();

            UI.TransitionFade fade = UI.TransitionFade.Ensure();
            yield return fade.FadeTo(1f, fadeOut);

            if (missionScene.IsValid() && missionScene.isLoaded)
                SceneManager.SetActiveScene(missionScene);

            // One player. The lobby's goes before the mission's arrives - PlayerSpawner refuses
            // to build a second one while the first exists, so without this the investigation
            // would silently reuse the lobby player, standing wherever the lobby left them.
            Player.PlayerSpawner.Despawn();

            yield return world.ActivateForEntry();

            if (lobbyScene.IsValid() && lobbyScene.isLoaded && lobbyScene != missionScene)
            {
                AsyncOperation unload = SceneManager.UnloadSceneAsync(lobbyScene);
                while (unload != null && !unload.isDone)
                    yield return null;
            }

            yield return fade.FadeTo(0f, fadeIn);
        }

        // ---- giving up ---------------------------------------------------------------------------

        /// <summary>
        /// Unloads a prepared world that is not going to be entered - a cancelled mission, or a
        /// preparation that failed. Says why, because a world quietly unloaded is a world that
        /// looks like it was never built.
        /// </summary>
        public static IEnumerator DiscardAsync(string reason)
        {
            Scene scene = SceneManager.GetSceneByName(CiycScenes.Investigation);
            if (!scene.IsValid() || !scene.isLoaded)
                yield break;

            // Never unload the scene the game is actually running in.
            if (scene == SceneManager.GetActiveScene())
            {
                CIYCLog.Warn(LogTag + "Refusing to discard the mission world: it is the active " +
                             "scene, so the game is already in it.");
                yield break;
            }

            CIYCLog.Info(LogTag + "Discarding the prepared mission world: " +
                         (string.IsNullOrEmpty(reason) ? "no reason given" : reason));

            AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
            while (unload != null && !unload.isDone)
                yield return null;
        }
    }
}
