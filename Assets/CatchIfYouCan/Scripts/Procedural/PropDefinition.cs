using CatchIfYouCan.Procedural.Deterministic;
using UnityEngine;

namespace CatchIfYouCan.Procedural
{
    [CreateAssetMenu(fileName = "PropDefinition", menuName = "Catch If You Can/Procedural/Prop Definition")]
    public class PropDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Frozen id used by deterministic generation and by the layout hash. " +
                 "Never reuse or repurpose an id: it is content identity, not a display name. " +
                 "Leave empty to fall back to PropName.")]
        public string StableId;

        public string PropName = "Furniture";

        [Tooltip("Furniture and small props are placed in separate passes from separate RNG " +
                 "streams, and are hashed as separate sections.")]
        public PropKind Kind = PropKind.Prop;

        [Header("Prefab")]
        public GameObject Prefab;

        [Header("Tags")]
        public string[] CategoryTags;

        [Header("Placement")]
        public Vector3 BoundsSize = Vector3.one;
        public float Weight = 1f;

        /// <summary>
        /// Content identity for generation. An array index is NOT usable here: the
        /// generator's propDefinitions array is inspector-ordered, so indices renumber on
        /// every reorder and would change layouts for stored seeds.
        /// </summary>
        public string ResolveStableId()
        {
            if (!string.IsNullOrEmpty(StableId))
                return StableId;
            if (!string.IsNullOrEmpty(PropName))
                return PropName;
            return name;
        }

        public bool MatchesRoom(RoomCategory category)
        {
            if (CategoryTags == null || CategoryTags.Length == 0)
                return true;

            string roomTag = category.ToString();
            for (int i = 0; i < CategoryTags.Length; i++)
            {
                if (string.Equals(CategoryTags[i], roomTag, System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(CategoryTags[i], "Any", System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
