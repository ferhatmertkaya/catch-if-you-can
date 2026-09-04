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

        [Tooltip("Die modulare Innenausbau-Quelle. Sie ersetzt fertige Raum-Prefabs: das " +
                 "Layout bestimmt die Huelle, dieser Katalog liefert die Teile. Leer heisst, " +
                 "dass keine Hausgeometrie gebaut werden kann - und das wird laut gemeldet, " +
                 "nicht durch Ersatz verdeckt.")]
        public ModularInteriorCatalog ModularInterior;

        [Header("Optional Materials")]
        public Material WallMaterial;
        public Material FloorMaterial;
    }
}
