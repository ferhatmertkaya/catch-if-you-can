using UnityEngine;
using UnityEngine.EventSystems;

namespace CatchIfYouCan.Input
{
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private RectTransform background;
        [SerializeField] private RectTransform handle;
        [SerializeField] private float handleRange = 80f;
        [SerializeField] private float deadZone = 0.1f;

        public Vector2 Direction { get; private set; }
        public bool IsActive { get; private set; }

        private Vector2 _pointerDownPos;
        private Canvas _canvas;
        private Camera _uiCamera;

        private void Awake()
        {
            _canvas = GetComponentInParent<Canvas>();
            if (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                _uiCamera = _canvas.worldCamera;

            ResetHandle();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            IsActive = true;
            _pointerDownPos = eventData.position;
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (background == null || handle == null)
            {
                Direction = Vector2.zero;
                return;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                background, eventData.position, _uiCamera, out Vector2 localPoint);

            Vector2 clamped = Vector2.ClampMagnitude(localPoint, handleRange);
            handle.anchoredPosition = clamped;

            Vector2 normalized = clamped / handleRange;
            Direction = normalized.magnitude < deadZone ? Vector2.zero : normalized;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            IsActive = false;
            Direction = Vector2.zero;
            ResetHandle();
        }

        private void ResetHandle()
        {
            if (handle != null)
                handle.anchoredPosition = Vector2.zero;
        }
    }
}
