using System;

namespace CatchIfYouCan.Equipment
{
    /// <summary>
    /// One thing the player can do with the item in their hand, beyond pressing Use.
    ///
    /// <para>
    /// Eight of the eleven items have controls that were unreachable on the platform this game
    /// is built for. The photo camera can zoom in, zoom out and switch on night vision; the EVP
    /// recorder can change its question; the projector, the video camera and the relic can all
    /// be installed in a room. On a phone there was one equipment button - Use - so none of it
    /// could be done, and the EVP recorder's question was bound to the Tab key.
    /// </para>
    ///
    /// <para>
    /// <b>Items describe; the HUD renders.</b> An item says "I can do this, call this when it
    /// is pressed" and knows nothing about buttons, layouts or screens. The HUD reads the list
    /// off whatever is selected and draws it. That is what keeps the touch layout changeable
    /// without touching gameplay, and what will let a gamepad or a keyboard bind the same list
    /// later without a second description of it.
    /// </para>
    /// </summary>
    public readonly struct EquipmentAction
    {
        /// <summary>What the button says. Short: it is a thumb-sized control.</summary>
        public readonly string Label;

        /// <summary>What pressing it does.</summary>
        public readonly Action Invoke;

        /// <summary>
        /// Whether it can be pressed right now. A disabled action is still listed, because a
        /// control that disappears when it is unavailable moves the ones next to it - and a
        /// thumb aiming at PLACE should never land on CANCEL because the layout shifted.
        /// </summary>
        public readonly bool Enabled;

        public EquipmentAction(string label, Action invoke, bool enabled = true)
        {
            Label = label;
            Invoke = invoke;
            Enabled = enabled;
        }

        public bool IsValid => Invoke != null && !string.IsNullOrEmpty(Label);
    }
}
