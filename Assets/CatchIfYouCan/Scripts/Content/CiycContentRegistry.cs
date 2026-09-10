using UnityEngine;

namespace CatchIfYouCan.Content
{
    /// <summary>
    /// The one asset the game loads by path; everything else it reaches through direct
    /// references from here.
    ///
    /// <para>
    /// The project reached content through a dozen <c>Resources.Load</c> calls with
    /// hard-coded strings, four of which pointed at folders that do not exist and failed
    /// silently. Strings are also why everything under Resources ships whether or not it is
    /// used - Unity cannot see which of them are reachable. One registry with real
    /// references inverts that: content is included because something points at it, and a
    /// missing reference is a null in the inspector rather than a runtime surprise.
    /// </para>
    ///
    /// <para>
    /// It stays under Resources itself because something has to be findable without a scene
    /// reference, and one entry is the smallest that can be. If this later becomes an
    /// Addressable, only this class changes.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "CIYC_ContentRegistry",
                     menuName = "Catch If You Can/Content Registry")]
    public sealed class CiycContentRegistry : ScriptableObject
    {
        /// <summary>Resources path, without extension. One lookup for the whole project.</summary>
        public const string ResourcePath = "CIYC_ContentRegistry";

        [Header("Player")]
        [Tooltip("The character-independent player rig. Left empty, the player is built in " +
                 "code from PlayerRigBuilder instead, which produces the same hierarchy.")]
        [SerializeField] private GameObject playerPrefab;

        [Header("Characters")]
        [Tooltip("Every playable character. The local selection is stored as an id and " +
                 "resolved through this.")]
        [SerializeField] private Character.CharacterCatalog characterCatalog;

        [Header("Equipment")]
        [Tooltip("Every equipment definition, in one place, so the shop, the loadout and a " +
                 "future content hash all read the same list.")]
        [SerializeField] private Equipment.EquipmentCatalog equipmentCatalog;

        [Header("Ghosts")]
        [SerializeField] private ScriptableObject ghostCatalog;

        public GameObject PlayerPrefab => playerPrefab;
        public Character.CharacterCatalog CharacterCatalog => characterCatalog;
        public Equipment.EquipmentCatalog EquipmentCatalog => equipmentCatalog;
        public ScriptableObject GhostCatalogRaw => ghostCatalog;

        private static CiycContentRegistry _cached;
        private static bool _missingReported;

        /// <summary>
        /// The registry, or null. Cached, because a null result is the common case until the
        /// asset is authored and re-asking every spawn would mean a Resources probe per
        /// player.
        /// </summary>
        public static CiycContentRegistry Load()
        {
            if (_cached != null)
                return _cached;

            _cached = Resources.Load<CiycContentRegistry>(ResourcePath);

            if (_cached == null && !_missingReported)
            {
                _missingReported = true;
                Core.CIYCLog.Warn(
                    "No content registry at Resources/" + ResourcePath + ". The game falls " +
                    "back to building content in code, which works but means no authored " +
                    "player prefab, character catalog or equipment catalog is in use. " +
                    "Create it with Catch If You Can > Content > Create Content Registry.");
            }

            return _cached;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            _cached = null;
            _missingReported = false;
        }
    }
}
