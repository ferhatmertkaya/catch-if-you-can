using CatchIfYouCan.Core;
using CatchIfYouCan.UI;
using UnityEngine;

namespace CatchIfYouCan.Interaction
{
    /// <summary>
    /// Der Weg zurück in die Lobby, für den kleinen Testraum hinter dem Portal.
    ///
    /// <para>
    /// <b>Es ist die Route, die es schon gibt.</b> Eine abgeschlossene Untersuchung kehrt über
    /// genau zwei Zeilen zurück — <c>MainMenuModeController.PendingEntryMode = DirectLobby</c>
    /// und <c>SceneLoader.LoadMainMenu()</c> —, und der Menü-Controller kommt dann direkt im Raum
    /// hoch, ohne Kino und ohne „tap to start". Hier wird dieselbe Route ausgelöst, nicht eine
    /// zweite gebaut: eine eigene Rückkehr wäre ein zweiter Weg in dieselbe Szene, und die
    /// beiden gingen beim ersten Umbau auseinander.
    /// </para>
    ///
    /// <para>
    /// <b>E, kein Trigger.</b> Ein Trigger vor einem Rückweg heisst, dass jeder Schritt
    /// rückwärts die Szene wechselt. Die Interaktion läuft über <c>IInteractable</c> und damit
    /// über dieselbe Taste und denselben Mobile-Knopf wie jede Tür.
    /// </para>
    /// </summary>
    public sealed class LobbyReturnPoint : MonoBehaviour, IInteractable
    {
        [SerializeField] private string prompt = "Zurueck zur Lobby";
        [SerializeField] private float distance = 2.6f;

        private bool _leaving;

        public string Prompt => prompt;
        public float HoldDuration => 0f;
        public InteractionType InteractionType => InteractionType.Use;
        public float Distance => distance;

        public bool CanInteract(GameObject interactor) => !_leaving;

        public void Interact(GameObject interactor)
        {
            // Einmal. Zweimal drücken, während die Szene lädt, wäre zwei Ladevorgänge auf
            // dieselbe Szene - und der zweite trifft eine, die es schon nicht mehr gibt.
            if (_leaving)
                return;

            _leaving = true;

            MainMenuModeController.PendingEntryMode = MainMenuEntryMode.DirectLobby;

            if (SceneLoader.Instance == null)
            {
                // Gesagt, nicht verschluckt: ohne Loader passiert gar nichts, und ein Rückweg,
                // der stumm nichts tut, sieht aus wie ein Rückweg, den man übersehen hat.
                CIYCLog.Error("[CIYC][TestRoom] Kein SceneLoader - der Rueckweg in die Lobby " +
                              "kann die Szene nicht laden. Die Absicht wird zurueckgenommen, " +
                              "damit sie nicht bei irgendeinem spaeteren Ladevorgang ein Intro " +
                              "ueberspringt, das niemand ueberspringen wollte.");
                MainMenuModeController.PendingEntryMode = MainMenuEntryMode.Cinematic;
                _leaving = false;
                return;
            }

            CIYCLog.Info("[CIYC][TestRoom] Zurueck in die Lobby, direkt in den Raum ohne Kino.");
            SceneLoader.Instance.LoadMainMenu();
        }
    }
}
