using System.Collections.Generic;
using UnityEngine;

namespace CatchIfYouCan.Procedural
{
    [CreateAssetMenu(fileName = "PropDefinition", menuName = "Catch If You Can/Procedural/Prop Definition")]
    public class PropDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string PropName = "Furniture";

        [Header("Prefab")]
        public GameObject Prefab;

        [Header("Tags")]
        public string[] CategoryTags;

        [Header("Placement")]
        public Vector3 BoundsSize = Vector3.one;
        public float Weight = 1f;

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
