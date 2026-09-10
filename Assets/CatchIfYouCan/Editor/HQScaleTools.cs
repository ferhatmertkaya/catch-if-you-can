#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CatchIfYouCan.EditorTools
{
    /// <summary>
    /// Two commands over one judgement: one that only says what the scene's HQ architecture is
    /// at, and one that offers to bring the untouched pieces to game scale.
    ///
    /// <para>
    /// Both read <see cref="HQScale"/>. A separate copy of "is this piece already corrected"
    /// inside the converter is how a tool ends up scaling something the check called correct.
    /// </para>
    /// </summary>
    public static class HQScaleTools
    {
        private const string CheckPath =
            "Catch If You Can/2. HQ MODULAR HOUSE/HQ-Massstab pruefen [NUR LESEN]";

        private const string MigratePath =
            "Catch If You Can/2. HQ MODULAR HOUSE/Alle HQ-Bauteile auf Spielmass bringen [UNDO]";

        // ------------------------------------------------------------------ read only

        [MenuItem(CheckPath, false, 202)]
        private static void Check()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=========================================================");
            sb.AppendLine("HQ-MASSSTAB  -  ES WURDE NICHTS GEAENDERT");
            sb.AppendLine("=========================================================");

            List<HQScale.Finding> findings = JudgeScene(sb);
            if (findings.Count == 0)
            {
                Debug.Log(sb.ToString());
                return;
            }

            AppendTable(findings, sb);
            AppendSummary(findings, sb);
            Debug.Log(sb.ToString());
        }

        // ------------------------------------------------------------------ audit, then offer

        /// <summary>
        /// Audits first, always, and prints the whole table BEFORE anything can be applied. The
        /// conversion is then offered with the counts in the dialog, so it cannot be taken
        /// without having been read.
        ///
        /// <para>
        /// Only pieces judged ORIGINAL SIZE are converted. Props are left alone - furniture may
        /// already be at real-world size, and shrinking a chair that was right is a silent wrong.
        /// Ambiguous pieces are left alone and named. Anything already correct is left alone,
        /// which is the whole point: applying 0.7526 to a piece that has it makes it 0.5664.
        /// </para>
        /// </summary>
        [MenuItem(MigratePath, false, 203)]
        private static void Migrate()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=========================================================");
            sb.AppendLine("AUF SPIELMASS BRINGEN  -  ZUERST NUR MESSEN");
            sb.AppendLine("=========================================================");

            List<HQScale.Finding> findings = JudgeScene(sb);
            if (findings.Count == 0)
            {
                Debug.Log(sb.ToString());
                return;
            }

            AppendTable(findings, sb);
            AppendSummary(findings, sb);

            var convert = new List<HQScale.Finding>();
            for (int i = 0; i < findings.Count; i++)
                if (findings[i].Verdict == HQScale.Verdict.OriginalSize)
                    convert.Add(findings[i]);

            int risky = Count(findings, HQScale.Verdict.DoubleScaleRisk);
            int ambiguous = Count(findings, HQScale.Verdict.Ambiguous);

            sb.AppendLine();
            if (convert.Count == 0)
            {
                sb.AppendLine("NICHTS ZU TUN: kein Teil steht auf Vendor-Originalgroesse.");
                Debug.Log(sb.ToString());
                return;
            }

            sb.AppendLine("Vorgeschlagen: " + convert.Count + " Teil(e) auf Spielmass bringen.");
            sb.AppendLine("Nicht angefasst: " + Count(findings, HQScale.Verdict.Correct) +
                          " bereits korrekt, " + Count(findings, HQScale.Verdict.Prop) +
                          " Moebel, " + ambiguous + " unklar, " + risky + " Doppelskalierungs-Risiko.");
            Debug.Log(sb.ToString());

            if (risky > 0)
            {
                EditorUtility.DisplayDialog(
                    "Abgebrochen",
                    risky + " Objekt(e) stehen unter einer bereits korrigierten Wurzel und haben " +
                    "trotzdem einen anderen Massstab. Solange das so ist, laesst sich nicht " +
                    "sagen, was hier schon einmal angewendet wurde.\n\n" +
                    "Die Liste steht in der Konsole. Erst das klaeren, dann erneut.",
                    "Verstanden");
                return;
            }

            if (!DangerousCommandGate.Confirm(
                    "Auf Spielmass bringen",
                    "Setzt " + convert.Count + " HQ-Architekturteil(e) auf den gemessenen " +
                    "Spielmassstab " + HQScale.Factor.ToString("F6") + " (" +
                    HQScale.TargetClearHeight.ToString("F2") + " / " +
                    HQScale.ReferenceClearHeight.ToString("F2") + ").\n\n" +
                    "Jedes Teil bekommt einen CIYC-Wrapper; das gekaufte Prefab darin bleibt " +
                    "unberuehrt und behaelt seine Prefab-Verbindung.\n\n" +
                    "Moebel, unklare Teile und alles bereits Korrigierte werden NICHT angefasst.",
                    convert.Count,
                    reimports: false, savesScenes: false,
                    actionLabel: "Ja, " + convert.Count + " Teil(e) umstellen"))
                return;

            int done = 0;
            for (int i = 0; i < convert.Count; i++)
                if (Wrap(convert[i].Object))
                    done++;

            Debug.Log("[CIYC][HQ] " + done + " von " + convert.Count + " Teil(en) auf Spielmass " +
                      "gebracht. Rueckgaengig machbar. Die Szene ist geaendert und NICHT " +
                      "gespeichert.");
        }

        // ------------------------------------------------------------------ the move itself

        /// <summary>
        /// Puts one vendor piece under a CIYC wrapper that carries the correction, without
        /// touching the piece.
        ///
        /// <para>
        /// The wrapper takes the piece's world pose, the piece keeps its prefab link and its own
        /// local values, and the scale is written once, uniformly, on the wrapper. Nothing is
        /// applied back to the purchased asset.
        /// </para>
        /// </summary>
        private static bool Wrap(GameObject piece)
        {
            if (piece == null)
                return false;

            Transform t = piece.transform;
            var wrapper = new GameObject(HQScale.WrapperPrefix + "SPIELMASS_" + piece.name);
            Undo.RegisterCreatedObjectUndo(wrapper, "Auf Spielmass bringen");

            wrapper.transform.SetParent(t.parent, false);
            wrapper.transform.SetPositionAndRotation(t.position, t.rotation);
            wrapper.transform.localScale = Vector3.one;

            Undo.SetTransformParent(t, wrapper.transform, "Auf Spielmass bringen");

            Undo.RecordObject(wrapper.transform, "Auf Spielmass bringen");
            wrapper.transform.localScale = Vector3.one * HQScale.Factor;
            return true;
        }

        // ------------------------------------------------------------------ shared reporting

        private static List<HQScale.Finding> JudgeScene(StringBuilder sb)
        {
            var objects = HQScale.CollectHQObjects();
            var findings = new List<HQScale.Finding>();

            sb.AppendLine();
            if (objects.Count == 0)
            {
                sb.AppendLine("Kein HQ-Objekt in der offenen Szene gefunden.");
                sb.AppendLine("Gesucht wird nach Wrappern mit Praefix '" +
                              HQScale.WrapperPrefix + "' und nach Instanzen aus " +
                              "'Assets/HQ Modular House'.");
                return findings;
            }

            sb.AppendLine("Gefunden: " + objects.Count + " HQ-Objekt(e), oberster Treffer je " +
                          "Zweig, inaktive eingeschlossen.");
            sb.AppendLine("Spielmassstab: " + HQScale.Factor.ToString("F6") + "   (" +
                          HQScale.TargetClearHeight.ToString("F2") + " / " +
                          HQScale.ReferenceClearHeight.ToString("F2") + ")");

            for (int i = 0; i < objects.Count; i++)
                findings.Add(HQScale.Judge(objects[i]));

            return findings;
        }

        private static void AppendTable(List<HQScale.Finding> findings, StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine("--- JEDES OBJEKT ---");
            for (int i = 0; i < findings.Count; i++)
            {
                HQScale.Finding f = findings[i];
                sb.AppendLine();
                sb.AppendLine("  " + Label(f.Verdict) + "  " + Path(f.Object.transform));
                sb.AppendLine("    Vendor-Quelle    : " +
                              (string.IsNullOrEmpty(f.Source) ? "(kein Prefab)" : f.Source));
                sb.AppendLine("    effektiver Massstab: " + HQScale.Fmt(f.Effective));
                sb.AppendLine("    Weltmasse        : " + HQScale.Metres(f.WorldSize));
                if (f.Verdict == HQScale.Verdict.OriginalSize)
                    sb.AppendLine("    danach           : " +
                                  HQScale.Metres(f.WorldSize * HQScale.Factor));
                sb.AppendLine("    Begruendung      : " + f.Why);
            }
        }

        private static void AppendSummary(List<HQScale.Finding> findings, StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine("--- ZUSAMMENFASSUNG ---");
            sb.AppendLine("  bereits korrekt        : " + Count(findings, HQScale.Verdict.Correct));
            sb.AppendLine("  Vendor-Originalgroesse : " + Count(findings, HQScale.Verdict.OriginalSize));
            sb.AppendLine("  Moebel / lose Objekte  : " + Count(findings, HQScale.Verdict.Prop));
            sb.AppendLine("  unklar                 : " + Count(findings, HQScale.Verdict.Ambiguous));
            sb.AppendLine("  Doppelskalierungsrisiko: " + Count(findings, HQScale.Verdict.DoubleScaleRisk));
            sb.AppendLine();
            sb.AppendLine("  Unklare Teile werden GENANNT, nicht geraten. Ein Moebelstueck kann " +
                          "schon Realmass haben;");
            sb.AppendLine("  eines zu verkleinern, das richtig war, sieht man nie.");
        }

        private static int Count(List<HQScale.Finding> f, HQScale.Verdict v)
        {
            int n = 0;
            for (int i = 0; i < f.Count; i++)
                if (f[i].Verdict == v)
                    n++;
            return n;
        }

        private static string Label(HQScale.Verdict v)
        {
            switch (v)
            {
                case HQScale.Verdict.Correct:         return "[KORREKT ]";
                case HQScale.Verdict.OriginalSize:    return "[ORIGINAL]";
                case HQScale.Verdict.Prop:            return "[MOEBEL  ]";
                case HQScale.Verdict.DoubleScaleRisk: return "[DOPPELT?]";
                default:                              return "[UNKLAR  ]";
            }
        }

        private static string Path(Transform t)
        {
            var sb = new StringBuilder(t.name);
            for (Transform p = t.parent; p != null; p = p.parent)
                sb.Insert(0, p.name + "/");
            return sb.ToString();
        }
    }
}
#endif
