using CatchIfYouCan.Interaction;
using UnityEngine;
using UnityEngine.UI;

namespace CatchIfYouCan.UI
{
    public class InteractionPromptUI : MonoBehaviour
    {
        [SerializeField] private Image handIcon;
        [SerializeField] private Component promptText;
        [SerializeField] private RectTransform rootRect;
        [SerializeField] private InteractionController interactionController;

        public RectTransform RootRect => rootRect;

        public void BindRuntime(Image handIcon, Component promptText)
        {
            this.handIcon = handIcon;
            this.promptText = promptText;
            rootRect = GetComponent<RectTransform>();
            ResolveInteractionController();
        }

        private void OnEnable()
        {
            ResolveInteractionController();
            Subscribe(true);
            HidePrompt();
        }

        private void OnDisable()
        {
            Subscribe(false);
        }

        private void ResolveInteractionController()
        {
            if (interactionController == null)
                interactionController = FindFirstObjectByType<InteractionController>();
        }

        private void Subscribe(bool subscribe)
        {
            if (interactionController == null) return;
            if (subscribe)
                interactionController.OnPromptChanged += HandlePrompt;
            else
                interactionController.OnPromptChanged -= HandlePrompt;
        }

        private void HandlePrompt(string prompt, InteractionType type, float holdProgress)
        {
            if (string.IsNullOrEmpty(prompt))
            {
                HidePrompt();
                NotifyHud(false);
                return;
            }

            gameObject.SetActive(true);
            string holdSuffix = holdProgress > 0f ? $" ({Mathf.RoundToInt(holdProgress * 100f)}%)" : string.Empty;
            UITheme.SetText(promptText, prompt + holdSuffix);
            UITheme.StyleBody(promptText);

            if (handIcon != null)
                handIcon.color = type == InteractionType.Hide ? UITheme.Warning : UITheme.Primary;

            NotifyHud(true);
        }

        private void HidePrompt()
        {
            UITheme.SetText(promptText, string.Empty);
            if (handIcon != null)
                handIcon.color = new Color(UITheme.Primary.r, UITheme.Primary.g, UITheme.Primary.b, 0.25f);
        }

        private void NotifyHud(bool available)
        {
            var hud = FindFirstObjectByType<MobileHUDController>();
            hud?.SetInteractAvailable(available);
        }
    }
}
