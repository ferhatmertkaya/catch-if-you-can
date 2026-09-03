using System.Collections;
using CatchIfYouCan.Art;
using CatchIfYouCan.Core;
using CatchIfYouCan.Missions;
using CatchIfYouCan.Procedural;
using UnityEngine;

namespace CatchIfYouCan.Environment
{
    /// <summary>What the lobby doorway is doing right now.</summary>
    public enum LobbyPortalState
    {
        /// <summary>No mission chosen. The doorway is an empty opening onto the hall.</summary>
        Inactive,

        /// <summary>A mission has been accepted; the far side is being built.</summary>
        MissionSelected,

        /// <summary>The opening is coming up: the edge is lighting, the view is fading in.</summary>
        Opening,

        /// <summary>Live. The far room is visible through the doorway and can be walked into.</summary>
        Open,

        /// <summary>The player has crossed the threshold. Input is theirs no longer.</summary>
        Entering,

        /// <summary>The mission world is taking over.</summary>
        Loading,

        /// <summary>Shut deliberately - the mission was cancelled, or the lobby is going away.</summary>
        Closed
    }

    /// <summary>
    /// The lobby doorway, and the one thing that decides whether it is a hole in a wall or a
    /// way into a mission.
    ///
    /// <para>
    /// <b>Accepting a mission opens the door. Walking through it starts the investigation.</b>
    /// Those are two separate moments on purpose, and the second one is the player's: the
    /// portal never moves them and never hands the game over until they step into the opening.
    /// </para>
    ///
    /// <para>
    /// <b>What is through the door is the mission itself.</b> Not a stand-in for it. The
    /// investigation scene is loaded additively while the player is still standing in the lobby
    /// and generates its house from <c>MissionRuntime.Seed</c> - the seed rolled once, by
    /// <c>MissionManager.StartInvestigation</c>, before this portal opened. The portal camera
    /// renders that geometry, and crossing the threshold makes that same scene the active one.
    /// One seed, one world, generated once; see <see cref="MissionWorldLoader"/>.
    /// </para>
    ///
    /// <para>
    /// This used to show <c>ReferenceApartment</c> - a hand-built flat with nothing to do with
    /// the mission being started - and then load the real world separately afterwards, so the
    /// player looked into one place and arrived in another. That is no longer the production
    /// destination.
    /// </para>
    ///
    /// <para>
    /// <b>Nothing here is silent.</b> Every way this can fail to open says so, at error level,
    /// with the reason and the fix, and then falls back to the direct scene load the game
    /// shipped with rather than leaving the player pressing a button that does nothing.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Catch If You Can/Lobby Portal")]
    public sealed class LobbyPortal : MonoBehaviour
    {
        private const string LogTag = "[CIYC][Portal] ";

        /// <summary>The lobby's portal, if the lobby is loaded and active.</summary>
        public static LobbyPortal Instance { get; private set; }

        [Header("Surface")]
        [Tooltip("The rendered opening. Built as a child if left empty, and kept inactive " +
                 "until a mission is accepted so no second camera runs in an idle lobby.")]
        [SerializeField] private PortalSurface surface;

        [Tooltip("The clear hole in metres. The lobby doorway measures 1.07 between the jamb " +
                 "faces and 2.4 to the lintel; the surface sits just inside that so the jambs " +
                 "frame it instead of intersecting it.")]
        [SerializeField] private Vector2 openingSize = new Vector2(1.06f, 2.4f);

        [Header("Opening")]
        [Tooltip("How long the edge takes to come up to full. The view behind it is live from " +
                 "the first frame; this is the energy at the rim, not a fade of the room.")]
        [SerializeField, Min(0f)] private float openDuration = 1.1f;

        [SerializeField, Min(0f)] private float openRimIntensity = 2.4f;

        [Tooltip("How much the air bends at the edge once the opening is fully up.")]
        [SerializeField, Range(0f, 0.06f)] private float openDistortion = 0.02f;

        [Header("Threshold")]
        [Tooltip("The volume that counts as walking through. Sits in the opening, not past it.")]
        [SerializeField] private Vector3 entryTriggerSize = new Vector3(1.2f, 2.4f, 0.8f);

        private BoxCollider _threshold;
        private PortalEffects _effects;
        private Coroutine _opening;
        private string _missionName;
        private bool _handedOver;

        /// <summary>What the doorway is doing. Read by the UI to decide what to say.</summary>
        public LobbyPortalState State { get; private set; } = LobbyPortalState.Inactive;

        /// <summary>True while the far world is visible and can be entered.</summary>
        public bool IsOpen => State == LobbyPortalState.Open;

        /// <summary>The mission this portal was opened for, or null.</summary>
        public string MissionName => _missionName;

        // ---- the one entry point from the interface ------------------------------------------

        /// <summary>
        /// Opens the lobby doorway onto the accepted mission.
        ///
        /// <para>
        /// Returns whether the doorway has <em>started</em> opening - the world behind it is
        /// built asynchronously, and a failure part-way through falls back to the direct scene
        /// load rather than stranding the player in the lobby. False means there is no portal
        /// here at all, and the caller should take the older route itself.
        /// </para>
        /// </summary>
        public static bool TryOpenForMission(string missionName)
        {
            if (Instance == null)
            {
                CIYCLog.Error(LogTag + "Mission selected but portal controller missing. " +
                              "Nothing in this scene carries a LobbyPortal, so START " +
                              "INVESTIGATION has no doorway to open. Add one to the lobby " +
                              "doorway in " + CiycScenes.MainMenu + ".");
                return false;
            }

            return Instance.Open(missionName);
        }

        /// <summary>Shuts the lobby doorway if one is open. Safe when there is none.</summary>
        public static void CloseIfOpen(string reason)
        {
            if (Instance != null)
                Instance.Close(reason);
        }

        // ---- lifetime --------------------------------------------------------------------------

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                CIYCLog.Error(LogTag + "A second LobbyPortal is present on '" + name +
                              "'. One doorway, one portal: the extra one is ignored, and it " +
                              "should be removed from the scene.");
                return;
            }

            Instance = this;
            EnsureSurface();
            EnsureThreshold();
            SetState(LobbyPortalState.Inactive);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            // Released here because the hold taken in BeginInvestigation has no other end: the
            // handover that follows it unloads the lobby and destroys this object without ever
            // closing a screen. MenuInputGate is static and outlives the scene, so a hold left
            // behind would keep the mission's player locked out of their own controls. Popping
            // something that is not held is a no-op, so this is safe on every other teardown.
            UI.MenuInputGate.Pop(nameof(LobbyPortal));
        }

        /// <summary>
        /// Builds the rendered surface as a child, unless one was assigned in the scene.
        ///
        /// <para>
        /// It starts inactive. An idle lobby then costs nothing: no second camera, no render
        /// texture written, no LateUpdate. The buffer and the camera are allocated the first
        /// time the door opens and are kept for the rest of the session.
        /// </para>
        /// </summary>
        private void EnsureSurface()
        {
            if (surface == null)
                surface = GetComponentInChildren<PortalSurface>(true);

            if (surface == null)
            {
                var go = new GameObject("Portal_Opening");
                go.transform.SetParent(transform, false);
                surface = go.AddComponent<PortalSurface>();
            }

            // The doorway owns the size of its own hole, whether the surface was authored in
            // the scene or built here. Applied before the object is ever enabled, which is the
            // only time a PortalSurface can still be sized.
            surface.SetOpening(openingSize, new Vector3(0f, openingSize.y * 0.5f, 0f));

            surface.gameObject.SetActive(false);
        }

        /// <summary>
        /// The wisps and the glow at the edge, built once and driven by the opening ramp.
        /// Restrained on purpose: the portal is a hole, and a hole surrounded by a firework is
        /// a firework.
        /// </summary>
        private void EnsureEffects()
        {
            if (_effects != null)
                return;

            if (surface == null)
                return;

            _effects = PortalEffects.Build(surface.transform, openingSize);
        }

        /// <summary>
        /// The volume that counts as stepping through, sitting in the plane of the opening.
        /// A trigger, so it never blocks the player, and it is only listened to while the
        /// portal is Open.
        /// </summary>
        private void EnsureThreshold()
        {
            if (_threshold != null)
                return;

            var go = new GameObject("Portal_Threshold");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, entryTriggerSize.y * 0.5f, 0f);

            _threshold = go.AddComponent<BoxCollider>();
            _threshold.isTrigger = true;
            _threshold.size = entryTriggerSize;
            go.AddComponent<LobbyPortalThreshold>().Bind(this);
        }

        // ---- opening ---------------------------------------------------------------------------

        private bool Open(string missionName)
        {
            if (surface == null)
            {
                CIYCLog.Error(LogTag + "Mission '" + missionName + "' accepted but the portal " +
                              "has no surface to render. The doorway stays shut.");
                return false;
            }

            _missionName = missionName;
            SetState(LobbyPortalState.MissionSelected);

            if (_opening != null)
                StopCoroutine(_opening);
            _opening = StartCoroutine(OpenRoutine());
            return true;
        }

        /// <summary>
        /// Builds the mission world behind the lobby, then brings the edge up over it.
        ///
        /// <para>
        /// The world is loaded before the surface is ever shown. A portal switched on over an
        /// unbuilt destination renders the skybox, which on screen is a bright hole in a wall
        /// and reads as a shader bug rather than as loading.
        /// </para>
        /// </summary>
        private IEnumerator OpenRoutine()
        {
            InvestigationBootstrap world = null;
            yield return MissionWorldLoader.PrepareAsync(w => world = w);

            if (world == null || world.ArrivalPoint == null)
            {
                CIYCLog.Error(LogTag + "The mission world could not be prepared" +
                              (world != null ? " (it has no arrival point)" : "") +
                              ", so the doorway cannot show it. Falling back to a direct scene " +
                              "load, which reaches the same mission without the walk.");
                _opening = null;
                SetState(LobbyPortalState.Inactive);
                FallBackToDirectLoad();
                yield break;
            }

            // The van's own spawn point: where this mission has always begun, so what the player
            // sees through the door is exactly where they will be standing when they step out
            // of it.
            CIYCLog.Info(LogTag + "preview camera bound to " + world.ArrivalPoint.name +
                         " at " + world.ArrivalPoint.position.ToString("F1"));

            surface.SetDestination(world.ArrivalPoint);

            // Closed, and dark, before anything is shown. The surface is activated at zero
            // opacity so the first frame of the opening is an empty doorway rather than a
            // finished portal that then animates for no reason.
            surface.SetOpacity(0f);
            surface.SetRimIntensity(0f);
            surface.SetDistortion(0f);
            surface.gameObject.SetActive(true);

            EnsureEffects();
            SetState(LobbyPortalState.Opening);
            CIYCLog.Info(LogTag + "state Opening");

            float t = 0f;
            while (t < openDuration && surface != null)
            {
                t += Time.deltaTime;
                float k = openDuration <= 0f ? 1f : Mathf.Clamp01(t / openDuration);

                // Smoothstep, so it swells rather than ramping linearly.
                float eased = k * k * (3f - 2f * k);

                // The edge leads and the view follows. The rim is at full by a third of the way
                // through while the far room is still coming up, which is what makes the
                // doorway read as tearing open rather than as a picture being switched on.
                surface.SetRimIntensity(openRimIntensity * Mathf.Clamp01(eased * 3f));
                surface.SetOpacity(eased);
                surface.SetDistortion(openDistortion * eased);

                if (_effects != null)
                    _effects.SetIntensity(eased);

                yield return null;
            }

            if (surface != null)
            {
                surface.SetRimIntensity(openRimIntensity);
                surface.SetOpacity(1f);
                surface.SetDistortion(openDistortion);
            }

            if (_effects != null)
                _effects.SetIntensity(1f);

            _opening = null;
            SetState(LobbyPortalState.Open);

            CIYCLog.Info(LogTag + "state Open - '" + _missionName +
                         "'. Walk through the lobby doorway to begin.");
        }

        /// <summary>
        /// The route this game shipped with: load the investigation, behind a loading screen,
        /// with no doorway to walk through.
        ///
        /// <para>
        /// A named alternative rather than a silent fallback. It is worse - the walk is the
        /// whole point of the portal - but a player who reaches their mission by the old road
        /// still has a game, and one who presses START and watches nothing happen does not.
        /// </para>
        /// </summary>
        private void FallBackToDirectLoad()
        {
            if (SceneLoader.Instance == null)
            {
                CIYCLog.Error(LogTag + "No portal world AND no SceneLoader: the mission was " +
                              "accepted and there is no way to reach it. SceneLoader lives on " +
                              "the boot object and persists; starting from " +
                              CiycScenes.MainMenu + " directly skips it.");
                return;
            }

            CIYCLog.Warn(LogTag + "Falling back to a direct scene load for '" +
                         (_missionName ?? "the mission") + "'.");
            SceneLoader.Instance.LoadInvestigation();
        }

        // ---- crossing ----------------------------------------------------------------------------

        /// <summary>
        /// Called by the threshold when something enters it. Only the local player counts, and
        /// only while the portal is actually open.
        /// </summary>
        internal void ThresholdEntered(Collider other)
        {
            if (State != LobbyPortalState.Open || _handedOver || other == null)
                return;

            GameObject local = LocalPlayerService.Root;
            if (local == null)
                return;

            // Compared by root, not by tag or by layer: the collider that trips this is the
            // character controller several levels below the player root.
            if (other.transform.root != local.transform.root)
                return;

            BeginInvestigation();
        }

        private void BeginInvestigation()
        {
            _handedOver = true;
            SetState(LobbyPortalState.Entering);

            // The controls go before the handover, not after: a player still walking when the
            // world changes underneath them arrives in the investigation mid-stride.
            UI.MenuInputGate.Push(nameof(LobbyPortal));

            if (!MissionWorldLoader.WorldReady)
            {
                CIYCLog.Error(LogTag + "Player crossed the threshold but the mission world is " +
                              "no longer prepared. Falling back to a direct scene load.");
                SetState(LobbyPortalState.Loading);
                FallBackToDirectLoad();
                return;
            }

            SetState(LobbyPortalState.Loading);
            CIYCLog.Info(LogTag + "Entered. Handing over to '" +
                         (_missionName ?? "the mission") + "'.");

            // Driven by the transition overlay, NOT by this component. The handover unloads the
            // lobby, which destroys this object - and a coroutine dies with the MonoBehaviour
            // running it, which would leave the screen black at whatever point the unload
            // happened. The overlay is DontDestroyOnLoad, so it survives to fade itself back out.
            UI.TransitionFade.Ensure().StartCoroutine(MissionWorldLoader.EnterAsync());
        }

        // ---- closing -----------------------------------------------------------------------------

        /// <summary>
        /// Shuts the doorway and unloads the world behind it. For a cancelled mission, or a
        /// lobby that is going away.
        /// </summary>
        public void Close(string reason)
        {
            if (State == LobbyPortalState.Inactive || State == LobbyPortalState.Closed)
                return;

            // Never while handing over: the world being unloaded here is the one the player is
            // walking into.
            if (_handedOver)
                return;

            if (_opening != null)
            {
                StopCoroutine(_opening);
                _opening = null;
            }

            if (surface != null)
            {
                surface.SetDestination(null);
                surface.gameObject.SetActive(false);
            }

            StartCoroutine(MissionWorldLoader.DiscardAsync(reason));

            _missionName = null;
            SetState(LobbyPortalState.Closed);
            CIYCLog.Info(LogTag + "Closed: " + (string.IsNullOrEmpty(reason) ? "no reason given" : reason));
            SetState(LobbyPortalState.Inactive);
        }

        private void SetState(LobbyPortalState next)
        {
            State = next;
        }

        /// <summary>A fresh process holds no portal from the last one.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => Instance = null;
    }

    /// <summary>
    /// The trigger in the doorway, forwarding to the portal that owns it.
    ///
    /// <para>
    /// Separate from <see cref="LobbyPortal"/> because the collider has to sit on its own
    /// object in the plane of the opening, and a trigger message only reaches a component on
    /// the object carrying the collider.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LobbyPortalThreshold : MonoBehaviour
    {
        private LobbyPortal _owner;

        internal void Bind(LobbyPortal owner) => _owner = owner;

        private void OnTriggerEnter(Collider other)
        {
            if (_owner != null)
                _owner.ThresholdEntered(other);
        }
    }
}
