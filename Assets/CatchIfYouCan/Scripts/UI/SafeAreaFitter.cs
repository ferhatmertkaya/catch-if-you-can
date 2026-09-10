using UnityEngine;

namespace CatchIfYouCan.UI
{
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaFitter : MonoBehaviour
    {
        [SerializeField] private bool applyLeft = true;
        [SerializeField] private bool applyRight = true;
        [SerializeField] private bool applyTop = true;
        [SerializeField] private bool applyBottom = true;
        [SerializeField] private Vector2 extraPadding;

        private RectTransform _rect;
        private Rect _lastSafeArea;
        private Vector2Int _lastScreenSize;

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
        }

        private void OnEnable()
        {
            if (RefuseOnARootCanvas())
                return;

            ApplySafeArea();
        }

        /// <summary>
        /// Says so, loudly, when this fitter has been put somewhere it cannot work.
        ///
        /// <para>
        /// A root Canvas drives its own RectTransform: it rewrites the anchors and offsets to
        /// the whole screen every frame, so the ones written below are overwritten before
        /// anything is drawn. A fitter here does not fail - it silently does nothing, and the
        /// notch is only found on a device, months later, by somebody who can see the
        /// screen and has no reason to suspect the component that is sitting right there
        /// claiming to have handled it.
        /// </para>
        ///
        /// <para>
        /// The fix is always the same: put the fitter on a stretched CHILD of the canvas and
        /// parent the content to that. <c>TouchHudFactory</c> already does exactly this, which
        /// is why the on-screen controls respect the safe area and the screens built by
        /// <c>RuntimeUIFactory.BuildCompleteUI</c> do not.
        /// </para>
        ///
        /// <para>
        /// It disables itself rather than running an Update that can never have an effect.
        /// That is behaviour-neutral: it was already doing nothing.
        /// </para>
        /// </summary>
        private bool RefuseOnARootCanvas()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas == null || !canvas.isRootCanvas)
                return false;

            Debug.LogError(
                "[CIYC] SafeAreaFitter on '" + name + "' is on a root Canvas, whose " +
                "RectTransform the canvas drives, so it can never apply the safe area. Put it " +
                "on a stretched child and parent the content to that - see TouchHudFactory. " +
                "Disabling it so it does not look like the safe area is handled.", this);

            enabled = false;
            return true;
        }

        private void Update()
        {
            if (_lastSafeArea != Screen.safeArea ||
                _lastScreenSize.x != Screen.width ||
                _lastScreenSize.y != Screen.height)
            {
                ApplySafeArea();
            }
        }

        public void ApplySafeArea()
        {
            if (_rect == null)
                _rect = GetComponent<RectTransform>();

            Rect safe = Screen.safeArea;
            _lastSafeArea = safe;
            _lastScreenSize = new Vector2Int(Screen.width, Screen.height);

            Vector2 anchorMin = safe.position;
            Vector2 anchorMax = safe.position + safe.size;
            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;

            if (!applyLeft) anchorMin.x = 0f;
            if (!applyBottom) anchorMin.y = 0f;
            if (!applyRight) anchorMax.x = 1f;
            if (!applyTop) anchorMax.y = 1f;

            _rect.anchorMin = anchorMin;
            _rect.anchorMax = anchorMax;
            _rect.offsetMin = new Vector2(applyLeft ? extraPadding.x : 0f, applyBottom ? extraPadding.y : 0f);
            _rect.offsetMax = new Vector2(applyRight ? -extraPadding.x : 0f, applyTop ? -extraPadding.y : 0f);
        }
    }
}
