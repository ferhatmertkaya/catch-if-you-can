using System.Collections.Generic;
using CatchIfYouCan.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CatchIfYouCan.EditorTools
{
    /// <summary>
    /// Writes the main menu into <c>01_MainMenu.unity</c> as REAL, SAVED GameObjects.
    ///
    /// <para>
    /// <b>This is authoring, not building.</b> Everything it writes is a normal Unity object you
    /// can select in the Hierarchy, drag in the Scene view, and edit in the Inspector. Once the
    /// scene is saved, no code creates any of it again: <see cref="MainMenuNavigation"/> only
    /// decides which row is selected and what a press does. The scene is the design; C# is the
    /// behaviour.
    /// </para>
    ///
    /// <para>
    /// <b>It never overwrites your work.</b> Every object is looked up before it is created, and
    /// an object that already exists keeps its RectTransform, its text, its font, its size and
    /// its colour. Run it twice and the second run changes nothing. That is what makes it safe to
    /// re-run after you have moved things by hand - it fills in what is missing and leaves the
    /// rest alone.
    /// </para>
    ///
    /// <para>
    /// <b>It reuses the Canvas and the EventSystem.</b> The scene already has
    /// <c>MainMenuBrandingCanvas</c> carrying the logo, and the menu goes inside it rather than
    /// beside it in a canvas of its own - two canvases would be two draw orders arguing about
    /// which one is on top, and a second EventSystem makes Unity disable one of them at random.
    /// </para>
    ///
    /// <para>
    /// The scene is marked dirty and NOT saved. Look at the result first, then save.
    /// </para>
    /// </summary>
    public static class MainMenuAuthoringTool
    {
        private static readonly string ScenePath =
            "Assets/CatchIfYouCan/Scenes/01_MainMenu.unity";

        // Every name and every number the menu is made of lives in MainMenuScreenBuilder,
        // which builds it. They were duplicated here while this file did the building, and a
        // copy that nothing reads is a second answer waiting to disagree with the first
        // (mistake 1).

        [MenuItem("Catch If You Can/1. LOBBY/Hauptmenue in die Szene schreiben [AENDERT SZENE]",
                  false, 106)]
        public static void AuthorMainMenu()
        {
            Scene scene = SceneManager.GetActiveScene();

            if (scene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return;

                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            // The construction lives in MainMenuScreenBuilder, which the RUNTIME also calls.
            // One builder, two callers: a copy here would be a second opinion about where PLAY
            // sits, and the one that runs less often is the one that drifts (mistake 1).
            var controller = FindComponentInScene<MainMenuModeController>(scene);
            if (controller == null)
            {
                Debug.LogError("[CIYC] No MainMenuModeController in " + scene.name +
                               ". The menu hangs off it, so there is nothing to build against.");
                return;
            }

            if (!MainMenuScreenBuilder.Build(controller, out string report))
            {
                Debug.LogError("[CIYC] Hauptmenue nicht geschrieben: " + report);
                return;
            }

            GameObject canvasGo = FindInScene(scene, MainMenuScreenBuilder.CanvasName);
            if (canvasGo != null)
                EditorUtility.SetDirty(canvasGo);

            EditorSceneManager.MarkSceneDirty(scene);

            GameObject navGo = FindInScene(scene, MainMenuScreenBuilder.NavName);
            if (navGo != null)
                Selection.activeGameObject = navGo;

            Debug.Log(
                "[CIYC] Hauptmenue in " + scene.name + " geschrieben: " + report + "\n" +
                "Die Beschriftungen stehen jetzt in der Szene: MainMenuRoot/Navigation/" +
                "Button_Play/Label_Play und so weiter. Der Text gehoert ab hier DIR - kein " +
                "Code schreibt ihn noch, und ein zweiter Lauf ueberschreibt nichts davon.\n" +
                "Die Szene ist GEAENDERT, aber NICHT gespeichert. Erst ansehen, dann speichern.");
        }

        // Everything this tool used to build - the panels, the labels, the brush strokes, the
        // tap-to-start retirement, the EventSystem - now lives in MainMenuScreenBuilder,
        // because the RUNTIME needs exactly the same construction. A copy here would be a
        // second answer to the same question, and the copy that runs less often is the one
        // that goes stale (mistake 1). What is left below is only what this file itself needs
        // in order to find things in the open scene.

        /// <summary>
        /// A descendant by name, INCLUDING inactive ones. The panels and the retired tap label
        /// are exactly the kind this looks for, and an active-only search would miss them and
        /// build a second one beside each.
        /// </summary>
        private static GameObject FindUnder(Transform root, string name)
        {
            var all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != root && all[i].name == name)
                    return all[i].gameObject;
            }
            return null;
        }

        private static GameObject FindInScene(Scene scene, string name)
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == name)
                    return roots[i];

                GameObject found = FindUnder(roots[i].transform, name);
                if (found != null)
                    return found;
            }
            return null;
        }

        private static T FindComponentInScene<T>(Scene scene) where T : Component
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var found = roots[i].GetComponentsInChildren<T>(true);
                if (found.Length > 0)
                    return found[0];
            }
            return null;
        }
    }
}
