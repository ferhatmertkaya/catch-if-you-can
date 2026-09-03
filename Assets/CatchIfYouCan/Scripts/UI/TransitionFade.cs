using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CatchIfYouCan.UI
{
    /// <summary>
    /// The black that covers a transition, on a canvas that outlives the scenes either side
    /// of it.
    ///
    /// <para>
    /// <b>It has to survive the swap.</b> The menu's fade was parented to the menu controller,
    /// which is fine for a fade that ends before the scene does and useless for one that has to
    /// stay up <em>while</em> the lobby is unloaded and the mission world takes over - the
    /// overlay would be destroyed mid-transition and the seam it exists to hide would be the
    /// one frame everybody sees.
    /// </para>
    ///
    /// <para>
    /// Order 500, above every other canvas in the project including the touch HUD at 200, and
    /// it swallows taps while it is up so a transition cannot be interrupted by the same thumb
    /// that started it.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TransitionFade : MonoBehaviour
    {
        /// <summary>Above every other canvas. The touch HUD's own comment names this number.</summary>
        public const int SortingOrder = 500;

        private static TransitionFade _instance;

        private Image _image;

        /// <summary>The live overlay, or null when nothing is transitioning.</summary>
        public static TransitionFade Instance => _instance;

        /// <summary>How black it is now.</summary>
        public float Alpha => _image != null ? _image.color.a : 0f;

        /// <summary>
        /// The overlay, built the first time it is needed and kept for the session. Transparent
        /// on creation, so calling this does not black the screen.
        /// </summary>
        public static TransitionFade Ensure()
        {
            if (_instance != null)
                return _instance;

            var go = new GameObject("CIYC_TransitionFade");
            DontDestroyOnLoad(go);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            _instance = go.AddComponent<TransitionFade>();

            var imageGo = new GameObject("Fade", typeof(RectTransform));
            imageGo.transform.SetParent(go.transform, false);
            var rect = imageGo.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            _instance._image = imageGo.AddComponent<Image>();
            _instance._image.color = new Color(0f, 0f, 0f, 0f);
            _instance._image.raycastTarget = false;

            return _instance;
        }

        /// <summary>Sets the blackness immediately.</summary>
        public void SetAlpha(float alpha)
        {
            if (_image == null)
                return;

            float a = Mathf.Clamp01(alpha);
            var c = _image.color;
            c.a = a;
            _image.color = c;

            // Only swallow taps while something is actually covered. A transparent overlay that
            // still blocks raycasts is an invisible wall over the whole interface.
            _image.raycastTarget = a > 0.01f;
        }

        /// <summary>
        /// Fades to a target over a duration. Unscaled, because a transition must run at the
        /// same speed whatever the game has done to <c>Time.timeScale</c>.
        /// </summary>
        public IEnumerator FadeTo(float target, float duration)
        {
            if (_image == null)
                yield break;

            float from = _image.color.a;

            if (duration <= 0f)
            {
                SetAlpha(target);
                yield break;
            }

            for (float e = 0f; e < duration; e += Time.unscaledDeltaTime)
            {
                SetAlpha(Mathf.Lerp(from, target, e / duration));
                yield return null;
            }

            SetAlpha(target);
        }

        /// <summary>Clears the overlay and lets go of it.</summary>
        public static void Dismiss()
        {
            if (_instance == null)
                return;

            Destroy(_instance.gameObject);
            _instance = null;
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        /// <summary>A fresh process holds no overlay from the last one.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => _instance = null;
    }
}
