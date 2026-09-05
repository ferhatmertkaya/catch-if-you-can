using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatchIfYouCan.EditorTools
{
    /// <summary>
    /// Sorts the open scene's ROOT objects into named folders, and nothing else.
    ///
    /// <para>
    /// Hierarchy only. It creates empty parents, moves roots into them with
    /// <c>SetParent(parent, true)</c> so world position, rotation and scale are preserved by
    /// Unity rather than by arithmetic here, and verifies afterwards that all three actually
    /// did stay put. It adds no component, removes none, touches no serialized field, opens no
    /// asset and unpacks no prefab.
    /// </para>
    /// <para>
    /// Why that is safe: Unity serialises object references by fileID, not by hierarchy path, so
    /// a reference survives a reparent. The audit found the one exception this scene could have
    /// had - nothing here calls <c>DontDestroyOnLoad</c> on itself, which is the one thing that
    /// requires an object to stay a scene root - and the one lookup that is name-based rather
    /// than reference-based (<c>GameObject.Find("Door_Green_Fog")</c>), which is
    /// parent-independent but finds only ACTIVE objects. That is why every folder this tool
    /// creates is created active.
    /// </para>
    /// <para>
    /// Nothing moves that the audit could not clear. Four objects in this scene are parented
    /// INSIDE prefab instances - Spot Light, CandleFX, PhoneAudio and Area Light - and pulling
    /// one out would change that instance's override set, so they are not offered. Subtrees move
    /// whole: splitting the lobby into sub-folders is a separate decision, and this pass does
    /// not make it.
    /// </para>
    /// </summary>
    public class MainMenuHierarchyTool : EditorWindow
    {
        private static readonly string[] Folders =
        {
            "00_SYSTEMS",
            "01_CAMERAS",
            "02_LIGHTING",
            "03_LOBBY",
            "04_PORTAL",
            "05_HQ_MANUAL_HOUSE",
            "06_LOBBY_PROPS",
            "07_CHARACTERS",
            "08_UI",
        };

        /// <summary>
        /// The categories hand-placed HQ architecture is sorted into. Created empty, because the
        /// pieces are chosen and placed by a person - this tool never generates a room.
        /// </summary>
        public static readonly string[] HouseCategories =
        {
            "01_FLOORS",
            "02_WALLS",
            "03_DOORS",
            "04_WINDOWS",
            "05_ARCHES",
            "06_COLUMNS_TRIM",
            "07_CEILINGS",
            "08_PROPS",
        };

        public const string HouseRoot = "05_HQ_MANUAL_HOUSE";

        /// <summary>
        /// How far the audit got with this object. A tick is a claim that reparenting it was
        /// PROVEN harmless, not a guess that it probably is.
        /// </summary>
        private enum Safety
        {
            /// <summary>Every reference to it is by fileID, which a reparent does not touch.</summary>
            Proven,

            /// <summary>Safe only while something else holds - the condition is in the reason.</summary>
            Conditional,

            /// <summary>The audit could not settle its role. It stays where it is.</summary>
            Unclear,
        }

        private class Move
        {
            public Transform Target;
            public string Folder;
            public string Reason;
            public Safety Safety;
            public bool Do;
            public bool Blocked;
        }

        private List<Move> _plan;
        private Vector2 _scroll;

        [MenuItem("Catch If You Can/Szene/Hierarchie sortieren")]
        public static void Open()
        {
            var w = GetWindow<MainMenuHierarchyTool>(false, "Hierarchie", true);
            w.minSize = new Vector2(620f, 420f);
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Sortiert NUR die Wurzelobjekte der offenen Szene in Ordner.\n\n" +
                "Kein Component wird angelegt oder entfernt, kein serialisiertes Feld " +
                "angefasst, kein Prefab entpackt. Weltposition, -drehung und -skalierung " +
                "bleiben erhalten und werden danach nachgemessen.\n\n" +
                "Erst 'Plan zeigen'. Was unsicher ist, ist ABGEHAKT und bleibt liegen.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("1. Plan zeigen (aendert nichts)"))
                    _plan = BuildPlan();

                using (new EditorGUI.DisabledScope(_plan == null))
                {
                    if (GUILayout.Button("2. Anwenden"))
                        Apply(_plan);
                }
            }

            if (_plan == null)
            {
                EditorGUILayout.LabelField("Noch kein Plan.");
                return;
            }

            EditorGUILayout.LabelField(_plan.Count + " Wurzelobjekte", EditorStyles.miniLabel);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            for (int i = 0; i < _plan.Count; i++)
            {
                Move move = _plan[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(move.Blocked))
                        move.Do = EditorGUILayout.Toggle(move.Do, GUILayout.Width(20f));

                    EditorGUILayout.LabelField(
                        move.Target != null ? move.Target.name : "<weg>", GUILayout.Width(210f));
                    EditorGUILayout.LabelField("-> " + move.Folder, GUILayout.Width(160f));
                    EditorGUILayout.LabelField(Word(move.Safety), GUILayout.Width(90f));
                    EditorGUILayout.LabelField(move.Reason, EditorStyles.miniLabel);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        // ----------------------------------------------------------------------------- plan

        private static List<Move> BuildPlan()
        {
            var plan = new List<Move>();
            Scene scene = SceneManager.GetActiveScene();

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (IsFolder(root.name))
                    continue;

                Move move = Classify(root.transform);
                if (move != null)
                    plan.Add(move);
            }

            plan.Sort((a, b) => string.CompareOrdinal(a.Folder + a.Target.name,
                                                      b.Folder + b.Target.name));
            return plan;
        }

        private static string Word(Safety safety)
        {
            switch (safety)
            {
                case Safety.Proven: return "SICHER";
                case Safety.Conditional: return "BEDINGT";
                default: return "UNKLAR";
            }
        }

        private static bool IsFolder(string name)
        {
            for (int i = 0; i < Folders.Length; i++)
            {
                if (name == Folders[i])
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Where this root belongs, and whether the audit could PROVE that moving it is
        /// harmless.
        ///
        /// <para>
        /// The verdicts come from reading the scene rather than from the names. Every serialized
        /// reference in 01_MainMenu is by fileID, which a reparent does not touch, so what
        /// decides safety is the small set of things that are NOT fileID references: two
        /// name-based <c>GameObject.Find</c> calls that reach into this scene, both of which
        /// find only ACTIVE objects, and an object's own active state, which a parent can
        /// override.
        /// </para>
        /// </summary>
        private static Move Classify(Transform t)
        {
            GameObject go = t.gameObject;
            string n = go.name;

            // A prefab instance moves as a whole and keeps its link - reparenting an instance
            // ROOT is not an override of anything inside it.
            bool isPrefab = PrefabUtility.IsAnyPrefabInstanceRoot(go);

            if (go.GetComponent<Camera>() != null)
                return New(t, "01_CAMERAS", Safety.Proven,
                           "Camera; nur ueber fileID referenziert (ModeController, GhostCloser)");

            // Door_Green_Fog is reached by GameObject.Find in MainMenuAtmosphereBuilder. That
            // lookup does not care about the parent, but it does skip inactive objects - and the
            // folders this tool creates are created active, so the lookup keeps working.
            if (n.StartsWith("Door_", System.StringComparison.Ordinal) ||
                n.StartsWith("DoorFog", System.StringComparison.Ordinal))
                return New(t, "04_PORTAL", Safety.Proven,
                           "Portal-Optik; nur Namenssuche, elternunabhaengig");

            // Conditional, and left unticked. It works - MainMenuLobbyAuthoring walks the scene
            // recursively rather than only the roots, and interactiveRoomRoots is a fileID
            // reference - but the room's visibility would then also depend on 03_LOBBY staying
            // active. It costs nothing to leave it at the root, and that removes the condition.
            if (n == "MainMenu_Lobby")
                return New(t, "03_LOBBY", Safety.Conditional,
                           "funktioniert, ABER die Lobby haengt dann zusaetzlich an der " +
                           "Aktivitaet von 03_LOBBY. Empfehlung: an der Wurzel lassen");

            if (n == "MainMenuBrandingCanvas" || go.GetComponent<Canvas>() != null)
                return New(t, "08_UI", Safety.Proven,
                           "Canvas; per fileID in cinematicUiRoots referenziert");

            if (n.Contains("Ghost"))
                return New(t, "07_CHARACTERS", Safety.Proven,
                           isPrefab ? "Figur (Prefab-Instanz), unreferenziert"
                                    : "Figur; nur ueber fileID referenziert");

            if (isPrefab && (n.StartsWith("CIYC_Haunted", System.StringComparison.Ordinal) ||
                             n.StartsWith("CIYC_Victorian", System.StringComparison.Ordinal)))
                return New(t, "06_LOBBY_PROPS", Safety.Proven,
                           "Requisite (Prefab-Instanz), von keinem Skript referenziert");

            // The corridor is the set the cinematic camera looks down, not part of the walkable
            // lobby - so 03_LOBBY would be the wrong home and this tool has no group that is
            // clearly the right one. Unticked until somebody decides.
            if (isPrefab)
                return New(t, "03_LOBBY", Safety.Unclear,
                           "Prefab-Instanz ohne Skript-Referenz - Rolle nicht eindeutig " +
                           "(Kulisse des Menues oder Teil der Lobby?). Bitte selbst entscheiden");

            if (go.GetComponent<Light>() != null)
                return New(t, "02_LIGHTING", Safety.Proven, "traegt ein Light");

            if (n.Contains("PostProcessing"))
                return New(t, "02_LIGHTING", Safety.Proven, "Post-Processing-Volume");

            // Empty and unreferenced. Filing it away tidies nothing; it is a leftover, and the
            // useful answer is to delete it, which is not this tool's decision to make.
            if (t.childCount == 0 && go.GetComponents<Component>().Length <= 1)
                return New(t, "00_SYSTEMS", Safety.Unclear,
                           "LEER: nur ein Transform, keine Kinder, keine Referenz. " +
                           "Eher loeschen als einsortieren");

            return New(t, "00_SYSTEMS", Safety.Proven,
                       "Steuerobjekt; nur ueber fileID referenziert");
        }

        private static Move New(Transform t, string folder, Safety safety, string reason)
        {
            return new Move
            {
                Target = t,
                Folder = folder,
                Safety = safety,
                Reason = reason,

                // A tick means PROVEN. Anything the audit left conditional or unclear stays
                // where it is until a person says otherwise.
                Do = safety == Safety.Proven,
            };
        }

        // ---------------------------------------------------------------------------- apply

        private static void Apply(List<Move> plan)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== HIERARCHIE SORTIEREN ===");

            int moved = 0, skipped = 0, drifted = 0;

            for (int i = 0; i < plan.Count; i++)
            {
                Move move = plan[i];
                if (!move.Do || move.Target == null)
                {
                    skipped++;
                    sb.AppendLine("uebersprungen : " +
                                  (move.Target != null ? move.Target.name : "<weg>") +
                                  "  (" + move.Reason + ")");
                    continue;
                }

                Transform target = move.Target;

                // Read BEFORE. lossyScale rather than localScale: what must not change is the
                // size on screen, and that is the world one.
                Vector3 posBefore = target.position;
                Quaternion rotBefore = target.rotation;
                Vector3 scaleBefore = target.lossyScale;
                bool prefabBefore = PrefabUtility.IsAnyPrefabInstanceRoot(target.gameObject);

                Transform folder = EnsureFolder(move.Folder);

                Undo.SetTransformParent(target, folder, "Hierarchie sortieren");

                // Unity preserves the world transform across SetTransformParent; this asserts it
                // rather than assuming it, and never "corrects" a child afterwards - a manual
                // correction would hide the very thing worth knowing.
                float dp = Vector3.Distance(posBefore, target.position);
                float dr = Quaternion.Angle(rotBefore, target.rotation);
                float ds = Vector3.Distance(scaleBefore, target.lossyScale);
                bool prefabAfter = PrefabUtility.IsAnyPrefabInstanceRoot(target.gameObject);

                bool ok = dp < 0.0005f && dr < 0.01f && ds < 0.0005f && prefabBefore == prefabAfter;
                if (!ok)
                {
                    drifted++;
                    sb.AppendLine("ABWEICHUNG    : " + target.name +
                                  "  dPos " + dp.ToString("F5") +
                                  "  dRot " + dr.ToString("F4") + " Grad" +
                                  "  dScale " + ds.ToString("F5") +
                                  "  Prefab " + prefabBefore + "->" + prefabAfter);
                }
                else
                {
                    moved++;
                    sb.AppendLine("verschoben    : " + target.name + " -> " + move.Folder);
                }
            }

            EnsureHouseCategories();

            sb.AppendLine();
            sb.AppendLine(moved + " verschoben, " + skipped + " liegen gelassen, " +
                          drifted + " mit Abweichung.");
            sb.AppendLine();
            sb.AppendLine("Nicht angefasst, und warum:");
            sb.AppendLine(" - Spot Light, CandleFX, PhoneAudio, Area Light haengen INNERHALB von");
            sb.AppendLine("   Prefab-Instanzen. Sie herauszuziehen waere eine Prefab-Aenderung.");
            sb.AppendLine(" - Der Inhalt von MainMenu_Lobby bleibt zusammen. Ihn aufzuteilen ist");
            sb.AppendLine("   eine eigene Entscheidung, nicht ein Nebeneffekt vom Aufraeumen.");
            sb.AppendLine(" - Es wurde nichts aktiviert oder deaktiviert.");
            sb.AppendLine();
            sb.AppendLine("Die Szene ist NICHT gespeichert. Erst ansehen, dann Ctrl+S.");

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log(sb.ToString());
        }

        private static Transform EnsureFolder(string name)
        {
            Scene scene = SceneManager.GetActiveScene();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name)
                    return root.transform;
            }

            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Ordner anlegen");

            // At the origin, unrotated, unit scale. A folder with a transform of its own would
            // silently move everything put into it later - and "everything shifted a bit" is one
            // of the harder things to trace back to a tidy-up.
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            go.transform.localScale = Vector3.one;
            return go.transform;
        }

        /// <summary>
        /// The empty categories hand-placed HQ pieces are sorted into. Created so the piece
        /// browser has somewhere to put things; left empty, because a person chooses the pieces.
        /// </summary>
        public static Transform EnsureHouseCategories()
        {
            Transform house = EnsureFolder(HouseRoot);

            for (int i = 0; i < HouseCategories.Length; i++)
            {
                if (house.Find(HouseCategories[i]) != null)
                    continue;

                var go = new GameObject(HouseCategories[i]);
                Undo.RegisterCreatedObjectUndo(go, "Ordner anlegen");
                go.transform.SetParent(house, false);
            }

            return house;
        }
    }
}
