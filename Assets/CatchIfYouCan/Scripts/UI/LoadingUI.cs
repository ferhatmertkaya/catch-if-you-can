using CatchIfYouCan.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CatchIfYouCan.UI
{
    public class LoadingUI : MonoBehaviour
    {
        private static readonly string[] Tips =
        {
            "EMF 5 is strong evidence — but not every spike means a hunt.",
            "Salt footprints glow under UV light.",
            "Stay silent during a hunt. Noise draws the entity.",
            "The Warding Relic can interrupt a hunt nearby.",
            "Cold rooms often mark the entity's favored space.",
            "Headphones reveal distant whispers more clearly.",
            "Hide spots are not always safe. Listen first.",
            "Spectral Grid silhouettes last only a moment."
        };

        [SerializeField] private Slider progressSlider;
        [SerializeField] private Component tipText;
        [SerializeField] private Component logoText;

        public Slider ProgressSlider => progressSlider;
        public Component TipText => tipText;
        public Component LogoText => logoText;

        public void BindRuntime(Slider progressSlider, Component tipText, Component logoText)
        {
            this.progressSlider = progressSlider;
            this.tipText = tipText;
            this.logoText = logoText;
            ShowRandomTip();
        }

        private void OnEnable()
        {
            SetProgress(0f);
            ShowRandomTip();
            GameEvents.OnTipRequested += ShowTip;
        }

        private void OnDisable()
        {
            GameEvents.OnTipRequested -= ShowTip;
        }

        public void SetProgress(float value)
        {
            if (progressSlider != null)
                progressSlider.value = Mathf.Clamp01(value);
        }

        public void ShowRandomTip()
        {
            ShowTip(Tips[Random.Range(0, Tips.Length)]);
        }

        public void ShowTip(string tip)
        {
            UITheme.SetText(tipText, tip);
            UITheme.StyleBody(tipText);
            UITheme.SetText(logoText, "CATCH IF YOU CAN");
            UITheme.StyleTitle(logoText);
        }
    }
}
