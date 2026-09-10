using CatchIfYouCan.Content;
using CatchIfYouCan.Procedural.Deterministic;

namespace CatchIfYouCan.Procedural.Furnishing
{
    /// <summary>Wie ein Gruppenmitglied zu seinem Ankermöbel steht.</summary>
    public enum GroupPattern
    {
        /// <summary>Davor, in Blickrichtung des Ankers.</summary>
        InFront = 0,

        /// <summary>Links und rechts daneben, abwechselnd.</summary>
        Beside = 1,

        /// <summary>Rundherum an den Seiten, wie Stühle an einem Tisch.</summary>
        Around = 2,
    }

    /// <summary>Ein Möbel, das nur zusammen mit einem anderen Sinn ergibt.</summary>
    public struct GroupSlot
    {
        public FurnitureRole Anchor;
        public FurnitureRole Member;
        public GroupPattern Pattern;
        public int Count;

        /// <summary>Abstand von der Kante des Ankers zur Kante des Mitglieds, in Metern.</summary>
        public float Gap;

        /// <summary>Fehlt zu wenig Platz, fällt genau das hier zuerst weg.</summary>
        public bool Optional;
    }

    /// <summary>Eine vollständige Einrichtungsidee für einen Raum.</summary>
    public struct FurnishingVariant
    {
        public string Name;

        /// <summary>Unter dieser Bodenfläche wird die Variante gar nicht erst versucht.</summary>
        public float MinFloorArea;

        /// <summary>Ohne diese Rollen ist die Variante nicht sie selbst. Fehlt eine im Katalog
        /// oder passt sie nicht hinein, wird die ganze Variante verworfen.</summary>
        public FurnitureRole[] Core;

        /// <summary>Was relativ zu einem Kernmöbel steht.</summary>
        public GroupSlot[] Groups;

        /// <summary>Nice to have, in dieser Reihenfolge versucht.</summary>
        public FurnitureRole[] Optional;

        /// <summary>Leuchten. Gegen das Lichtbudget des Katalogs gezählt.</summary>
        public FurnitureRole[] Lights;

        /// <summary>Kleinkram, der auf schon platzierten Flächen landet.</summary>
        public FurnitureRole[] Decor;
    }

    /// <summary>
    /// Die Einrichtungsprofile: was in welchem Raum steht und wie es zueinander steht.
    ///
    /// <para>
    /// <b>Warum als Code und nicht als Asset.</b> Das hier sind REGELN, keine Inhalte. Welche
    /// Prefabs es gibt, steht im <see cref="RoomFurnishingCatalog"/> und gehört dorthin, weil es
    /// sich mit dem Paket ändert. Dass ein Esszimmer einen Tisch mit Stühlen ringsum hat, ändert
    /// sich nicht mit dem Paket - und eine Regel, die im Inspector steht, ist eine Regel, die
    /// jemand versehentlich leeren kann, ohne dass es auffällt. Genau das ist in diesem Projekt
    /// schon einmal passiert (CLAUDE.md Fehler 18).
    /// </para>
    ///
    /// <para>
    /// <b>Reihenfolge ist bedeutungstragend.</b> Die Varianten einer Kategorie werden in dieser
    /// Reihenfolge nummeriert und deterministisch gewählt; eine dazwischenzuschieben ändert die
    /// Einrichtung bestehender Seeds. Anhängen ändert sie nicht.
    /// </para>
    ///
    /// <para>
    /// Engine-frei bis auf die Aufzählungen: nichts hier kennt einen GameObject, misst etwas oder
    /// entscheidet, ob etwas passt. Das tut <see cref="RoomFurnisher"/> mit dem gemessenen Modell
    /// in der Hand.
    /// </para>
    /// </summary>
    public static class FurnishingProfiles
    {
        private static readonly FurnishingVariant[] Empty = new FurnishingVariant[0];

        /// <summary>
        /// Die Varianten für diese Raumart, in stabiler Reihenfolge.
        ///
        /// <para>
        /// Eine Kategorie ohne Profil bekommt ausdrücklich ein leeres Array und wird gemeldet -
        /// sie bekommt NICHT heimlich das Profil einer anderen. Einem Heizungsraum die
        /// Einrichtung eines Wohnzimmers zu geben, sähe im Editor nach Fortschritt aus und im
        /// Spiel nach einem Fehler, den niemand mehr zuordnen kann.
        /// </para>
        /// </summary>
        public static FurnishingVariant[] For(RoomCategory category)
        {
            switch (category)
            {
                case RoomCategory.LivingRoom: return LivingRoom;
                case RoomCategory.DiningRoom: return DiningRoom;
                case RoomCategory.Bedroom: return Bedroom;
                case RoomCategory.KidsRoom: return KidsRoom;
                case RoomCategory.Kitchen: return Kitchen;
                case RoomCategory.Bathroom: return Bathroom;
                case RoomCategory.Office: return Office;
                case RoomCategory.Hallway: return Hallway;
                case RoomCategory.Entrance: return Entrance;
                case RoomCategory.Storage: return Storage;
                case RoomCategory.Basement: return Storage;
                case RoomCategory.Attic: return Storage;
                case RoomCategory.Laundry: return Utility;
                case RoomCategory.UtilityRoom: return Utility;
                case RoomCategory.Garage: return Utility;
                default: return Empty;
            }
        }

        // ---------------------------------------------------------------- Wohnzimmer

        private static readonly FurnishingVariant[] LivingRoom =
        {
            new FurnishingVariant
            {
                Name = "Sitzgruppe am Kamin",
                MinFloorArea = 12f,
                Core = new[] { FurnitureRole.Sofa, FurnitureRole.CoffeeTable },
                Groups = new[]
                {
                    // Der Couchtisch gehört VOR das Sofa. Ihn frei im Raum zu platzieren wäre
                    // dieselbe Zufallsverteilung, die diese ganze Arbeit ersetzen soll.
                    new GroupSlot { Anchor = FurnitureRole.Sofa, Member = FurnitureRole.CoffeeTable,
                                    Pattern = GroupPattern.InFront, Count = 1, Gap = 0.45f },
                    new GroupSlot { Anchor = FurnitureRole.CoffeeTable, Member = FurnitureRole.Armchair,
                                    Pattern = GroupPattern.Beside, Count = 2, Gap = 0.55f, Optional = true },
                },
                Optional = new[] { FurnitureRole.Dresser, FurnitureRole.Bookshelf, FurnitureRole.SideTable },
                Lights = new[] { FurnitureRole.FloorLamp, FurnitureRole.CeilingLamp },
                Decor = new[] { FurnitureRole.Clock, FurnitureRole.Candle, FurnitureRole.Books },
            },
            new FurnishingVariant
            {
                Name = "Kleiner Salon",
                MinFloorArea = 0f,
                Core = new[] { FurnitureRole.Armchair },
                Groups = new[]
                {
                    new GroupSlot { Anchor = FurnitureRole.Armchair, Member = FurnitureRole.SideTable,
                                    Pattern = GroupPattern.Beside, Count = 1, Gap = 0.25f, Optional = true },
                },
                Optional = new[] { FurnitureRole.Bookshelf },
                Lights = new[] { FurnitureRole.FloorLamp },
                Decor = new[] { FurnitureRole.Candle, FurnitureRole.Telephone },
            },
        };

        // ---------------------------------------------------------------- Esszimmer

        private static readonly FurnishingVariant[] DiningRoom =
        {
            new FurnishingVariant
            {
                Name = "Esstisch mit Stuehlen",
                MinFloorArea = 10f,
                Core = new[] { FurnitureRole.DiningTable },
                Groups = new[]
                {
                    new GroupSlot { Anchor = FurnitureRole.DiningTable, Member = FurnitureRole.DiningChair,
                                    Pattern = GroupPattern.Around, Count = 4, Gap = 0.12f },
                },
                Optional = new[] { FurnitureRole.Sideboard, FurnitureRole.Dresser },
                Lights = new[] { FurnitureRole.CeilingLamp, FurnitureRole.WallLamp },
                Decor = new[] { FurnitureRole.Candle, FurnitureRole.Clock },
            },
            new FurnishingVariant
            {
                Name = "Essecke",
                MinFloorArea = 0f,
                Core = new[] { FurnitureRole.DiningTable },
                Groups = new[]
                {
                    new GroupSlot { Anchor = FurnitureRole.DiningTable, Member = FurnitureRole.DiningChair,
                                    Pattern = GroupPattern.Around, Count = 2, Gap = 0.12f },
                },
                Optional = new FurnitureRole[0],
                Lights = new[] { FurnitureRole.WallLamp },
                Decor = new[] { FurnitureRole.Candle },
            },
        };

        // ---------------------------------------------------------------- Schlafzimmer

        private static readonly FurnishingVariant[] Bedroom =
        {
            new FurnishingVariant
            {
                Name = "Schlafzimmer mit Schrank",
                MinFloorArea = 11f,
                Core = new[] { FurnitureRole.Bed, FurnitureRole.Wardrobe },
                Groups = new[]
                {
                    new GroupSlot { Anchor = FurnitureRole.Bed, Member = FurnitureRole.Nightstand,
                                    Pattern = GroupPattern.Beside, Count = 2, Gap = 0.08f, Optional = true },
                },
                Optional = new[] { FurnitureRole.Dresser, FurnitureRole.Mirror },
                Lights = new[] { FurnitureRole.TableLamp, FurnitureRole.CeilingLamp },
                Decor = new[] { FurnitureRole.Candle, FurnitureRole.Picture },
            },
            new FurnishingVariant
            {
                Name = "Kammer",
                MinFloorArea = 0f,
                Core = new[] { FurnitureRole.Bed },
                Groups = new[]
                {
                    new GroupSlot { Anchor = FurnitureRole.Bed, Member = FurnitureRole.Nightstand,
                                    Pattern = GroupPattern.Beside, Count = 1, Gap = 0.08f, Optional = true },
                },
                Optional = new[] { FurnitureRole.Dresser },
                Lights = new[] { FurnitureRole.TableLamp },
                Decor = new[] { FurnitureRole.Candle },
            },
        };

        private static readonly FurnishingVariant[] KidsRoom =
        {
            new FurnishingVariant
            {
                Name = "Kinderzimmer",
                MinFloorArea = 0f,
                Core = new[] { FurnitureRole.Bed },
                Groups = new GroupSlot[0],
                Optional = new[] { FurnitureRole.Dresser, FurnitureRole.Bookshelf },
                Lights = new[] { FurnitureRole.TableLamp },
                Decor = new[] { FurnitureRole.Clutter, FurnitureRole.Books },
            },
        };

        // ---------------------------------------------------------------- Küche

        private static readonly FurnishingVariant[] Kitchen =
        {
            new FurnishingVariant
            {
                Name = "Kueche mit Essplatz",
                MinFloorArea = 13f,
                Core = new[] { FurnitureRole.KitchenCounter, FurnitureRole.Stove },
                Groups = new[]
                {
                    new GroupSlot { Anchor = FurnitureRole.DiningTable, Member = FurnitureRole.DiningChair,
                                    Pattern = GroupPattern.Around, Count = 2, Gap = 0.12f, Optional = true },
                },
                Optional = new[] { FurnitureRole.Sink, FurnitureRole.Fridge, FurnitureRole.KitchenCupboard,
                                   FurnitureRole.DiningTable, FurnitureRole.KitchenShelf },
                Lights = new[] { FurnitureRole.CeilingLamp },
                Decor = new[] { FurnitureRole.Clutter },
            },
            new FurnishingVariant
            {
                Name = "Schmale Kueche",
                MinFloorArea = 0f,
                // Kein erzwungener Esstisch: eine kleine Küche bekommt eine reduzierte
                // Ausstattung, statt einen Tisch hineingedrückt zu bekommen, der den Weg nimmt.
                Core = new[] { FurnitureRole.KitchenCounter },
                Groups = new GroupSlot[0],
                Optional = new[] { FurnitureRole.Sink, FurnitureRole.Stove, FurnitureRole.KitchenShelf },
                Lights = new[] { FurnitureRole.CeilingLamp },
                Decor = new[] { FurnitureRole.Clutter },
            },
        };

        // ---------------------------------------------------------------- Bad

        private static readonly FurnishingVariant[] Bathroom =
        {
            new FurnishingVariant
            {
                Name = "Bad mit Wanne",
                MinFloorArea = 8f,
                Core = new[] { FurnitureRole.Bathtub, FurnitureRole.WashBasin },
                Groups = new GroupSlot[0],
                Optional = new[] { FurnitureRole.Toilet, FurnitureRole.Mirror },
                Lights = new[] { FurnitureRole.WallLamp },
                Decor = new FurnitureRole[0],
            },
            new FurnishingVariant
            {
                Name = "Kleines Bad",
                MinFloorArea = 0f,
                Core = new[] { FurnitureRole.WashBasin },
                Groups = new GroupSlot[0],
                Optional = new[] { FurnitureRole.Toilet, FurnitureRole.Mirror },
                Lights = new[] { FurnitureRole.WallLamp },
                Decor = new FurnitureRole[0],
            },
        };

        // ---------------------------------------------------------------- Arbeitszimmer

        private static readonly FurnishingVariant[] Office =
        {
            new FurnishingVariant
            {
                Name = "Arbeitszimmer",
                MinFloorArea = 0f,
                Core = new[] { FurnitureRole.Desk },
                Groups = new[]
                {
                    new GroupSlot { Anchor = FurnitureRole.Desk, Member = FurnitureRole.DeskChair,
                                    Pattern = GroupPattern.InFront, Count = 1, Gap = 0.20f },
                },
                Optional = new[] { FurnitureRole.Bookshelf, FurnitureRole.Dresser },
                Lights = new[] { FurnitureRole.TableLamp, FurnitureRole.FloorLamp },
                Decor = new[] { FurnitureRole.Books, FurnitureRole.Telephone, FurnitureRole.Candle },
            },
        };

        // ---------------------------------------------------------------- Flur und Diele

        private static readonly FurnishingVariant[] Hallway =
        {
            new FurnishingVariant
            {
                Name = "Flur",
                MinFloorArea = 0f,
                // Kein Kernmöbel: die Verkehrsfläche hat Vorrang, und ein Flur ohne Möbel ist
                // ein richtig eingerichteter Flur.
                Core = new FurnitureRole[0],
                Groups = new GroupSlot[0],
                Optional = new[] { FurnitureRole.Console },
                Lights = new[] { FurnitureRole.WallLamp },
                Decor = new[] { FurnitureRole.Picture, FurnitureRole.Mirror },
            },
        };

        private static readonly FurnishingVariant[] Entrance =
        {
            new FurnishingVariant
            {
                Name = "Diele",
                MinFloorArea = 0f,
                Core = new FurnitureRole[0],
                Groups = new GroupSlot[0],
                Optional = new[] { FurnitureRole.Console, FurnitureRole.Dresser },
                Lights = new[] { FurnitureRole.WallLamp },
                Decor = new[] { FurnitureRole.Mirror, FurnitureRole.Picture, FurnitureRole.Telephone },
            },
        };

        // ---------------------------------------------------------------- Lager und Technik

        private static readonly FurnishingVariant[] Storage =
        {
            new FurnishingVariant
            {
                Name = "Abstellraum",
                MinFloorArea = 0f,
                Core = new[] { FurnitureRole.StorageShelf },
                Groups = new GroupSlot[0],
                Optional = new[] { FurnitureRole.Crate, FurnitureRole.Dresser },
                Lights = new[] { FurnitureRole.WallLamp },
                Decor = new[] { FurnitureRole.Clutter },
            },
        };

        private static readonly FurnishingVariant[] Utility =
        {
            new FurnishingVariant
            {
                Name = "Wirtschaftsraum",
                MinFloorArea = 0f,
                Core = new FurnitureRole[0],
                Groups = new GroupSlot[0],
                Optional = new[] { FurnitureRole.StorageShelf, FurnitureRole.Crate },
                Lights = new[] { FurnitureRole.WallLamp },
                Decor = new[] { FurnitureRole.Clutter },
            },
        };
    }
}
