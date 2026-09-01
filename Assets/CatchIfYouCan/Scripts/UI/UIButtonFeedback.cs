using CatchIfYouCan.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CatchIfYouCan.UI
{
    [RequireComponent(typeof(Selectable))]
    public class UIButtonFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        [SerializeField] private float pressedScale = 0.96f;
        [SerializeField] private bool useHaptic = true;
        [SerializeField] private HapticIntensity hapticIntensity = HapticIntensity.Light;

        private RectTransform _rect;
        private Vector3 _baseScale = Vector3.one;
        private bool _pressed;

        /// <summary>True while a pointer is held down on this button.</summary>
        public bool IsPressed => _pressed;

        private void Awake()
        {
            _rect = transform as RectTransform;
            if (_rect != null)
                _baseScale = _rect.localScale;
        }

        private void OnDisable()
        {
            ResetScale();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _pressed = true;
            if (_rect != null)
                _rect.localScale = _baseScale * pressedScale;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            ResetScale();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!useHaptic) return;
            if (HapticManager.Instance != null)
                HapticManager.Instance.Play(hapticIntensity);
        }

        private void ResetScale()
        {
            _pressed = false;
            if (_rect != null)
                _rect.localScale = _baseScale;
        }
    }
}
