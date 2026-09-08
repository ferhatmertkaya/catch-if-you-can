using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CatchIfYouCan.EditorTools
{
    /// <summary>
    /// Gibt der von Hand gebauten Lobby ihre Kollision — gemessen, nicht geraten.
    ///
    /// <para>
    /// <b>Warum das ein Werkzeug ist und keine Änderung an der Szenendatei.</b> Ein Collider ist
    /// ein eigenes Dokument mit einer fileID, das in die Komponentenliste seines GameObjects
    /// eingetragen werden muss. So etwas von Hand in die YAML zu schreiben ist genau das, wovor
    /// CLAUDE.md warnt: eine Szene ist ein Graph aus sich gegenseitig referenzierenden
    /// Dokumenten, und eine erfundene fileID sieht aus wie eine richtige, bis Unity sie öffnet.
    /// Hier läuft alles über <c>Undo</c>, auf der Maschine, auf der das Paket installiert ist.
    /// </para>
    ///
    /// <para>
    /// <b>Warum der Pivot keine Rolle spielt.</b> Die Wandteile dieses Pakets haben Pivots bis zu
    /// 40 m neben ihrer eigenen Geometrie — <c>'2 '</c> steht in dieser Szene auf
    /// (7.28, 6.07, -61.64), während seine Wand mitten in der Lobby steht. Aus dem Transform
    /// lässt sich also nicht ablesen, wo eine Wand ist. Der Collider wird deshalb an den
    /// <b>Renderer</b> gehängt, und <c>mesh.bounds</c> ist relativ zu genau dessen Transform.
    /// Damit muss die Weltlage nie bekannt sein.
    /// </para>
    /// </summary>
    public static class LobbyCollisionTool
    {
        private const string CheckPath  = "Catch If You Can/1. LOBBY/Kollision pruefen [NUR LESEN]";
        private const string ApplyPath  = "Catch If You Can/1. LOBBY/Kollision setzen [UNDO]";

        /// <summary>Was zu klein ist, um dem Spieler im Weg zu stehen.</summary>
        private const float IgnoreBelowMetres = 0.30f;

        /// <summary>Die Wurzel, unter der die von Hand gesetzte Architektur hängt.</summary>
        private const string ArchitectureRoot = "HQ_ROOM_SCALE_ROOT";

        // ---- das Portal, das nichts zumauern darf ------------------------------------------
        //
        // Die Sperrzone wird vom PORTAL erfragt, nicht abgeschrieben. Seine Öffnung steht in
        // genau einer Quelle - LobbyPortal.OpeningSize - und eine hier eingetippte Kopie wäre
        // eine zweite Wahrheit daneben, die in dem Moment falsch wird, in dem jemand die Öffnung
        // ändert. Ohne Portal wird NICHTS gesetzt: die Öffnung zuzumauern ist die unsichtbare
        // Wand vor der Tür, und das ist schlimmer als gar keine Kollision.
        private static bool TryPortalKeepOut(out Transform portal, out Vector3 half)
        {
            portal = null;
            half = Vector3.zero;

            var found = Object.FindFirstObjectByType<CatchIfYouCan.Environment.LobbyPortal>(
                FindObjectsInactive.Include);

            if (found == null)
                return false;

            Vector2 opening = found.OpeningSize;
            portal = found.transform;

            // Ein halber Meter Zugabe rundherum, und die volle erlaubte Wandstärke nach vorn:
            // ein Collider, der die Öffnung streift, steht dem Spieler genauso im Weg wie einer
            // mitten darin.
            half = new Vector3(opening.x * 0.5f + 0.25f,
                               opening.y * 0.5f + 0.25f,
                               Mathf.Max(0.5f, found.MaxWallThickness));
            return true;
        }

        /// <summary>
        /// Ob dieser Renderer in die Portalöffnung ragt - geprüft im Raum des PORTALS.
        ///
        /// Nicht als weltachsenparalleler Boxtest: das Portal steht in dieser Szene um 180 Grad
        /// gedreht, und bei einer anderen Drehung wäre eine Weltbox entweder zu groß und sperrt
        /// halbe Wände aus, oder zu klein und lässt genau den Collider durch, um den es geht.
        /// </summary>
        private static bool IntersectsPortal(Renderer r, Transform portal, Vector3 half)
        {
            Bounds b = r.bounds;
            Vector3 centre = portal.position + portal.up * half.y;

            Vector3 d = b.center - centre;
            float x = Mathf.Abs(Vector3.Dot(d, portal.right));
            float y = Mathf.Abs(Vector3.Dot(d, portal.up));
            float z = Mathf.Abs(Vector3.Dot(d, portal.forward));

            // Die Ausdehnung des Renderers entlang derselben Achsen (Stützfunktion der
            // Welt-AABB), damit ein breites Wandteil nicht nur mit seinem Mittelpunkt zählt.
            Vector3 e = b.extents;
            float ex = Support(e, portal.right);
            float ey = Support(e, portal.up);
            float ez = Support(e, portal.forward);

            return x < half.x + ex && y < half.y + ey && z < half.z + ez;
        }

        private static float Support(Vector3 extents, Vector3 axis) =>
            Mathf.Abs(extents.x * axis.x) + Mathf.Abs(extents.y * axis.y) + Mathf.Abs(extents.z * axis.z);

        private struct Candidate
        {
            public Renderer Renderer;
            public Vector3 LocalSize;
            public bool Architecture;
            public string Reason;
        }

        [MenuItem(CheckPath, false, 120)]
        public static void Check() => Run(false);

        [MenuItem(ApplyPath, false, 121)]
        public static void Apply() => Run(true);

        private static void Run(bool apply)
        {
            if (!TryPortalKeepOut(out Transform portal, out Vector3 keepOutHalf))
            {
                EditorUtility.DisplayDialog("Lobby-Kollision",
                    "Kein LobbyPortal in dieser Szene gefunden.\n\n" +
                    "Ohne das Portal ist nicht bekannt, welcher Bereich frei bleiben muss - und " +
                    "ein Collider in der Portaloeffnung ist die unsichtbare Wand vor der Tuer. " +
                    "Es wird deshalb NICHTS gesetzt.", "OK");
                return;
            }

            List<Candidate> candidates = Collect(out List<string> skipped, out int already);

            var sb = new StringBuilder();
            sb.Append(candidates.Count).Append(" Objekte brauchen Kollision, ")
              .Append(already).Append(" haben schon eine.\n\n");

            int mesh = 0, box = 0, portalBlocked = 0;
            var portalNames = new List<string>();

            foreach (Candidate c in candidates)
            {
                // Kein Collider in die Portalöffnung. Gemeldet statt gesetzt.
                if (IntersectsPortal(c.Renderer, portal, keepOutHalf))
                {
                    portalBlocked++;
                    if (portalNames.Count < 8)
                        portalNames.Add(c.Renderer.gameObject.name);
                    continue;
                }

                if (c.Architecture) mesh++; else box++;

                if (!apply)
                    continue;

                AddCollider(c);
            }

            sb.Append("Architektur (MeshCollider, nicht konvex): ").Append(mesh).Append('\n');
            sb.Append("Moebel (BoxCollider aus gemessenen Mesh-Bounds): ").Append(box).Append('\n');

            if (portalBlocked > 0)
            {
                sb.Append("\nUEBERSPRUNGEN, weil sie in die Portaloeffnung ragen (")
                  .Append(portalBlocked).Append("):\n  ")
                  .Append(string.Join("\n  ", portalNames))
                  .Append("\nDiese bitte von Hand entscheiden - ein Collider dort ist die " +
                          "unsichtbare Wand vor der Tuer.\n");
            }

            if (skipped.Count > 0)
            {
                sb.Append("\nZu klein oder ohne Mesh, uebersprungen: ").Append(skipped.Count)
                  .Append('\n');
            }

            if (apply)
            {
                sb.Append("\nGesetzt. Rueckgaengig mit Strg+Z (ein Schritt).");
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            }
            else
            {
                sb.Append("\nNichts geaendert. Setzen mit: 1. LOBBY > Kollision setzen.");
            }

            Debug.Log("[CIYC][LobbyCollision] " + sb.ToString().Replace('\n', ' '));
            EditorUtility.DisplayDialog("Lobby-Kollision", sb.ToString(), "OK");
        }

        /// <summary>
        /// Alles, was Kollision braucht - und nichts, was schon eine hat.
        ///
        /// Doppelte Collider sind schlimmer als keine: sie fangen den Spieler zwischen zwei
        /// Flaechen ein, und im Editor sieht man einen davon nie.
        /// </summary>
        private static List<Candidate> Collect(out List<string> skipped, out int already)
        {
            var result = new List<Candidate>();
            skipped = new List<string>();
            already = 0;

            Transform archRoot = FindInScene(ArchitectureRoot);

            foreach (Renderer renderer in Object.FindObjectsByType<Renderer>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (renderer == null || renderer is ParticleSystemRenderer)
                    continue;

                GameObject go = renderer.gameObject;

                // Schon vorhandene Kollision wird wiederverwendet, nie verdoppelt.
                if (go.GetComponent<Collider>() != null)
                {
                    already++;
                    continue;
                }

                var filter = go.GetComponent<MeshFilter>();
                Mesh m = filter != null ? filter.sharedMesh : null;
                if (m == null)
                {
                    skipped.Add(go.name + " (kein Mesh)");
                    continue;
                }

                // GEMESSEN im eigenen Raum des Renderers mal seiner Weltskalierung - nie als
                // Welt-AABB (CLAUDE.md Fehler 12). Der Collider sitzt auf demselben Transform,
                // also ist genau das die Groesse, die er braucht.
                Vector3 ls = renderer.transform.lossyScale;
                Vector3 size = m.bounds.size;
                var world = new Vector3(Mathf.Abs(size.x * ls.x), Mathf.Abs(size.y * ls.y),
                                        Mathf.Abs(size.z * ls.z));

                if (Mathf.Max(world.x, Mathf.Max(world.y, world.z)) < IgnoreBelowMetres)
                {
                    skipped.Add(go.name + " (kleiner als " + IgnoreBelowMetres.ToString("F2") + " m)");
                    continue;
                }

                bool architecture = archRoot != null && renderer.transform.IsChildOf(archRoot);

                result.Add(new Candidate
                {
                    Renderer = renderer,
                    LocalSize = world,
                    Architecture = architecture,
                    Reason = architecture ? "Architektur" : "Moebel",
                });
            }

            return result;
        }

        /// <summary>
        /// Architektur bekommt einen nicht-konvexen MeshCollider, Moebel eine Box.
        ///
        /// <para>
        /// <b>Und das ist keine Bequemlichkeit.</b> Eine Wand dieses Pakets ist EIN Mesh mit
        /// einem Loch darin - die Tueroeffnung ist Teil der Geometrie, nicht ein zweites Objekt.
        /// Ein BoxCollider ueber so eine Wand mauert die Tuer zu, und der Spieler steht vor einer
        /// Oeffnung, durch die er nicht kann. Genau davor warnt der Auftrag. Ein nicht-konvexer
        /// MeshCollider auf statischer Geometrie ohne Rigidbody ist in Unity der Normalfall und
        /// kostet nichts pro Frame.
        /// </para>
        /// </summary>
        private static void AddCollider(Candidate c)
        {
            GameObject go = c.Renderer.gameObject;

            if (c.Architecture)
            {
                var mc = Undo.AddComponent<MeshCollider>(go);
                mc.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
                mc.convex = false;
                return;
            }

            var filter = go.GetComponent<MeshFilter>();
            var box = Undo.AddComponent<BoxCollider>(go);
            box.center = filter.sharedMesh.bounds.center;
            box.size = filter.sharedMesh.bounds.size;
        }

        /// <summary>
        /// Ein Objekt irgendwo in der Szene, INAKTIVE eingeschlossen.
        ///
        /// Nicht <c>GameObject.Find</c>: das ueberspringt inaktive Objekte, und
        /// <c>MainMenu_Lobby</c> ist in dieser Szene absichtlich inaktiv - sie wird erst vom
        /// MainMenuModeController eingeschaltet. Genau die Objekte, die hier gesucht werden,
        /// waeren also unsichtbar.
        /// </summary>
        private static Transform FindInScene(string name)
        {
            foreach (Transform t in Object.FindObjectsByType<Transform>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t != null && t.name == name)
                    return t;
            }

            return null;
        }
    }
}
