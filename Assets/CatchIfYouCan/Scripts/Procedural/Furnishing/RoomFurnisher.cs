using System.Collections.Generic;
using System.Text;
using CatchIfYouCan.Content;
using CatchIfYouCan.Procedural.Deterministic;
using UnityEngine;

namespace CatchIfYouCan.Procedural.Furnishing
{
    /// <summary>Ein Stück, das wirklich im Raum steht - mit dem, was man dafür wissen muss.</summary>
    public struct PlacedPiece
    {
        public FurnitureRole Role;
        public string Id;
        public GameObject Instance;
        public FootRect Rect;
        public float Height;
        public float Yaw;

        /// <summary>Einheitsvektor, in den die Vorderseite zeigt.</summary>
        public Vector2 Facing;

        /// <summary>Ob auf der Oberseite etwas abgestellt werden darf, und wie hoch die liegt.</summary>
        public bool CanHoldDecor;
    }

    /// <summary>Was in einem Raum passiert ist - eine Zeile Diagnose je Raum.</summary>
    public sealed class FurnishReport
    {
        public int RoomId;
        public RoomCategory Category;
        public string Variant = "<keine>";
        public float InteriorArea;
        public float UsableArea;
        public int Major;
        public int Optional;
        public int Decor;
        public int Lights;
        public int Switches;
        public readonly List<string> Rejected = new List<string>();
        public readonly List<string> MissingRoles = new List<string>();
        public bool WalkwaysClear = true;

        public string Compact()
        {
            return "room=" + RoomId + " " + Category +
                   " variant='" + Variant + "'" +
                   " area=" + InteriorArea.ToString("F1") + "m2" +
                   " usable=" + UsableArea.ToString("F1") + "m2" +
                   " major=" + Major + " optional=" + Optional + " decor=" + Decor +
                   " lights=" + Lights +
                   " missing=" + (MissingRoles.Count == 0 ? "-" : string.Join(",", MissingRoles)) +
                   " walkways=" + (WalkwaysClear ? "frei" : "BLOCKIERT");
        }
    }

    /// <summary>
    /// Richtet einen Raum ein - nach Funktion, nicht nach Zufall.
    ///
    /// <para>
    /// <b>Stage B, und das ist der Kern der Sache.</b> Der Grundriss steht schon: welche Räume es
    /// gibt, wie gross sie sind, welche Wand eine Tür trägt, ist in Stage A entschieden und im
    /// Layout-Hash. Diese Einrichtung liest das und stellt Möbel hinein. Sie kann keinen Raum
    /// verschieben, keine Tür verlegen und keinen Nachbarn wählen - und deshalb bewegt sich der
    /// Layout-Hash nicht, wenn die Einrichtung sich ändert. Genau dieselbe Grenze, an der auch
    /// <see cref="ModularRoomBuilder"/> steht.
    /// </para>
    ///
    /// <para>
    /// <b>Die Reihenfolge ist die Aussage.</b> Erst wird der Raum vermessen, dann werden die
    /// Flächen reserviert, die frei bleiben MÜSSEN - Türschwenkbereich, Laufwege, Fenster -, und
    /// erst danach darf etwas hineingestellt werden. Andersherum entsteht eine Einrichtung, die
    /// hübsch aussieht und in der man nicht durch die Tür kommt, und dann wird sie hinterher
    /// wieder abgeräumt. Freie Wege sind wichtiger als die Zahl der Möbel.
    /// </para>
    /// </summary>
    public static class RoomFurnisher
    {
        /// <summary>Wie hoch eine Fläche liegen darf, damit Deko darauf glaubwürdig ist.</summary>
        private const float DecorSurfaceMin = 0.35f;
        private const float DecorSurfaceMax = 1.35f;

        private static readonly List<FurnitureEntry> _candidates = new List<FurnitureEntry>(16);
        private static readonly List<float> _weights = new List<float>(16);
        private static readonly List<WallRun> _wallOrder = new List<WallRun>(4);

        public static FurnishReport Furnish(GeneratedRoomInstance room, RoomShellInfo shell,
                                            RoomFurnishingCatalog catalog,
                                            FurniturePrefabCache cache, int layoutSeed)
        {
            var report = new FurnishReport
            {
                RoomId = shell.RoomId,
                Category = shell.Category,
            };

            var footprint = new RoomFootprint(shell);
            report.InteriorArea = footprint.InteriorArea;
            report.UsableArea = footprint.UsableArea;

            FurnishingVariant[] variants = FurnishingProfiles.For(shell.Category);
            if (variants.Length == 0)
            {
                // Ausdrücklich, nicht heimlich: dieser Raumart ist kein Profil zugeordnet, und
                // sie bekommt deshalb keine Einrichtung. Ihr die eines Wohnzimmers zu geben,
                // sähe im Editor nach Fortschritt aus und im Spiel nach einem Fehler, den
                // niemand mehr zuordnen kann.
                report.MissingRoles.Add("kein Profil fuer " + shell.Category);
                return report;
            }

            if (catalog == null || catalog.Entries == null || catalog.Entries.Length == 0)
            {
                report.MissingRoles.Add("Katalog leer");
                return report;
            }

            var rng = FurnishingRandom.ForRoom(layoutSeed, shell.RoomId, "furnish");

            // Benachbarte Räume bekommen verschiedene Varianten, weil die Raum-IDs im Layout
            // fortlaufend sind und hier addiert werden: zwei Schlafzimmer nebeneinander sind
            // dann nicht dasselbe Schlafzimmer zweimal.
            int start = (rng.NextInt(variants.Length) + shell.RoomId) % variants.Length;

            var root = new GameObject("Furnishing");
            root.transform.SetParent(room.Root.transform, false);

            var placed = new List<PlacedPiece>(16);

            for (int attempt = 0; attempt < variants.Length; attempt++)
            {
                FurnishingVariant variant = variants[(start + attempt) % variants.Length];

                if (footprint.UsableArea < variant.MinFloorArea)
                {
                    report.Rejected.Add(variant.Name + ": Raum zu klein (" +
                                        footprint.UsableArea.ToString("F1") + " < " +
                                        variant.MinFloorArea.ToString("F1") + " m2)");
                    continue;
                }

                if (TryVariant(variant, root.transform, footprint, shell, catalog, cache,
                               ref rng, placed, report))
                {
                    report.Variant = variant.Name;
                    break;
                }

                // Verworfen heisst: restlos abgeräumt. Halb eingerichtete Reste einer Variante,
                // über die die nächste gelegt wird, sind zwei Einrichtungen übereinander.
                ClearPlaced(placed);
                report.Rejected.Add(variant.Name + ": Kernmoebel nicht platzierbar");
            }

            if (placed.Count == 0 && report.Variant == "<keine>")
                return report;

            AddGroups(report.Variant, variants, start, root.transform, footprint, catalog, cache,
                      ref rng, placed, report);
            AddOptional(report.Variant, variants, start, root.transform, footprint, shell, catalog,
                        cache, ref rng, placed, report);
            AddLights(report.Variant, variants, start, root.transform, footprint, shell, catalog,
                      cache, ref rng, placed, report);
            AddDecor(report.Variant, variants, start, root.transform, catalog, cache,
                     ref rng, placed, report);

            report.WalkwaysClear = VerifyWalkways(footprint);
            return report;
        }

        // ------------------------------------------------------------------ Kernmöbel

        private static bool TryVariant(FurnishingVariant variant, Transform parent,
                                       RoomFootprint footprint, RoomShellInfo shell,
                                       RoomFurnishingCatalog catalog, FurniturePrefabCache cache,
                                       ref FurnishingRandom rng, List<PlacedPiece> placed,
                                       FurnishReport report)
        {
            if (variant.Core == null || variant.Core.Length == 0)
                return true;

            for (int i = 0; i < variant.Core.Length; i++)
            {
                FurnitureRole role = variant.Core[i];

                if (!catalog.HasRole(role, shell.Category))
                {
                    // Fehlt ein KERNmöbel im Katalog, wird die Variante verworfen statt durch
                    // etwas Unpassendes ersetzt. Ein Bad ohne Waschbecken ist kein Bad mit einem
                    // Sessel darin.
                    if (!report.MissingRoles.Contains(role.ToString()))
                        report.MissingRoles.Add(role.ToString());
                    return false;
                }

                if (!PlaceAgainstWall(role, parent, footprint, shell, catalog, cache, ref rng,
                                      placed, report))
                    return false;

                report.Major++;
            }

            return true;
        }

        /// <summary>
        /// Sucht einen Wandabschnitt, an dem dieses Stück mit dem Rücken stehen kann.
        ///
        /// <para>
        /// Der Bedienraum vor dem Stück gehört zum geprüften Rechteck. Ein Schrank, dessen Tür
        /// gegen ein Bett schlägt, steht formal frei und ist trotzdem falsch aufgestellt.
        /// </para>
        /// </summary>
        private static bool PlaceAgainstWall(FurnitureRole role, Transform parent,
                                             RoomFootprint footprint, RoomShellInfo shell,
                                             RoomFurnishingCatalog catalog,
                                             FurniturePrefabCache cache, ref FurnishingRandom rng,
                                             List<PlacedPiece> placed, FurnishReport report)
        {
            catalog.CollectRole(role, shell.Category, _candidates);
            if (_candidates.Count == 0)
                return false;

            BuildWeights(_candidates);

            _wallOrder.Clear();
            _wallOrder.AddRange(footprint.Walls);
            rng.Shuffle(_wallOrder);

            int attempts = Mathf.Max(1, catalog.PlacementAttempts);

            for (int c = 0; c < _candidates.Count; c++)
            {
                int pick = rng.NextWeighted(_weights);
                FurnitureEntry entry = _candidates[pick];

                GameObject prefab = cache.Resolve(entry);
                if (prefab == null)
                    continue;

                GameObject instance = Object.Instantiate(prefab, parent);
                instance.name = entry.Id;
                FurnitureMetrics metrics = cache.Measure(prefab, instance);

                if (!metrics.Valid)
                {
                    report.Rejected.Add(entry.Id + ": nicht messbar (kein Renderer)");
                    Discard(instance);
                    continue;
                }

                ApplyMaterialOverride(instance, entry);

                bool done = false;

                for (int w = 0; w < _wallOrder.Count && !done; w++)
                {
                    WallRun wall = _wallOrder[w];

                    for (int a = 0; a < attempts && !done; a++)
                    {
                        float along = SlotAlongWall(wall, metrics, a, attempts);
                        if (float.IsNaN(along))
                            break;

                        if (TrySeat(instance, entry, metrics, wall, along, footprint, out PlacedPiece piece,
                                    out string reason))
                        {
                            placed.Add(piece);
                            done = true;
                        }
                        else if (catalog.VerboseDiagnostics)
                        {
                            report.Rejected.Add(entry.Id + " an " + wall.Direction + ": " + reason);
                        }
                    }
                }

                if (done)
                    return true;

                report.Rejected.Add(entry.Id + ": an keiner Wand Platz");
                Discard(instance);
            }

            return false;
        }

        /// <summary>
        /// Eine Stützstelle entlang der Wand. Erst die Mitte, dann abwechselnd nach aussen -
        /// Möbel stehen mittiger als am Rand, und die Ecken bleiben für das Nächste frei.
        /// </summary>
        private static float SlotAlongWall(WallRun wall, FurnitureMetrics metrics, int attempt, int attempts)
        {
            float span = wall.Length - metrics.Width;
            if (span < 0f)
                return float.NaN;

            if (attempts <= 1 || span < 0.01f)
                return 0f;

            int step = (attempt + 1) / 2;
            float sign = (attempt % 2) == 0 ? 1f : -1f;
            float unit = span * 0.5f / Mathf.Max(1, attempts / 2);

            return Mathf.Clamp(sign * step * unit, -span * 0.5f, span * 0.5f);
        }

        /// <summary>
        /// Setzt ein Stück an eine Wand, wenn es dort passt - Bedienraum eingerechnet.
        /// </summary>
        private static bool TrySeat(GameObject instance, FurnitureEntry entry,
                                    FurnitureMetrics metrics, WallRun wall, float along,
                                    RoomFootprint footprint, out PlacedPiece piece, out string reason)
        {
            piece = default;

            bool alongX = wall.Direction == SocketDirection.North || wall.Direction == SocketDirection.South;

            float width = alongX ? metrics.Width : metrics.Depth;
            float depth = alongX ? metrics.Depth : metrics.Width;

            // Mitte des Möbels: an der Wand, um die halbe Tiefe in den Raum gerückt.
            Vector2 tangent = alongX ? new Vector2(1f, 0f) : new Vector2(0f, 1f);
            Vector2 centre = wall.Centre + tangent * along + wall.Inward * (depth * 0.5f);

            FootRect body = FootRect.FromCentre(centre, width, depth);

            // Der Bedienraum zählt mit. Ein Schrank, dessen Tür gegen ein Bett schlägt, steht
            // formal frei und ist trotzdem falsch aufgestellt.
            float clearance = Mathf.Max(0f, entry.ClearanceFront);
            FootRect tested = body;
            if (clearance > 0.001f)
            {
                Vector2 grown = centre + wall.Inward * (clearance * 0.5f);
                tested = alongX
                    ? FootRect.FromCentre(grown, width, depth + clearance)
                    : FootRect.FromCentre(grown, depth + clearance, width);
            }

            if (!footprint.Fits(tested, metrics.Height, out reason))
                return false;

            Seat(instance, metrics, centre, wall.Yaw);
            footprint.Occupy(body);

            piece = new PlacedPiece
            {
                Role = entry.Role,
                Id = entry.Id,
                Instance = instance,
                Rect = body,
                Height = metrics.Height,
                Yaw = wall.Yaw,
                Facing = wall.Inward,
                CanHoldDecor = metrics.Height >= DecorSurfaceMin && metrics.Height <= DecorSurfaceMax,
            };

            reason = null;
            return true;
        }

        /// <summary>
        /// Setzt das Modell so, dass seine GEMESSENE Mitte auf dem gewünschten Punkt liegt und
        /// seine Unterkante auf dem Boden.
        ///
        /// <para>
        /// Über den Pivot zu setzen wäre falsch: dieses Paket legt Pivots bis zu 40 m neben die
        /// eigene Geometrie, und ein Möbelstück über seinen Pivot zu setzen heisst dann, es
        /// irgendwohin zu setzen. Dieselbe Korrektur, die die Tür- und Fenstereinsätze schon
        /// benutzen.
        /// </para>
        /// </summary>
        private static void Seat(GameObject instance, FurnitureMetrics metrics, Vector2 centre, float yaw)
        {
            var rotation = Quaternion.Euler(0f, yaw, 0f);
            instance.transform.localRotation = rotation;

            var wanted = new Vector3(centre.x, metrics.Size.y * 0.5f, centre.y);
            instance.transform.localPosition = wanted - rotation * metrics.CentreOffset;
        }

        // ------------------------------------------------------------------ Gruppen

        private static void AddGroups(string variantName, FurnishingVariant[] variants, int start,
                                      Transform parent, RoomFootprint footprint,
                                      RoomFurnishingCatalog catalog, FurniturePrefabCache cache,
                                      ref FurnishingRandom rng, List<PlacedPiece> placed,
                                      FurnishReport report)
        {
            FurnishingVariant variant = FindVariant(variantName, variants, start);
            if (variant.Groups == null)
                return;

            for (int g = 0; g < variant.Groups.Length; g++)
            {
                GroupSlot slot = variant.Groups[g];

                int anchorIndex = IndexOfRole(placed, slot.Anchor);
                if (anchorIndex < 0)
                    continue;

                PlacedPiece anchor = placed[anchorIndex];

                catalog.CollectRole(slot.Member, report.Category, _candidates);
                if (_candidates.Count == 0)
                {
                    if (!slot.Optional && !report.MissingRoles.Contains(slot.Member.ToString()))
                        report.MissingRoles.Add(slot.Member.ToString());
                    continue;
                }

                BuildWeights(_candidates);

                for (int n = 0; n < slot.Count; n++)
                {
                    int pick = rng.NextWeighted(_weights);
                    FurnitureEntry entry = _candidates[pick];

                    GameObject prefab = cache.Resolve(entry);
                    if (prefab == null)
                        continue;

                    GameObject instance = Object.Instantiate(prefab, parent);
                    instance.name = entry.Id + "_" + n;
                    FurnitureMetrics metrics = cache.Measure(prefab, instance);

                    if (!metrics.Valid || !SeatInGroup(instance, entry, metrics, anchor, slot, n,
                                                       footprint, placed))
                    {
                        // Bei Platzmangel fällt zuerst das Optionale weg - genau hier, und ohne
                        // dafür ein Kernmöbel zu verschieben.
                        Discard(instance);
                        continue;
                    }

                    ApplyMaterialOverride(instance, entry);
                    report.Optional++;
                }
            }
        }

        /// <summary>Ein Gruppenmitglied relativ zu seinem Anker.</summary>
        private static bool SeatInGroup(GameObject instance, FurnitureEntry entry,
                                        FurnitureMetrics metrics, PlacedPiece anchor,
                                        GroupSlot slot, int index, RoomFootprint footprint,
                                        List<PlacedPiece> placed)
        {
            Vector2 anchorCentre = anchor.Rect.Centre;
            Vector2 forward = anchor.Facing;
            Vector2 side = new Vector2(-forward.y, forward.x);

            float anchorHalfForward = Mathf.Abs(forward.x) > 0.5f
                ? anchor.Rect.Width * 0.5f : anchor.Rect.Depth * 0.5f;
            float anchorHalfSide = Mathf.Abs(side.x) > 0.5f
                ? anchor.Rect.Width * 0.5f : anchor.Rect.Depth * 0.5f;

            Vector2 centre;
            float yaw;

            switch (slot.Pattern)
            {
                case GroupPattern.InFront:
                    centre = anchorCentre + forward * (anchorHalfForward + slot.Gap + metrics.Depth * 0.5f);
                    yaw = anchor.Yaw + 180f;
                    break;

                case GroupPattern.Beside:
                {
                    float sign = (index % 2) == 0 ? 1f : -1f;
                    centre = anchorCentre + side * sign * (anchorHalfSide + slot.Gap + metrics.Width * 0.5f);
                    yaw = anchor.Yaw;
                    break;
                }

                default:
                {
                    // Rund um den Tisch: die vier Seiten der Reihe nach, damit vier Stühle nicht
                    // alle an derselben Kante landen.
                    int sideIndex = index % 4;
                    Vector2 dir = sideIndex switch
                    {
                        0 => forward,
                        1 => -forward,
                        2 => side,
                        _ => -side,
                    };

                    float half = Mathf.Abs(dir.x) > 0.5f ? anchor.Rect.Width * 0.5f : anchor.Rect.Depth * 0.5f;
                    centre = anchorCentre + dir * (half + slot.Gap + metrics.Depth * 0.5f);
                    yaw = Mathf.Atan2(-dir.x, -dir.y) * Mathf.Rad2Deg;
                    break;
                }
            }

            bool alongX = Mathf.Abs(Mathf.Sin(yaw * Mathf.Deg2Rad)) < 0.5f;
            float width = alongX ? metrics.Width : metrics.Depth;
            float depth = alongX ? metrics.Depth : metrics.Width;

            FootRect rect = FootRect.FromCentre(centre, width, depth);

            if (!footprint.Fits(rect, metrics.Height, out _))
                return false;

            Seat(instance, metrics, centre, yaw);
            footprint.Occupy(rect);

            placed.Add(new PlacedPiece
            {
                Role = entry.Role,
                Id = entry.Id,
                Instance = instance,
                Rect = rect,
                Height = metrics.Height,
                Yaw = yaw,
                Facing = -new Vector2(Mathf.Sin(yaw * Mathf.Deg2Rad), Mathf.Cos(yaw * Mathf.Deg2Rad)),
                CanHoldDecor = metrics.Height >= DecorSurfaceMin && metrics.Height <= DecorSurfaceMax,
            });

            return true;
        }

        // ------------------------------------------------------------------ Optionales

        private static void AddOptional(string variantName, FurnishingVariant[] variants, int start,
                                        Transform parent, RoomFootprint footprint, RoomShellInfo shell,
                                        RoomFurnishingCatalog catalog, FurniturePrefabCache cache,
                                        ref FurnishingRandom rng, List<PlacedPiece> placed,
                                        FurnishReport report)
        {
            FurnishingVariant variant = FindVariant(variantName, variants, start);
            if (variant.Optional == null || variant.Optional.Length == 0)
                return;

            int budget = MajorBudget(catalog, footprint) - report.Major;

            for (int i = 0; i < variant.Optional.Length && budget > 0; i++)
            {
                if (PlaceAgainstWall(variant.Optional[i], parent, footprint, shell, catalog, cache,
                                     ref rng, placed, report))
                {
                    report.Optional++;
                    budget--;
                }
            }
        }

        // ------------------------------------------------------------------ Licht

        /// <summary>
        /// Leuchten - und nur so viele echte Lichtquellen, wie das Budget erlaubt.
        ///
        /// <para>
        /// Fehlt ein Leuchten-Prefab, wird die Lücke GEMELDET und nichts gebaut. Ein unsichtbares
        /// Hilfslicht an einer Stelle, an der keine Lampe steht, wäre eine Lichtquelle ohne
        /// Ursache - im Dunkeln der auffälligste Fehler, den ein Horrorspiel machen kann, und im
        /// Log nicht von einer richtig eingerichteten Lampe zu unterscheiden.
        /// </para>
        /// </summary>
        private static void AddLights(string variantName, FurnishingVariant[] variants, int start,
                                      Transform parent, RoomFootprint footprint, RoomShellInfo shell,
                                      RoomFurnishingCatalog catalog, FurniturePrefabCache cache,
                                      ref FurnishingRandom rng, List<PlacedPiece> placed,
                                      FurnishReport report)
        {
            FurnishingVariant variant = FindVariant(variantName, variants, start);
            if (variant.Lights == null || variant.Lights.Length == 0)
                return;

            int budget = Mathf.Max(0, catalog.LightsPerRoom);
            if (budget == 0)
                return;

            for (int i = 0; i < variant.Lights.Length && report.Lights < budget; i++)
            {
                FurnitureRole role = variant.Lights[i];

                if (!catalog.HasRole(role, shell.Category))
                {
                    if (!report.MissingRoles.Contains(role.ToString()))
                        report.MissingRoles.Add(role.ToString());
                    continue;
                }

                int before = placed.Count;
                if (!PlaceAgainstWall(role, parent, footprint, shell, catalog, cache, ref rng,
                                      placed, report))
                    continue;

                for (int p = before; p < placed.Count; p++)
                    AttachPractical(placed[p], report);
            }
        }

        /// <summary>
        /// Hängt eine echte Lichtquelle an eine platzierte Leuchte.
        ///
        /// <para>
        /// <c>InstallRoomInteractables</c> im Generator sucht je Raum ein <see cref="Light"/> und
        /// baut nur dann einen Lichtschalter dazu. Bisher fand es keins, weil nichts je eines
        /// erzeugt hat - das ist die gemeldete Bilanz "0 von 0 practicals", und sie war keine
        /// Fehlfunktion der Suche, sondern das ehrliche Ergebnis eines Hauses ohne Lampen.
        /// </para>
        ///
        /// <para>
        /// Kein Schattenwurf. Ein Punktlicht mit Echtzeitschatten kostet auf einem Telefon mehr
        /// als der ganze Raum, und dieses Projekt zielt zuerst auf Mobile.
        /// </para>
        /// </summary>
        private static void AttachPractical(PlacedPiece piece, FurnishReport report)
        {
            if (piece.Instance == null)
                return;

            var go = new GameObject("Practical");
            go.transform.SetParent(piece.Instance.transform, false);
            go.transform.localPosition = new Vector3(0f, Mathf.Max(0.2f, piece.Height * 0.9f), 0f);

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.86f, 0.66f);
            light.intensity = 1.1f;
            light.range = 6f;
            light.shadows = LightShadows.None;

            report.Lights++;
        }

        // ------------------------------------------------------------------ Dekoration

        /// <summary>
        /// Kleinkram auf schon platzierten Flächen - und nur dort.
        ///
        /// <para>
        /// Erst nach den Möbeln, weil Deko ohne Trägermöbel in der Luft steht. Die Auflagefläche
        /// wird geprüft: sie muss hoch genug sein, um eine Fläche zu sein, niedrig genug, um eine
        /// erreichbare zu sein, und breit genug für das, was daraufkommt.
        /// </para>
        /// </summary>
        private static void AddDecor(string variantName, FurnishingVariant[] variants, int start,
                                     Transform parent, RoomFurnishingCatalog catalog,
                                     FurniturePrefabCache cache, ref FurnishingRandom rng,
                                     List<PlacedPiece> placed, FurnishReport report)
        {
            FurnishingVariant variant = FindVariant(variantName, variants, start);
            if (variant.Decor == null || variant.Decor.Length == 0)
                return;

            var surfaces = new List<PlacedPiece>();
            for (int i = 0; i < placed.Count; i++)
            {
                if (placed[i].CanHoldDecor)
                    surfaces.Add(placed[i]);
            }

            if (surfaces.Count == 0)
                return;

            rng.Shuffle(surfaces);

            int budget = DecorBudget(catalog, placed);

            for (int i = 0; i < variant.Decor.Length && budget > 0; i++)
            {
                catalog.CollectRole(variant.Decor[i], report.Category, _candidates);
                if (_candidates.Count == 0)
                    continue;

                BuildWeights(_candidates);

                int pick = rng.NextWeighted(_weights);
                FurnitureEntry entry = _candidates[pick];

                GameObject prefab = cache.Resolve(entry);
                if (prefab == null)
                    continue;

                PlacedPiece surface = surfaces[i % surfaces.Count];

                GameObject instance = Object.Instantiate(prefab, parent);
                instance.name = entry.Id;
                FurnitureMetrics metrics = cache.Measure(prefab, instance);

                if (!metrics.Valid || metrics.Width > surface.Rect.Width * 0.8f ||
                    metrics.Depth > surface.Rect.Depth * 0.8f)
                {
                    // Die Trägerfläche muss den Gegenstand wirklich tragen. Eine Standuhr auf
                    // einem Beistelltisch ist kein eingerichteter Raum, sondern ein Fehler, der
                    // wie Absicht aussieht.
                    Discard(instance);
                    continue;
                }

                Vector2 centre = surface.Rect.Centre;
                var rotation = Quaternion.Euler(0f, surface.Yaw, 0f);
                instance.transform.localRotation = rotation;

                // Unterkante GENAU auf der Oberseite des Trägers: der Pivot-Versatz wird
                // herausgerechnet, sonst schwebt oder versinkt der Gegenstand.
                var wanted = new Vector3(centre.x, surface.Height + metrics.Size.y * 0.5f, centre.y);
                instance.transform.localPosition = wanted - rotation * metrics.CentreOffset;

                ApplyMaterialOverride(instance, entry);
                StripPhysics(instance);

                report.Decor++;
                budget--;
            }
        }

        // ------------------------------------------------------------------ Hilfsmittel

        private static int MajorBudget(RoomFurnishingCatalog catalog, RoomFootprint footprint)
        {
            float tens = Mathf.Max(1f, footprint.InteriorArea / 10f);
            return Mathf.Max(1, Mathf.RoundToInt(catalog.MajorPiecesPerTenSquareMetres * tens));
        }

        private static int DecorBudget(RoomFurnishingCatalog catalog, List<PlacedPiece> placed)
        {
            return Mathf.Max(0, Mathf.RoundToInt(catalog.DecorPerTenSquareMetres));
        }

        private static void BuildWeights(List<FurnitureEntry> candidates)
        {
            _weights.Clear();
            for (int i = 0; i < candidates.Count; i++)
                _weights.Add(candidates[i].ResolvedWeight);
        }

        private static int IndexOfRole(List<PlacedPiece> placed, FurnitureRole role)
        {
            for (int i = 0; i < placed.Count; i++)
            {
                if (placed[i].Role == role)
                    return i;
            }

            return -1;
        }

        private static FurnishingVariant FindVariant(string name, FurnishingVariant[] variants, int start)
        {
            for (int i = 0; i < variants.Length; i++)
            {
                if (variants[i].Name == name)
                    return variants[i];
            }

            return variants[start % variants.Length];
        }

        private static void ApplyMaterialOverride(GameObject instance, FurnitureEntry entry)
        {
            if (entry.MaterialOverride == null || instance == null)
                return;

            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    renderers[i].sharedMaterial = entry.MaterialOverride;
            }
        }

        /// <summary>
        /// Nimmt kleiner Deko alles ab, was sie nicht braucht.
        ///
        /// Ein Buch auf einem Tisch ist ein Bild und kein Körper: ein Rigidbody darauf kostet
        /// jeden Frame Simulation, und ein Collider kostet jede Interaktionsabfrage einen Treffer,
        /// der den Tisch dahinter verdeckt.
        /// </summary>
        private static void StripPhysics(GameObject instance)
        {
            var bodies = instance.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < bodies.Length; i++)
                Object.Destroy(bodies[i]);

            var colliders = instance.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
                colliders[i].enabled = false;
        }

        /// <summary>
        /// Prüft, dass die reservierten Laufwege wirklich frei geblieben sind.
        ///
        /// Nachgemessen, nicht angenommen: <see cref="RoomFootprint.Fits"/> lehnt zwar jede
        /// Platzierung in einer Sperrzone ab, aber genau diese Behauptung ist es wert, am Ende
        /// noch einmal gegen das tatsächliche Ergebnis gehalten zu werden.
        /// </summary>
        private static bool VerifyWalkways(RoomFootprint footprint)
        {
            for (int r = 0; r < footprint.Reserved.Count; r++)
            {
                ReservedZone zone = footprint.Reserved[r];
                if (zone.MaxHeight > 0.001f)
                    continue;

                for (int o = 0; o < footprint.Occupied.Count; o++)
                {
                    if (zone.Rect.Overlaps(footprint.Occupied[o]))
                        return false;
                }
            }

            return true;
        }

        private static void ClearPlaced(List<PlacedPiece> placed)
        {
            for (int i = 0; i < placed.Count; i++)
                Discard(placed[i].Instance);

            placed.Clear();
        }

        /// <summary>
        /// Abschalten statt zerstören, aus demselben Grund wie bei den Wandeinsätzen: Destroy ist
        /// verzögert und im Edit-Modus gar nicht erlaubt, und die Wahl per Kontext zu treffen ist,
        /// wie Editor und Gerät in diesem Projekt einmal aufgehört haben, sich einig zu sein.
        /// </summary>
        private static void Discard(GameObject instance)
        {
            if (instance != null)
                instance.SetActive(false);
        }
    }
}
