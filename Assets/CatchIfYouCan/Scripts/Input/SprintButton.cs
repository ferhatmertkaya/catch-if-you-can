using UnityEngine;
using UnityEngine.EventSystems;

namespace CatchIfYouCan.Input
{
    /// <summary>
    /// Hold-to-run button, sitting beside the movement thumb.
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
    /// Releasing on pointer-up <em>and</em> on disable matters: if the HUD is hidden mid-sprint,
    /// the button would otherwise never report the release and the player would run forever.
    /// </para>
    /// </summary>
    public sealed class SprintButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private int _activePointer = int.MinValue;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_activePointer != int.MinValue)
                return;

            _activePointer = eventData.pointerId;
            MobileInputController.Instance?.SetSprint(true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != _activePointer)
                return;

            _activePointer = int.MinValue;
            MobileInputController.Instance?.SetSprint(false);
        }

        private void OnDisable()
        {
            if (_activePointer == int.MinValue)
                return;

            _activePointer = int.MinValue;
            MobileInputController.Instance?.SetSprint(false);
        }
    }
}
