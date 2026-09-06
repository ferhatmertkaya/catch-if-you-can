using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using CatchIfYouCan.Environment;

namespace CatchIfYouCan.EditorTools
{
    /// <summary>
    /// Says what actually stands at the portal's opening, and why <c>ResolveWall</c> would accept
    /// or reject each of it. It MEASURES; it changes nothing.
    ///
    /// <para>
    /// The lobby's procedural shell was replaced by hand-placed pieces from the purchased pack,
    /// and <c>Lobby_Wall_North</c> - the one solid box the tear was cut into - went with it. The
    /// portal has no <c>wallCollider</c> assigned, so at runtime it falls back to finding the
    /// wall by SHAPE: one collider, at most <c>maxWallThickness</c> across the opening's normal,
    /// and at least as wide and as tall as the opening itself. A wall module of the pack is
    /// 3.97 m wide against an opening of 4.70 m, and each module is its own collider, so two
    /// modules side by side do not add up to one wide enough. Whether that is what happens here
    /// depends on facts only the installed pack can answer - do those prefabs carry colliders at
    /// all, and how wide is the one at the opening - which is why this reads them instead of
    /// arguing about them.
    /// </para>
    /// <para>
    /// Two passes, because they answer different questions and disagree for a good reason:
    /// </para>
    /// <para>
    /// The GEOMETRIC pass walks every collider in the open scenes, inactive ones included, and
    /// applies exactly the three tests <c>ResolveWall</c> applies. Inactive ones included is the
    /// point: <c>MainMenu_Lobby</c> is saved switched off, so in the editor its colliders are not
    /// in the physics scene at all, while at runtime the room is switched on before the portal
    /// ever opens. A physics query alone would therefore report the room as empty and be wrong
    /// about the only moment that matters.
    /// </para>
    /// <para>
    /// The PHYSICS pass runs the same <c>Physics.OverlapBox</c> the runtime runs, and is reported
    /// as what the editor's physics scene can see right now - not as a verdict. Where the two
    /// disagree, the difference is itself the finding.
    /// </para>
    /// <para>
    /// It also lists renderers that overlap the opening and carry NO collider, because "the wall
    /// is not solid" and "the wall is too narrow" look identical from inside the game and need
    /// different repairs.
    /// </para>
    /// </summary>
    public static class LobbyPortalWallProbe
    {
        private const string MenuPath = "Catch If You Can/Lobby/Portalwand messen";

        [MenuItem(MenuPath, false, 40)]
        private static void Measure()
        {
            var portals = Object.FindObjectsByType<LobbyPortal>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (portals == null || portals.Length == 0)
            {
                Debug.LogWarning("[CIYC] Portalwand messen: kein LobbyPortal in den offenen " +
                                 "Szenen. 01_MainMenu.unity oeffnen.");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("=========================================================");
            sb.AppendLine("PORTALWAND  -  ES WURDE NICHTS GEAENDERT");
            sb.AppendLine("=========================================================");

            for (int i = 0; i < portals.Length; i++)
            {
                if (i > 0)
                    sb.AppendLine();
                Report(portals[i], sb);
            }

            if (portals.Length > 1)
            {
                sb.AppendLine();
                sb.AppendLine("HINWEIS: " + portals.Length + " Portale gefunden. Es darf genau " +
                              "eines geben - check_ui_and_portal.sh prueft das.");
            }

            sb.AppendLine();
            sb.AppendLine("ENDE. Keine Datei und keine Komponente wurde angefasst.");
            Debug.Log(sb.ToString());
        }

        private static void Report(LobbyPortal portal, StringBuilder sb)
        {
            Transform t = portal.transform;
            Vector2 opening = portal.OpeningSize;
            float maxThickness = portal.MaxWallThickness;

            // The same box ResolveWall builds: centred half the opening's height above the
            // portal's origin, because the portal's origin sits on the floor.
            Vector3 centre = t.position + t.up * (opening.y * 0.5f);
            Vector3 half = new Vector3(opening.x * 0.5f,
                                       opening.y * 0.5f,
                                       Mathf.Max(0.1f, maxThickness));

            sb.AppendLine();
            sb.AppendLine("--- PORTAL: " + Path(t) + " ---");
            sb.AppendLine("  Szene            : " + t.gameObject.scene.name +
                          (t.gameObject.activeInHierarchy ? "" : "   (Objekt ist AUS)"));
            sb.AppendLine("  Position         : " + Fmt(t.position));
            sb.AppendLine("  Blickrichtung    : " + Fmt(t.forward));
            sb.AppendLine("  Oeffnung         : " + opening.x.ToString("F2") + " x " +
                          opening.y.ToString("F2") + " m");
            sb.AppendLine("  maxWallThickness : " + maxThickness.ToString("F2") + " m");
            sb.AppendLine("  Suchbox (Mitte)  : " + Fmt(centre));

            Collider wired = portal.AssignedWallCollider;
            if (wired != null)
            {
                sb.AppendLine("  wallCollider     : " + Path(wired.transform) +
                              "   -> gesetzt, die Formsuche laeuft gar nicht erst");
                Describe(wired, t, opening, maxThickness, sb, "    ");
                sb.AppendLine();
                sb.AppendLine("  Der Rest dieses Berichts ist trotzdem interessant: das " +
                              "eingetragene Collider muss die Oeffnung auch WIRKLICH ueberdecken,");
                sb.AppendLine("  sonst schneidet EnsureWallAperture ein Loch, das ueber die Wand " +
                              "hinausragt.");
            }
            else
            {
                sb.AppendLine("  wallCollider     : NICHT gesetzt -> gesucht wird per FORM");
            }

            // ---------- geometric pass ----------

            sb.AppendLine();
            sb.AppendLine("  --- WAS DIE OEFFNUNG UEBERDECKT (geometrisch, inaktive " +
                          "eingeschlossen) ---");
            sb.AppendLine("  Das ist der Zustand, auf den es ankommt: zur Laufzeit ist die Lobby " +
                          "eingeschaltet,");
            sb.AppendLine("  bevor das Portal aufgeht. Im Editor ist sie es nicht.");

            Bounds probe = new Bounds(centre, Vector3.zero);
            probe.Encapsulate(centre + half);
            probe.Encapsulate(centre - half);

            var colliders = Object.FindObjectsByType<Collider>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            var overlapping = new List<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider c = colliders[i];
                if (c == null || c.isTrigger)
                    continue;
                if (c.transform == t || c.transform.IsChildOf(t))
                    continue;
                Bounds b = MeasuredBounds(c);
                if (b.size == Vector3.zero)
                    continue;
                if (b.Intersects(probe))
                    overlapping.Add(c);
            }

            if (overlapping.Count == 0)
            {
                sb.AppendLine("  NICHTS. Kein einziges Collider ueberdeckt die Oeffnung.");
                sb.AppendLine("  Dann ist der Riss ein Bild: der Spieler laeuft durch eine Wand, " +
                              "die er sehen kann,");
                sb.AppendLine("  und EnsureWallAperture bricht mit einer Fehlermeldung ab.");
            }
            else
            {
                int accepted = 0;
                for (int i = 0; i < overlapping.Count; i++)
                {
                    if (Describe(overlapping[i], t, opening, maxThickness, sb, "  "))
                        accepted++;
                }

                sb.AppendLine();
                sb.AppendLine("  ERGEBNIS: " + accepted + " von " + overlapping.Count +
                              " Collidern wuerde ResolveWall annehmen.");
                if (accepted == 0)
                {
                    sb.AppendLine("  Bei null nimmt das Portal KEINE Wand und schneidet KEIN " +
                                  "Loch. Zwei Wandmodule");
                    sb.AppendLine("  nebeneinander helfen nicht: jedes ist ein eigenes Collider " +
                                  "und wird einzeln gemessen.");
                }
                else if (accepted > 1)
                {
                    sb.AppendLine("  Bei mehr als einem gewinnt das BREITESTE. Das ist definiert, " +
                                  "aber nicht unbedingt");
                    sb.AppendLine("  das gemeinte - hier lohnt sich ein ausdrueckliches " +
                                  "wallCollider.");
                }
            }

            // ---------- renderers without a collider ----------

            sb.AppendLine();
            sb.AppendLine("  --- SICHTBAR, ABER NICHT FEST ---");
            var renderers = Object.FindObjectsByType<MeshRenderer>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            int solidless = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer r = renderers[i];
                if (r == null || r.transform == t || r.transform.IsChildOf(t))
                    continue;
                if (r.GetComponent<Collider>() != null ||
                    r.GetComponentInParent<Collider>() != null)
                    continue;
                Bounds b = r.bounds;
                if (b.size == Vector3.zero || !b.Intersects(probe))
                    continue;
                solidless++;
                if (solidless <= 12)
                    sb.AppendLine("    " + Path(r.transform) + "   " + Fmt(b.size) + " m");
            }
            if (solidless == 0)
                sb.AppendLine("    nichts - alles, was die Oeffnung ueberdeckt, ist auch fest.");
            else if (solidless > 12)
                sb.AppendLine("    ... und " + (solidless - 12) + " weitere.");
            if (solidless > 0)
                sb.AppendLine("    Diese Teile sind zu sehen und halten niemanden auf. Ein " +
                              "gekauftes Prefab bringt");
            if (solidless > 0)
                sb.AppendLine("    nicht zwangslaeufig ein Collider mit.");

            // ---------- physics pass ----------

            sb.AppendLine();
            sb.AppendLine("  --- WAS DIE PHYSIK GERADE SIEHT (dieselbe Abfrage wie zur " +
                          "Laufzeit) ---");
            Physics.SyncTransforms();
            Collider[] hits = Physics.OverlapBox(centre, half, t.rotation, ~0,
                                                 QueryTriggerInteraction.Ignore);
            int counted = 0;
            for (int i = 0; i < hits.Length; i++)
            {
                Collider c = hits[i];
                if (c == null || c.transform == t || c.transform.IsChildOf(t))
                    continue;
                counted++;
                if (counted <= 12)
                    sb.AppendLine("    " + Path(c.transform));
            }
            if (counted == 0)
                sb.AppendLine("    nichts. Im Editor ist das erwartbar, solange MainMenu_Lobby " +
                              "aus ist -");
            if (counted == 0)
                sb.AppendLine("    ein Collider auf einem inaktiven Objekt ist nicht in der " +
                              "Physikszene.");
            else if (counted > 12)
                sb.AppendLine("    ... und " + (counted - 12) + " weitere.");
        }

        /// <summary>
        /// Measures one collider the way <c>ResolveWall</c> does - along the PORTAL's axes, not
        /// the world's - and says which of the three tests it passes.
        /// </summary>
        private static bool Describe(Collider c, Transform portal, Vector2 opening,
                                     float maxThickness, StringBuilder sb, string indent)
        {
            Bounds b = MeasuredBounds(c);
            float thickness = Support(b.extents, portal.forward) * 2f;
            float width = Support(b.extents, portal.right) * 2f;
            float height = Support(b.extents, portal.up) * 2f;

            bool thinEnough = thickness <= maxThickness;
            bool wideEnough = width >= opening.x;
            bool tallEnough = height >= opening.y;
            bool accepted = thinEnough && wideEnough && tallEnough;

            sb.AppendLine();
            sb.AppendLine(indent + (accepted ? "[ANGENOMMEN] " : "[abgelehnt]  ") +
                          Path(c.transform));
            sb.AppendLine(indent + "  Szene   : " + c.gameObject.scene.name +
                          (c.gameObject.activeInHierarchy ? "" : "   (AUS)") +
                          (c.enabled ? "" : "   (Collider deaktiviert)"));
            sb.AppendLine(indent + "  Typ     : " + c.GetType().Name);
            sb.AppendLine(indent + "  Breite  : " + width.ToString("F2") + " m   " +
                          (wideEnough ? "ok" : "ZU SCHMAL, gebraucht " +
                                               opening.x.ToString("F2")));
            sb.AppendLine(indent + "  Hoehe   : " + height.ToString("F2") + " m   " +
                          (tallEnough ? "ok" : "ZU NIEDRIG, gebraucht " +
                                               opening.y.ToString("F2")));
            sb.AppendLine(indent + "  Dicke   : " + thickness.ToString("F2") + " m   " +
                          (thinEnough ? "ok" : "ZU DICK, erlaubt " +
                                               maxThickness.ToString("F2")));
            return accepted;
        }

        /// <summary>
        /// A collider's world bounds, with the one case that silently reads as "nothing here"
        /// handled: on an inactive GameObject Unity can return an empty box, and an empty box
        /// intersects nothing. The renderer on the same object is the honest stand-in - it is
        /// the same geometry, and it is what the eye sees anyway.
        /// </summary>
        private static Bounds MeasuredBounds(Collider c)
        {
            Bounds b = c.bounds;
            if (b.size != Vector3.zero)
                return b;

            var r = c.GetComponent<Renderer>();
            return r != null ? r.bounds : b;
        }

        /// <summary>The support function of an axis-aligned box along one direction.</summary>
        private static float Support(Vector3 extents, Vector3 dir) =>
            Mathf.Abs(extents.x * dir.x) + Mathf.Abs(extents.y * dir.y) +
            Mathf.Abs(extents.z * dir.z);

        private static string Fmt(Vector3 v) =>
            "(" + v.x.ToString("F2") + ", " + v.y.ToString("F2") + ", " + v.z.ToString("F2") + ")";

        private static string Path(Transform t)
        {
            var sb = new StringBuilder(t.name);
            for (Transform p = t.parent; p != null; p = p.parent)
                sb.Insert(0, p.name + "/");
            return sb.ToString();
        }
    }
}
