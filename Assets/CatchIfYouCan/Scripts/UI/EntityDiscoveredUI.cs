using System.Collections;
using CatchIfYouCan.Core;
using CatchIfYouCan.Ghost;
using UnityEngine;

namespace CatchIfYouCan.UI
{
    public class EntityDiscoveredUI : MonoBehaviour
    {
        [SerializeField] private Component titleText;
        [SerializeField] private Component nameText;
        [SerializeField] private Component descText;
        [SerializeField] private float displayDuration = 4f;
        [SerializeField] private float animDuration = 0.45f;

        private Coroutine _showRoutine;

        public void BindRuntime(Component titleText, Component nameText, Component descText)
        {
            this.titleText = titleText;
            this.nameText = nameText;
            this.descText = descText;
        }

        private void OnEnable()
        {
            GameEvents.OnEntityDiscovered += ShowEntity;
            gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            GameEvents.OnEntityDiscovered -= ShowEntity;
        }

        public void ShowEntity(GhostDefinition ghost)
        {
            if (ghost == null) return;
            if (_showRoutine != null)
                StopCoroutine(_showRoutine);
            _showRoutine = StartCoroutine(ShowRoutine(ghost));
        }

        private IEnumerator ShowRoutine(GhostDefinition ghost)
        {
            gameObject.SetActive(true);
            if (UIManager.Instance != null)
                UIManager.Instance.Show(UIScreen.EntityDiscovered, false);

            UITheme.SetText(titleText, "ENTITY DISCOVERED");
            UITheme.StyleTitle(titleText);
            UITheme.SetText(nameText, ghost.DisplayName.ToUpperInvariant());
            UITheme.SetTextColor(nameText, UITheme.Secondary);
            UITheme.SetText(descText, ghost.Description);
            UITheme.StyleBody(descText);

            var panel = transform.GetChild(0);
            if (panel != null)
            {
                var rect = panel as RectTransform;
                Vector3 start = Vector3.one * 0.85f;
                Vector3 end = Vector3.one;
                float t = 0f;
                panel.localScale = start;
                while (t < animDuration)
                {
                    t += Time.unscaledDeltaTime;
                    panel.localScale = Vector3.Lerp(start, end, t / animDuration);
                    yield return null;
                }
                panel.localScale = end;
            }

            yield return new WaitForSecondsRealtime(displayDuration);

            gameObject.SetActive(false);
            if (UIManager.Instance != null && UIManager.Instance.CurrentScreen == UIScreen.EntityDiscovered)
                UIManager.Instance.Show(UIScreen.HUD, false);

            _showRoutine = null;
        }
    }
}
