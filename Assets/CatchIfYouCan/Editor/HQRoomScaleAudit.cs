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
    /// World bounds are the right measurement here and NOT CLAUDE.md mistake 12. That mistake was
    /// dividing a wanted size by a WORLD AABB to get a LOCAL scale, which double-applies every
    /// ancestor's scale. What is wanted here is a world height in metres, and the factor is a
    /// RATIO of two world heights - the ancestor chain cancels out of a ratio.
    /// </para>
    /// <para>
    /// The clear height between finished floor and ceiling underside is the only room dimension
    /// this derives the factor from, because it is the only one that can be measured without
    /// guessing which piece is a door and which is a window. Doors and windows are listed with
    /// their measured sizes so the numbers can be read off, not inferred.
    /// </para>
    /// </summary>
    public static class HQRoomScaleAudit
    {
        private const string MenuPath = "Catch If You Can/Lobby/Raumgroesse messen";

        /// <summary>Roots whose name starts with this are the hand-placed pack pieces.</summary>
        private const string RoomPrefix = "HQ_";

        /// <summary>The slab under the room, placed by hand beside the pack pieces.</summary>
        private const string FloorName = "FLOOR_Lobby_01";

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

            var room = CollectRoom(sb);
            if (room.Count == 0)
            {
                sb.AppendLine();
                sb.AppendLine("Kein Objekt gefunden, dessen Name mit '" + RoomPrefix +
                              "' beginnt. Ist 01_MainMenu.unity offen?");
                Debug.Log(sb.ToString());
                return;
            }

            var pieces = new List<Piece>();
            for (int i = 0; i < room.Count; i++)
                Collect(room[i], pieces);

            if (pieces.Count == 0)
            {
                sb.AppendLine();
                sb.AppendLine("Die Raumobjekte tragen keinen einzigen Renderer. Nichts zu messen.");
                Debug.Log(sb.ToString());
                return;
            }

            Report(pieces, sb);
            Debug.Log(sb.ToString());
        }

        private struct Piece
        {
            public Transform Transform;
            public Bounds World;
            public string Source;
        }

        /// <summary>
        /// The room, by name rather than by selection, so the same set is measured every time and
        /// the report can be compared with the last one. Every root taken is named in the report:
        /// a set that is only implied is a set nobody can check.
        /// </summary>
        private static List<GameObject> CollectRoom(StringBuilder sb)
        {
            var roots = new List<GameObject>();
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var all = scene.GetRootGameObjects();

            for (int i = 0; i < all.Length; i++)
            {
                string n = all[i].name;
                if (n.StartsWith(RoomPrefix) || n == FloorName)
                    roots.Add(all[i]);
            }

            sb.AppendLine();
            sb.AppendLine("--- WAS ALS RAUM GEZAEHLT WIRD (" + roots.Count + " Wurzeln) ---");
            for (int i = 0; i < roots.Count && i < 60; i++)
                sb.AppendLine("    " + roots[i].name +
                              (roots[i].activeSelf ? "" : "   (AUS)"));
            if (roots.Count > 60)
                sb.AppendLine("    ... und " + (roots.Count - 60) + " weitere.");

            sb.AppendLine();
            sb.AppendLine("--- WAS AUSDRUECKLICH NICHT DAZUGEHOERT ---");
            sb.AppendLine("    MainMenu_Lobby und alles darin: Spawn, Lichter, Spiegel, Sessel,");
            sb.AppendLine("      Tisch, Board, Ambience, Exterior - und das Portal.");
            sb.AppendLine("    Main Camera, EventSystem, MainMenu_ModeController,");
            sb.AppendLine("      MainMenu_HorrorEvent, MAIN_MENU_ROOT, die Tuer-Atmosphaere.");
            sb.AppendLine("    Der Spieler wird zur Laufzeit gebaut und ist hier ohnehin nicht.");

            return roots;
        }

        private static void Collect(GameObject go, List<Piece> into)
        {
            var renderers = go.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer r = renderers[i];
                if (r == null)
                    continue;
                Bounds b = r.bounds;
                if (b.size == Vector3.zero)
                    continue;

                // Grouped by the MESH, not by the prefab asset. Asking PrefabUtility which
                // asset a renderer came from would work in Unity and is unverifiable here - the
                // offline stub does not carry that overload, and a stub agreeing with me is not
                // verification (CLAUDE.md mistake 9). The mesh name is on the object itself, is
                // what the Inspector shows, and this pack gives its meshes distinct names.
                var mf = r.GetComponent<MeshFilter>();
                string mesh = mf != null && mf.sharedMesh != null ? mf.sharedMesh.name : "?";
                into.Add(new Piece
                {
                    Transform = r.transform,
                    World = b,
                    Source = mesh + "   in " + NearestNamedAncestor(r.transform)
                });
            }
        }

        private static void Report(List<Piece> pieces, StringBuilder sb)
        {
            // The room's outer box, and the two surfaces that decide the clear height. A floor
            // piece is wide and flat and low; a ceiling piece is wide and flat and high. Neither
            // is decided by name - this pack numbers its prefabs, so a name says nothing.
            Bounds room = pieces[0].World;
            for (int i = 1; i < pieces.Count; i++)
                room.Encapsulate(pieces[i].World);

            float lowest = room.min.y, highest = room.max.y;
            float mid = (lowest + highest) * 0.5f;

            float floorTop = float.NegativeInfinity;
            float ceilingBottom = float.PositiveInfinity;
            int flatLow = 0, flatHigh = 0;

            for (int i = 0; i < pieces.Count; i++)
            {
                Bounds b = pieces[i].World;
                bool flat = b.size.y < Mathf.Min(b.size.x, b.size.z) * 0.5f;
                if (!flat)
                    continue;

                if (b.center.y < mid)
                {
                    flatLow++;
                    floorTop = Mathf.Max(floorTop, b.max.y);
                }
                else
                {
                    flatHigh++;
                    ceilingBottom = Mathf.Min(ceilingBottom, b.min.y);
                }
            }

            sb.AppendLine();
            sb.AppendLine("--- GEMESSEN, IN WELTMETERN ---");
            sb.AppendLine("  Teile mit Renderer   : " + pieces.Count);
            sb.AppendLine("  Aussenmass des Raums : " +
                          room.size.x.ToString("F2") + " x " +
                          room.size.z.ToString("F2") + " m Grundflaeche, " +
                          room.size.y.ToString("F2") + " m hoch");
            sb.AppendLine("  tiefster Punkt       : y = " + lowest.ToString("F2"));
            sb.AppendLine("  hoechster Punkt      : y = " + highest.ToString("F2"));

            bool haveClear = flatLow > 0 && flatHigh > 0 && ceilingBottom > floorTop;
            if (!haveClear)
            {
                sb.AppendLine();
                sb.AppendLine("  KEINE LICHTE HOEHE MESSBAR.");
                sb.AppendLine("    flache Teile unten: " + flatLow + ", oben: " + flatHigh);
                sb.AppendLine("  Ohne Boden UND Decke laesst sich der Abstand nicht messen, und " +
                              "eine geratene Zahl");
                sb.AppendLine("  ist hier schlimmer als keine: der Faktor wuerde den ganzen Raum " +
                              "daneben legen.");
                return;
            }

            float clear = ceilingBottom - floorTop;
            sb.AppendLine("  Fertigfussboden oben : y = " + floorTop.ToString("F3") +
                          "   (aus " + flatLow + " flachen Teilen unten)");
            sb.AppendLine("  Deckenunterkante     : y = " + ceilingBottom.ToString("F3") +
                          "   (aus " + flatHigh + " flachen Teilen oben)");
            sb.AppendLine("  LICHTE HOEHE         : " + clear.ToString("F3") + " m");
            sb.AppendLine("  Der Spieler ist " + PlayerFactory.CapsuleHeight.ToString("F2") +
                          " m hoch, also passt er " + (clear / PlayerFactory.CapsuleHeight)
                              .ToString("F2") + " mal untereinander.");

            // Distinct sources, with the size each one is placed at. This is where a door and a
            // window can be read off rather than guessed.
            var bySource = new Dictionary<string, Bounds>();
            var count = new Dictionary<string, int>();
            for (int i = 0; i < pieces.Count; i++)
            {
                string s = pieces[i].Source;
                if (bySource.TryGetValue(s, out Bounds have))
                {
                    // The biggest of that source, so a door leaf is not averaged into its frame.
                    if (pieces[i].World.size.magnitude > have.size.magnitude)
                        bySource[s] = pieces[i].World;
                    count[s] = count[s] + 1;
                }
                else
                {
                    bySource[s] = pieces[i].World;
                    count[s] = 1;
                }
            }

            sb.AppendLine();
            sb.AppendLine("--- JEDE QUELLE MIT IHRER GESETZTEN GROESSE ---");
            sb.AppendLine("  Tuer- und Fensterhoehe hier ablesen, nicht raten.");
            foreach (var kv in bySource)
            {
                Vector3 s = kv.Value.size;
                sb.AppendLine("    " + s.x.ToString("F2").PadLeft(6) + " x " +
                              s.y.ToString("F2").PadLeft(6) + " x " +
                              s.z.ToString("F2").PadLeft(6) + " m   x" +
                              count[kv.Key].ToString().PadLeft(3) + "   " +
                              Shorten(kv.Key));
            }

            // The factor.
            sb.AppendLine();
            sb.AppendLine("--- DER FAKTOR ---");
            sb.AppendLine("  Ein einziger, gleichmaessiger Faktor: Ziel geteilt durch gemessen.");
            sb.AppendLine("  Ziel      Faktor    danach lichte Hoehe");
            for (int i = 0; i < Targets.Length; i++)
            {
                float f = Targets[i] / clear;
                sb.AppendLine("  " + Targets[i].ToString("F2") + " m    " + f.ToString("F4") +
                              "    " + (clear * f).ToString("F2") + " m");
            }

            float chosen = Targets[1] / clear;
            sb.AppendLine();
            sb.AppendLine("  Mit " + chosen.ToString("F4") + " (Ziel " +
                          Targets[1].ToString("F2") + " m) wuerde aus dem Aussenmass:");
            sb.AppendLine("    " + (room.size.x * chosen).ToString("F2") + " x " +
                          (room.size.z * chosen).ToString("F2") + " m Grundflaeche");

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
        private static void ReportPortal(float factor, StringBuilder sb)
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

        /// <summary>
        /// The first ancestor whose name says which placed piece this is - the wrapper the room
        /// was built out of, rather than a numbered child inside a vendor prefab.
        /// </summary>
        private static string NearestNamedAncestor(Transform t)
        {
            for (Transform p = t; p != null; p = p.parent)
            {
                if (p.parent == null || p.name.StartsWith(RoomPrefix))
                    return p.name;
            }
            return t.name;
        }

        private static string Shorten(string path)
        {
            if (string.IsNullOrEmpty(path))
                return "(unbekannt)";
            int i = path.LastIndexOf('/');
            return i >= 0 && i < path.Length - 1 ? path.Substring(i + 1) : path;
        }
    }
}
