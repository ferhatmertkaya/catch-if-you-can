using CatchIfYouCan.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatchIfYouCan.EditorTools
{
    /// <summary>
    /// Puts the main menu into <c>01_MainMenu.unity</c> as real objects the moment that scene is
    /// open for editing, so it can be seen, dragged and saved like the logo beside it.
    ///
    /// <para>
    /// <b>Why this runs by itself.</b> The menu was a menu COMMAND for three rounds, and the
    /// command was never clicked - so the scene kept its old contents and the screen kept saying
    /// TAP ANYWHERE TO START, with nothing anywhere explaining why (mistake 47, three times). A
    /// step that a person has to remember is a step that does not happen. The command is still
    /// there for a deliberate re-run; this is what makes the normal case need no step at all.
    /// </para>
    ///
    /// <para>
    /// <b>It writes, it never saves.</b> The scene is marked dirty and left for the user to look
    /// at and save - the same rule every authoring command in this project follows, because a
    /// tool that saves behind somebody can bury an unrelated edit they had open.
    /// </para>
    ///
    /// <para>
    /// <b>It builds only what is missing.</b> `MainMenuScreenBuilder` looks every object up
    /// before it creates one, so a scene that already carries the menu is adopted untouched -
    /// position, size, text and font stay exactly as they were dragged and typed. This therefore
    /// runs at most once per scene with anything to do, and afterwards is a no-op.
    /// </para>
    ///
    /// <para>
    /// Switch it off for a session with <c>Catch If You Can &gt; 1. LOBBY &gt; Hauptmenue
    /// automatisch schreiben</c>.
    /// </para>
    /// </summary>
    [InitializeOnLoad]
    public static class MainMenuSceneAuthoring
    {
        private const string ScenePath = "Assets/CatchIfYouCan/Scenes/01_MainMenu.unity";
        private const string DisabledKey = "CIYC.MainMenuAutoAuthor.Disabled";
        private const string MenuPath =
            "Catch If You Can/1. LOBBY/Hauptmenue automatisch schreiben [EDITOR]";

        /// <summary>The scene path this has already looked at, so it asks once rather than per frame.</summary>
        private static string _checked;

        static MainMenuSceneAuthoring()
        {
            EditorApplication.update += Tick;
        }

        [MenuItem(MenuPath, false, 101)]
        private static void Toggle()
        {
            bool nowDisabled = !SessionState.GetBool(DisabledKey, false);
            SessionState.SetBool(DisabledKey, nowDisabled);

            // Re-ask on the next tick, so switching it back on takes effect immediately.
            _checked = null;

            Debug.Log("[CIYC] Hauptmenue automatisch schreiben: " +
                      (nowDisabled ? "AUS fuer diese Sitzung." : "AN."));
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, !SessionState.GetBool(DisabledKey, false));
            return true;
        }

        private static void Tick()
        {
            // Never while entering Play or mid-compile: the scene is being serialised or the
            // types are being swapped underneath, and writing objects into either is how an
            // editor tool corrupts what it was meant to help with.
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                return;

            if (SessionState.GetBool(DisabledKey, false))
                return;

            Scene scene = SceneManager.GetActiveScene();

            if (scene.path != ScenePath)
            {
                // A different scene is open - forget what was checked, so coming back re-asks.
                _checked = null;
                return;
            }

            if (_checked == scene.path)
                return;

            _checked = scene.path;

            if (!scene.isLoaded)
            {
                _checked = null;
                return;
            }

            var controller = FindController(scene);
            if (controller == null)
                return;

            if (MainMenuScreenBuilder.AlreadyAuthored(controller))
                return;

            if (!MainMenuScreenBuilder.Build(controller, out string report))
            {
                Debug.LogWarning("[CIYC] Hauptmenue nicht geschrieben: " + report);
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log(
                "[CIYC] Hauptmenue in die Szene geschrieben: " + report + "\n" +
                "Zu finden unter MainMenuBrandingCanvas/MainMenuRoot/Navigation. Verschieben, " +
                "umbenennen und den Text aendern kannst du alles im Inspector - beim naechsten " +
                "Mal wird es uebernommen statt neu gebaut.\n" +
                "Die Szene ist GEAENDERT, aber NICHT gespeichert. Erst ansehen, dann speichern.");
        }

        private static MainMenuModeController FindController(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var found = roots[i].GetComponentsInChildren<MainMenuModeController>(true);
                if (found.Length > 0)
                    return found[0];
            }
            return null;
        }
    }
}
