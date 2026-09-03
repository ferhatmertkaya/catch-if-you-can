using CatchIfYouCan.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CatchIfYouCan.UI
{
    /// <summary>
    /// What a control does when it is touched, and what it looks like when it is the chosen one.
    ///
    /// <para>
    /// The scale and the accent bar are both written <b>in the pointer-down handler</b>, with no
    /// coroutine, no animation and no timer between the touch and the pixels. Together with
    /// <see cref="UITheme.ApplyButtonColors"/> setting <c>fadeDuration</c> to zero, that is the
    /// whole reaction path: touch, same frame, done.
    /// </para>
    ///
    /// <para>
    /// The accent bar is the only green on a button. It is four pixels wide, it lives on the
    /// leading edge, and it is the answer to "how does a selected row look different" that does
    /// not involve filling the row with brand colour.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(Selectable))]
    public class UIButtonFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        [SerializeField] private float pressedScale = 0.96f;
        [SerializeField] private bool useHaptic = true;
        [SerializeField] private HapticIntensity hapticIntensity = HapticIntensity.Light;

        [Tooltip("The thin bar on the leading edge that marks a held or selected control. " +
                 "RuntimeUIFactory.CreateButton binds one; a hand-built control may leave it " +
                 "empty, in which case there is simply no accent.")]
        [SerializeField] private Image accentBar;

        private RectTransform _rect;
        private Vector3 _baseScale = Vector3.one;
        private bool _pressed;
        private bool _selected;

        /// <summary>True while a pointer is held down on this button.</summary>
        public bool IsPressed => _pressed;

        /// <summary>True while this control is the chosen one in its group.</summary>
        public bool IsSelected => _selected;

        private void Awake()
        {
            _rect = transform as RectTransform;
            if (_rect != null)
                _baseScale = _rect.localScale;
            RefreshAccent();
        }

        private void OnDisable()
        {
            ResetScale();
        }

        /// <summary>Binds the accent bar built alongside this control.</summary>
        public void BindAccent(Image image)
        {
            accentBar = image;
            RefreshAccent();
        }

        /// <summary>
        /// Marks this control as the current choice - the selected mission in a list, the open
        /// tab in a journal. Persistent, unlike a press, and it does not fill anything.
        /// </summary>
        public void SetSelected(bool selected)
        {
            if (_selected == selected)
                return;
            _selected = selected;
            RefreshAccent();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _pressed = true;
            if (_rect != null)
                _rect.localScale = _baseScale * pressedScale;
            RefreshAccent();
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
            RefreshAccent();
        }

        private void RefreshAccent()
        {
            if (accentBar == null)
                return;

            if (_pressed)
                accentBar.color = UITheme.Primary;
            else if (_selected)
                accentBar.color = UITheme.Secondary;
            else
                accentBar.color = Color.clear;
        }
    }
}
