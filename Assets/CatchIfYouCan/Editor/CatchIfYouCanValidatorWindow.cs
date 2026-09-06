using System.Collections.Generic;
using System.IO;
using System.Text;
using CatchIfYouCan.Ghost;
using CatchIfYouCan.Interaction;
using CatchIfYouCan.Procedural;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatchIfYouCan.EditorTools
{
    public class CatchIfYouCanValidatorWindow : EditorWindow
    {
        private readonly List<string> _issues = new List<string>();
        private Vector2 _scroll;

        [MenuItem("Catch If You Can/Scene Authoring/Validator [OEFFNET SZENEN]", false, 305)]
        public static void ShowWindow()
        {
            GetWindow<CatchIfYouCanValidatorWindow>("CIYC Validator");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Catch If You Can — Project Validator", EditorStyles.boldLabel);
            if (GUILayout.Button("Run Validation", GUILayout.Height(28)))
                RunValidation();

            EditorGUILayout.Space();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (int i = 0; i < _issues.Count; i++)
            {
                var style = _issues[i].StartsWith("[ERROR]") ? EditorStyles.boldLabel : EditorStyles.label;
                EditorGUILayout.LabelField(_issues[i], style);
            }

            EditorGUILayout.EndScrollView();
        }

        private void RunValidation()
        {
            _issues.Clear();
            ValidateMissingScripts();
            ValidateGhostDefinitions();
            ValidateDoors();
            ValidateRoomSockets();
            ValidateSceneReferences();
            ValidateBuildScenes();

            if (_issues.Count == 0)
                _issues.Add("[OK] No issues found.");

            var sb = new StringBuilder();
            for (int i = 0; i < _issues.Count; i++)
            {
                sb.AppendLine(_issues[i]);
                if (_issues[i].StartsWith("[ERROR]"))
                    Debug.LogError(_issues[i]);
                else if (_issues[i].StartsWith("[WARN]"))
                    Debug.LogWarning(_issues[i]);
                else
                    Debug.Log(_issues[i]);
            }

            Debug.Log("=== CIYC Validator complete ===");
            Repaint();
        }

        private void ValidateMissingScripts()
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/CatchIfYouCan" });
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                CheckGameObjectForMissingScripts(AssetDatabase.LoadAssetAtPath<GameObject>(path), path);
            }

            for (int i = 0; i < EditorBuildSettings.scenes.Length; i++)
            {
                var scenePath = EditorBuildSettings.scenes[i].path;
                if (!File.Exists(scenePath))
                    continue;

                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                foreach (var root in scene.GetRootGameObjects())
                    CheckHierarchyForMissingScripts(root, scenePath);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private void CheckHierarchyForMissingScripts(GameObject go, string context)
        {
            CheckGameObjectForMissingScripts(go, context);
            for (int i = 0; i < go.transform.childCount; i++)
                CheckHierarchyForMissingScripts(go.transform.GetChild(i).gameObject, context);
        }

        private void CheckGameObjectForMissingScripts(GameObject go, string context)
        {
            if (go == null)
                return;

            var components = go.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] == null)
                    _issues.Add($"[ERROR] Missing script on '{go.name}' in {context}");
            }
        }

        private void ValidateGhostDefinitions()
        {
            string folder = "Assets/CatchIfYouCan/ScriptableObjects";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                _issues.Add("[WARN] ScriptableObjects folder missing.");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:GhostDefinition", new[] { folder });
            if (guids.Length == 0)
            {
                _issues.Add("[WARN] No GhostDefinition assets in ScriptableObjects.");
                return;
            }

            for (int i = 0; i < guids.Length; i++)
            {
                var ghost = AssetDatabase.LoadAssetAtPath<GhostDefinition>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (ghost == null)
                    continue;

                if (string.IsNullOrWhiteSpace(ghost.Id))
                    _issues.Add($"[ERROR] Ghost '{ghost.name}' has empty Id.");
                if (string.IsNullOrWhiteSpace(ghost.DisplayName))
                    _issues.Add($"[WARN] Ghost '{ghost.name}' has empty DisplayName.");
            }
        }

        private void ValidateDoors()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/CatchIfYouCan" });
            for (int i = 0; i < guids.Length; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (prefab == null)
                    continue;

                var doors = prefab.GetComponentsInChildren<InteractiveDoor>(true);
                for (int d = 0; d < doors.Length; d++)
                {
                    var door = doors[d];
                    var hingeField = typeof(InteractiveDoor).GetField("hinge",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var hinge = hingeField?.GetValue(door) as Transform;
                    if (hinge == null || hinge == door.transform)
                        _issues.Add($"[WARN] Door '{door.name}' in prefab '{prefab.name}' may lack dedicated hinge transform.");
                }
            }
        }

        private void ValidateRoomSockets()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/CatchIfYouCan" });
            for (int i = 0; i < guids.Length; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (prefab == null)
                    continue;

                var sockets = prefab.GetComponentsInChildren<RoomSocket>(true);
                for (int s = 0; s < sockets.Length; s++)
                {
                    if (sockets[s] == null)
                        _issues.Add($"[ERROR] Null RoomSocket reference in prefab '{prefab.name}'.");
                }
            }
        }

        private void ValidateSceneReferences()
        {
            string[] required = Core.CiycScenes.ProductionPaths();
            for (int i = 0; i < required.Length; i++)
            {
                if (!File.Exists(required[i]))
                    _issues.Add($"[ERROR] Missing scene file: {required[i]}");
            }
        }

        private void ValidateBuildScenes()
        {
            var scenes = EditorBuildSettings.scenes;
            var required = Core.CiycScenes.ProductionPaths();

            for (int i = 0; i < required.Length; i++)
            {
                bool registered = false;
                for (int s = 0; scenes != null && s < scenes.Length; s++)
                {
                    if (scenes[s] != null && scenes[s].path == required[i])
                    {
                        registered = true;
                        break;
                    }
                }

                // Naming the missing scene matters more than counting them: a build list
                // that is one short is a scene that loads in the editor and nowhere else.
                if (!registered)
                    _issues.Add($"[WARN] Build Settings does not include {required[i]}.");
            }

            // The opposite mistake, and the more expensive one: a lab in the shipping list.
            for (int s = 0; scenes != null && s < scenes.Length; s++)
            {
                if (scenes[s] == null || !scenes[s].enabled)
                    continue;

                if (Development.DevelopmentScenes.IsDevelopmentScenePath(scenes[s].path))
                    _issues.Add($"[ERROR] Development scene enabled in Build Settings: {scenes[s].path}");
            }
        }
    }
}
