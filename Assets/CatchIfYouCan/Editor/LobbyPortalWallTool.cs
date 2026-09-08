using System.Collections.Generic;
using System.Text;
using CatchIfYouCan.Environment;
using UnityEditor;
using UnityEngine;

namespace CatchIfYouCan.EditorTools
{
    /// <summary>
    /// Baut die EINE Wand, in die das Portal sein Loch schneidet — aus den Wandteilen, die
    /// wirklich an der Öffnung stehen.
    ///
    /// <para>
    /// <b>Warum es genau eine sein muss.</b> <c>LobbyPortal</c> hält seine Wand in einem einzigen
    /// Feld und schaltet sie um: geschlossen ist <c>_wallSolid.enabled = true</c> und die Öffnung
    /// massiv, offen ist sie aus und vier Streifen — links, rechts, Sturz, Brüstung — treten an
    /// ihre Stelle. Ein Portal kann also genau ein Collider öffnen. Bekämen die drei Wandmodule
    /// an der Öffnung je ein eigenes, könnte das Portal sie nicht abschalten, und der Spieler
    /// stünde vor einer unsichtbaren Wand, obwohl die Tür offen ist. Genau deshalb hat
    /// <see cref="LobbyCollisionTool"/> sie übersprungen.
    /// </para>
    ///
    /// <para>
    /// <b>Warum die Formsuche hier scheitert.</b> Ohne eingetragene Wand sucht
    /// <c>ResolveWall</c> per Form: höchstens <c>maxWallThickness</c> dick, mindestens so breit
    /// und so hoch wie die Öffnung. Ein Wandmodul des gekauften Pakets ist schmaler als die
    /// 4.70 m der Öffnung, und drei nebeneinander addieren sich nicht zu einem Collider. Das ist
    /// der Guard, der in <c>check_ui_and_portal.sh</c> rot steht — kein Fehler im Portal, sondern
    /// eine Folge des Umbaus von der erzeugten Hülle auf gekaufte Module.
    /// </para>
    ///
    /// <para>
    /// Gemessen, nicht getippt: die Größe kommt aus den Renderern, die die Öffnung überlappen,
    /// in den Achsen des Portals. Nichts hier kennt eine Koordinate der Szene.
    /// </para>
    /// </summary>
    public static class LobbyPortalWallTool
    {
        private const string MenuPath = "Catch If You Can/3. PORTAL/Portalwand setzen [UNDO]";

        private const string WallName = "Lobby_PortalWall_Solid";

        [MenuItem(MenuPath, false, 301)]
        public static void Build()
        {
            var portal = Object.FindFirstObjectByType<LobbyPortal>(FindObjectsInactive.Include);
            if (portal == null)
            {
                EditorUtility.DisplayDialog("Portalwand",
                    "Kein LobbyPortal in den offenen Szenen. 01_MainMenu.unity oeffnen.", "OK");
                return;
            }

            Transform t = portal.transform;
            Vector2 opening = portal.OpeningSize;
            float maxThickness = portal.MaxWallThickness;

            // Dieselbe Box, die ResolveWall baut: die Oeffnung steigt vom Boden auf, also liegt
            // ihre Mitte eine halbe Oeffnungshoehe ueber dem Ursprung des Portals.
            Vector3 centre = t.position + t.up * (opening.y * 0.5f);
            var half = new Vector3(opening.x * 0.5f, opening.y * 0.5f, Mathf.Max(0.1f, maxThickness));

            List<Renderer> wallParts = FindWallParts(portal, centre, half);

            if (wallParts.Count == 0)
            {
                EditorUtility.DisplayDialog("Portalwand",
                    "An der Oeffnung steht kein einziger Renderer.\n\n" +
                    "Das Portal haengt dann buchstaeblich in der Luft - es gibt keine Wand, in " +
                    "die es schneiden koennte. Erst ein Wandteil an die Oeffnung stellen.", "OK");
                return;
            }

            // Ausdehnung IN DEN ACHSEN DES PORTALS, aus den transformierten Ecken. Eine
            // weltachsenparallele Box waere bei einem gedrehten Portal zu gross - und dieses
            // steht um 180 Grad gedreht.
            if (!MeasureInPortalSpace(wallParts, t, out Vector3 localMin, out Vector3 localMax))
            {
                EditorUtility.DisplayDialog("Portalwand", "Die Wandteile lassen sich nicht " +
                    "messen (kein Mesh). Ist das HQ Modular House Paket installiert?", "OK");
                return;
            }

            Vector3 size = localMax - localMin;
            Vector3 mid = (localMax + localMin) * 0.5f;

            var report = new StringBuilder();
            report.Append("Wandteile an der Oeffnung: ").Append(wallParts.Count).Append('\n');
            for (int i = 0; i < wallParts.Count && i < 8; i++)
                report.Append("  ").Append(wallParts[i].gameObject.name).Append('\n');

            report.Append("\nGemessen in den Achsen des Portals:\n")
                  .Append("  Breite ").Append(size.x.ToString("F2"))
                  .Append(" m   (Oeffnung ").Append(opening.x.ToString("F2")).Append(" m)\n")
                  .Append("  Hoehe  ").Append(size.y.ToString("F2"))
                  .Append(" m   (Oeffnung ").Append(opening.y.ToString("F2")).Append(" m)\n")
                  .Append("  Dicke  ").Append(size.z.ToString("F2"))
                  .Append(" m   (erlaubt bis ").Append(maxThickness.ToString("F2")).Append(" m)\n");

            // Die drei Tests, die ResolveWall anwendet - hier vorher, damit eine Wand, die das
            // Portal ohnehin ablehnen wuerde, gar nicht erst entsteht.
            var problems = new List<string>();
            if (size.x < opening.x)
                problems.Add("zu schmal: " + size.x.ToString("F2") + " < " + opening.x.ToString("F2"));
            if (size.y < opening.y)
                problems.Add("zu niedrig: " + size.y.ToString("F2") + " < " + opening.y.ToString("F2"));
            if (size.z > maxThickness)
                problems.Add("zu dick: " + size.z.ToString("F2") + " > " + maxThickness.ToString("F2") +
                             " - die Wandteile stehen nicht in einer Ebene");

            if (problems.Count > 0)
            {
                report.Append("\nNICHT gesetzt:\n  ").Append(string.Join("\n  ", problems))
                      .Append("\n\nDas Portal wuerde diese Wand aus demselben Grund ablehnen. " +
                              "Erst die Wandteile so stellen, dass sie die Oeffnung wirklich " +
                              "ueberdecken.");
                Debug.LogWarning("[CIYC][PortalWall] " + report.ToString().Replace('\n', ' '));
                EditorUtility.DisplayDialog("Portalwand", report.ToString(), "OK");
                return;
            }

            // Vorhandene ersetzen statt eine zweite danebenstellen.
            Transform existing = FindInScene(WallName);
            if (existing != null)
                Undo.DestroyObjectImmediate(existing.gameObject);

            var go = new GameObject(WallName);
            Undo.RegisterCreatedObjectUndo(go, "Portalwand setzen");

            // Als Geschwister des Portals, damit die Wand mit der Lobby ein- und ausgeschaltet
            // wird. Ausserhalb waere sie auch dann massiv, wenn die Lobby gar nicht laeuft.
            Undo.SetTransformParent(go.transform, t.parent, "Portalwand setzen");
            go.transform.SetPositionAndRotation(t.TransformPoint(mid), t.rotation);

            var box = go.AddComponent<BoxCollider>();
            box.size = size;

            // KEIN Renderer. Die Wand ist schon da und sichtbar - das hier ist nur ihr Koerper,
            // und ein zweiter Renderer waere z-Fighting auf der ganzen Flaeche.
            Undo.RecordObject(portal, "Portalwand setzen");
            portal.EditorAssignWallCollider(box);

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(t.gameObject.scene);
            Selection.activeGameObject = go;

            report.Append("\nGesetzt: '").Append(WallName).Append("' unter '")
                  .Append(t.parent != null ? t.parent.name : "<Szenenwurzel>").Append("'\n")
                  .Append("  Weltposition ").Append(go.transform.position.ToString("F2")).Append('\n')
                  .Append("  und als wallCollider am Portal eingetragen.\n\n")
                  .Append("Geschlossen ist diese Wand massiv; oeffnet das Portal, schaltet es sie " +
                          "ab und setzt vier Streifen um das Loch. Die drei Vendor-Wandteile " +
                          "bleiben absichtlich OHNE eigenes Collider - sonst koennte das Portal " +
                          "sie nicht oeffnen.\n\nRueckgaengig mit Strg+Z.");

            Debug.Log("[CIYC][PortalWall] " + report.ToString().Replace('\n', ' '));
            EditorUtility.DisplayDialog("Portalwand", report.ToString(), "OK");
        }

        /// <summary>
        /// Die Renderer, die die Öffnung überlappen — dieselbe Prüfung, mit der
        /// <see cref="LobbyCollisionTool"/> sie übersprungen hat, damit beide dieselben Teile
        /// meinen.
        /// </summary>
        private static List<Renderer> FindWallParts(LobbyPortal portal, Vector3 centre, Vector3 half)
        {
            var result = new List<Renderer>();
            Transform t = portal.transform;

            foreach (Renderer r in Object.FindObjectsByType<Renderer>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (r == null || r is ParticleSystemRenderer)
                    continue;

                // Nichts, was zum Portal selbst gehoert.
                if (r.transform.IsChildOf(t))
                    continue;

                Bounds b = r.bounds;
                Vector3 d = b.center - centre;
                Vector3 e = b.extents;

                if (Mathf.Abs(Vector3.Dot(d, t.right)) < half.x + Support(e, t.right) &&
                    Mathf.Abs(Vector3.Dot(d, t.up)) < half.y + Support(e, t.up) &&
                    Mathf.Abs(Vector3.Dot(d, t.forward)) < half.z + Support(e, t.forward))
                {
                    result.Add(r);
                }
            }

            // Stabile Reihenfolge, damit der Bericht bei zwei Laeufen derselbe ist.
            result.Sort((a, b) => string.CompareOrdinal(a.gameObject.name, b.gameObject.name));
            return result;
        }

        private static bool MeasureInPortalSpace(List<Renderer> parts, Transform portal,
                                                 out Vector3 min, out Vector3 max)
        {
            min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            bool any = false;

            for (int i = 0; i < parts.Count; i++)
            {
                Bounds b = parts[i].bounds;

                for (int c = 0; c < 8; c++)
                {
                    var corner = new Vector3(
                        (c & 1) == 0 ? b.min.x : b.max.x,
                        (c & 2) == 0 ? b.min.y : b.max.y,
                        (c & 4) == 0 ? b.min.z : b.max.z);

                    Vector3 local = portal.InverseTransformPoint(corner);
                    min = Vector3.Min(min, local);
                    max = Vector3.Max(max, local);
                    any = true;
                }
            }

            return any;
        }

        private static float Support(Vector3 extents, Vector3 axis) =>
            Mathf.Abs(extents.x * axis.x) + Mathf.Abs(extents.y * axis.y) + Mathf.Abs(extents.z * axis.z);

        /// <summary>Ein Objekt irgendwo in der Szene, INAKTIVE eingeschlossen.</summary>
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
