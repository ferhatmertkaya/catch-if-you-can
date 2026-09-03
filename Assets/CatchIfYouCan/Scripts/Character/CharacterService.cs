using UnityEngine;

namespace CatchIfYouCan.Character
{
    /// <summary>
    /// Which character the local player is, and how anything resolves that to content.
    ///
    /// <para>
    /// The selection is a string id and nothing else. That is what a save file can hold,
    /// what a menu can set before any player exists, and what a join handshake could later
    /// carry - a prefab reference is none of those things.
    /// </para>
    ///
    /// <para>
    /// Everything here tolerates having no catalog. Until the character assets are authored
    /// this resolves to null, and the player factory falls back to the path it used before,
    /// so the foundation can land without changing what ships.
    /// </para>
    /// </summary>
    public static class CharacterService
    {
        /// <summary>The chosen character's id, or null for "whatever the catalog defaults to".</summary>
        public static string LocalCharacterId { get; private set; }

        public static void SetLocalCharacter(string characterId)
        {
            LocalCharacterId = characterId;
            _resolved = null;
        }

        private static CharacterDefinition _resolved;
        private static bool _noCatalogReported;

        /// <summary>
        /// The local player's character, or null when no catalog is authored yet.
        ///
        /// Cached, because the player factory asks for it several times while building one
        /// player and a Resources probe per question would be wasteful.
        /// </summary>
        public static CharacterDefinition Resolve()
        {
            if (_resolved != null)
                return _resolved;

            var catalog = Catalog();
            if (catalog == null)
                return null;

            _resolved = catalog.Resolve(LocalCharacterId);
            return _resolved;
        }

        public static CharacterCatalog Catalog()
        {
            var registry = Content.CiycContentRegistry.Load();
            var catalog = registry != null ? registry.CharacterCatalog : null;

            if (catalog == null && !_noCatalogReported)
            {
                _noCatalogReported = true;
                Core.CIYCLog.Warn(
                    "No character catalog in the content registry. The player falls back to " +
                    "the built-in visual path and the built-in rig naming, which is Nathan. " +
                    "Author one with Catch If You Can > Characters > Build Character Assets.");
            }

            return catalog;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            LocalCharacterId = null;
            _resolved = null;
            _noCatalogReported = false;
        }
    }
}
