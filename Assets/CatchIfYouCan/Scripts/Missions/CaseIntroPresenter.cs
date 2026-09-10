using System.Collections;
using CatchIfYouCan.UI;
using UnityEngine;
using UnityEngine.UI;

namespace CatchIfYouCan.Missions
{
    public class CaseIntroPresenter : MonoBehaviour
    {
        [SerializeField] private CanvasGroup overlay;
        [SerializeField] private Component introText;
        [SerializeField] private float holdSeconds = 3f;
        [SerializeField] private float fadeDuration = 1.2f;

        private Coroutine _presentRoutine;

        public static CaseIntroPresenter Ensure(CanvasGroup fadeOverlay)
        {
            var existing = Object.FindAnyObjectByType<CaseIntroPresenter>();
            if (existing != null)
            {
                if (fadeOverlay != null && existing.overlay == null)
                    existing.overlay = fadeOverlay;
                return existing;
            }

            var canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null)
                return null;

            var go = new GameObject("CaseIntroPresenter");
            go.transform.SetParent(canvas.transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var presenter = go.AddComponent<CaseIntroPresenter>();
            presenter.overlay = fadeOverlay;

            var textGo = new GameObject("IntroText");
            textGo.transform.SetParent(go.transform, false);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.1f, 0.35f);
            textRect.anchorMax = new Vector2(0.9f, 0.65f);
            textRect.offsetMin = textRect.offsetMax = Vector2.zero;

            presenter.introText = RuntimeUIFactory.CreateText(
                textGo.transform,
                "Body",
                string.Empty,
                36,
                TextAnchor.MiddleCenter,
                true);

            UITheme.StyleTitle(presenter.introText);
            return presenter;
        }

        public void Present(MissionRuntime mission, float holdOverride = -1f, float fadeOverride = -1f)
        {
            if (mission == null)
                return;

            if (_presentRoutine != null)
                StopCoroutine(_presentRoutine);

            float hold = holdOverride >= 0f ? holdOverride : holdSeconds;
            float fade = fadeOverride >= 0f ? fadeOverride : fadeDuration;
            _presentRoutine = StartCoroutine(PresentRoutine(mission, hold, fade));
        }

        private IEnumerator PresentRoutine(MissionRuntime mission, float hold, float fade)
        {
            string timeText = System.DateTime.Now.ToString("HH:mm");
            string body = $"CASE #{mission.CaseNumber}\n{mission.LocationName}\n{timeText}";

            if (introText != null)
            {
                introText.gameObject.SetActive(true);
                UITheme.SetText(introText, body);
                UITheme.StyleTitle(introText);
            }

            if (overlay != null)
                overlay.alpha = 1f;

            yield return new WaitForSeconds(hold);

            if (overlay != null && fade > 0f)
            {
                float elapsed = 0f;
                while (elapsed < fade)
                {
                    elapsed += Time.deltaTime;
                    overlay.alpha = 1f - Mathf.Clamp01(elapsed / fade);
                    yield return null;
                }

                overlay.alpha = 0f;
            }
            else if (overlay != null)
            {
                overlay.alpha = 0f;
            }

            if (introText != null)
                introText.gameObject.SetActive(false);

            _presentRoutine = null;
        }
    }
}
