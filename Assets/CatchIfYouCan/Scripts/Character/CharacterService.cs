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

        /// <summary>
        /// The local choice as the compact index another machine would be sent.
        ///
        /// <para>
        /// Derived from the id rather than stored beside it. Two fields for one fact is how
        /// they end up disagreeing after the catalog is reordered - the id is the identity,
        /// and the index is a view of it that is only valid against a particular catalog.
        /// </para>
        ///
        /// <para>
        /// <see cref="Procedural.Deterministic.CharacterSelection.Unset"/> when nothing has
        /// been chosen, when there is no catalog, or when the chosen id is not in it. The
        /// receiving host substitutes its own default for all three, which is the same
        /// outcome the local fallback in <see cref="CharacterCatalog.Resolve"/> produces.
        /// </para>
        /// </summary>
        public static int LocalCharacterIndex
        {
            get
            {
                var catalog = Catalog();
                if (catalog == null)
                    return Procedural.Deterministic.CharacterSelection.Unset;

                int index = catalog.IndexOf(LocalCharacterId);
                return index >= 0 ? index : Procedural.Deterministic.CharacterSelection.Unset;
            }
        }

        /// <summary>
        /// Chooses by compact index - what a lobby row hands back, and what arrives from
        /// another machine.
        ///
        /// <para>
        /// Validated through the catalog rather than indexed, so an index from a peer or from
        /// a stale save cannot reach the array. An index nothing answers to leaves the
        /// selection alone rather than clearing it, because losing a choice silently is worse
        /// than ignoring an impossible one.
        /// </para>
        /// </summary>
        public static void SetLocalCharacterIndex(int index)
        {
            var catalog = Catalog();
            if (catalog == null)
                return;

            var verdict = Procedural.Deterministic.CharacterSelection.Check(index, catalog.Count);
            if (!Procedural.Deterministic.CharacterSelection.IsAccepted(verdict))
            {
                Core.CIYCLog.Warn(
                    "Character index " + index + " was refused: " +
                    Procedural.Deterministic.CharacterSelection.Describe(verdict) +
                    " The selection is unchanged.");
                return;
            }

            var character = catalog.ResolveIndex(index);
            if (character != null)
                SetLocalCharacter(character.Id);
        }

        private static CharacterDefinition _resolved;
        private static bool _noCatalogReported;
        private static bool _tooLargeReported;

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

            // A character past the encoding limit cannot be named over a wire and would
            // silently be somebody else on another machine. Said once, here, because this is
            // the only place the catalog is obtained.
            if (catalog != null && !catalog.FitsCompactEncoding && !_tooLargeReported)
            {
                _tooLargeReported = true;
                Core.CIYCLog.Error(
                    "The character catalog holds " + catalog.Count + " characters, more than " +
                    Procedural.Deterministic.CharacterSelection.MaxCharacters +
                    ". Everything past that cannot be named over a wire and would appear as " +
                    "a different character to other players.");
            }

            return catalog;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            LocalCharacterId = null;
            _resolved = null;
            _noCatalogReported = false;
            _tooLargeReported = false;
        }
    }
}
