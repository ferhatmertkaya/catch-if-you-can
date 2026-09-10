using System.Text;
using UnityEditor;
using UnityEngine;

namespace CatchIfYouCan.EditorTools
{
    /// <summary>
    /// Zwei Handgriffe an der von Hand gebauten Lobby, beide gemessen statt geraten: die
    /// Stehlampe neben dem Sofa bekommt ihr Licht, und Spiegel und Ermittlungsbrett bekommen
    /// eine Wand.
    ///
    /// <para>
    /// <b>Warum als Werkzeug.</b> Beides erzeugt beziehungsweise verschiebt Szenenobjekte, und
    /// das Licht wird ein Kind einer PREFAB-Instanz. Von Hand in die Szenendatei geschrieben
    /// waeren das erfundene fileIDs und eine <c>m_AddedGameObjects</c>-Aenderung - genau der
    /// Graph, vor dem CLAUDE.md warnt. Ueber <c>Undo</c> im Editor ist es ein Schritt, den ein
    /// Strg+Z zurueckholt.
    /// </para>
    /// </summary>
    public static class LobbyPlacementTool
    {
        private const string LampPath  = "Catch If You Can/1. LOBBY/Sofa-Lampe beleuchten [UNDO]";
        private const string WallPath  = "Catch If You Can/1. LOBBY/Spiegel und Brett an die Wand [UNDO]";

        private const string PracticalName = "SofaLamp_Practical";

        // ---- die Lampe -------------------------------------------------------------------

        /// <summary>Warmes Wolfram, rund 2400 K. Ausdruecklich kein Weiss.</summary>
        private static readonly Color LampWarm = new Color(1f, 0.72f, 0.42f);

        /// <summary>
        /// Lokal, nicht raumfuellend. Eine Stehlampe leuchtet das Sofa, ein Stueck Wand und
        /// etwas Boden aus - alles darueber hinaus ist das flache Licht, das die Ecken auffuellt
        /// und dem Raum seine Tiefe nimmt.
        /// </summary>
        private const float LampRange = 4.5f;

        private const float LampIntensity = 2.6f;

        [MenuItem(LampPath, false, 122)]
        public static void LightTheSofaLamp()
        {
            Transform lamp = FindInScene("floor lamp");
            if (lamp == null)
            {
                EditorUtility.DisplayDialog("Sofa-Lampe",
                    "Kein Objekt namens 'floor lamp' in dieser Szene.\n\n" +
                    "Erwartet wird die vom Nutzer platzierte HQ-Stehlampe " +
                    "(props/Living Room/floor lamp.prefab).", "OK");
                return;
            }

            Transform existing = lamp.Find(PracticalName);
            if (existing != null)
            {
                EditorUtility.DisplayDialog("Sofa-Lampe",
                    "Die Lampe hat schon ein '" + PracticalName + "'.\n\n" +
                    "Nichts geaendert - ein zweites Licht an derselben Lampe waere die doppelte " +
                    "Helligkeit an genau der Stelle, an der sie am meisten auffaellt.", "OK");
                return;
            }

            // GEMESSEN: der Schirm ist die Oberseite der Lampe. Das Modell wird dabei nicht
            // bewegt - nur das Licht setzt sich dorthin, wo die Gluehbirne sitzen wuerde.
            Bounds b = MeasureWorld(lamp);
            if (b.size == Vector3.zero)
            {
                EditorUtility.DisplayDialog("Sofa-Lampe",
                    "Die Lampe hat keinen Renderer, also laesst sich ihr Schirm nicht messen. " +
                    "Ist das HQ Modular House Paket installiert?", "OK");
                return;
            }

            var go = new GameObject(PracticalName);
            Undo.RegisterCreatedObjectUndo(go, "Sofa-Lampe beleuchten");
            Undo.SetTransformParent(go.transform, lamp, "Sofa-Lampe beleuchten");

            // Knapp unter der Oberkante: im Schirm, nicht darueber. Ein Licht ueber dem Schirm
            // beleuchtet die Decke und laesst den Schirm selbst dunkel.
            go.transform.position = new Vector3(b.center.x, b.max.y - b.size.y * 0.08f, b.center.z);
            go.transform.rotation = Quaternion.identity;

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = LampWarm;
            light.intensity = LampIntensity;
            light.range = LampRange;

            // Keine Echtzeitschatten. Dieses Projekt zielt zuerst auf Mobile, und ein
            // schattenwerfendes Punktlicht rendert die Szene sechsmal.
            light.shadows = LightShadows.None;

            Selection.activeGameObject = go;
            EditorUtility.SetDirty(go);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(lamp.gameObject.scene);

            string msg = "Licht gesetzt in '" + lamp.name + "'.\n\n" +
                         "Position (Welt): " + go.transform.position.ToString("F2") + "\n" +
                         "Gemessene Lampe: " + b.size.ToString("F2") + " m, Oberkante y=" +
                         b.max.y.ToString("F2") + "\n" +
                         "Point Light, warm (2400 K), Intensitaet " + LampIntensity +
                         ", Reichweite " + LampRange + " m, ohne Schatten.\n\n" +
                         "Das Modell wurde NICHT bewegt.";
            Debug.Log("[CIYC][LobbyLamp] " + msg.Replace('\n', ' '));
            EditorUtility.DisplayDialog("Sofa-Lampe", msg, "OK");
        }

        // ---- Spiegel und Brett -----------------------------------------------------------

        /// <summary>Wie weit ein Moebel von der Wandflaeche wegbleibt.</summary>
        private const float WallGap = 0.02f;

        /// <summary>Wie weit hinter dem Objekt nach einer Wand gesucht wird.</summary>
        private const float SearchDistance = 8f;

        [MenuItem(WallPath, false, 123)]
        public static void PlaceAgainstWall()
        {
            var sb = new StringBuilder();

            // Die Wand muss einen Collider haben, sonst findet der Strahl nichts. Das ist die
            // Reihenfolge, nicht ein Zufall: erst "1. LOBBY > Kollision setzen", dann das hier.
            bool any = false;
            any |= Seat("Lobby_MirrorCorner", 0.05f, sb);
            any |= Seat("Lobby_InvestigationBoard", 0.12f, sb);

            if (!any)
            {
                sb.Append("\nNichts bewegt. Wahrscheinlichste Ursache: die Waende haben noch " +
                          "keine Collider, also findet der Strahl sie nicht.\n" +
                          "Erst ausfuehren: 1. LOBBY > Kollision setzen.");
            }
            else
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                sb.Append("\nRueckgaengig mit Strg+Z.");
            }

            Debug.Log("[CIYC][LobbyPlacement] " + sb.ToString().Replace('\n', ' '));
            EditorUtility.DisplayDialog("Spiegel und Brett", sb.ToString(), "OK");
        }

        /// <summary>
        /// Schiebt ein Objekt so weit zurueck, dass seine Rueckseite die naechste Wand beruehrt.
        ///
        /// <para>
        /// <b>Gemessen, nicht geraten - und die Tiefe kommt von aussen.</b> Sowohl der Spiegel
        /// als auch das Brett bauen ihre Geometrie erst in <c>Start()</c>, im Editor haben sie
        /// also nichts, was man messen koennte. Ihre Dicke steht aber fest: das Brett traegt
        /// einen BoxCollider von 0.12 m Tiefe, der Spiegel einen Rahmen von 0.05 m. Diese Zahl
        /// wird uebergeben statt aus einem leeren Objekt abgeleitet.
        /// </para>
        /// <para>
        /// Die Blickrichtung bleibt, wie der Nutzer sie gesetzt hat. Gesucht wird die Wand
        /// HINTER dem Objekt - also entgegen seiner Blickrichtung -, weil ein Spiegel und ein
        /// Anschlagbrett mit dem Ruecken an der Wand haengen und in den Raum schauen.
        /// </para>
        /// </summary>
        private static bool Seat(string name, float depth, StringBuilder sb)
        {
            Transform t = FindInScene(name);
            if (t == null)
            {
                sb.Append(name).Append(": nicht in der Szene.\n");
                return false;
            }

            Vector3 back = -t.forward;
            Vector3 from = t.position + Vector3.up * 1.4f;

            if (!Physics.Raycast(from, back, out RaycastHit hit, SearchDistance, ~0,
                                 QueryTriggerInteraction.Ignore))
            {
                sb.Append(name).Append(": keine Wand innerhalb ")
                  .Append(SearchDistance.ToString("F0")).Append(" m hinter dem Objekt.\n");
                return false;
            }

            Vector3 before = t.position;

            // Die Rueckseite an die Wand, plus ein kleiner Spalt. Nicht der Ursprung an die
            // Wand: das haette die Haelfte des Rahmens im Mauerwerk.
            Vector3 target = hit.point - back * (depth * 0.5f + WallGap);
            target.y = before.y;

            Undo.RecordObject(t, "Spiegel und Brett an die Wand");
            t.position = target;

            sb.Append(name).Append(":\n  Wand '").Append(hit.collider.name)
              .Append("' bei ").Append(hit.point.ToString("F2"))
              .Append(", Abstand ").Append(hit.distance.ToString("F2")).Append(" m\n")
              .Append("  vorher ").Append(before.ToString("F2"))
              .Append(" -> jetzt ").Append(target.ToString("F2"))
              .Append("\n  Drehung unveraendert: ")
              .Append(t.rotation.eulerAngles.ToString("F0")).Append('\n');
            return true;
        }

        // ---- Hilfsmittel ------------------------------------------------------------------

        private static Bounds MeasureWorld(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            bool any = false;
            Bounds b = default;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null || renderers[i] is ParticleSystemRenderer)
                    continue;

                if (!any) { b = renderers[i].bounds; any = true; }
                else b.Encapsulate(renderers[i].bounds);
            }

            return any ? b : new Bounds(root.position, Vector3.zero);
        }

        /// <summary>
        /// Ein Objekt irgendwo in der Szene, INAKTIVE eingeschlossen.
        ///
        /// Nicht <c>GameObject.Find</c>: das ueberspringt inaktive Objekte, und
        /// <c>MainMenu_Lobby</c> - in der Spiegel und Brett haengen - ist in dieser Szene
        /// absichtlich inaktiv, bis der MainMenuModeController sie einschaltet.
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
