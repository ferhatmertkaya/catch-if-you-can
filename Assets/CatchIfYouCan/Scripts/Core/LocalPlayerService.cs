using System;
using UnityEngine;

namespace CatchIfYouCan.Core
{
    /// <summary>
    /// Who the local player is, and what renders and hears for them.
    ///
    /// <para>
    /// Twenty-two call sites used to ask <c>Camera.main</c>, eight asked
    /// <c>FindGameObjectWithTag("Player")</c> and three asked for any AudioListener in the
    /// scene. All three questions have the same two problems. Before the player exists they
    /// answer null or, worse, whatever placeholder camera the scene happens to carry - and
    /// two of the callers latched that answer in Awake and never asked again, so a system
    /// created one frame early stayed broken for the whole session. With more than one
    /// player they answer "an arbitrary one", which is not a thing any of these systems
    /// wanted to know.
    /// </para>
    ///
    /// <para>
    /// This does not decide anything the callers did not already decide. It only makes the
    /// answer explicit, late-bindable and singular.
    /// </para>
    /// </summary>
    public static class LocalPlayerService
    {
        /// <summary>The local player's root, or null before one is spawned.</summary>
        public static GameObject Root { get; private set; }

        public static Transform RootTransform => Root != null ? Root.transform : null;

        /// <summary>The camera the local player sees through, or null.</summary>
        public static Camera ViewCamera { get; private set; }

        /// <summary>The local player's listener, or null.</summary>
        public static AudioListener Listener { get; private set; }

        public static bool HasPlayer => Root != null;

        /// <summary>
        /// Raised after a player becomes the local one. Systems that exist before the
        /// player - the mirror, the room tone, every ambience emitter - subscribe to this
        /// instead of resolving once and hoping they were late enough.
        /// </summary>
        public static event Action PlayerRegistered;

        /// <summary>Raised after the local player goes away, before the fields clear.</summary>
        public static event Action PlayerUnregistered;

        /// <summary>
        /// The camera a scene renders through while no player exists - a menu camera, or
        /// the camera a development scene carries so it is not black on entry. Set by the
        /// scene, never by gameplay.
        /// </summary>
        private static Camera _fallbackCamera;

        private static bool _cameraMainFallbackWarned;

        public static void Register(GameObject root, Camera viewCamera, AudioListener listener)
        {
            if (root == null)
            {
                CIYCLog.Error("LocalPlayerService.Register was given a null root; ignoring.");
                return;
            }

            // A second registration is a bug worth naming rather than a state to merge:
            // in single player it means two players exist, and every consumer below is
            // about to start following whichever one registered last.
            if (Root != null && Root != root)
            {
                CIYCLog.Warn("A second local player registered while '" + Root.name +
                             "' was still the local one. The newer player wins; the older " +
                             "one is now orphaned from camera, listener and HUD lookups.");
            }

            Root = root;
            ViewCamera = viewCamera;
            Listener = listener;

            // Announce the player's existence as well as its ownership. These are two
            // different questions - "which one is mine" and "who is here" - and conflating
            // them is what left the ghost able to see only the host: everything that needed a
            // player position asked this service, which holds exactly one.
            //
            // Announced here because this is where a local player appearing is already known.
            // A remote player is announced by whatever spawns it, and never through this
            // method: a remote player is present and is emphatically not local.
            var presence = root.GetComponent<Player.PlayerPresence>()
                           ?? root.AddComponent<Player.PlayerPresence>();
            presence.Bind(isLocal: true, clientId: Player.PlayerPresence.LocalOnlyClientId);

            if (viewCamera != null)
                presence.SetEyePoint(viewCamera.transform);

            PlayerRegistered?.Invoke();
        }

        /// <summary>
        /// Clears the local player. Ignores a root that is not the current one, so a
        /// despawn arriving after a replacement registered cannot blank the live player.
        /// </summary>
        public static void Unregister(GameObject root)
        {
            if (Root == null || (root != null && root != Root))
                return;

            PlayerUnregistered?.Invoke();

            Root = null;
            ViewCamera = null;
            Listener = null;
        }

        /// <summary>Declares the camera to use while no player exists.</summary>
        public static void SetFallbackCamera(Camera camera) => _fallbackCamera = camera;

        public static void ClearFallbackCamera(Camera camera)
        {
            if (_fallbackCamera == camera)
                _fallbackCamera = null;
        }

        /// <summary>
        /// The camera to render or aim through right now.
        ///
        /// The player's camera wins. A scene's declared fallback comes next. Camera.main is
        /// the last resort and is reported once, because reaching it means some scene put a
        /// camera in without telling anyone, and that is the state where two cameras make
        /// this answer arbitrary.
        /// </summary>
        public static Camera ResolveViewCamera()
        {
            if (ViewCamera != null)
                return ViewCamera;

            if (_fallbackCamera != null)
                return _fallbackCamera;

            var main = Camera.main;
            if (main != null && !_cameraMainFallbackWarned)
            {
                _cameraMainFallbackWarned = true;
                CIYCLog.Warn("LocalPlayerService fell back to Camera.main ('" + main.name +
                             "'). No player is registered and this scene declared no " +
                             "fallback camera, so with a second tagged camera present this " +
                             "answer is arbitrary.");
            }

            return main;
        }

        /// <summary>
        /// Where the local ear is. Falls back to the resolving camera, which is what every
        /// caller of this used to do by hand after asking for any AudioListener at all.
        /// </summary>
        public static Transform ResolveListenerTransform()
        {
            if (Listener != null)
                return Listener.transform;

            var camera = ResolveViewCamera();
            return camera != null ? camera.transform : null;
        }

        /// <summary>
        /// A component on the local player, or null. Replaces
        /// <c>FindAnyObjectByType&lt;T&gt;()</c> at the call sites that meant "the local
        /// player's one", which is every one of them.
        /// </summary>
        public static T GetPlayerComponent<T>() where T : Component =>
            Root != null ? Root.GetComponentInChildren<T>() : null;

        /// <summary>
        /// Test and domain-reload seam. Statics survive a scene load by design, which is
        /// the point, but they must not survive a play session.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            Root = null;
            ViewCamera = null;
            Listener = null;
            _fallbackCamera = null;
            _cameraMainFallbackWarned = false;
            PlayerRegistered = null;
            PlayerUnregistered = null;
        }
    }
}
