using CatchIfYouCan.Procedural;
using UnityEngine;

namespace CatchIfYouCan.Content
{
    [CreateAssetMenu(fileName = "InvestigationContentCatalog", menuName = "Catch If You Can/Content/Investigation Content Catalog")]
    public class InvestigationContentCatalog : ScriptableObject
    {
        [Header("House Generation")]
        public PropDefinition[] PropDefinitions;
        public RoomDefinition[] RoomDefinitions;
        public GameObject DoorPrefab;

        [Header("Optional Materials")]
        public Material WallMaterial;
        public Material FloorMaterial;
    }
}
