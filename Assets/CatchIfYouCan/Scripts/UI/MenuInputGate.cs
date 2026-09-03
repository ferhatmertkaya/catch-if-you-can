using System.Collections.Generic;
using CatchIfYouCan.Core;
using UnityEngine;

namespace CatchIfYouCan.UI
{
    /// <summary>
    /// The one owner of the fact "a fullscreen menu is up", and the only thing that suspends
    /// the player's controls and the on-screen HUD for one.
    ///
    /// <para>
    /// <b>Why this exists.</b> The lobby board used to hand the controls back itself, in
    /// <c>Close()</c>, and then show mission select - so the joystick, sprint, crouch, torch and
    /// interact buttons all came back <em>underneath</em> a fullscreen menu, on top of the
    /// mission list, taking its touches. Two screens each restoring the HUD on their own way out
    /// cannot be sequenced correctly; one counter that only restores when the last of them has
    /// gone, can.
    /// </para>
    ///
    /// <para>
    /// <b>It suspends, it does not destroy.</b> <see cref="Player.PlayerSpawner.SetHudVisible"/>
    /// deactivates the touch HUD object the spawner already built and reactivates the same one;
    /// nothing is rebuilt, no second joystick is created, and the HUD's own state survives the
    /// menu.
    /// </para>
    ///
    /// <para>
    /// Holders are named rather than counted so that a screen which opens twice does not take
    /// the gate twice, an unbalanced release is reported with the name of whoever is still
    /// holding, and the player can never be left permanently locked by a screen that closed
    /// without saying so.
    /// </para>
    /// </summary>
    public static class MenuInputGate
    {
        private static readonly List<string> Holders = new List<string>();

        /// <summary>True while at least one fullscreen menu is up.</summary>
        public static bool IsMenuOpen => Holders.Count > 0;

        /// <summary>How many screens are holding the gate. For diagnostics.</summary>
        public static int HolderCount => Holders.Count;

        /// <summary>
        /// Takes the player's controls and hides the touch HUD for a named screen. Taking it
        /// twice under the same name is the same as taking it once.
        /// </summary>
        public static void Push(string owner)
        {
            if (string.IsNullOrEmpty(owner))
            {
                CIYCLog.Error("[CIYC][UI] MenuInputGate.Push with no owner name. Every holder " +
                              "must name itself, or an unbalanced release cannot be traced.");
                return;
            }

            if (Holders.Contains(owner))
                return;

            Holders.Add(owner);
            if (Holders.Count == 1)
                Suspend();
        }

        /// <summary>
        /// Gives the controls and the HUD back, but only once the last menu has released.
        /// Releasing something that is not holding is reported and changes nothing.
        /// </summary>
        public static void Pop(string owner)
        {
            if (string.IsNullOrEmpty(owner))
                return;

            if (!Holders.Remove(owner))
                return;

            if (Holders.Count == 0)
                Restore();
        }

        /// <summary>
        /// Drops every hold and gives the player back their controls.
        ///
        /// <para>
        /// For a scene change, where the screens holding the gate are about to be destroyed
        /// without closing. Not a way to overrule a menu that is still on screen: it says who
        /// was holding, because a hold left behind at a scene boundary is a bug in that screen.
        /// </para>
        /// </summary>
        public static void Clear()
        {
            if (Holders.Count > 0)
            {
                CIYCLog.Warn("[CIYC][UI] MenuInputGate cleared while held by: " +
                             string.Join(", ", Holders) + ". A screen closed without releasing.");
                Holders.Clear();
                Restore();
            }
        }

        private static bool _suspended;
        private static bool _restoreInput = true;
        private static bool _restoreHud = true;

        /// <summary>
        /// Takes the controls away, remembering what they were.
        ///
        /// <para>
        /// <b>What is remembered matters.</b> Restoring by forcing "on" would be wrong at every
        /// moment the game deliberately has them off - the lobby handover holds both back
        /// behind a fade on purpose, and a menu that opened and closed inside that window would
        /// have put the joystick on screen over black. With no player yet, the remembered state
        /// is "in control", because that is what a player who arrives later should get.
        /// </para>
        /// </summary>
        private static void Suspend()
        {
            bool hasPlayer = Player.PlayerSpawner.HasPlayer;
            _restoreInput = !hasPlayer || Player.PlayerSpawner.IsInputEnabled;
            _restoreHud = !hasPlayer || Player.PlayerSpawner.IsHudVisible;
            _suspended = true;

            Player.PlayerSpawner.SetInputEnabled(false);
            Player.PlayerSpawner.SetHudVisible(false);
        }

        /// <summary>Puts back exactly what <see cref="Suspend"/> took.</summary>
        private static void Restore()
        {
            if (!_suspended)
                return;
            _suspended = false;

            Player.PlayerSpawner.SetInputEnabled(_restoreInput);
            Player.PlayerSpawner.SetHudVisible(_restoreHud);
        }

        /// <summary>A fresh process holds nothing, whatever the last one was showing.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            Holders.Clear();
            _suspended = false;
            _restoreInput = true;
            _restoreHud = true;
        }
    }
}
