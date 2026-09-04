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

        /// <summary>
        /// The energy is up but the far world is not finished. The portal looks like it is
        /// charging and the threshold REFUSES entry - crossing a half-built world is the race
        /// this state exists to make impossible.
        /// </summary>
        PreparingDestination,

        /// <summary>Live. The far room is visible through the doorway and can be walked into.</summary>
        Open,

        /// <summary>The player has crossed the threshold. Input is theirs no longer.</summary>
        Entering,

        /// <summary>The mission world is taking over.</summary>
        Loading,

        /// <summary>
        /// The far world could not be built, so the doorway collapsed with nobody in it.
        ///
        /// <para>
        /// Distinct from <see cref="Closed"/>, which is deliberate, and from
        /// <see cref="Inactive"/>, which is a doorway that was never asked to do anything. A
        /// failure that reports itself as either of those is a failure nobody can see: the
        /// player is standing in the lobby in all three cases, and only this one means
        /// something went wrong and the reason is in the log above. Recoverable - pressing
        /// START INVESTIGATION again is a fresh attempt, not a retry of a broken one.
        /// </para>
        /// </summary>
        Failed,

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

        [Tooltip("Everything the portal looks like and how fast it gets there. One object so " +
                 "the shape, the colours, the shader, the particles, the light, the timings " +
                 "and the render budget can all be tuned without opening a script.")]
        [SerializeField] private PortalStyle style = new PortalStyle();

        [Header("Threshold")]
        [Tooltip("The volume that counts as walking through. Sits in the opening, not past it.")]
        [SerializeField] private Vector3 entryTriggerSize = new Vector3(1.2f, 2.4f, 0.8f);

        [Tooltip("Show a lit magenta room through the opening until the mission world is ready. " +
                 "It cannot be walked into - entry still needs a prepared world - and it is the " +
                 "one thing that tells a broken render path apart from a dark destination.")]
        [SerializeField] private bool showProbeRoomUntilWorldReady = true;

        [Tooltip("How far up the player's own origin the crossing is measured, in metres. " +
                 "Chest height: a capsule's origin is on the floor, and the floor crosses the " +
                 "plane a step before the body does.")]
        [SerializeField, Min(0f)] private float crossingProbeHeight = 1.1f;

        [Tooltip("How much of the opening counts as the aperture, 1 being exactly its size. " +
                 "Slightly under 1 so brushing the jamb is not an entry.")]
        [SerializeField, Range(0.2f, 1.2f)] private float apertureTolerance = 0.95f;

        [Tooltip("How far behind the portal surface the wall fill sits, in metres. Far enough " +
                 "not to z-fight with it, near enough not to show a gap at a grazing angle.")]
        [SerializeField, Range(0.005f, 0.2f)] private float wallPlugDepth = 0.04f;

        private Renderer _wallPlug;
        private bool _sealedByHunt;
        private Coroutine _sealing;
        private BoxCollider _threshold;
        private float _previousSide;
        private bool _hasPreviousSide;
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
            // Before anything else: the wall must never be a hole, not even for the frames
            // before a mission is chosen.
            EnsureWallPlug();
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
            Vector2 opening = style.openingSize;
            surface.SetOpening(opening, new Vector3(0f, opening.y * 0.5f, 0f));
            surface.ApplyStyle(style);

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

            _effects = PortalEffects.Build(surface.transform, style);
        }

        /// <summary>
        /// Fills the doorway the lobby was built with, so the wall is a wall.
        ///
        /// <para>
        /// <b>The lobby's north wall has a real hole in it.</b> It is authored as a left panel,
        /// a right panel and a header with a gap between them, framed by jambs and a lintel -
        /// an actual doorway, through which the player sees the night sky and the mountains
        /// from the moment they walk in. That is wrong: there is no door here and there should
        /// be nothing to see until a portal tears the wall open.
        /// </para>
        ///
        /// <para>
        /// So the gap is filled, permanently. The plug is opaque and stays for the whole
        /// session; the portal surface is a TRANSPARENT quad that draws in front of it, so
        /// where the breach is you see the far world and everywhere else you see this. Nothing
        /// has to be switched off at the right moment and there is no frame where both are
        /// wrong.
        /// </para>
        ///
        /// <para>
        /// Its material is copied from the wall beside it rather than invented, so the fill
        /// cannot be a slightly different shade of the surface it is pretending to be part of.
        /// </para>
        /// </summary>
        private void EnsureWallPlug()
        {
            if (_wallPlug != null)
                return;

            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Portal_WallPlug";
            go.transform.SetParent(transform, false);

            Vector2 opening = style.openingSize;
            // A little larger than the hole, so no seam of night sky survives at the jambs.
            go.transform.localScale = new Vector3(opening.x * 1.06f, opening.y * 1.04f, 1f);
            // NEGATIVE Z. PortalSurface documents local +Z as the side the player looks in
            // from, so a plug in front of the surface would hide the portal entirely rather
            // than backing it.
            go.transform.localPosition = new Vector3(0f, opening.y * 0.5f, -wallPlugDepth);
            // Turned to face the player. A Quad's own normal is +Z, and one facing away is
            // culled - an invisible plug is the same as no plug at all.
            go.transform.localRotation = Quaternion.identity;

            var collider = go.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            _wallPlug = go.GetComponent<Renderer>();
            if (_wallPlug == null)
                return;

            _wallPlug.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            Material wall = FindWallMaterial();
            if (wall != null)
            {
                _wallPlug.sharedMaterial = wall;
                return;
            }

            // No wall to copy: a flat dark fill is still better than a hole onto the skybox,
            // and it says so rather than looking deliberate.
            Shader lit = Art.CiycShaders.FindLit();
            if (lit != null)
            {
                var fallback = new Material(lit) { name = "Portal_WallPlug_Fallback" };
                fallback.color = new Color(0.08f, 0.08f, 0.09f);
                if (fallback.HasProperty("_BaseColor"))
                    fallback.SetColor("_BaseColor", new Color(0.08f, 0.08f, 0.09f));
                _wallPlug.sharedMaterial = fallback;
            }

            CIYCLog.Warn(LogTag + "No lobby wall was found to copy a material from, so the " +
                         "doorway is filled with a flat dark panel. Name a wall object " +
                         "Lobby_Wall_North_Left or Lobby_Wall_North_Right and it will match.");
        }

        /// <summary>
        /// The material of the wall this doorway is cut into, or null.
        ///
        /// <para>
        /// Walked from this portal's own parent rather than through GameObject.Find. A global
        /// find sweeps every object in every loaded scene, and the lobby has the investigation
        /// world sitting additively behind it by the time this could matter; the wall is a
        /// sibling, so there is no reason to look further than the room.
        /// </para>
        /// </summary>
        private Material FindWallMaterial()
        {
            Transform room = transform.parent != null ? transform.parent : transform;
            var renderers = room.GetComponentsInChildren<Renderer>(true);

            for (int n = 0; n < WallSiblingNames.Length; n++)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i].sharedMaterial == null)
                        continue;
                    if (!string.Equals(renderers[i].gameObject.name, WallSiblingNames[n],
                                       System.StringComparison.Ordinal))
                        continue;

                    return renderers[i].sharedMaterial;
                }
            }

            return null;
        }

        private static readonly string[] WallSiblingNames =
        {
            "Lobby_Wall_North_Left", "Lobby_Wall_North_Right", "Lobby_Wall_North_Header",
            "Lobby_Wall_West", "Lobby_Wall_South"
        };

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

            // A DISABLED collider, kept only so the aperture is visible in the scene view and
            // so the destabilise path has something to switch off. Nothing listens to it: entry
            // is decided by the plane-crossing test in LateUpdate, because a trigger volume
            // cannot tell walking through from standing near.
            _threshold = go.AddComponent<BoxCollider>();
            _threshold.isTrigger = true;
            _threshold.size = entryTriggerSize;
            _threshold.enabled = false;
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

            if (State == LobbyPortalState.Failed)
                CIYCLog.Info(LogTag + "Opening again after a failed preparation. This is a " +
                             "fresh attempt: the previous world was discarded when the doorway " +
                             "collapsed, so nothing from it is being reused.");

            _missionName = missionName;
            if (_threshold != null)
                _threshold.enabled = true;
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
        private InvestigationBootstrap _pendingWorld;
        private bool _prepareFinished;

        /// <summary>
        /// Opens the doorway NOW, and binds the far world to it when that world is ready.
        ///
        /// <para>
        /// <b>The opening used to wait for the world.</b> PrepareAsync loads a scene additively
        /// and generates a house before returning, and only then was the surface activated - so
        /// for the whole of that the doorway was an ordinary hole in a wall, and if preparation
        /// failed it stayed one and the player was teleported instead. Pressing START
        /// INVESTIGATION and seeing nothing happen to the doorway is exactly that.
        /// </para>
        ///
        /// <para>
        /// The edge is energy in the air; it does not need to know where the door leads. So the
        /// rim, the wisps and the surface come up immediately on the same frame as the press,
        /// and the VIEW through them fades in separately when the destination arrives. An
        /// unbound portal renders its rim over a black centre, which reads as an opening that
        /// has not finished forming - honest, and visibly different from nothing at all.
        /// </para>
        /// </summary>
        private IEnumerator OpenRoutine()
        {
            // ---- 0.00s: the doorway reacts on the frame of the press ------------------------
            // Nothing here waits on anything. The energy is in the air; it does not need to
            // know where the door leads.
            surface.SetOpacity(0f);
            surface.SetViewOpacity(0f);
            surface.SetEnergy(0f);
            surface.SetOpen(0f);
            surface.gameObject.SetActive(true);
            EnsureEffects();

            SetState(LobbyPortalState.Opening);
            ReportOpening();

            _pendingWorld = null;
            _prepareFinished = false;
            StartCoroutine(PrepareWorldRoutine());

            // Something to look at while the world is built, and a diagnostic in its own right.
            // A dark portal centre has two causes that look identical from a screenshot - the
            // render path is broken, or it works and the far side is black - and this separates
            // them: magenta means the path is fine. It is never enterable; CanBeEntered still
            // demands MissionWorldLoader.WorldReady, which this is not.
            if (showProbeRoomUntilWorldReady)
            {
                var probe = PortalProbeRoom.Ensure();
                if (probe != null && probe.ViewPoint != null)
                {
                    surface.SetDestination(probe.ViewPoint);
                    surface.SetViewOpacity(1f);
                }
            }

            float openDuration = Mathf.Max(0.0001f, style.openDuration);
            float t = 0f;
            bool bound = false;
            float viewFade = 0f;

            while (t < openDuration || !bound)
            {
                if (surface == null)
                    yield break;

                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / openDuration);
                float eased = k * k * (3f - 2f * k);

                // The outline draws itself first and the body of the energy follows, so the
                // doorway reads as tearing open rather than as a picture being switched on.
                // Opacity leads energy: an outline at full brightness over a portal that is not
                // yet there is what the first fifth of a second should look like.
                // The wall tears. SetOpen grows the breach out of nothing, so the first frames
                // are a crack in an intact wall rather than a shape fading in over a hole that
                // was always there.
                surface.SetOpen(eased);
                surface.SetOpacity(Mathf.Clamp01(eased * 2.2f));
                surface.SetEnergy(eased);

                if (_effects != null)
                    _effects.SetIntensity(eased);

                if (!bound && _pendingWorld != null && _pendingWorld.ArrivalPoint != null)
                {
                    CIYCLog.Info(LogTag + "preview camera bound to " +
                                 _pendingWorld.ArrivalPoint.name + " at " +
                                 _pendingWorld.ArrivalPoint.position.ToString("F1"));
                    surface.SetDestination(_pendingWorld.ArrivalPoint);
                    bound = true;

                    // The probe was standing in; the real world takes the opening from here and
                    // fades in on its own ramp.
                    surface.SetViewOpacity(0f);
                    viewFade = 0f;
                }

                // The far room has its own fade, started when the destination camera exists and
                // NOT restarted from the energy ramp. Until then the centre is black behind a
                // burning rim, which is an opening that has not finished forming.
                if (bound)
                {
                    viewFade = Mathf.Min(1f, viewFade + Time.deltaTime /
                                             Mathf.Max(0.0001f, style.destinationFadeDuration));
                    surface.SetViewOpacity(viewFade * viewFade * (3f - 2f * viewFade));
                }
                else if (State == LobbyPortalState.Opening && t >= openDuration)
                {
                    // The energy is up and the world is not. Named, so the difference between
                    // "still charging" and "waiting for you" is visible rather than implied -
                    // and the threshold refuses either way until CanBeEntered says otherwise.
                    SetState(LobbyPortalState.PreparingDestination);
                }

                // Preparation finished and produced nothing: stop waiting rather than holding a
                // burning doorway open forever over a world that is never coming.
                if (_prepareFinished && !bound && t >= openDuration)
                    break;

                yield return null;
            }

            _opening = null;

            if (!bound)
            {
                yield return StartCoroutine(DestabiliseRoutine());
                yield break;
            }

            surface.SetOpen(1f);
            surface.SetOpacity(1f);
            surface.SetEnergy(1f);
            surface.SetViewOpacity(1f);
            if (_effects != null)
                _effects.SetIntensity(1f);

            SetState(LobbyPortalState.Open);
            CIYCLog.Info(LogTag + "state Open - '" + _missionName +
                         "'. Walk through the lobby doorway to begin.");
        }

        /// <summary>
        /// The portal coming apart, seen rather than merely logged.
        ///
        /// <para>
        /// A doorway that silently stops existing is indistinguishable from a button that did
        /// nothing, which is the exact complaint this whole path was built to answer. So the
        /// energy flickers, the view goes, the rim collapses, the particles stop emitting and
        /// the light fades - and only then does the fallback take over. It leaves nothing
        /// behind: no black portal, no live trigger, no held input gate.
        /// </para>
        /// </summary>
        private IEnumerator DestabiliseRoutine()
        {
            CIYCLog.Error(LogTag + "The mission world could not be prepared, so the doorway has " +
                          "nothing to show. Collapsing it and falling back to a direct scene " +
                          "load, which reaches the same mission without the walk.");

            if (_effects != null)
                _effects.SetDestabilising(true);

            float duration = Mathf.Max(0.0001f, style.destabiliseDuration);
            float t = 0f;

            while (t < duration && surface != null)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);

                // Stutter on the way down rather than a clean fade: this is a failure, and it
                // should not look like a graceful close.
                float flicker = Mathf.PerlinNoise(Time.time * 22f, 0f);
                float collapse = (1f - k) * Mathf.Lerp(0.35f, 1f, flicker);

                surface.SetViewOpacity(0f);
                surface.SetEnergy(collapse);
                surface.SetOpacity(collapse);
                // The breach shuts as it dies, so the wall is whole again rather than left as
                // a transparent hole.
                surface.SetOpen(1f - k);

                if (_effects != null)
                    _effects.SetIntensity(collapse * 0.6f);

                yield return null;
            }

            if (surface != null)
            {
                surface.SetOpen(0f);
                surface.SetOpacity(0f);
                surface.SetEnergy(0f);
                surface.SetViewOpacity(0f);
                surface.SetDestination(null);
                surface.gameObject.SetActive(false);
            }

            if (_effects != null)
            {
                _effects.SetIntensity(0f);
                _effects.SetDestabilising(false);
            }

            // The threshold must not be able to fire into a portal that no longer leads
            // anywhere, and the input gate must not be left holding the player's controls.
            if (_threshold != null)
                _threshold.enabled = false;
            UI.MenuInputGate.Pop(nameof(LobbyPortal));

            // Failed, not Inactive. The player is standing in the lobby with their controls
            // either way, so the state is the only thing that can tell "nothing was asked of
            // this doorway" apart from "this doorway was asked and could not".
            SetState(LobbyPortalState.Failed);

            // NOTHING is loaded here, and that is the fix. This used to call
            // FallBackToDirectLoad(), which ran SceneLoader.LoadInvestigation() - a full scene
            // load, reached WITHOUT the player ever walking anywhere. A preparation that failed
            // roughly a second after the press therefore looked exactly like "the portal opened
            // and then teleported me on its own", and dropped the player into an investigation
            // that had been entered by a route the handover was never written for.
            //
            // A failure now fails where it happened: the player keeps their controls, keeps
            // their lobby, and can press START INVESTIGATION again.
            CIYCLog.Error(LogTag + "The doorway has collapsed and the player has NOT been " +
                          "moved. They are still in the lobby with their controls. Fix the " +
                          "preparation failure above; there is deliberately no automatic " +
                          "route into a mission that could not be built.");
        }

        /// <summary>
        /// One block, once, when the doorway starts opening. Not per frame: a portal that logs
        /// every frame buries the line that says which piece is missing.
        /// </summary>
        private void ReportOpening()
        {
            CIYCLog.Info(LogTag +
                         "state=" + State +
                         " mission=" + (_missionName ?? "<none>") +
                         " " + (surface != null ? surface.Describe() : "surface=MISSING") +
                         " destinationReady=" + MissionWorldLoader.WorldReady +
                         " particles=" + (_effects != null ? "sparks/streaks/wisps" : "<none>") +
                         " particleScale=" + style.ResolveParticleScale().ToString("F2") +
                         " quality=" + PortalStyle.QualityFraction01().ToString("F2"));
        }

        private IEnumerator PrepareWorldRoutine()
        {
            yield return MissionWorldLoader.PrepareAsync(w => _pendingWorld = w);
            _prepareFinished = true;
        }

        // ---- crossing ----------------------------------------------------------------------------

        /// <summary>
        /// Whether the doorway will accept somebody walking into it right now.
        ///
        /// <para>
        /// Open is not enough. The far world has to be finished, or a player who reaches the
        /// doorway before the house does walks into a scene that is still being built.
        /// </para>
        /// </summary>
        public bool CanBeEntered =>
            State == LobbyPortalState.Open && !_handedOver && !_sealedByHunt &&
            MissionWorldLoader.WorldReady;

        /// <summary>
        /// Watches the player against the plane of the opening and commits exactly once, when
        /// they actually go through it.
        ///
        /// <para>
        /// <b>A trigger volume is not a crossing.</b> The threshold used to be a
        /// 1.2 x 2.4 x 0.8 m box with <c>OnTriggerEnter</c>, which fires when a collider so much
        /// as touches the volume - brushing the door frame, standing in the doorway as the
        /// portal reached Open, or being nudged into it. It cannot tell walking through from
        /// standing near, and it has no idea which way anybody was going.
        /// </para>
        ///
        /// <para>
        /// This tracks the signed distance from the plane of the surface between frames. Entry
        /// needs the sign to actually change from the lobby side to the far side, AND the
        /// crossing point to be inside the opening, so walking past the frame does nothing.
        /// </para>
        /// </summary>
        private void LateUpdate()
        {
            if (!CanBeEntered || surface == null)
            {
                _hasPreviousSide = false;
                return;
            }

            Transform player = LocalPlayerService.Root != null
                ? LocalPlayerService.Root.transform
                : null;

            if (player == null)
            {
                _hasPreviousSide = false;
                return;
            }

            // The plane of the opening, taken from the surface rather than from this transform:
            // the surface is what the player can see and therefore what they aim at.
            Transform plane = surface.transform;
            Vector3 planePoint = plane.position + plane.up * (style.openingSize.y * 0.5f);
            Vector3 planeNormal = plane.forward;

            // Chest height, not the feet: a capsule's origin is on the floor, and the floor
            // crosses the plane a step before the body does.
            Vector3 probe = player.position + Vector3.up * crossingProbeHeight;
            float side = Vector3.Dot(probe - planePoint, planeNormal);

            if (!_hasPreviousSide)
            {
                _previousSide = side;
                _hasPreviousSide = true;
                return;
            }

            float previous = _previousSide;
            _previousSide = side;

            // The lobby is the positive side, because the surface's forward is the side the
            // player looks in from. Only lobby -> destination counts; backing out through the
            // far side is not an entry.
            if (previous <= 0f || side > 0f)
                return;

            // Inside the opening, not beside it. Measured on the plane's own axes at the point
            // the player is standing, so a doorway they walked PAST cannot swallow them.
            Vector3 offset = probe - planePoint;
            float across = Vector3.Dot(offset, plane.right);
            float up = Vector3.Dot(offset, plane.up);

            float halfWidth = style.openingSize.x * 0.5f * apertureTolerance;
            float halfHeight = style.openingSize.y * 0.5f * apertureTolerance;

            if (Mathf.Abs(across) > halfWidth || Mathf.Abs(up) > halfHeight)
            {
                CIYCLog.Info(LogTag + "Plane crossed beside the opening (across=" +
                             across.ToString("F2") + "m up=" + up.ToString("F2") +
                             "m); not an entry.");
                return;
            }

            CIYCLog.Info(LogTag + "Threshold crossed: " + previous.ToString("F3") + " -> " +
                         side.ToString("F3") + " at across=" + across.ToString("F2") +
                         "m up=" + up.ToString("F2") + "m.");

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
                // Refuse, and give the player back what was taken. Loading the mission anyway
                // is how somebody ends up inside an investigation that was never built.
                CIYCLog.Error(LogTag + "Crossing refused: the mission world is not prepared. " +
                              "The player stays in the lobby with their controls.");
                UI.MenuInputGate.Pop(nameof(LobbyPortal));
                _handedOver = false;
                SetState(LobbyPortalState.Open);
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

        /// <summary>
        /// Shuts the tear for the duration of a hunt, and opens it again afterwards.
        ///
        /// <para>
        /// <b>Not the same as closing.</b> Close discards the prepared world and ends the
        /// mission's route in; this keeps the world standing by and only takes the way in away,
        /// so a hunt can seal the players inside and the doorway is there again when it passes.
        /// The state does not leave Open either - the portal is still this mission's portal, it
        /// simply cannot be crossed, because <see cref="CanBeEntered"/> tests the tear.
        /// </para>
        /// </summary>
        public void SetSealedByHunt(bool sealedShut)
        {
            if (_sealedByHunt == sealedShut || _handedOver)
                return;

            _sealedByHunt = sealedShut;

            if (_sealing != null)
                StopCoroutine(_sealing);
            _sealing = StartCoroutine(SealRoutine(sealedShut));

            CIYCLog.Info(LogTag + (sealedShut
                ? "Sealed for the hunt: the way in is gone until it passes."
                : "Unsealed: the way in is back."));
        }

        /// <summary>True while a hunt has taken the way in away.</summary>
        public bool IsSealedByHunt => _sealedByHunt;

        private IEnumerator SealRoutine(bool sealedShut)
        {
            float duration = Mathf.Max(0.0001f, style.huntSealDuration);
            float from = sealedShut ? 1f : 0f;
            float to = sealedShut ? 0f : 1f;
            float t = 0f;

            while (t < duration && surface != null)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                float eased = k * k * (3f - 2f * k);
                float open = Mathf.Lerp(from, to, eased);

                surface.SetOpen(open);
                surface.SetOpacity(open);
                surface.SetEnergy(open);
                if (_effects != null)
                    _effects.SetIntensity(open);

                yield return null;
            }

            if (surface != null)
            {
                surface.SetOpen(to);
                surface.SetOpacity(to);
                surface.SetEnergy(to);
            }
            if (_effects != null)
                _effects.SetIntensity(to);

            _sealing = null;
        }

        // ---- closing -----------------------------------------------------------------------------

        /// <summary>
        /// Shuts the doorway and unloads the world behind it. For a cancelled mission, or a
        /// lobby that is going away.
        /// </summary>
        public void Close(string reason)
        {
            // Failed is included: the collapse already stopped the effects, disabled the
            // threshold, returned the controls and discarded the world, so closing it again
            // would only unload a world that is not there.
            if (State == LobbyPortalState.Inactive || State == LobbyPortalState.Closed ||
                State == LobbyPortalState.Failed)
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
                surface.SetOpacity(0f);
                surface.SetEnergy(0f);
                surface.SetViewOpacity(0f);
                surface.SetDestination(null);
                surface.gameObject.SetActive(false);
            }

            // Emission off with the doorway. A closed portal that is still spitting sparks is
            // the same bug as a closed portal that is still rendering.
            if (_effects != null)
                _effects.SetIntensity(0f);

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

}
