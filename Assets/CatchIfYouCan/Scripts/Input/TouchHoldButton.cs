using UnityEngine;
using UnityEngine.EventSystems;

namespace CatchIfYouCan.Input
{
    /// <summary>
    /// A HUD button that does not take the look away from the thumb that presses it.
    ///
    /// <para>
    /// The right of the screen is the look area, so any button placed there would ordinarily
    /// punch a hole in it: the EventSystem hands each finger to exactly one widget, so a thumb
    /// that lands on the button owns that finger until it lifts, and dragging it does nothing.
    /// On a phone that is the whole right thumb — the one you look with — so a run button on the
    /// right would mean run <em>or</em> look, never both.
    /// </para>
    ///
    /// <para>
    /// So this forwards its own drag to <see cref="MobileInputController.AddLookDelta"/>. Press
    /// and hold and the action is held; slide the same thumb and the camera turns under it. A
    /// second finger anywhere in the look area still works as it always did, because that is a
    /// different pointer and the EventSystem routes it separately.
    /// </para>
    ///
    /// <para>
    /// Only the delta is forwarded, never the position, which is what
    /// <see cref="TouchLookArea"/> does too — so sliding off the button and back on again turns
    /// the camera by the distance travelled and nothing else.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public abstract class TouchHoldButton : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Tooltip("Turn the camera while this button is held. Off makes it an ordinary button " +
                 "that swallows the finger.")]
        [SerializeField] private bool lookWhileHeld = true;

        [Tooltip("How far the thumb may travel, in screen pixels, before the press counts as a " +
                 "look rather than a tap. Only matters to buttons that act on release.")]
        [SerializeField, Min(0f)] private float dragSlop = 24f;

        private int _activePointer = int.MinValue;
        private float _travelled;

        /// <summary>True between press and release.</summary>
        protected bool IsHeld => _activePointer != int.MinValue;

        /// <summary>True once the thumb has moved far enough to read as a look, not a tap.</summary>
        protected bool WasDragged => _travelled >= dragSlop;

        /// <summary>Called when the thumb lands. </summary>
        protected virtual void OnPressed() { }

        /// <summary>
        /// Called when the thumb lifts. <paramref name="dragged"/> is true when the press turned
        /// into a look, which is how a tap-to-toggle button tells the two apart.
        /// </summary>
        protected virtual void OnReleased(bool dragged) { }

        /// <summary>
        /// Called when the button is taken away while still held - the HUD being hidden, the
        /// player being put into a cinematic. Distinct from a release on purpose: a release is
        /// something the player did and may mean "toggle", while this is the control vanishing
        /// under their thumb and must never be read as an intention. Anything held has to be
        /// dropped here, or it stays held with nothing left to release it.
        /// </summary>
        protected virtual void OnCancelled() { }

        public void OnPointerDown(PointerEventData eventData)
        {
            // One finger at a time. A second landing on the same button is ignored rather than
            // stealing ownership, so lifting it cannot release an action the first still holds.
            if (_activePointer != int.MinValue)
                return;

            _activePointer = eventData.pointerId;
            _travelled = 0f;
            OnPressed();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != _activePointer)
                return;

            _travelled += eventData.delta.magnitude;

            if (lookWhileHeld)
                MobileInputController.Instance?.AddLookDelta(eventData.delta);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != _activePointer)
                return;

            _activePointer = int.MinValue;
            OnReleased(WasDragged);
            _travelled = 0f;
        }

        /// <summary>
        /// Releases a held press when the button goes away under the finger. Without this, a HUD
        /// hidden mid-sprint would never report the release and the player would run forever.
        /// </summary>
        protected virtual void OnDisable()
        {
            if (_activePointer == int.MinValue)
                return;

            _activePointer = int.MinValue;
            _travelled = 0f;
            OnCancelled();
        }
    }
}
