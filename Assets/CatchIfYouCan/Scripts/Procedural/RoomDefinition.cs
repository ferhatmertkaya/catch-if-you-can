using UnityEngine;

namespace CatchIfYouCan.Procedural
{
    [CreateAssetMenu(fileName = "RoomDefinition", menuName = "Catch If You Can/Procedural/Room Definition")]
    public class RoomDefinition : ScriptableObject
    {
        [Header("Identity")]
        public RoomCategory Category = RoomCategory.Hallway;

        [Header("Prefabs")]
        public GameObject[] PrefabVariants;

        [Header("Footprint")]
        public Vector3 Size = new Vector3(6f, 3f, 6f);

        [Header("Selection")]
        [Min(0.01f)] public float Weight = 1f;

        public GameObject PickPrefab(System.Random rng)
        {
            if (PrefabVariants == null || PrefabVariants.Length == 0)
                return null;

            if (PrefabVariants.Length == 1)
                return PrefabVariants[0];

            int index = rng.Next(0, PrefabVariants.Length);
            return PrefabVariants[index];
        }
    }
}
