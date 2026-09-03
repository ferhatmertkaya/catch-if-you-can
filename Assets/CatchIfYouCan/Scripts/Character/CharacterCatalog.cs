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
