using System;
using CatchIfYouCan.Procedural;
using UnityEngine;

namespace CatchIfYouCan.Content
{
    /// <summary>
    /// Wozu ein Möbelstück im Raum da ist. Die Einrichtung fragt nach einer ROLLE, der Katalog
    /// antwortet mit dem, was er dafür hat - so wie der Modulkatalog auf eine Wandrolle
    /// antwortet. Ein Profil, das "Sofa" verlangt, muss nichts über Prefab-Namen wissen.
    /// </summary>
    /// <remarks>Nur anhängen. Die Reihenfolge geht in keinen Hash ein, aber in Assets.</remarks>
    public enum FurnitureRole
    {
        None = 0,

        // Sitzen und Wohnen
        Sofa = 1,
        Armchair = 2,
        CoffeeTable = 3,
        SideTable = 4,

        // Essen
        DiningTable = 10,
        DiningChair = 11,
        Sideboard = 12,

        // Schlafen
        Bed = 20,
        Nightstand = 21,
        Wardrobe = 22,
        Dresser = 23,

        // Küche
        KitchenCounter = 30,
        KitchenCupboard = 31,
        Stove = 32,
        Sink = 33,
        Fridge = 34,
        KitchenShelf = 35,

        // Bad
        Bathtub = 40,
        Toilet = 41,
        WashBasin = 42,

        // Arbeiten
        Desk = 50,
        DeskChair = 51,
        Bookshelf = 52,

        // Lager
        StorageShelf = 60,
        Crate = 61,

        // Flur
        Console = 70,
        Mirror = 71,

        // Licht
        FloorLamp = 80,
        TableLamp = 81,
        WallLamp = 82,
        CeilingLamp = 83,

        // Kleinkram auf Flächen
        Clock = 90,
        Telephone = 91,
        Candle = 92,
        Books = 93,
        Picture = 94,
        Clutter = 95,
    }

    /// <summary>Wie ein Stück im Raum steht - und damit, wogegen es geprüft wird.</summary>
    public enum FurnitureAnchor
    {
        /// <summary>Mit dem Rücken an einer Wand, Vorderseite in den Raum.</summary>
        AgainstWall = 0,

        /// <summary>Frei im Raum, z. B. ein Couchtisch vor einem Sofa.</summary>
        FreeStanding = 1,

        /// <summary>Auf der Oberseite eines anderen Möbels.</summary>
        OnSurface = 2,

        /// <summary>An der Wand hängend, in einer Höhe über dem Boden.</summary>
        WallMounted = 3,
    }

    /// <summary>
    /// Ein Einrichtungsgegenstand, wie die Einrichtung ihn braucht.
    ///
    /// <para>
    /// <b>Zwei Quellen, absichtlich.</b> <see cref="Prefab"/> ist eine direkte Referenz und der
    /// Normalfall für gekaufte Packs - die liegen nicht unter <c>Resources</c> und dürfen dort
    /// auch nicht liegen, weil dann jedes Möbelstück in jedem Build landet.
    /// <see cref="ResourcePath"/> ist der Weg für projekteigene Modelle, die schon unter
    /// <c>Resources/Props</c> stehen; der funktioniert ohne Editor und ohne installiertes Pack.
    /// Ist beides gesetzt, gewinnt das Prefab.
    /// </para>
    ///
    /// <para>
    /// <b>Hier stehen keine Maße.</b> Die Größe eines Möbelstücks wird zur Laufzeit an der
    /// Instanz GEMESSEN, nicht aus diesem Asset gelesen. Eine hier eingetippte Zahl wäre eine
    /// zweite Wahrheit neben dem Modell, und die beiden gehen beim ersten Reimport auseinander -
    /// mit dem Ergebnis, dass ein Schrank in einer Lücke steht, in die er nicht passt. Was hier
    /// steht, sind Dinge, die man dem Modell NICHT ansehen kann: wozu es dient, wo es hingehört,
    /// wie viel Platz davor frei bleiben muss.
    /// </para>
    /// </summary>
    [Serializable]
    public struct FurnitureEntry
    {
        [Tooltip("Stabile Kennung. Geht in die deterministische Auswahl ein, also NICHT ändern, " +
                 "ohne zu wissen, dass sich damit die Einrichtung bestehender Seeds ändert.")]
        public string Id;

        public FurnitureRole Role;

        [Tooltip("Direkte Prefab-Referenz. Der Normalfall für gekaufte Packs.")]
        public GameObject Prefab;

        [Tooltip("Alternativ ein Pfad unter einem Resources-Ordner, ohne Endung. Für " +
                 "projekteigene Modelle, die dort ohnehin liegen. Prefab gewinnt.")]
        public string ResourcePath;

        [Tooltip("Optional ein Material, das dem geladenen Modell aufgeprägt wird. Leer heißt: " +
                 "so lassen, wie es importiert wurde.")]
        public Material MaterialOverride;

        [Tooltip("Leer heißt: in jedem Raum erlaubt. Sonst nur in diesen.")]
        public RoomCategory[] Rooms;

        public FurnitureAnchor Anchor;

        [Tooltip("Wie viel Platz vor dem Stück frei bleiben muss, damit man es benutzen kann - " +
                 "die Schranktür, der Platz vor dem Waschbecken, das Herausziehen eines Stuhls.")]
        public float ClearanceFront;

        [Tooltip("Höhe über dem Boden für WallMounted. Bei allem anderen ohne Bedeutung.")]
        public float MountHeight;

        [Tooltip("Auswahlgewicht innerhalb der Rolle. Größer = häufiger. 0 wird als 1 gelesen.")]
        public float Weight;

        [Tooltip("Trägt eine echte Lichtquelle. Nur solche Einträge zählen gegen das " +
                 "Lichtbudget des Raums.")]
        public bool IsLightFixture;

        public bool IsSet => Prefab != null || !string.IsNullOrEmpty(ResourcePath);

        public float ResolvedWeight => Weight > 0.0001f ? Weight : 1f;

        public bool ServesRoom(RoomCategory category)
        {
            if (Rooms == null || Rooms.Length == 0)
                return true;

            for (int i = 0; i < Rooms.Length; i++)
            {
                if (Rooms[i] == category)
                    return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Alles, womit ein Raum eingerichtet werden kann.
    ///
    /// <para>
    /// Getrennt von <see cref="ModularInteriorCatalog"/>, weil das die STRUKTUR liefert - Wand,
    /// Boden, Decke, Tür, Fenster - und diese Grenze bereits trägt: die Struktur kommt aus
    /// generierter Geometrie mit Paket-Oberflächen, die Einrichtung aus fertigen Prefabs. Beides
    /// in ein Asset zu legen hieße, eine Rolle wie "Wand mit Tür" neben eine wie "Sessel" zu
    /// stellen, und die eine wird gebaut, während die andere instanziiert wird.
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Catch If You Can/Room Furnishing Catalog",
                     fileName = "RoomFurnishingCatalog")]
    public class RoomFurnishingCatalog : ScriptableObject
    {
        [Tooltip("Nur für Meldungen. Zur Laufzeit wird nichts über diesen Pfad gesucht.")]
        public string SourceDescription;

        public FurnitureEntry[] Entries = new FurnitureEntry[0];

        [Header("Budgets")]
        [Tooltip("Höchstzahl großer Möbel je angefangene 10 m² Grundfläche.")]
        public float MajorPiecesPerTenSquareMetres = 4f;

        [Tooltip("Höchstzahl kleiner Dekorationsobjekte je angefangene 10 m² Grundfläche.")]
        public float DecorPerTenSquareMetres = 3f;

        [Tooltip("Höchstzahl echter Lichtquellen je Raum. Nicht jede dekorative Lampe bekommt " +
                 "eine; das ist das Budget, gegen das gezählt wird.")]
        public int LightsPerRoom = 1;

        [Tooltip("Wie oft die Einrichtung höchstens einen anderen Platz für dasselbe Stück " +
                 "probiert, bevor sie es aufgibt. Begrenzt und deterministisch.")]
        public int PlacementAttempts = 12;

        [Tooltip("Ausführliche Diagnose je Raum statt einer Zusammenfassung. Für die Suche " +
                 "nach einem einzelnen Platzierungsproblem; standardmäßig aus.")]
        public bool VerboseDiagnostics;

        /// <summary>
        /// Die Einträge, die diese Rolle in diesem Raum spielen können - in STABILER Reihenfolge.
        ///
        /// <para>
        /// Sortiert nach <see cref="FurnitureEntry.Id"/>, nicht nach Inspector-Reihenfolge. Das
        /// Umsortieren eines Arrays im Inspector darf nicht ändern, welches Möbelstück ein Seed
        /// erzeugt - genau dieselbe Regel, die <c>ContentSnapshotFactory</c> für Stage A schon
        /// befolgt.
        /// </para>
        /// </summary>
        public void CollectRole(FurnitureRole role, RoomCategory category,
                                System.Collections.Generic.List<FurnitureEntry> into)
        {
            if (into == null)
                return;

            into.Clear();
            if (Entries == null)
                return;

            for (int i = 0; i < Entries.Length; i++)
            {
                FurnitureEntry entry = Entries[i];
                if (entry.Role != role || !entry.IsSet || !entry.ServesRoom(category))
                    continue;

                into.Add(entry);
            }

            into.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
        }

        public bool HasRole(FurnitureRole role, RoomCategory category)
        {
            if (Entries == null)
                return false;

            for (int i = 0; i < Entries.Length; i++)
            {
                if (Entries[i].Role == role && Entries[i].IsSet && Entries[i].ServesRoom(category))
                    return true;
            }

            return false;
        }
    }
}
