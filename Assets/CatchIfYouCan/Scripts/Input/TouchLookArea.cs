using UnityEngine;
using UnityEngine.EventSystems;

namespace CatchIfYouCan.Input
{
    /// <summary>
    /// The right-hand side of the screen, used to look around.
    ///
    /// <para>
    /// This is a UI element rather than a raw touch scan, and that is the whole point. The
    /// EventSystem already tracks which finger owns which widget, so the thumb on the joystick and
    /// the thumb on this area never contend: each pointer is delivered to exactly one handler.
    /// The previous approach scanned the raw touch list and asked "is any pointer over UI?" once
    /// per frame, which meant holding the movement joystick — itself UI — switched looking off
    /// entirely.
    /// </para>
    ///
    /// <para>
    /// Only <see cref="PointerEventData.delta"/> is read, never the absolute position, so lifting
    /// a thumb and putting it down somewhere else moves the camera by nothing. That is what stops
    /// the view snapping when you reposition your grip.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class TouchLookArea : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Tooltip("Raycasting is switched off when the device has no touchscreen, so a mouse on " +
                 "desktop passes straight through this area and free-look still works.")]
        [SerializeField] private bool onlyRaycastWithTouch = true;

        private UnityEngine.UI.Graphic _graphic;
        private int _activePointer = int.MinValue;

        private void Awake()
        {
            _graphic = GetComponent<UnityEngine.UI.Graphic>();
            ApplyRaycastMode();
        }

        private void OnEnable()
        {
            ApplyRaycastMode();
            MobileInputController.RegisterLookArea(this);
        }

        private void OnDisable()
        {
            _activePointer = int.MinValue;
            MobileInputController.UnregisterLookArea(this);
        }

        private void ApplyRaycastMode()
        {
            if (_graphic == null)
                return;

            _graphic.raycastTarget = !onlyRaycastWithTouch || UnityEngine.Input.touchSupported;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            // First finger down here owns the camera until it lifts. A second finger landing in
            // the same area is ignored rather than fighting the first.
            if (_activePointer == int.MinValue)
                _activePointer = eventData.pointerId;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != _activePointer)
                return;

            MobileInputController.Instance?.AddLookDelta(eventData.delta);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId == _activePointer)
                _activePointer = int.MinValue;
        }
    }
}
