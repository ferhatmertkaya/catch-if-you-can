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
