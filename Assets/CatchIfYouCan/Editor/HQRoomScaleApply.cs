using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CatchIfYouCan.Environment;
using CatchIfYouCan.Player;

namespace CatchIfYouCan.EditorTools
{
    /// <summary>
    /// Applies the measured uniform correction to the hand-built lobby: one root, one factor, one
    /// vertical move, and then it MEASURES AGAIN and says what it actually got.
    ///
    /// <para>
    /// The factor is derived, not typed: <see cref="TargetClearHeight"/> divided by
    /// <see cref="ExpectedClearHeight"/> = 2.95 / 3.92 = 0.752551. That is the 0.7526 the
    /// measurement produced; writing the quotient rather than the rounded result keeps the two
    /// numbers from drifting apart later, and over the room's full height the difference is
    /// 0.2 mm.
    /// </para>
    /// <para>
    /// It refuses to run unless the room it finds is still the room that was measured. A factor
    /// is only valid for one measurement: applied to a room somebody has since rebuilt, it is
    /// simply a wrong number, and a uniformly wrong room is the hardest kind of wrong to see.
    /// </para>
    /// <para>
    /// Reparenting goes through <see cref="Undo.SetTransformParent"/>, which preserves the world
    /// transform, and every root's world position, rotation and lossy scale is re-measured
    /// afterwards. A drift is REPORTED, never silently corrected by hand: a manual correction
    /// hides the bad reparent that caused it.
    /// </para>
    /// <para>
    /// What it does NOT do: it does not touch the player, the camera, the portal, any vendor
    /// prefab, any material, or any individual piece's own scale. And it does not save the scene
    /// - it leaves it dirty so the result can be looked at first.
    /// </para>
    /// </summary>
    public static class HQRoomScaleApply
    {
        private const string MenuPath = "Catch If You Can/Lobby/Raum skalieren";
        private const string RootName = "HQ_ROOM_SCALE_ROOT";

        /// <summary>The clear height the factor was derived from. Verified before applying.</summary>
        private const float ExpectedClearHeight = 3.92f;

        /// <summary>The clear height wanted, in metres.</summary>
        private const float TargetClearHeight = 2.95f;

        /// <summary>How far the room may have moved since it was measured and still count.</summary>
        private const float PreconditionTolerance = 0.05f;

        /// <summary>How close the result must land to be reported as achieved.</summary>
        private const float ResultTolerance = 0.02f;

        /// <summary>Derived, never typed. 2.95 / 3.92.</summary>
        private static float Factor => TargetClearHeight / ExpectedClearHeight;

        [MenuItem(MenuPath, false, 42)]
        private static void Apply()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=========================================================");
            sb.AppendLine("RAUM SKALIEREN");
            sb.AppendLine("=========================================================");

            // ---------- precondition ----------

            List<GameObject> roots = HQRoomMeasurement.CollectRoots();
            roots.RemoveAll(g => g != null && g.name == RootName);

            GameObject existing = FindRoot();
            if (existing != null)
            {
                sb.AppendLine();
                sb.AppendLine("ABGEBROCHEN: '" + RootName + "' gibt es schon, mit Massstab " +
                              existing.transform.localScale.x.ToString("F4") + ".");
                sb.AppendLine("Ein zweites Mal skalieren wuerde den Faktor quadrieren. Wer neu " +
                              "anfangen will,");
                sb.AppendLine("macht das ueber Undo oder loest die Wurzel von Hand auf.");
                Debug.LogWarning(sb.ToString());
                return;
            }

            if (roots.Count == 0)
            {
                sb.AppendLine();
                sb.AppendLine("ABGEBROCHEN: kein Objekt gefunden, dessen Name mit '" +
                              HQRoomMeasurement.RoomPrefix + "' beginnt.");
                Debug.LogWarning(sb.ToString());
                return;
            }

            HQRoomMeasurement.Result before =
                HQRoomMeasurement.Measure(HQRoomMeasurement.CollectPieces(roots));

            if (!before.HasClearHeight)
            {
                sb.AppendLine();
                sb.AppendLine("ABGEBROCHEN: die lichte Hoehe ist nicht messbar (flach unten " +
                              before.FlatLow + ", oben " + before.FlatHigh + ").");
                sb.AppendLine("Ohne sie ist der Faktor durch nichts gedeckt.");
                Debug.LogWarning(sb.ToString());
                return;
            }

            float drift = Mathf.Abs(before.ClearHeight - ExpectedClearHeight);
            if (drift > PreconditionTolerance)
            {
                sb.AppendLine();
                sb.AppendLine("ABGEBROCHEN: der Raum ist nicht mehr der gemessene.");
                sb.AppendLine("  erwartet : " + ExpectedClearHeight.ToString("F2") + " m");
                sb.AppendLine("  gefunden : " + before.ClearHeight.ToString("F3") + " m" +
                              "   (Abweichung " + drift.ToString("F3") + " m)");
                sb.AppendLine("Der Faktor gilt nur fuer die Messung, aus der er stammt. Erst " +
                              "neu messen mit");
                sb.AppendLine("'Catch If You Can/Lobby/Raumgroesse messen'.");
                Debug.LogWarning(sb.ToString());
                return;
            }

            sb.AppendLine();
            sb.AppendLine("--- VORHER ---");
            sb.AppendLine("  lichte Hoehe        : " + before.ClearHeight.ToString("F3") + " m");
            sb.AppendLine("  Fertigfussboden oben: y = " + before.FloorTop.ToString("F3"));
            sb.AppendLine("  Faktor              : " + Factor.ToString("F6") +
                          "   (" + TargetClearHeight.ToString("F2") + " / " +
                          ExpectedClearHeight.ToString("F2") + ")");

            // ---------- the move ----------

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Raum skalieren");
            // At the origin, unrotated, unscaled, so it cannot shift what is put into it.
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            root.transform.localScale = Vector3.one;

            var recorded = new List<(GameObject go, Vector3 pos, Quaternion rot, Vector3 scale)>();
            for (int i = 0; i < roots.Count; i++)
            {
                Transform t = roots[i].transform;
                recorded.Add((roots[i], t.position, t.rotation, t.lossyScale));
            }

            for (int i = 0; i < roots.Count; i++)
                Undo.SetTransformParent(roots[i].transform, root.transform, "Raum skalieren");

            // Re-measured, not assumed. SetTransformParent preserves the world transform; saying
            // so is not the same as having checked.
            var drifted = new List<string>();
            for (int i = 0; i < recorded.Count; i++)
            {
                Transform t = recorded[i].go.transform;
                if ((t.position - recorded[i].pos).sqrMagnitude > 1e-8f
                    || Quaternion.Angle(t.rotation, recorded[i].rot) > 0.01f
                    || (t.lossyScale - recorded[i].scale).sqrMagnitude > 1e-8f)
                    drifted.Add(recorded[i].go.name);
            }

            Undo.RecordObject(root.transform, "Raum skalieren");
            root.transform.localScale = Vector3.one * Factor;

            // The floor rule, measured rather than computed: the pieces have moved, so ask them
            // again where their top is instead of multiplying the old number.
            HQRoomMeasurement.Result scaled =
                HQRoomMeasurement.Measure(HQRoomMeasurement.CollectPieces(roots));

            if (scaled.HasClearHeight)
            {
                Vector3 p = root.transform.position;
                p.y -= scaled.FloorTop;
                root.transform.position = p;
            }

            HQRoomMeasurement.Result after =
                HQRoomMeasurement.Measure(HQRoomMeasurement.CollectPieces(roots));

            // ---------- what was actually achieved ----------

            sb.AppendLine();
            sb.AppendLine("--- UNTER '" + RootName + "' (" + roots.Count + " Wurzeln) ---");
            for (int i = 0; i < roots.Count; i++)
                sb.AppendLine("    " + roots[i].name +
                              (roots[i].activeSelf ? "" : "   (AUS)"));

            sb.AppendLine();
            if (drifted.Count == 0)
            {
                sb.AppendLine("  Beim Umhaengen hat sich nichts verschoben (" + recorded.Count +
                              " geprueft).");
            }
            else
            {
                sb.AppendLine("  ACHTUNG: beim Umhaengen verschoben, NICHT von Hand korrigiert:");
                for (int i = 0; i < drifted.Count && i < 20; i++)
                    sb.AppendLine("    " + drifted[i]);
                sb.AppendLine("  Eine Handkorrektur wuerde die Ursache verdecken. Rueckgaengig " +
                              "machen und nachsehen.");
            }

            sb.AppendLine();
            sb.AppendLine("--- NACHHER, GEMESSEN ---");
            sb.AppendLine("  " + RootName + " Position : " +
                          Fmt(root.transform.position));
            sb.AppendLine("  " + RootName + " Massstab : " +
                          root.transform.localScale.x.ToString("F6") + " (gleichmaessig)");

            if (!after.HasClearHeight)
            {
                sb.AppendLine("  LICHTE HOEHE NICHT MEHR MESSBAR - das ist ein Befund, kein " +
                              "Erfolg.");
                Debug.LogWarning(sb.ToString());
                return;
            }

            float heightMiss = Mathf.Abs(after.ClearHeight - TargetClearHeight);
            float floorMiss = Mathf.Abs(after.FloorTop);

            sb.AppendLine("  lichte Hoehe         : " + after.ClearHeight.ToString("F3") +
                          " m   (Ziel " + TargetClearHeight.ToString("F2") + ", Abweichung " +
                          heightMiss.ToString("F3") + ")   " +
                          (heightMiss <= ResultTolerance ? "erreicht" : "NICHT ERREICHT"));
            sb.AppendLine("  Fertigfussboden oben : y = " + after.FloorTop.ToString("F4") +
                          "   (Ziel 0, Abweichung " + floorMiss.ToString("F4") + ")   " +
                          (floorMiss <= ResultTolerance ? "erreicht" : "NICHT ERREICHT"));
            sb.AppendLine("  Aussenmass           : " + after.Room.size.x.ToString("F2") + " x " +
                          after.Room.size.z.ToString("F2") + " m, " +
                          after.Room.size.y.ToString("F2") + " m hoch");
            sb.AppendLine("  Der Spieler ist " + PlayerFactory.CapsuleHeight.ToString("F2") +
                          " m hoch und hat jetzt " +
                          (after.ClearHeight - PlayerFactory.CapsuleHeight).ToString("F2") +
                          " m Luft ueber dem Kopf.");

            sb.AppendLine();
            sb.AppendLine("--- JEDE QUELLE MIT IHRER NEUEN GROESSE ---");
            sb.AppendLine("  Tuer- und Fensterhoehe hier ABLESEN, nicht aus dem Faktor rechnen.");
            HQRoomMeasurement.AppendSources(after, sb);

            ReportStrandedAnchors(before, after, sb);
            HQRoomScaleAudit.ReportPortal(Factor, sb);

            sb.AppendLine();
            sb.AppendLine("Die Szene ist geaendert, aber NICHT gespeichert. Erst ansehen, dann " +
                          "speichern.");

            EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Selection.activeGameObject = root;
            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// The part the task description does not cover, and it is not small.
        ///
        /// <para>
        /// The room shrinks. The things standing IN it - the player's spawn, the lights, the
        /// mirror, the armchair, the table, the board, and the portal - are excluded on purpose
        /// and do not. So they end up in the wrong place relative to the walls, and no choice of
        /// pivot fixes that: they are spread across the room, and one factor cannot hold more
        /// than one of them still.
        /// </para>
        /// <para>
        /// This reports, for each of them, the position the same map would give it. It applies
        /// none of them. Moving the spawn is a decision about where the player stands; moving the
        /// portal is a decision about a doorway that is currently unresolved anyway.
        /// </para>
        /// </summary>
        private static void ReportStrandedAnchors(HQRoomMeasurement.Result before,
                                                  HQRoomMeasurement.Result after,
                                                  StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine("--- WAS JETZT NEBEN DEM RAUM STEHT ---");
            sb.AppendLine("  Der Raum ist kleiner geworden. Was drin steht, nicht - das war so " +
                          "vorgegeben.");
            sb.AppendLine("  Diese Objekte muessten durch dieselbe Abbildung, sonst stehen sie " +
                          "in einer Wand");
            sb.AppendLine("  oder daneben. NICHTS DAVON WURDE ANGEWENDET.");
            sb.AppendLine();
            sb.AppendLine("  Abbildung: p_neu = p_alt * " + Factor.ToString("F6") +
                          "  +  (0, " + (-after.FloorTop + 0f).ToString("F4") + ", 0)" +
                          "   ... y aus der Bodenregel");

            var lobby = GameObject.Find("MainMenu_Lobby");
            if (lobby == null)
            {
                // Find skips inactive objects, and the lobby is saved switched off - which is the
                // only kind this looks for.
                var all = UnityEngine.SceneManagement.SceneManager.GetActiveScene()
                          .GetRootGameObjects();
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i].name == "MainMenu_Lobby")
                    {
                        lobby = all[i];
                        break;
                    }
                }
            }

            if (lobby == null)
            {
                sb.AppendLine("  MainMenu_Lobby nicht gefunden.");
                return;
            }

            float yOffset = -after.FloorTop;
            var children = lobby.GetComponentsInChildren<Transform>(true);
            int shown = 0;
            for (int i = 0; i < children.Length; i++)
            {
                Transform t = children[i];
                if (t == lobby.transform || t.parent != lobby.transform)
                    continue;
                Vector3 p = t.position;
                Vector3 mapped = new Vector3(p.x * Factor, p.y * Factor + yOffset, p.z * Factor);
                if ((mapped - p).sqrMagnitude < 1e-6f)
                    continue;
                shown++;
                if (shown <= 30)
                    sb.AppendLine("    " + t.name.PadRight(26) + Fmt(p) + "  ->  " + Fmt(mapped));
            }
            if (shown > 30)
                sb.AppendLine("    ... und " + (shown - 30) + " weitere.");
            if (shown == 0)
                sb.AppendLine("    nichts - alles steht schon richtig.");
        }

        private static GameObject FindRoot()
        {
            var all = UnityEngine.SceneManagement.SceneManager.GetActiveScene()
                      .GetRootGameObjects();
            for (int i = 0; i < all.Length; i++)
                if (all[i].name == RootName)
                    return all[i];
            return null;
        }

        private static string Fmt(Vector3 v) =>
            "(" + v.x.ToString("F2") + ", " + v.y.ToString("F2") + ", " + v.z.ToString("F2") + ")";
    }
}
