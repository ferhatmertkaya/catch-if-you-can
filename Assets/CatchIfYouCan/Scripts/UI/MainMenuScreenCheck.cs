using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatchIfYouCan.UI
{
    /// <summary>
    /// Checks that the authored main menu is actually in the scene, and says so loudly when it
    /// is not.
    ///
    /// <para>
    /// <b>This class builds nothing.</b> It replaces a runtime builder that did, and the reason
    /// it is a CHECK rather than a builder is a decision about who owns the design: the menu is
    /// a set of real GameObjects saved in <c>01_MainMenu.unity</c> so that it can be selected,
    /// dragged and retyped in the Inspector. Code that recreated any of that on load would undo
    /// the editing it exists to allow.
    /// </para>
    ///
    /// <para>
    /// <b>But a missing menu must never be silent.</b> That is the whole of mistake 47: a screen
    /// that only appears once somebody clicks a menu item in Unity, and a scene that therefore
    /// looks exactly as it did before the work was done - with nothing anywhere saying why. So
    /// when the authored objects are absent this names the one command that writes them and the
    /// exact path it will write them to. A person reading the console gets the fix, not a
    /// mystery.
    /// </para>
    /// </summary>
    public static class MainMenuScreenCheck
    {
        /// <summary>The menu command that authors the menu, quoted verbatim in the error.</summary>
        public const string AuthoringCommand =
            "Catch If You Can > 1. LOBBY > Hauptmenue in die Szene schreiben";

        /// <summary>
        /// Looks for the authored navigation and reports what it found. Returns true when the
        /// menu is there and usable.
        /// </summary>
        public static bool Verify(MainMenuModeController controller, out string report)
        {
            if (controller == null)
            {
                report = "no MainMenuModeController - nothing owns the menu";
                return false;
            }

            Scene scene = controller.gameObject.scene;
            MainMenuNavigation nav = FindNavigation(scene);

            if (nav == null)
            {
                report =
                    "the authored main menu is NOT in this scene. Nothing is broken in code - " +
                    "the objects have simply never been written. Run:  " + AuthoringCommand +
                    "  and save the scene. It creates MainMenuBrandingCanvas/MainMenuRoot/" +
                    "Navigation with Button_Play, Button_Settings and Button_Credits, each " +
                    "carrying its own SelectionBrush and Label. Until then TAP ANYWHERE TO " +
                    "START is still the way into the lobby.";
                return false;
            }

            if (nav.RowCount == 0)
            {
                report =
                    "MainMenuNavigation is in the scene but has no rows bound. Re-run:  " +
                    AuthoringCommand + "  which rebinds them, then save the scene.";
                return false;
            }

            report = "authored menu found: " + nav.RowCount + " rows on '" + nav.name + "'.";
            return true;
        }

        private static MainMenuNavigation FindNavigation(Scene scene)
        {
            if (!scene.IsValid())
                return null;

            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                // Inactive included on purpose: a menu switched off in the Hierarchy is a
                // different problem from a menu that was never authored, and the two must not
                // produce the same message.
                var found = roots[i].GetComponentsInChildren<MainMenuNavigation>(true);
                if (found.Length > 0)
                    return found[0];
            }
            return null;
        }
    }
}
