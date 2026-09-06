using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using CatchIfYouCan.Environment;
using CatchIfYouCan.Player;

namespace CatchIfYouCan.EditorTools
{
    /// <summary>
    /// Measures the hand-built lobby against the player who has to stand in it, and works out the
    /// one uniform factor that would fix it. It MEASURES and it PROPOSES; it changes nothing.
    ///
    /// <para>
    /// The reference is not a cube placed in the scene. It is
    /// <see cref="PlayerFactory.CapsuleHeight"/> = 1.86 m and
    /// <see cref="PlayerFactory.EyeHeight"/> = 1.68 m - the numbers the game actually builds the
    /// player from. A measuring cube is a second source for a number that already has one, and
    /// the moment the two disagree the room is scaled to the cube while the player keeps the
    /// constant. So the constants are read, and the cube, if there is one, is only reported.
    /// </para>
    /// <para>
    /// The measuring itself lives in <see cref="HQRoomMeasurement"/>, shared with the tool that
    /// acts on it. Two copies of "how tall is this room" would agree on the day they were written
    /// and drift the first time either was corrected - silently, because the applier would then
    /// scale by a factor the audit never proposed.
    /// </para>
    /// </summary>
    public static class HQRoomScaleAudit
    {
        private const string MenuPath = "Catch If You Can/Lobby/Raumgroesse messen";

        /// <summary>Ceiling targets to price, in metres of clear height.</summary>
        private static readonly float[] Targets = { 2.80f, 2.95f, 3.10f };

        [MenuItem(MenuPath, false, 41)]
        private static void Measure()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=========================================================");
            sb.AppendLine("RAUMGROESSE  -  ES WURDE NICHTS GEAENDERT");
            sb.AppendLine("=========================================================");
            sb.AppendLine();
            sb.AppendLine("--- REFERENZ: DER SPIELER, WIE DER CODE IHN BAUT ---");
            sb.AppendLine("  Kapselhoehe (PlayerFactory.CapsuleHeight) : " +
                          PlayerFactory.CapsuleHeight.ToString("F2") + " m");
            sb.AppendLine("  Augenhoehe  (PlayerFactory.EyeHeight)     : " +
                          PlayerFactory.EyeHeight.ToString("F2") + " m");
            sb.AppendLine("  Das sind die Zahlen, gegen die gerechnet wird. Ein Messwuerfel in " +
                          "der Szene waere eine");
            sb.AppendLine("  zweite Quelle fuer dieselbe Zahl.");

            List<GameObject> roots = HQRoomMeasurement.CollectRoots();
            AppendScope(roots, sb);

            if (roots.Count == 0)
            {
                sb.AppendLine();
                sb.AppendLine("Kein Objekt gefunden, dessen Name mit '" +
                              HQRoomMeasurement.RoomPrefix + "' beginnt. Ist 01_MainMenu.unity " +
                              "offen?");
                Debug.Log(sb.ToString());
                return;
            }

            HQRoomMeasurement.Result r =
                HQRoomMeasurement.Measure(HQRoomMeasurement.CollectPieces(roots));

            if (r.Pieces.Count == 0)
            {
                sb.AppendLine();
                sb.AppendLine("Die Raumobjekte tragen keinen einzigen Renderer. Nichts zu messen.");
                Debug.Log(sb.ToString());
                return;
            }

            Report(r, sb);
            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// Says what was counted and what was not. A set that is only implied is a set nobody
        /// can check.
        /// </summary>
        internal static void AppendScope(List<GameObject> roots, StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine("--- WAS ALS RAUM GEZAEHLT WIRD (" + roots.Count + " Wurzeln) ---");
            for (int i = 0; i < roots.Count && i < 60; i++)
                sb.AppendLine("    " + roots[i].name + (roots[i].activeSelf ? "" : "   (AUS)"));
            if (roots.Count > 60)
                sb.AppendLine("    ... und " + (roots.Count - 60) + " weitere.");

            sb.AppendLine();
            sb.AppendLine("--- WAS AUSDRUECKLICH NICHT DAZUGEHOERT ---");
            sb.AppendLine("    MainMenu_Lobby und alles darin: Spawn, Lichter, Spiegel, Sessel,");
            sb.AppendLine("      Tisch, Board, Ambience, Exterior - und das Portal.");
            sb.AppendLine("    Main Camera, EventSystem, MainMenu_ModeController,");
            sb.AppendLine("      MainMenu_HorrorEvent, MAIN_MENU_ROOT, die Tuer-Atmosphaere.");
            sb.AppendLine("    Der Spieler wird zur Laufzeit gebaut und ist hier ohnehin nicht.");
        }

        private static void Report(HQRoomMeasurement.Result r, StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine("--- GEMESSEN, IN WELTMETERN ---");
            sb.AppendLine("  Teile mit Renderer   : " + r.Pieces.Count);
            sb.AppendLine("  Aussenmass des Raums : " +
                          r.Room.size.x.ToString("F2") + " x " +
                          r.Room.size.z.ToString("F2") + " m Grundflaeche, " +
                          r.Room.size.y.ToString("F2") + " m hoch");
            sb.AppendLine("  tiefster Punkt       : y = " + r.Room.min.y.ToString("F2"));
            sb.AppendLine("  hoechster Punkt      : y = " + r.Room.max.y.ToString("F2"));

            if (!r.HasClearHeight)
            {
                sb.AppendLine();
                sb.AppendLine("  KEINE LICHTE HOEHE MESSBAR.");
                sb.AppendLine("    flache Teile unten: " + r.FlatLow + ", oben: " + r.FlatHigh);
                sb.AppendLine("  Ohne Boden UND Decke laesst sich der Abstand nicht messen, und " +
                              "eine geratene Zahl");
                sb.AppendLine("  ist hier schlimmer als keine: der Faktor wuerde den ganzen Raum " +
                              "daneben legen.");
                return;
            }

            sb.AppendLine("  Fertigfussboden oben : y = " + r.FloorTop.ToString("F3") +
                          "   (aus " + r.FlatLow + " flachen Teilen unten)");
            sb.AppendLine("  Deckenunterkante     : y = " + r.CeilingBottom.ToString("F3") +
                          "   (aus " + r.FlatHigh + " flachen Teilen oben)");
            sb.AppendLine("  LICHTE HOEHE         : " + r.ClearHeight.ToString("F3") + " m");
            sb.AppendLine("  Der Spieler ist " + PlayerFactory.CapsuleHeight.ToString("F2") +
                          " m hoch, also passt er " +
                          (r.ClearHeight / PlayerFactory.CapsuleHeight).ToString("F2") +
                          " mal untereinander.");

            sb.AppendLine();
            sb.AppendLine("--- JEDE QUELLE MIT IHRER GESETZTEN GROESSE ---");
            sb.AppendLine("  Tuer- und Fensterhoehe hier ablesen, nicht raten.");
            HQRoomMeasurement.AppendSources(r, sb);

            sb.AppendLine();
            sb.AppendLine("--- DER FAKTOR ---");
            sb.AppendLine("  Ein einziger, gleichmaessiger Faktor: Ziel geteilt durch gemessen.");
            sb.AppendLine("  Ziel      Faktor    danach lichte Hoehe");
            for (int i = 0; i < Targets.Length; i++)
            {
                float f = Targets[i] / r.ClearHeight;
                sb.AppendLine("  " + Targets[i].ToString("F2") + " m    " + f.ToString("F4") +
                              "    " + (r.ClearHeight * f).ToString("F2") + " m");
            }

            float chosen = Targets[1] / r.ClearHeight;
            sb.AppendLine();
            sb.AppendLine("  Mit " + chosen.ToString("F4") + " (Ziel " +
                          Targets[1].ToString("F2") + " m) wuerde aus dem Aussenmass:");
            sb.AppendLine("    " + (r.Room.size.x * chosen).ToString("F2") + " x " +
                          (r.Room.size.z * chosen).ToString("F2") + " m Grundflaeche");

            ReportPortal(chosen, sb);
        }

        /// <summary>
        /// The one thing scaling the room makes WORSE, said out loud before anything is applied.
        ///
        /// <para>
        /// The portal's opening is fixed in metres and is not part of the room, so it does not
        /// shrink with it. The wall it has to be cut into does. ResolveWall needs ONE collider at
        /// least as wide as the opening, and the pack's modules are already narrower than that;
        /// after a factor below 1 they are narrower still.
        /// </para>
        /// </summary>
        internal static void ReportPortal(float factor, StringBuilder sb)
        {
            var portals = Object.FindObjectsByType<LobbyPortal>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            sb.AppendLine();
            sb.AppendLine("--- WAS DAS MIT DEM PORTAL MACHT ---");
            if (portals == null || portals.Length == 0)
            {
                sb.AppendLine("  Kein Portal in der Szene gefunden.");
                return;
            }

            LobbyPortal p = portals[0];
            Vector2 opening = p.OpeningSize;
            sb.AppendLine("  Oeffnung : " + opening.x.ToString("F2") + " x " +
                          opening.y.ToString("F2") + " m - in Metern festgelegt und NICHT Teil " +
                          "des Raums,");
            sb.AppendLine("             sie skaliert also nicht mit.");
            sb.AppendLine("  wallCollider: " + (p.AssignedWallCollider != null
                          ? p.AssignedWallCollider.name : "nicht gesetzt"));
            sb.AppendLine();
            sb.AppendLine("  ResolveWall braucht EIN Collider von mindestens " +
                          opening.x.ToString("F2") + " m Breite.");
            sb.AppendLine("  Die Wandmodule stehen in dieser Szene 3.30 m auseinander, sind also " +
                          "schon jetzt zu schmal;");
            sb.AppendLine("  mit Faktor " + factor.ToString("F4") + " werden daraus " +
                          (3.30f * factor).ToString("F2") + " m.");
            sb.AppendLine("  Das Skalieren behebt das Wandproblem nicht, es verschaerft es. Erst " +
                          "messen mit");
            sb.AppendLine("  'Catch If You Can/Lobby/Portalwand messen', dann entscheiden.");
        }
    }
}
