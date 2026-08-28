using CatchIfYouCan.Equipment;
using UnityEngine;
using UnityEngine.UI;

namespace CatchIfYouCan.UI
{
    public class CameraMonitorUI : MonoBehaviour
    {
        [SerializeField] private Component cameraNameText;
        [SerializeField] private Button prevButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Toggle nightVisionToggle;
        [SerializeField] private Image distortionOverlay;

        public Button PrevButton => prevButton;
        public Button NextButton => nextButton;
        public Image DistortionOverlay => distortionOverlay;

        public void BindRuntime(
            Component cameraNameText,
            Button prevButton,
            Button nextButton,
            Toggle nightVisionToggle,
            Image distortionOverlay)
        {
            this.cameraNameText = cameraNameText;
            this.prevButton = prevButton;
            this.nextButton = nextButton;
            this.nightVisionToggle = nightVisionToggle;
            this.distortionOverlay = distortionOverlay;
            WireButtons();
        }

        private void OnEnable() => WireButtons();

        private void Update()
        {
            RefreshDisplay();
        }

        private void WireButtons()
        {
            if (prevButton != null)
            {
                prevButton.onClick.RemoveAllListeners();
                prevButton.onClick.AddListener(() => CameraNetworkManager.Instance?.SelectPrevious());
            }
            if (nextButton != null)
            {
                nextButton.onClick.RemoveAllListeners();
                nextButton.onClick.AddListener(() => CameraNetworkManager.Instance?.SelectNext());
            }
            if (nightVisionToggle != null)
            {
                nightVisionToggle.onValueChanged.RemoveAllListeners();
                nightVisionToggle.onValueChanged.AddListener(on =>
                {
                    if (CameraNetworkManager.Instance != null && on != CameraNetworkManager.Instance.NightVisionEnabled)
                        CameraNetworkManager.Instance.ToggleNightVision();
                });
            }
        }

        private void RefreshDisplay()
        {
            var network = CameraNetworkManager.Instance;
            if (network == null)
            {
                UITheme.SetText(cameraNameText, "NO FEED");
                return;
            }

            var cam = network.ActiveCamera;
            string name = cam != null ? cam.name : "NO SIGNAL";
            UITheme.SetText(cameraNameText, name.ToUpperInvariant());
            UITheme.StyleTitle(cameraNameText);

            if (nightVisionToggle != null)
                nightVisionToggle.SetIsOnWithoutNotify(network.NightVisionEnabled);

            if (distortionOverlay != null)
            {
                float d = network.SignalDistortion;
                distortionOverlay.color = new Color(0.15f, 1f, 0.35f, d * 0.35f);
            }
        }
    }
}
