using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatchIfYouCan.EditorTools
{
    /// <summary>
    /// Makes the authored lobby visible for editing, without changing what the game does.
    ///
    /// <para>
    /// Nothing builds the lobby at runtime. Every one of its thirty-odd objects - floor, walls,
    /// ceiling, the window assembly, the lights, the exterior, the portal component - is already
    /// in 01_MainMenu.unity. It is only invisible in the editor because
    /// <c>MainMenu_Lobby</c> is saved with <c>m_IsActive: 0</c>, and
    /// <c>MainMenuModeController.interactiveRoomRoots</c> switches it on when the player leaves
    /// the cinematic menu. So there is nothing to bake: there is a switch to flip while working,
    /// and to put back before anything else reads the scene.
    /// </para>
    /// <para>
    /// Putting it back is not tidiness. The room being off IN THE FILE is load-bearing, and
    /// MainMenuModeController.Awake says why: Unity does not define whether that Awake runs
    /// before or after the Awake and OnEnable of everything under the room, so a room that is
    /// active when the scene loads has already had its moon light claim the scene's sun and its
    /// emitters start, over the top of the menu. Its own <c>SetRoomActive(false)</c> is the belt
    /// to that braces, and it cannot undo what already ran.
    /// </para>
    /// <para>
    /// Which is why this does not simply leave the switch on. While editing is enabled the room
    /// is visible; the moment the scene is saved or Play is entered it is switched off, and
    /// switched back on afterwards. The file therefore always has the room off, whatever the
    /// editor is showing.
    /// </para>
    /// </summary>
    [InitializeOnLoad]
    public static class MainMenuLobbyAuthoring
    {
        private const string LobbyName = "MainMenu_Lobby";
        private const string EditingKey = "CIYC.MainMenuLobby.Editing";
        private const string MenuPath = "Catch If You Can/Main Menu/Lobby bearbeiten";

        /// <summary>True while the room is being shown for editing.</summary>
        public static bool Editing
        {
            get => SessionState.GetBool(EditingKey, false);
            private set => SessionState.SetBool(EditingKey, value);
        }

        static MainMenuLobbyAuthoring()
        {
            EditorSceneManager.sceneSaving += OnSceneSaving;
            EditorSceneManager.sceneSaved += OnSceneSaved;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        // ------------------------------------------------------------------------- the switch

        [MenuItem(MenuPath, false, 10)]
        private static void Toggle()
        {
            GameObject lobby = FindLobby();
            if (lobby == null)
            {
                EditorUtility.DisplayDialog(
                    "Keine Lobby",
                    "In der offenen Szene gibt es kein Objekt namens " + LobbyName + ".\n\n" +
                    "01_MainMenu oeffnen.",
                    "OK");
                return;
            }

            Editing = !Editing;
            Apply(lobby, Editing);

            if (Editing)
            {
                Selection.activeGameObject = lobby;
                SceneView.lastActiveSceneView?.FrameSelected();
            }

            Debug.Log("[CIYC][Lobby] Bearbeiten ist " + (Editing ? "AN" : "AUS") +
                      ". Die Datei behaelt den Raum ausgeschaltet - beim Speichern und beim " +
                      "Start von Play wird er automatisch abgeschaltet und danach wieder " +
                      "eingeblendet.");
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Editing);
            return FindLobby() != null;
        }

        private static void Apply(GameObject lobby, bool visible)
        {
            if (lobby.activeSelf == visible)
                return;

            // Recorded, so a stray toggle is undoable like anything else. Only the active flag
            // is touched - no transform, no component, no child.
            Undo.RecordObject(lobby, "Lobby bearbeiten");
            lobby.SetActive(visible);
            EditorSceneManager.MarkSceneDirty(lobby.scene);
        }

        // ------------------------------------------------------------- save and play mode

        /// <summary>
        /// Switched off for the write, so the saved file always has the room dormant however the
        /// editor happens to be showing it. Restored in <see cref="OnSceneSaved"/>.
        /// </summary>
        private static void OnSceneSaving(Scene scene, string path)
        {
            if (!Editing)
                return;

            GameObject lobby = FindLobby(scene);
            if (lobby != null && lobby.activeSelf)
                lobby.SetActive(false);
        }

        private static void OnSceneSaved(Scene scene)
        {
            if (!Editing)
                return;

            GameObject lobby = FindLobby(scene);
            if (lobby != null && !lobby.activeSelf)
                lobby.SetActive(true);
        }

        /// <summary>
        /// Switched off before Play starts.
        ///
        /// <para>
        /// Entering Play does not re-read the file; it serialises whatever the editor currently
        /// holds. So a room left visible would enter Play active, which is precisely the state
        /// MainMenuModeController is written to avoid - and its own Awake cannot undo an Awake
        /// that already ran under the room.
        /// </para>
        /// </summary>
        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (!Editing)
                return;

            if (change == PlayModeStateChange.ExitingEditMode)
            {
                GameObject lobby = FindLobby();
                if (lobby != null && lobby.activeSelf)
                {
                    lobby.SetActive(false);
                    Debug.Log("[CIYC][Lobby] Fuer Play abgeschaltet - der Raum muss beim " +
                              "Szenenstart schlafen, sonst laufen sein Mondlicht und seine " +
                              "Emitter ueber dem Menue an. Nach Play wieder sichtbar.");
                }
            }
            else if (change == PlayModeStateChange.EnteredEditMode)
            {
                GameObject lobby = FindLobby();
                if (lobby != null && !lobby.activeSelf)
                    lobby.SetActive(true);
            }
        }

        // --------------------------------------------------------------------------- checking

        [MenuItem("Catch If You Can/Main Menu/Authored Lobby pruefen", false, 11)]
        private static void Validate()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== AUTHORED LOBBY ===");

            Scene scene = SceneManager.GetActiveScene();
            sb.AppendLine("Szene: " + scene.name);

            GameObject lobby = FindLobby(scene);
            if (lobby == null)
            {
                sb.AppendLine("FEHLT: " + LobbyName + " gibt es in dieser Szene nicht.");
                Debug.LogError(sb.ToString());
                return;
            }

            sb.AppendLine("Lobby       : " + Count(lobby) + " Objekte, aktiv=" + lobby.activeSelf);
            sb.AppendLine("Bearbeiten  : " + (Editing ? "AN" : "AUS"));

            // Duplicates are the failure this whole design would produce if the room were ever
            // rebuilt at runtime instead of authored. It is not - but a second one in the scene
            // would be just as wrong, and is the kind of thing a copy-paste leaves behind.
            var duplicates = new List<GameObject>();
            foreach (GameObject root in scene.GetRootGameObjects())
                Collect(root.transform, LobbyName, duplicates);

            sb.AppendLine(duplicates.Count == 1
                ? "Doppelt     : nein"
                : "DOPPELT     : " + duplicates.Count + " Objekte heissen " + LobbyName);

            Report(sb, lobby, "Lobby_Floor", "Boden");
            Report(sb, lobby, "Lobby_Ceiling", "Decke");
            Report(sb, lobby, "Lobby_Wall_North", "Wand Nord");
            Report(sb, lobby, "Lobby_PlayerSpawn", "Spawn");
            Report(sb, lobby, "Lobby_Portal", "Portal");
            Report(sb, lobby, "Lobby_KeyLight", "Licht");

            // The hand-placed house must not inherit the room's dormancy: it is the one thing in
            // this scene the user owns outright, and it has to be visible while building.
            Transform house = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == MainMenuHierarchyTool.HouseRoot)
                    house = root.transform;
            }

            if (house == null)
            {
                sb.AppendLine(MainMenuHierarchyTool.HouseRoot + " : fehlt - " +
                              "Szene > Hierarchie sortieren legt ihn an");
            }
            else
            {
                bool underLobby = house.IsChildOf(lobby.transform);
                sb.AppendLine(MainMenuHierarchyTool.HouseRoot + " : " + house.childCount +
                              " Kategorien" + (underLobby
                                  ? "  ACHTUNG: haengt UNTER der Lobby und ist damit " +
                                    "unsichtbar, solange die Lobby schlaeft"
                                  : "  (eigene Wurzel - bleibt immer sichtbar)"));
            }

            sb.AppendLine();
            sb.AppendLine("Nichts an dieser Lobby wird zur Laufzeit gebaut. Sie steht komplett");
            sb.AppendLine("in der Szenendatei; MainMenuModeController schaltet sie nur ein.");

            Debug.Log(sb.ToString());
        }

        private static void Report(StringBuilder sb, GameObject lobby, string name, string label)
        {
            Transform t = lobby.transform.Find(name);
            sb.AppendLine(string.Format("  {0,-12}: {1}", label,
                t != null ? name + " vorhanden" : name + " FEHLT"));
        }

        private static void Collect(Transform t, string name, List<GameObject> into)
        {
            if (t.name == name)
                into.Add(t.gameObject);

            for (int i = 0; i < t.childCount; i++)
                Collect(t.GetChild(i), name, into);
        }

        private static int Count(GameObject go)
        {
            return go.GetComponentsInChildren<Transform>(true).Length;
        }

        // ---------------------------------------------------------------------------- lookup

        private static GameObject FindLobby()
        {
            return FindLobby(SceneManager.GetActiveScene());
        }

        /// <summary>
        /// By name, and only inside this scene.
        ///
        /// Not <c>GameObject.Find</c>: that one skips inactive objects, and an inactive object
        /// is exactly what this is looking for.
        /// </summary>
        private static GameObject FindLobby(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return null;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == LobbyName)
                    return root;

                Transform nested = FindIn(root.transform);
                if (nested != null)
                    return nested.gameObject;
            }

            return null;
        }

        private static Transform FindIn(Transform t)
        {
            for (int i = 0; i < t.childCount; i++)
            {
                Transform c = t.GetChild(i);
                if (c.name == LobbyName)
                    return c;

                Transform deeper = FindIn(c);
                if (deeper != null)
                    return deeper;
            }

            return null;
        }
    }
}
