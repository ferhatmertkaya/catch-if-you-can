using CatchIfYouCan.Interaction;
using UnityEditor;
using UnityEngine;

namespace CatchIfYouCan.EditorTools
{
    /// <summary>
    /// Zieht die Funktion des Ermittlungsbretts auf das von Hand gesetzte Korkbrett um und
    /// entfernt das alte Objekt.
    ///
    /// <para>
    /// <b>Die Funktion ist nicht das Modell.</b> <c>LobbyInvestigationBoard</c> baute bisher
    /// beides: eine Platzhalter-Tafel aus drei Quadern UND den Zugang zum Lobby-Panel, über das
    /// Einzelspieler, Mehrspieler und die Missionswahl laufen. Das Modell ist ersetzbar und
    /// inzwischen ersetzt — es hängt als Korkbrett an der Wand. Der Zugang ist es nicht: der
    /// öffnet <c>LobbyBoardUI</c>, und den zweimal zu haben hieße zwei Bretter, von denen nur
    /// eines etwas tut.
    /// </para>
    ///
    /// <para>
    /// Also wandert die Komponente auf das vorhandene Modell, mit <c>useExistingModel</c>, und
    /// das alte Objekt geht. Der Interaktionskörper wird dabei um das gemessen, was wirklich da
    /// ist — die Platzhaltermaße beschreiben eine Tafel, die es nicht mehr gibt.
    /// </para>
    /// </summary>
    public static class LobbyBoardMoveTool
    {
        private const string MenuPath =
            "Catch If You Can/1. LOBBY/Ermittlungsbrett auf das Korkbrett [UNDO]";

        /// <summary>Woran das von Hand gesetzte Brett erkannt wird, wenn nichts ausgewählt ist.</summary>
        private const string CorkPrefix = "Meshy_AI_cork_bulletin_board";

        [MenuItem(MenuPath, false, 124)]
        public static void Move()
        {
            var old = Object.FindFirstObjectByType<LobbyInvestigationBoard>(
                FindObjectsInactive.Include);

            // Das Ziel: was ausgewählt ist, sonst das Korkbrett am Namen. Die Auswahl gewinnt,
            // damit ein anderes Brett benutzt werden kann, ohne dieses Werkzeug zu ändern.
            GameObject target = Selection.activeGameObject;
            if (target == null || target.GetComponentInChildren<Renderer>() == null)
                target = FindCork();

            if (target == null)
            {
                EditorUtility.DisplayDialog("Ermittlungsbrett",
                    "Kein Ziel gefunden.\n\nEntweder das Brett im Hierarchie-Fenster auswaehlen, " +
                    "oder ein Objekt mit '" + CorkPrefix + "' im Namen in die Szene stellen.", "OK");
                return;
            }

            if (old != null && old.gameObject == target)
            {
                EditorUtility.DisplayDialog("Ermittlungsbrett",
                    "Das Brett sitzt bereits auf '" + target.name + "'. Nichts geaendert.", "OK");
                return;
            }

            if (target.GetComponent<LobbyInvestigationBoard>() != null)
            {
                EditorUtility.DisplayDialog("Ermittlungsbrett",
                    "'" + target.name + "' hat schon eine LobbyInvestigationBoard.\n\n" +
                    "Zwei davon waeren zwei Zugaenge zu demselben Panel. Nichts geaendert.", "OK");
                return;
            }

            var board = Undo.AddComponent<LobbyInvestigationBoard>(target);
            board.EditorUseExistingModel();

            string removed = "<keines>";
            if (old != null)
            {
                removed = old.gameObject.name;

                // Das ganze Objekt, nicht nur die Komponente: es ist ein leerer Halter, der seine
                // Tafel erst in Start() baut. Ohne die Komponente stuende dort nichts mehr - ein
                // unsichtbares Objekt mitten im Raum, das beim naechsten Aufraeumen niemand
                // zuordnen kann.
                Undo.DestroyObjectImmediate(old.gameObject);
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(target.scene);
            Selection.activeGameObject = target;

            string msg = "Das Ermittlungsbrett sitzt jetzt auf:\n  " + target.name + "\n" +
                         "  Weltposition " + target.transform.position.ToString("F2") + "\n\n" +
                         "Entfernt: " + removed + "\n\n" +
                         "useExistingModel ist AN - es wird keine Platzhalter-Tafel mehr gebaut " +
                         "und kein Prefab instanziiert. Der Interaktionskoerper entsteht beim " +
                         "Start um die vorhandenen Renderer, sofern das Modell nicht schon einen " +
                         "Collider mitbringt.\n\nRueckgaengig mit Strg+Z.";

            Debug.Log("[CIYC][LobbyBoard] " + msg.Replace('\n', ' '));
            EditorUtility.DisplayDialog("Ermittlungsbrett", msg, "OK");
        }

        /// <summary>
        /// Das Korkbrett, INAKTIVE eingeschlossen — es haengt in MainMenu_Lobby, und die ist in
        /// dieser Szene absichtlich ausgeschaltet, bis der Modus-Controller sie einschaltet.
        /// </summary>
        private static GameObject FindCork()
        {
            foreach (Transform t in Object.FindObjectsByType<Transform>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t != null && t.name.StartsWith(CorkPrefix, System.StringComparison.Ordinal) &&
                    t.GetComponentInChildren<Renderer>() != null)
                {
                    return t.gameObject;
                }
            }

            return null;
        }
    }
}
