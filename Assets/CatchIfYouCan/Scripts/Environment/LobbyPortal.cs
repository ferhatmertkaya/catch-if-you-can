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

        /// <summary>The raised counterpart of the arrival point. See ResolveViewAnchor.</summary>
        private Transform _viewAnchor;

        [Header("Threshold")]
        [Tooltip("How DEEP the threshold volume is, in metres. Its width and height are not " +
                 "authored: they are the opening, because a threshold that is not the size of " +
                 "the hole is a second opening size that can disagree with the first.")]
        [SerializeField, Min(0.05f)] private float entryTriggerDepth = 0.8f;

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

        [Tooltip("The wall the tear is cut into. Left empty it is found by looking for the " +
                 "collider the opening is standing inside, which is what the lobby needs - the " +
                 "scene sets nothing.")]
        [SerializeField] private Collider wallCollider;

        private bool _sealedByHunt;
        private Coroutine _sealing;
        private BoxCollider _threshold;

        /// <summary>The wall's own collider, switched off while the tear is open.</summary>
        private Collider _wallSolid;

        /// <summary>The replacement collision: the same wall with a hole in it.</summary>
        private GameObject _aperture;
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
            EnsureSurface();
            EnsureThreshold();
            EnsureWallAperture();
            SetState(LobbyPortalState.Inactive);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            // The aperture is unparented, so it does not go with this object. Left behind it
            // would be a wall-shaped set of colliders in a scene with no wall.
            if (_aperture != null)
                Destroy(_aperture);

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

#if UNITY_EDITOR
        /// <summary>
        /// Makes the style editable while the game is running.
        ///
        /// <para>
        /// Without this the numbers were unreachable in both directions. Before play there is no
        /// portal - the surface is built at runtime - and during play editing the style did
        /// nothing, because the values are pushed exactly once when the surface is built. The
        /// only field that appeared to work was the material's, on PortalSurface, and editing a
        /// material is editing the copy: PushStyle overwrites it on the next build.
        /// </para>
        ///
        /// <para>
        /// Editor-only, and it runs on an inspector edit rather than every frame - resizing
        /// re-derives the mesh, the plane and the culling bounds, which is not a thing to do
        /// sixty times a second.
        /// </para>
        /// </summary>
        private void OnValidate()
        {
            if (!Application.isPlaying || surface == null || !surface.IsBuilt)
                return;

            // Order matters: the surface needs the new style before it re-derives geometry from
            // it, and SetOpening rebuilds only when the size actually moved.
            surface.ApplyStyle(style);
            surface.SetOpening(style.openingSize,
                               new Vector3(0f, style.openingSize.y * 0.5f, 0f));

            if (_effects != null)
                _effects.ApplyStyle(style);

            // The hole in the collision is cut from the opening size, so a resize re-cuts it.
            EnsureWallAperture();
            SetWallOpen(State == LobbyPortalState.Open || State == LobbyPortalState.Entering);

            // The far anchor is derived from the opening height, so a taller opening moves it.
            if (_pendingWorld != null && _pendingWorld.ArrivalPoint != null)
                surface.SetDestination(ResolveViewAnchor(_pendingWorld.ArrivalPoint));
        }
#endif

        /// <summary>
        /// The transform the portal's view is anchored to, which is NOT where the player lands.
        ///
        /// <para>
        /// <b>One value was being asked to mean two things</b>, which is CLAUDE.md mistake 13.
        /// <c>ArrivalPoint</c> is the van's player spawn - it sits on the FLOOR, because that is
        /// where a pair of feet goes. The portal's own reference is the surface CENTRE, half the
        /// opening's height up the wall. Feeding the floor-level point straight into
        /// <see cref="PortalSurface.SetDestination"/> pairs a transform 1.2 m up with one at
        /// zero, so the portal camera stood 1.2 m too low in the far world and the far floor
        /// rode up into the opening - which reads as "the room behind the portal is too high",
        /// and is really the camera being too low.
        /// </para>
        ///
        /// <para>
        /// Raised along the arrival point's OWN up axis, and parented to it, so the anchor
        /// follows if the world is ever moved. The arrival point itself is untouched: the
        /// player's feet still land on the floor.
        /// </para>
        /// </summary>
        private Transform ResolveViewAnchor(Transform arrival)
        {
            if (arrival == null)
                return null;

            if (_viewAnchor == null || _viewAnchor.parent != arrival)
            {
                if (_viewAnchor != null)
                    Destroy(_viewAnchor.gameObject);

                var go = new GameObject("Portal_ViewAnchor");
                _viewAnchor = go.transform;
                _viewAnchor.SetParent(arrival, false);
                _viewAnchor.localRotation = Quaternion.identity;
            }

            // The same offset the surface uses on this side, so the two transforms of the pair
            // sit at the same height above their own floors. Read from the style rather than
            // stored, so re-tuning the opening height cannot leave the anchor behind.
            _viewAnchor.localPosition = new Vector3(0f, style.openingSize.y * 0.5f, 0f);
            return _viewAnchor;
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
        /// Cuts the opening out of the wall's COLLISION, leaving its geometry alone.
        ///
        /// <para>
        /// The tear was only ever a picture. The wall behind it is one solid box - 10.6 x 3.6 x
        /// 0.3 in the lobby - and its collider stayed whole, so the player walked into a wall
        /// they could see through. A portal you cannot step into is a screen.
        /// </para>
        ///
        /// <para>
        /// <b>The renderer is not touched.</b> The wall stays one object with one mesh, so
        /// nothing z-fights and there is no runtime patch in it. Only the collision gains a
        /// hole, and it gains it the way a doorway does: the solid box is switched off and four
        /// boxes take its place - left of the opening, right of it, above it, and below it if
        /// the wall reaches under the floor. Walk into any of those and you stop; walk into the
        /// opening and you keep going.
        /// </para>
        ///
        /// <para>
        /// Built in the portal's own frame rather than in world axes, so a wall at any angle
        /// still gets a rectangular hole in the right place.
        /// </para>
        /// </summary>
        private void EnsureWallAperture()
        {
            if (_aperture != null)
                Destroy(_aperture);

            _wallSolid = ResolveWall();
            if (_wallSolid == null)
            {
                CIYCLog.Error(LogTag + "No wall collider found around the opening, so the tear " +
                              "is a picture: the player will walk into a wall they can see " +
                              "through. Assign 'wallCollider' on this component.");
                return;
            }

            Bounds b = _wallSolid.bounds;
            Vector3 right = transform.right, up = transform.up, forward = transform.forward;

            // Extent of the wall's box along each of the portal's axes. The support function of
            // an axis-aligned box, so this is right for a wall of any orientation rather than
            // only for one lined up with the world.
            float halfWide = Support(b.extents, right);
            float halfTall = Support(b.extents, up);
            float thickness = Support(b.extents, forward) * 2f;

            Vector3 offset = b.center - transform.position;
            float cx = Vector3.Dot(offset, right);
            float cy = Vector3.Dot(offset, up);
            float cz = Vector3.Dot(offset, forward);

            float left = cx - halfWide, rightEdge = cx + halfWide;
            float bottom = cy - halfTall, top = cy + halfTall;

            // The hole. The portal's origin sits on the floor and the opening rises from it.
            float ow = style.openingSize.x * 0.5f;
            float oh = style.openingSize.y;

            // Unparented, at the portal's world pose. A child would inherit any scale on this
            // transform and a BoxCollider's size is multiplied by it - which is CLAUDE.md
            // mistake 12, a local size computed through somebody else's scale.
            _aperture = new GameObject("Portal_WallAperture");
            _aperture.transform.SetPositionAndRotation(transform.position, transform.rotation);

            AddPiece(left, -ow, bottom, top, cz, thickness, "Left");
            AddPiece(ow, rightEdge, bottom, top, cz, thickness, "Right");
            AddPiece(-ow, ow, oh, top, cz, thickness, "Header");
            AddPiece(-ow, ow, bottom, 0f, cz, thickness, "Sill");

            SetWallOpen(false);
        }

        /// <summary>One slab of the wall that is left standing. Skipped when it has no width.</summary>
        private void AddPiece(float x0, float x1, float y0, float y1, float z, float thickness,
                              string label)
        {
            float width = x1 - x0;
            float height = y1 - y0;
            if (width <= 0.001f || height <= 0.001f)
                return;

            var go = new GameObject("Wall_" + label);
            go.transform.SetParent(_aperture.transform, false);
            go.transform.localPosition = new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, z);

            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(width, height, Mathf.Max(0.01f, thickness));
        }

        /// <summary>Extent of an axis-aligned box along an arbitrary direction.</summary>
        private static float Support(Vector3 extents, Vector3 direction)
        {
            return Mathf.Abs(extents.x * direction.x) +
                   Mathf.Abs(extents.y * direction.y) +
                   Mathf.Abs(extents.z * direction.z);
        }

        /// <summary>
        /// Open means the wall's own collider is off and the four pieces are on. Closed is the
        /// other way round, and closed is the resting state: a wall with a hole in it that no
        /// portal is holding open is a bug you fall through.
        /// </summary>
        private void SetWallOpen(bool open)
        {
            if (_wallSolid != null)
                _wallSolid.enabled = !open;

            if (_aperture != null)
                _aperture.SetActive(open);
        }

        /// <summary>
        /// The wall the opening is standing in.
        ///
        /// <para>
        /// The serialized reference wins. Without one, the collider whose bounds CONTAIN the
        /// middle of the opening is the wall it is cut into - found by looking rather than by
        /// name, because a hard-coded object name that stops resolving fails silently and
        /// forever, which this repository has now done three times.
        /// </para>
        /// </summary>
        private Collider ResolveWall()
        {
            if (wallCollider != null)
                return wallCollider;

            Vector3 middle = transform.position + transform.up * (style.openingSize.y * 0.5f);

            Collider best = null;
            float bestVolume = float.MaxValue;

            // A physics query at the opening, not a sweep of the scene. FindObjectsByType would
            // walk every collider in the lobby, and this file is forbidden from searching the
            // scene at all - the guard does not distinguish a one-off from a per-frame one, and
            // it is right not to: the cheap version is available and this is it.
            Collider[] candidates = Physics.OverlapBox(
                middle, new Vector3(0.05f, 0.05f, 0.05f), transform.rotation,
                ~0, QueryTriggerInteraction.Ignore);

            foreach (Collider candidate in candidates)
            {
                if (candidate == null)
                    continue;

                // Never the portal's own furniture, and never something the player is carrying.
                if (candidate.transform.IsChildOf(transform))
                    continue;

                Bounds b = candidate.bounds;
                if (!b.Contains(middle))
                    continue;

                float volume = b.size.x * b.size.y * b.size.z;
                if (volume < bestVolume)
                {
                    bestVolume = volume;
                    best = candidate;
                }
            }

            if (best != null)
                CIYCLog.Info(LogTag + "wall resolved to '" + best.name + "' (" +
                             best.bounds.size.ToString("F2") + ").");

            return best;
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
            go.transform.localPosition = new Vector3(0f, style.openingSize.y * 0.5f, 0f);

            // A DISABLED collider, kept only so the aperture is visible in the scene view and
            // so the destabilise path has something to switch off. Nothing listens to it: entry
            // is decided by the plane-crossing test in LateUpdate, because a trigger volume
            // cannot tell walking through from standing near.
            _threshold = go.AddComponent<BoxCollider>();
            _threshold.isTrigger = true;
            _threshold.size = new Vector3(style.openingSize.x, style.openingSize.y,
                                          Mathf.Max(0.05f, entryTriggerDepth));
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
                    Transform anchor = ResolveViewAnchor(_pendingWorld.ArrivalPoint);
                    CIYCLog.Info(LogTag + "preview camera bound to " +
                                 _pendingWorld.ArrivalPoint.name + " at " +
                                 _pendingWorld.ArrivalPoint.position.ToString("F1") +
                                 ", view anchor raised to " + anchor.position.ToString("F1"));
                    surface.SetDestination(anchor);
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

            // The collision follows the picture. Only a portal that is actually open is a hole
            // you can walk into: while it is forming, sealed by a hunt, failed or inactive, the
            // wall is solid again - otherwise a portal that collapsed would leave a doorway
            // shaped like nothing at all, and the player would walk into the far room's back.
            SetWallOpen(next == LobbyPortalState.Open || next == LobbyPortalState.Entering);
        }

        /// <summary>A fresh process holds no portal from the last one.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => Instance = null;
    }

}
