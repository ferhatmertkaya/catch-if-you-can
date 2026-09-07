using System.Collections.Generic;
using CatchIfYouCan.Procedural.Deterministic;
using UnityEngine;

namespace CatchIfYouCan.Procedural
{
    /// <summary>
    /// STAGE B - prop instantiation.
    ///
    /// This class no longer decides anything. It receives placements that Stage A already
    /// resolved and builds the GameObjects for them.
    ///
    /// It previously gated every spawn on Physics.OverlapBox. That read the live PhysX
    /// scene, whose contents depend on frame timing, on deferred Object.Destroy, and on
    /// whether Physics.SyncTransforms had run (m_AutoSyncTransforms is 0 in this project).
    /// Worse, the RNG draws happened BEFORE the overlap test, so a client whose query
    /// disagreed kept a perfectly in-sync RNG stream while its layout silently diverged -
    /// no check short of a full layout hash could have caught it.
    ///
    /// Overlap is now resolved analytically in OccupancyGrid during Stage A. Colliders are
    /// an output of generation and never feed back into it.
    /// </summary>
    public class PropSpawner
    {
        private readonly Transform _propRoot;

        public PropSpawner(Transform propRoot)
        {
            _propRoot = propRoot;
        }

        /// <summary>Instantiates every planned placement. Returns how many were built.</summary>
        private int _skipped;
        private string _skippedSample;

        public int SpawnPlacements(
            IReadOnlyList<LayoutProp> placements,
            PropDefinition[] library,
            IReadOnlyDictionary<int, GeneratedRoomInstance> roomsById)
        {
            if (placements == null || placements.Count == 0)
                return 0;

            _skipped = 0;
            _skippedSample = null;

            int spawned = 0;
            for (int i = 0; i < placements.Count; i++)
            {
                if (TrySpawn(placements[i], library, roomsById))
                    spawned++;
            }

            if (_skipped > 0)
            {
                Core.CIYCLog.Warn("[CIYC][House][Props] " + _skipped + " von " + placements.Count +
                                  " geplanten Platzierungen uebersprungen, weil der " +
                                  "Content-Katalog keine PropDefinition dafuer hat (z. B. '" +
                                  _skippedSample + "'). Stage A plant sie aus dem eingebauten " +
                                  "Ersatz-Snapshot, den ContentSnapshotFactory bei leeren " +
                                  "Katalogen liefert. Sie bekommen KEINEN Wuerfel mehr - die " +
                                  "Einrichtung kommt aus dem RoomFurnishingCatalog.");
            }

            return spawned;
        }

        private bool TrySpawn(
            LayoutProp placement,
            PropDefinition[] library,
            IReadOnlyDictionary<int, GeneratedRoomInstance> roomsById)
        {
            var definition = ContentSnapshotFactory.FindProp(library, placement.PropDefinitionId);

            Vector3 position = new Vector3(
                Quantize.Metres(placement.PositionMm.X),
                Quantize.Metres(placement.PositionMm.Y),
                Quantize.Metres(placement.PositionMm.Z));

            Quaternion rotation = Quaternion.Euler(0f, placement.RotationIndex * 90f, 0f);

            // ---- ohne Prefab wird KEIN Moebelstueck erfunden --------------------------------
            //
            // Hier stand der grosse dunkle Block, und seine Herkunft ist belegt:
            // ContentSnapshotFactory.Create liefert bei leeren Katalogen einen eingebauten
            // Ersatz-Snapshot (FURN_SHELF, FURN_TABLE, PROP_CRATE, PROP_LAMP). Stage A plant
            // daraus Moebel und faltet die Platzierungen in den Layout-Hash. Stage B findet dann
            // fuer keine davon eine PropDefinition - beide Kataloge sind seit dem Entfernen der
            // Kenney-Inhalte leer (CLAUDE.md Fehler 14) - und baute je Platzierung einen
            // 1x1x1-Wuerfel im dunklen Trim-Material, weil "definition == null" auf
            // "size = Vector3.one" fiel. Mehrere davon pro Raum, mitten im Zimmer, und aus einem
            // Meter Entfernung im First-Person fuellt einer davon das Bild.
            //
            // Ein Platzhalter, der wie fertiger Inhalt aussieht, ist genau der Fehler, den dieses
            // Projekt schon dreimal gemacht hat. Die Platzierung wird stattdessen uebersprungen
            // und gezaehlt; eingerichtet wird durch RoomFurnisher, mit echten Moebeln.
            if (definition == null || definition.Prefab == null)
            {
                _skipped++;
                if (_skippedSample == null)
                    _skippedSample = placement.PropDefinitionId;

                return false;
            }

            GameObject instance = Object.Instantiate(definition.Prefab, position, rotation, _propRoot);

            var category = roomsById != null && roomsById.TryGetValue(placement.RoomId, out var room)
                ? room.Category.ToString()
                : "Room";

            instance.name = $"{placement.PropDefinitionId}_{category}_{placement.RoomId}_{placement.PropInstanceId}";
            return true;
        }
    }
}
