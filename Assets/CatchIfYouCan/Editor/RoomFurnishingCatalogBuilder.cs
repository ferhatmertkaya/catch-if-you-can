using System.Collections.Generic;
using System.Text;
using CatchIfYouCan.Content;
using UnityEditor;
using UnityEngine;

namespace CatchIfYouCan.EditorTools
{
    /// <summary>
    /// Trägt die Möbel des HQ-Modular-House-Pakets in den <see cref="RoomFurnishingCatalog"/> ein.
    ///
    /// <para>
    /// <b>Warum ein Werkzeug und keine handgeschriebene Referenz.</b> Eine Prefab-Referenz in
    /// einem Asset ist <c>{fileID, guid, type}</c>, und die fileID ist die des Wurzelobjekts IM
    /// Prefab - eine Zahl, die nur Unity kennt. Sie zu raten ergibt eine Referenz, die auf nichts
    /// zeigt und im Inspector genauso aussieht wie eine, die es tut. Genau diese Form ist in
    /// diesem Projekt schon zweimal teuer geworden (CLAUDE.md Fehler 3 und 15). Das Werkzeug löst
    /// stattdessen jeden Pfad über die AssetDatabase auf und schreibt nur ein, was wirklich
    /// aufgegangen ist.
    /// </para>
    ///
    /// <para>
    /// <b>Die Pfadtabelle ist belegt, nicht geraten.</b> Sie stammt aus
    /// <c>Docs/VENDOR_ASSET_MANIFEST.txt</c>, das auf der Maschine mit dem installierten Paket
    /// geschrieben wurde und je Zeile eine guid und einen echten Pfad nennt. Was hier steht, ist
    /// die Zuordnung dieser Pfade zu Rollen - und die ist eine Designentscheidung, keine Messung.
    /// </para>
    ///
    /// <para>
    /// <b>Keine Maße hier.</b> Wie groß ein Möbelstück ist, misst die Einrichtung zur Laufzeit am
    /// Modell. Eine hier eingetippte Zahl wäre eine zweite Wahrheit daneben.
    /// </para>
    /// </summary>
    public static class RoomFurnishingCatalogBuilder
    {
        private const string MenuPath =
            "Catch If You Can/4. SPIELINHALT/Content/Moebelkatalog aus HQ-Paket [SCHREIBT ASSET]";

        private const string CatalogPath =
            "Assets/CatchIfYouCan/ScriptableObjects/Content/RoomFurnishingCatalog.asset";

        private const string PackRoot = "Assets/HQ Modular House/props/";

        /// <summary>Ein Eintrag der belegten Tabelle: Pfad, Rolle, wie er steht.</summary>
        private struct Source
        {
            public string Path;
            public FurnitureRole Role;
            public FurnitureAnchor Anchor;
            public float ClearanceFront;
            public float MountHeight;
            public bool IsLight;

            public Source(string path, FurnitureRole role, FurnitureAnchor anchor = FurnitureAnchor.AgainstWall,
                          float clearance = 0.6f, float mountHeight = 0f, bool isLight = false)
            {
                Path = path;
                Role = role;
                Anchor = anchor;
                ClearanceFront = clearance;
                MountHeight = mountHeight;
                IsLight = isLight;
            }
        }

        /// <summary>
        /// Die Zuordnung. Pfade aus dem Vendor-Manifest, Rollen als Designentscheidung.
        ///
        /// <para>
        /// Bewusst NICHT vollständig: Einträge, bei denen aus dem Namen nicht sicher hervorgeht,
        /// was das Stück ist, bleiben weg. Ein falsch zugeordnetes Möbelstück ist schlimmer als
        /// ein fehlendes - das fehlende wird gemeldet, das falsche sieht nach Absicht aus.
        /// </para>
        /// </summary>
        private static readonly Source[] Sources =
        {
            // --- Wohnzimmer -------------------------------------------------------------
            new Source("Living Room/sofa1.prefab", FurnitureRole.Sofa, clearance: 0.7f),
            new Source("Living Room/table 2.prefab", FurnitureRole.CoffeeTable, FurnitureAnchor.FreeStanding, 0.3f),
            new Source("Living Room/chair3.prefab", FurnitureRole.Armchair, clearance: 0.5f),
            new Source("Living Room/chair4.prefab", FurnitureRole.Armchair, clearance: 0.5f),
            new Source("Living Room/floor lamp.prefab", FurnitureRole.FloorLamp, clearance: 0.2f, isLight: true),
            new Source("Living Room/deer head.prefab", FurnitureRole.Picture, FurnitureAnchor.WallMounted, 0f, 2.0f),
            new Source("Library/armchair.prefab", FurnitureRole.Armchair, clearance: 0.5f),

            // --- Esszimmer --------------------------------------------------------------
            new Source("Kitchen/table3.prefab", FurnitureRole.DiningTable, FurnitureAnchor.FreeStanding, 0.4f),
            new Source("Kitchen/chair.prefab", FurnitureRole.DiningChair, FurnitureAnchor.FreeStanding, 0.3f),
            new Source("Bedroom/chair.prefab", FurnitureRole.DiningChair, FurnitureAnchor.FreeStanding, 0.3f),

            // --- Schlafzimmer -----------------------------------------------------------
            new Source("Bedroom/bed.prefab", FurnitureRole.Bed, clearance: 0.7f),
            new Source("Bedroom/bed2.prefab", FurnitureRole.Bed, clearance: 0.7f),
            new Source("Bedroom/commode 1.prefab", FurnitureRole.Dresser, clearance: 0.6f),
            new Source("Bedroom/commode 2.prefab", FurnitureRole.Dresser, clearance: 0.6f),
            new Source("Bedroom/nightstand.prefab", FurnitureRole.Nightstand, clearance: 0.3f),
            new Source("Bedroom/bedside table l.prefab", FurnitureRole.Nightstand, clearance: 0.3f),
            new Source("Bedroom/bedside table1 .prefab", FurnitureRole.Nightstand, clearance: 0.3f),
            new Source("Bedroom/old mirror .prefab", FurnitureRole.Mirror, clearance: 0.4f),
            new Source("Bedroom/mirror.prefab", FurnitureRole.Mirror, clearance: 0.4f),
            new Source("Bedroom/lamp.prefab", FurnitureRole.TableLamp, FurnitureAnchor.OnSurface, 0f, 0f, true),
            new Source("Bedroom/floor lamp2.prefab", FurnitureRole.FloorLamp, clearance: 0.2f, isLight: true),
            new Source("Bedroom/Suitcase 1.prefab", FurnitureRole.Clutter, FurnitureAnchor.FreeStanding, 0.2f),
            new Source("Bedroom/Suitcase 2.prefab", FurnitureRole.Clutter, FurnitureAnchor.FreeStanding, 0.2f),
            new Source("Bedroom/Suitcase 3.prefab", FurnitureRole.Clutter, FurnitureAnchor.FreeStanding, 0.2f),

            // --- Küche ------------------------------------------------------------------
            new Source("Kitchen/cupboard 1.prefab", FurnitureRole.KitchenCounter, clearance: 0.8f),
            new Source("Kitchen/cupboard 2.prefab", FurnitureRole.KitchenCupboard, clearance: 0.8f),
            new Source("Kitchen/stove.prefab", FurnitureRole.Stove, clearance: 0.8f),
            new Source("Kitchen/sink 2.prefab", FurnitureRole.Sink, clearance: 0.8f),
            new Source("Kitchen/fridge.prefab", FurnitureRole.Fridge, clearance: 0.9f),
            new Source("Kitchen/shelf.prefab", FurnitureRole.KitchenShelf, FurnitureAnchor.WallMounted, 0f, 1.5f),
            new Source("Kitchen/shelf 2.prefab", FurnitureRole.KitchenShelf, FurnitureAnchor.WallMounted, 0f, 1.5f),
            new Source("Kitchen/dishes.prefab", FurnitureRole.Clutter, FurnitureAnchor.OnSurface),
            new Source("Kitchen/Knives.prefab", FurnitureRole.Clutter, FurnitureAnchor.OnSurface),

            // --- Bad --------------------------------------------------------------------
            new Source("Bathroom/bath.prefab", FurnitureRole.Bathtub, clearance: 0.7f),
            new Source("Bathroom/toilet.prefab", FurnitureRole.Toilet, clearance: 0.6f),
            new Source("Bathroom/toilet bowl.prefab", FurnitureRole.Toilet, clearance: 0.6f),
            new Source("Bathroom/faucet.prefab", FurnitureRole.WashBasin, clearance: 0.7f),

            // --- Arbeitszimmer und Bibliothek -------------------------------------------
            new Source("Office Room/table 4.prefab", FurnitureRole.Desk, clearance: 0.8f),
            new Source("Library/table 1.prefab", FurnitureRole.Desk, clearance: 0.8f),
            new Source("Office Room/Chair 1.prefab", FurnitureRole.DeskChair, FurnitureAnchor.FreeStanding, 0.3f),
            new Source("Office Room/chair 2.prefab", FurnitureRole.DeskChair, FurnitureAnchor.FreeStanding, 0.3f),
            new Source("Office Room/cupboard 7 .prefab", FurnitureRole.Bookshelf, clearance: 0.7f),
            new Source("Library/cupboard 1.prefab", FurnitureRole.Bookshelf, clearance: 0.7f),
            new Source("Library/cupboard 2.prefab", FurnitureRole.Bookshelf, clearance: 0.7f),
            new Source("Library/books.prefab", FurnitureRole.Books, FurnitureAnchor.OnSurface),
            new Source("Library/newspapers.prefab", FurnitureRole.Books, FurnitureAnchor.OnSurface),
            new Source("Library/Clock 1.prefab", FurnitureRole.Clock, FurnitureAnchor.OnSurface),
            new Source("Library/picture.prefab", FurnitureRole.Picture, FurnitureAnchor.WallMounted, 0f, 1.8f),
            new Source("Office Room/Radio.prefab", FurnitureRole.Clutter, FurnitureAnchor.OnSurface),

            // --- Lager ------------------------------------------------------------------
            new Source("Nursery/cupboard 1.prefab", FurnitureRole.StorageShelf, clearance: 0.6f),
            new Source("Nursery/commode 2.prefab", FurnitureRole.Dresser, clearance: 0.6f),
        };

        [MenuItem(MenuPath, false, 415)]
        public static void Build()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<RoomFurnishingCatalog>(CatalogPath);
            if (catalog == null)
            {
                EditorUtility.DisplayDialog("Moebelkatalog",
                    "Kein RoomFurnishingCatalog unter\n" + CatalogPath +
                    "\n\nDas Asset gehoert zum Repository und sollte da sein.", "OK");
                return;
            }

            // Was schon drinsteht, bleibt: der projekteigene Teil des Katalogs wird ueber
            // Resources-Pfade geladen und funktioniert auch ohne das Paket. Nur die Eintraege
            // aus dem Paket werden ersetzt, erkennbar an ihrer Id.
            var kept = new List<FurnitureEntry>();
            if (catalog.Entries != null)
            {
                for (int i = 0; i < catalog.Entries.Length; i++)
                {
                    if (!catalog.Entries[i].Id.StartsWith("HQ_", System.StringComparison.Ordinal))
                        kept.Add(catalog.Entries[i]);
                }
            }

            int added = 0;
            var missing = new List<string>();

            for (int i = 0; i < Sources.Length; i++)
            {
                Source source = Sources[i];
                string full = PackRoot + source.Path;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(full);
                if (prefab == null)
                {
                    // Gemeldet, nicht geraten. Ein Pfad, der ins Leere zeigt, ist genau der
                    // Fehler, den dieses Werkzeug verhindern soll - er wird also benannt und
                    // nicht durch irgendein anderes Prefab ersetzt.
                    missing.Add(full);
                    continue;
                }

                kept.Add(new FurnitureEntry
                {
                    Id = "HQ_" + source.Path.Replace('/', '_').Replace(".prefab", "").Trim(),
                    Role = source.Role,
                    Prefab = prefab,
                    ResourcePath = string.Empty,
                    MaterialOverride = null,
                    Rooms = new CatchIfYouCan.Procedural.RoomCategory[0],
                    Anchor = source.Anchor,
                    ClearanceFront = source.ClearanceFront,
                    MountHeight = source.MountHeight,
                    Weight = 1f,
                    IsLightFixture = source.IsLight,
                });

                added++;
            }

            catalog.Entries = kept.ToArray();
            catalog.SourceDescription = "HQ Modular House props + projekteigene Resources-Props. " +
                                        "Erzeugt von RoomFurnishingCatalogBuilder.";

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            var report = new StringBuilder();
            report.Append(added).Append(" von ").Append(Sources.Length)
                  .Append(" Paket-Moebeln eingetragen.\n")
                  .Append(kept.Count - added).Append(" projekteigene Eintraege behalten.\n");

            if (missing.Count > 0)
            {
                report.Append("\nNICHT gefunden (").Append(missing.Count).Append("):\n");
                for (int i = 0; i < missing.Count && i < 12; i++)
                    report.Append("  ").Append(missing[i]).Append('\n');

                if (missing.Count > 12)
                    report.Append("  ... und ").Append(missing.Count - 12).Append(" weitere\n");

                report.Append("\nIst das HQ Modular House Paket installiert? Es ist gitignored " +
                              "und liegt deshalb nur auf Maschinen, die es gekauft haben.");
            }

            Debug.Log("[CIYC][Furnishing] " + report.ToString().Replace('\n', ' '));
            EditorUtility.DisplayDialog("Moebelkatalog", report.ToString(), "OK");
        }
    }
}
