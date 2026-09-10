#if UNITY_EDITOR
using UnityEditor;

namespace CatchIfYouCan.EditorTools
{
    /// <summary>
    /// The one confirmation a wide-reaching command has to pass before it runs.
    ///
    /// <para>
    /// One implementation, not one per command. Four tools in this project can rewrite large
    /// parts of the asset folder, and four hand-written dialogs would say four different things
    /// - which is how a user learns to click through them without reading.
    /// </para>
    /// <para>
    /// It always states the same five facts, because those are the ones that decide whether the
    /// click is safe: what changes, how many assets if that is knowable, whether anything is
    /// reimported, whether any scene is saved, and how to get out.
    /// </para>
    /// <para>
    /// On Unity's <c>DisplayDialog</c> the caller cannot choose which button has keyboard focus,
    /// so "Cancel is the default" is implemented the only way that actually holds: ANY answer
    /// other than the explicit affirmative aborts, and the affirmative button is labelled with
    /// the action rather than with "OK". Closing the dialog does nothing.
    /// </para>
    /// </summary>
    public static class DangerousCommandGate
    {
        /// <summary>Pass -1 when the number of affected assets cannot be known in advance.</summary>
        public const int UnknownCount = -1;

        /// <summary>
        /// Returns true only if the user explicitly chose the action.
        /// </summary>
        public static bool Confirm(string title, string whatChanges, int assetsAffected,
                                   bool reimports, bool savesScenes, string actionLabel)
        {
            string message =
                whatChanges + "\n\n" +
                "Betroffene Assets : " +
                (assetsAffected == UnknownCount
                    ? "nicht im Voraus bekannt"
                    : assetsAffected.ToString()) + "\n" +
                "Reimport          : " + (reimports
                    ? "JA - das kann bei einem grossen Paket Minuten dauern"
                    : "nein") + "\n" +
                "Szenen speichern  : " + (savesScenes
                    ? "JA - ungespeicherte Aenderungen werden mitgeschrieben"
                    : "nein") + "\n\n" +
                "Abbrechen ist die sichere Antwort. Im Zweifel abbrechen und nachsehen.";

            return EditorUtility.DisplayDialog(title, message, actionLabel, "Abbrechen");
        }
    }
}
#endif
