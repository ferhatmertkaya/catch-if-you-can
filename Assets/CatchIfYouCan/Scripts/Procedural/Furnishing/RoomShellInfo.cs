using CatchIfYouCan.Procedural.Deterministic;
using UnityEngine;

namespace CatchIfYouCan.Procedural.Furnishing
{
    /// <summary>
    /// Was die fertige Raumhuelle tatsaechlich geworden ist: Groesse, welche Wand eine Tuer
    /// traegt, welche ein Fenster.
    ///
    /// <para>
    /// <b>Warum das hier steht und nicht noch einmal abgeleitet wird.</b> Die Fenstermaske
    /// entsteht in <see cref="ModularRoomBuilder.Build(LayoutRoom, Vector3, Transform,
    /// Content.ModularInteriorCatalog, out string)"/> aus der Identitaet des Raums. Wer sie
    /// spaeter noch einmal berechnet, hat eine zweite Implementierung derselben Entscheidung -
    /// CLAUDE.md Fehler 1 - und die beiden gehen genau dann auseinander, wenn jemand die Regel
    /// aendert und nur eine Stelle findet. Also schreibt die Huelle auf, was sie gebaut hat,
    /// und die Einrichtung liest es.
    /// </para>
    ///
    /// <para>
    /// Reine Daten, kein Verhalten. Nichts hier entscheidet etwas, nichts laeuft pro Frame.
    /// </para>
    /// </summary>
    public sealed class RoomShellInfo : MonoBehaviour
    {
        /// <summary>Innenmasse des Raums in Metern, wie die Huelle sie gebaut hat.</summary>
        public Vector3 Size { get; private set; }

        /// <summary>Bitmaske der Richtungen mit einer Tueroeffnung.</summary>
        public int DoorMask { get; private set; }

        /// <summary>Bitmaske der Richtungen mit einem Fenster.</summary>
        public int WindowMask { get; private set; }

        public RoomCategory Category { get; private set; }

        public int RoomId { get; private set; }

        public void Configure(Vector3 size, int doorMask, int windowMask,
                              RoomCategory category, int roomId)
        {
            Size = size;
            DoorMask = doorMask;
            WindowMask = windowMask;
            Category = category;
            RoomId = roomId;
        }

        public bool HasDoor(SocketDirection dir) => (DoorMask & LayoutRoom.DirectionMask(dir)) != 0;

        public bool HasWindow(SocketDirection dir) => (WindowMask & LayoutRoom.DirectionMask(dir)) != 0;
    }
}
