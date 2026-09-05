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

        [Header("Room Surfaces")]
        [Tooltip("Die Materialien fuer die vom Code gebaute Raumhuelle. Sie werden pro " +
                 "Flaeche gekachelt: eine Kachel je Meter, wie in MAT_Room_Wall authored " +
                 "(5.3 Kacheln ueber eine 5.3 m breite Wand). Fehlen sie, bleiben die " +
                 "Raeume einfarbig - das ist kein Fehler, aber es sieht aus wie eine " +
                 "gescheiterte Migration und wird deshalb gemeldet.")]
        public Material WallMaterial;
        public Material FloorMaterial;
        public Material CeilingMaterial;
        public Material TrimMaterial;
    }
}
