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
            ApplySafeArea();
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
