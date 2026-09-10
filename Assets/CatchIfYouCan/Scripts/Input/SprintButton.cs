using UnityEngine;

namespace CatchIfYouCan.Input
{
    /// <summary>
    /// Hold-to-run, on the right of the screen under the look thumb.
    ///
    /// <para>
    /// Held rather than toggled, because a toggle that survives letting go of the joystick is the
    /// kind of thing that leaves you sprinting into a room you meant to creep into. It reports
    /// through <see cref="MobileInputController"/> like every other control, so
    /// <see cref="Player.PlayerController"/> never learns whether a thumb or the shift key asked
    /// for it.
    /// </para>
    ///
    /// <para>
    /// It sits on the right because that is the hand that is free — the left thumb is on the
    /// movement stick and cannot leave it while running — and it inherits
    /// <see cref="TouchHoldButton"/> so being there costs nothing: the same thumb that holds it
    /// still turns the camera by sliding, which is what makes running and looking one gesture
    /// rather than two fingers competing for one corner of the screen.
    /// </para>
    /// </summary>
    public sealed class SprintButton : TouchHoldButton
    {
        protected override void OnPressed() => MobileInputController.Instance?.SetSprint(true);

        protected override void OnReleased(bool dragged) => MobileInputController.Instance?.SetSprint(false);

        protected override void OnCancelled() => MobileInputController.Instance?.SetSprint(false);
    }
}
