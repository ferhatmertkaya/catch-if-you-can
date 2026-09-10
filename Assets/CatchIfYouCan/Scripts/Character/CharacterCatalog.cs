using UnityEngine;

namespace CatchIfYouCan.Character
{
    /// <summary>
    /// Every playable character, and the id-to-character lookup everything else uses.
    ///
    /// <para>
    /// One list rather than a folder scan. A scan answers "whatever happened to be
    /// imported", which is a different set on two machines and therefore a different set on
    /// two clients - the exact thing a content hash exists to catch. An explicit list is
    /// reviewable and orderable, and order is what a byte-sized character index over a wire
    /// would later depend on.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterCatalog", menuName = "Catch If You Can/Character Catalog")]
    public sealed class CharacterCatalog : ScriptableObject
    {
        [Tooltip("Order is meaningful: it is the index a compact network encoding would use.")]
        [SerializeField] private CharacterDefinition[] characters = new CharacterDefinition[0];

        [Tooltip("Used when a save carries no choice, or an id nothing answers to.")]
        [SerializeField] private string defaultCharacterId = "nathan";

        public CharacterDefinition[] Characters => characters;
        public string DefaultCharacterId => defaultCharacterId;
        public int Count => characters != null ? characters.Length : 0;

        public CharacterDefinition Resolve(string characterId)
        {
            if (characters == null)
                return null;

            if (!string.IsNullOrEmpty(characterId))
            {
                for (int i = 0; i < characters.Length; i++)
                {
                    if (characters[i] != null &&
                        string.Equals(characters[i].Id, characterId, System.StringComparison.Ordinal))
                        return characters[i];
                }

                // Naming the id matters: a save carrying a character that was renamed or
                // removed should say which one, not quietly become somebody else.
                Core.CIYCLog.Warn("No character with id '" + characterId + "' in " + name +
                                  "; falling back to '" + defaultCharacterId + "'.");
            }

            for (int i = 0; i < characters.Length; i++)
            {
                if (characters[i] != null &&
                    string.Equals(characters[i].Id, defaultCharacterId, System.StringComparison.Ordinal))
                    return characters[i];
            }

            return characters.Length > 0 ? characters[0] : null;
        }

        /// <summary>
        /// The character at a compact index, with the index validated first.
        ///
        /// <para>
        /// The counterpart to <see cref="IndexOf"/> and the one place an index that came from
        /// another machine is turned back into a character. It goes through
        /// <see cref="Procedural.Deterministic.CharacterSelection"/> rather than indexing the
        /// array, because an index off a wire is a claim: 4000, -1 and 255 are all things a
        /// peer can send, and none of them may reach <c>characters[i]</c>.
        /// </para>
        /// </summary>
        public CharacterDefinition ResolveIndex(int index)
        {
            int resolved = Procedural.Deterministic.CharacterSelection.Resolve(index, Count);

            if (resolved == Procedural.Deterministic.CharacterSelection.Unset)
                return null;

            // Resolve guarantees this is inside the array. The null check is for a slot that
            // was left empty in the asset, which is an authoring mistake rather than a claim.
            var character = characters[resolved];
            return character != null ? character : Resolve(defaultCharacterId);
        }

        /// <summary>
        /// Whether every character in this catalog can be named over a wire.
        ///
        /// <para>
        /// False means a character past the encoding limit exists and would silently become
        /// somebody else on another machine. A content problem, checked where the content is.
        /// </para>
        /// </summary>
        public bool FitsCompactEncoding =>
            Count <= Procedural.Deterministic.CharacterSelection.MaxCharacters;

        public int IndexOf(string characterId)
        {
            if (characters == null)
                return -1;

            for (int i = 0; i < characters.Length; i++)
                if (characters[i] != null &&
                    string.Equals(characters[i].Id, characterId, System.StringComparison.Ordinal))
                    return i;

            return -1;
        }
    }
}
