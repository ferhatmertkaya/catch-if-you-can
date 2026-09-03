using System.Collections;
using CatchIfYouCan.Art;
using CatchIfYouCan.Core;
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

        /// <summary>The investigation is loading.</summary>
        Loading,

        /// <summary>Shut deliberately - the mission was cancelled, or the lobby is going away.</summary>
        Closed
    }

    /// <summary>
    /// The lobby doorway, and the one thing that decides whether it is a hole in a wall or a
    /// way into a mission.
    ///
    /// <para>
    /// <b>Why this exists at all.</b> START INVESTIGATION used to call
    /// <c>SceneLoader.LoadInvestigation()</c> directly. That is a scene load: the lobby, the
    /// doorway and the player all cease to exist, a loading screen covers the cut, and the
    /// player arrives somewhere else without ever having walked anywhere. There was no portal
    /// in that path - not a broken one, not a hidden one, none - which is why pressing the
    /// button produced no portal no matter what was wrong with the portal code.
    /// <see cref="PortalSurface"/> and <see cref="ReferenceApartment"/> both existed and were
    /// referenced by nothing.
    /// </para>
    ///
    /// <para>
    /// <b>Accepting a mission opens the door. Walking through it starts the investigation.</b>
    /// Those are two separate moments on purpose, and the second one is the player's: the
    /// portal never moves them and never loads anything until they step into the opening.
    /// </para>
    ///
    /// <para>
    /// <b>It renders one apartment, honestly.</b> The far side is
    /// <see cref="ReferenceApartment"/> - a real, walkable, two-storey interior built off to the
    /// side of the lobby in the same scene, because a portal is a second camera rendering real
    /// geometry and a scene that has not been loaded has no geometry to render. It is
    /// <b>not</b> the generated house the investigation itself builds. What the player sees
    /// through the door is a true interior of the right scale, lighting and layout language;
    /// it is not a preview of the specific rooms they are about to search. Making it one means
    /// running the deterministic generator here, from the mission's seed, and that is a
    /// separate piece of work - see <c>Docs/TWO_FLOOR_GENERATION.md</c>.
    /// </para>
    ///
    /// <para>
    /// <b>Nothing here is silent.</b> Every way this can fail to open says so, at error level,
    /// with the reason and the fix. A button that appears to do nothing is the failure this
    /// class was written to end.
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

        [Header("Destination")]
        [Tooltip("Where the far apartment is built, relative to this portal. Far enough that " +
                 "nothing of it can be seen from inside the lobby by any route except the " +
                 "portal itself.")]
        [SerializeField] private Vector3 destinationOffset = new Vector3(0f, -400f, 0f);

        [Header("Opening")]
        [Tooltip("How long the edge takes to come up to full. The view behind it is live from " +
                 "the first frame; this is the energy at the rim, not a fade of the room.")]
        [SerializeField, Min(0f)] private float openDuration = 1.1f;

        [SerializeField, Min(0f)] private float openRimIntensity = 2.4f;

        [Header("Threshold")]
        [Tooltip("The volume that counts as walking through. Sits in the opening, not past it.")]
        [SerializeField] private Vector3 entryTriggerSize = new Vector3(1.4f, 2.4f, 0.8f);

        private ReferenceApartment _destination;
        private BoxCollider _threshold;
        private Coroutine _opening;
        private string _missionName;

        /// <summary>What the doorway is doing. Read by the UI to decide what to say.</summary>
        public LobbyPortalState State { get; private set; } = LobbyPortalState.Inactive;

        /// <summary>True while the far room is visible and can be entered.</summary>
        public bool IsOpen => State == LobbyPortalState.Open;

        /// <summary>The mission this portal was opened for, or null.</summary>
        public string MissionName => _missionName;

        // ---- the one entry point from the interface ------------------------------------------

        /// <summary>
        /// Opens the lobby doorway onto the accepted mission.
        ///
        /// <para>
        /// Returns false and says why when it cannot - there is no portal in this scene, or its
        /// surface could not be built. The caller is expected to tell the player rather than
        /// close the menu onto an unchanged wall.
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
            // scene load that follows it destroys this object without ever closing a screen.
            // MenuInputGate is static and outlives the scene, so a hold left behind would keep
            // the next scene's player locked out of their own controls. Popping something that
            // is not held is a no-op, so this is safe on every other teardown too.
            UI.MenuInputGate.Pop(nameof(LobbyPortal));
        }

        /// <summary>
        /// Builds the rendered surface as a child, unless one was assigned in the scene.
        ///
        /// <para>
        /// It starts inactive. An idle lobby then costs nothing: no second camera, no render
        /// texture written, no LateUpdate. The buffer and the camera are allocated the first
        /// time the door opens and are kept for the rest of the session, because a player who
        /// backs out of mission select and returns should not pay for a reallocation.
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
            _missionName = missionName;
            SetState(LobbyPortalState.MissionSelected);

            if (!EnsureDestination())
            {
                SetState(LobbyPortalState.Inactive);
                return false;
            }

            if (surface == null)
            {
                CIYCLog.Error(LogTag + "Mission '" + missionName + "' accepted but the portal " +
                              "has no surface to render. The doorway stays shut.");
                SetState(LobbyPortalState.Inactive);
                return false;
            }

            surface.SetDestination(_destination.transform);
            surface.SetRimIntensity(0f);
            surface.gameObject.SetActive(true);

            if (!surface.IsBuilt)
            {
                // Build happens in the surface's own Start, on the frame after activation. Not
                // an error - just worth knowing which frame the opening actually appears on.
                CIYCLog.Info(LogTag + "Surface activated; it builds on its first frame.");
            }

            SetState(LobbyPortalState.Opening);

            if (_opening != null)
                StopCoroutine(_opening);
            _opening = StartCoroutine(RaiseEdge());

            CIYCLog.Info(LogTag + "Opening for mission '" + missionName + "'. Walk through the " +
                         "lobby doorway to begin.");
            return true;
        }

        /// <summary>
        /// Brings the edge up over <see cref="openDuration"/>. The room behind is live
        /// throughout; what animates is the energy at the rim, which is what makes the doorway
        /// read as opening rather than as a picture being switched on.
        /// </summary>
        private IEnumerator RaiseEdge()
        {
            float t = 0f;
            while (t < openDuration && surface != null)
            {
                t += Time.deltaTime;
                float k = openDuration <= 0f ? 1f : Mathf.Clamp01(t / openDuration);
                // Eased, so it swells rather than ramping linearly.
                surface.SetRimIntensity(openRimIntensity * (k * k * (3f - 2f * k)));
                yield return null;
            }

            if (surface != null)
                surface.SetRimIntensity(openRimIntensity);

            _opening = null;
            SetState(LobbyPortalState.Open);
        }

        /// <summary>
        /// Builds the far apartment, once, and keeps it for the session.
        ///
        /// <para>
        /// Built on demand rather than left standing in the lobby scene: ten rooms of geometry
        /// and a lamp each are not something to carry while the player is only reading a
        /// noticeboard.
        /// </para>
        /// </summary>
        private bool EnsureDestination()
        {
            if (_destination != null)
                return true;

            var go = new GameObject("Portal_Destination_Apartment");
            go.transform.SetParent(null, true);

            // Placed relative to the portal, keeping the portal's own facing, so the pair's
            // maths does not depend on where the lobby happens to sit in world space.
            go.transform.SetPositionAndRotation(
                transform.position + transform.rotation * destinationOffset,
                transform.rotation * Quaternion.Euler(0f, 180f, 0f));

            _destination = go.AddComponent<ReferenceApartment>();

            // AddComponent runs Awake, which builds the flat, so by this line the geometry
            // either exists or it does not. Checked rather than assumed: a portal onto an empty
            // transform renders the skybox and reads as a bright hole in the wall, which looks
            // like a shader bug and is not one.
            if (go.GetComponentsInChildren<MeshRenderer>(true).Length == 0)
            {
                CIYCLog.Error(LogTag + "The destination apartment built no geometry, so the " +
                              "doorway would open onto an empty skybox. Keeping it shut. Check " +
                              "ReferenceApartment and ApartmentShell.");
                Destroy(go);
                _destination = null;
                return false;
            }

            return true;
        }

        // ---- crossing ----------------------------------------------------------------------------

        /// <summary>
        /// Called by the threshold when something enters it. Only the local player counts, and
        /// only while the portal is actually open.
        /// </summary>
        internal void ThresholdEntered(Collider other)
        {
            if (State != LobbyPortalState.Open || other == null)
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
            SetState(LobbyPortalState.Entering);

            // The controls go before the load, not after: a player still walking when the
            // loading screen comes up arrives in the investigation mid-stride.
            UI.MenuInputGate.Push(nameof(LobbyPortal));

            if (SceneLoader.Instance == null)
            {
                CIYCLog.Error(LogTag + "Player entered the portal but there is no SceneLoader, " +
                              "so the investigation cannot be loaded. SceneLoader lives on the " +
                              "boot object and persists; starting from " +
                              CiycScenes.MainMenu + " directly skips it.");
                UI.MenuInputGate.Pop(nameof(LobbyPortal));
                SetState(LobbyPortalState.Open);
                return;
            }

            SetState(LobbyPortalState.Loading);
            CIYCLog.Info(LogTag + "Entered. Loading the investigation for '" +
                         (_missionName ?? "unnamed mission") + "'.");
            SceneLoader.Instance.LoadInvestigation();
        }

        // ---- closing -----------------------------------------------------------------------------

        /// <summary>
        /// Shuts the doorway and takes the far apartment down with it. For a cancelled mission,
        /// or a lobby that is going away.
        /// </summary>
        public void Close(string reason)
        {
            if (State == LobbyPortalState.Inactive || State == LobbyPortalState.Closed)
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

            if (_destination != null)
            {
                Destroy(_destination.gameObject);
                _destination = null;
            }

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
