using CatchIfYouCan.Procedural.Deterministic;
using UnityEngine;

namespace CatchIfYouCan.Procedural
{
    [CreateAssetMenu(fileName = "RoomDefinition", menuName = "Catch If You Can/Procedural/Room Definition")]
    public class RoomDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Frozen id used by deterministic generation and by the layout hash. " +
                 "Leave empty to derive it from the category.")]
        public string StableId;

        public RoomCategory Category = RoomCategory.Hallway;

        [Header("Prefabs")]
        public GameObject[] PrefabVariants;

        [Header("Footprint")]
        public Vector3 Size = new Vector3(6f, 3f, 6f);

        [Header("Selection")]
        [Min(0.01f)] public float Weight = 1f;

        public string ResolveStableId() =>
            !string.IsNullOrEmpty(StableId) ? StableId : "ARCH_" + Category;

        public int VariantCount => PrefabVariants != null && PrefabVariants.Length > 0 ? PrefabVariants.Length : 1;

        /// <summary>
        /// Returns the variant the LAYOUT chose. Stage A picks the index from the
        /// RoomVariants stream; Stage B only looks it up, so instantiation makes no
        /// random choice of its own.
        /// </summary>
        public GameObject GetPrefabVariant(int variantIndex)
        {
            if (PrefabVariants == null || PrefabVariants.Length == 0)
                return null;

            int index = variantIndex % PrefabVariants.Length;
            if (index < 0)
                index += PrefabVariants.Length;

            return PrefabVariants[index];
        }
    }
}
