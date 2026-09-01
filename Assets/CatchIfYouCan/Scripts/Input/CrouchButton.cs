using UnityEngine;
using UnityEngine.UI;

namespace CatchIfYouCan.Input
{
    /// <summary>
    /// Tap-to-crouch, beside the run button on the right.
    ///
    /// <para>
    /// Toggled where sprint is held, and the asymmetry is deliberate. Sprinting is a burst you
    /// take and give back within a few seconds; crouching is a way of being in a room, held while
    /// you cross it, look around it and wait in it. A button you must keep a thumb on for that
    /// long is a thumb you no longer have for looking.
    /// </para>
    ///
    /// <para>
    /// The toggle fires on release rather than on press, and only when the thumb barely moved.
    /// That is what lets the same button be looked through: slide off it and the camera turns and
    /// the crouch is left exactly as it was, so reaching past the button for the look area never
    /// stands the player up in front of whatever they were hiding from.
    /// </para>
    /// </summary>
    public sealed class CrouchButton : TouchHoldButton
    {
        [Tooltip("Brightened while crouched, so the latch is visible. A toggle with no readable " +
                 "state is a toggle nobody trusts. Optional.")]
        [SerializeField] private Graphic activeIndicator;

        [SerializeField] private Color idleColor = new Color(0.55f, 0.78f, 0.66f, 0.22f);
        [SerializeField] private Color activeColor = new Color(0.66f, 0.88f, 0.74f, 0.85f);

        private bool _crouched;

        /// <summary>Whether the button is currently latched down.</summary>
        public bool Crouched => _crouched;

        private void OnEnable() => Refresh();

        protected override void OnReleased(bool dragged)
        {
            // A drag was a look, not a tap. Leaving the crouch alone is the whole reason the
            // button can sit inside the look area at all.
            if (dragged)
                return;

            _crouched = !_crouched;
            MobileInputController.Instance?.SetCrouch(_crouched);
            Refresh();
        }

        protected override void OnCancelled()
        {
            // The HUD went away under the thumb. Stand up rather than leaving the player latched
            // into a crouch with no button left to release it.
            if (!_crouched)
                return;

            _crouched = false;
            MobileInputController.Instance?.SetCrouch(false);
            Refresh();
        }

        private void Refresh()
        {
            if (activeIndicator != null)
                activeIndicator.color = _crouched ? activeColor : idleColor;
        }
    }
}
