using CatchIfYouCan.Equipment;
using UnityEngine;
using UnityEngine.UI;

namespace CatchIfYouCan.UI
{
    /// <summary>
    /// The monitor the placed video cameras feed into.
    ///
    /// <para>
    /// It had every control and no picture: buttons, a night-vision toggle, a name and a
    /// distortion tint, and nowhere for the feed itself to appear. The camera network answered
    /// by enabling a Camera with no target texture, which draws the room over the top of the
    /// player's view rather than into a panel. The <see cref="RawImage"/> below is the missing
    /// half.
    /// </para>
    ///
    /// <para>
    /// Being open is also now a fact the network is told, rather than one it assumes. Nothing
    /// renders while no monitor is up, which is what keeps four installed cameras from being
    /// four render passes a frame on a phone.
    /// </para>
    /// </summary>
    public class CameraMonitorUI : MonoBehaviour
    {
        [SerializeField] private Component cameraNameText;
        [SerializeField] private Button prevButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Toggle nightVisionToggle;
        [SerializeField] private Image distortionOverlay;
        [SerializeField] private RawImage feedSurface;

        public Button PrevButton => prevButton;
        public Button NextButton => nextButton;
        public Image DistortionOverlay => distortionOverlay;

        /// <summary>Where the picture goes. Without one there is a monitor and no feed.</summary>
        public RawImage FeedSurface => feedSurface;

        public void BindRuntime(
            Component cameraNameText,
            Button prevButton,
            Button nextButton,
            Toggle nightVisionToggle,
            Image distortionOverlay,
            RawImage feedSurface = null)
        {
            this.cameraNameText = cameraNameText;
            this.prevButton = prevButton;
            this.nextButton = nextButton;
            this.nightVisionToggle = nightVisionToggle;
            this.distortionOverlay = distortionOverlay;
            this.feedSurface = feedSurface;
            WireButtons();
        }

        private void OnEnable()
        {
            WireButtons();
            CameraNetworkManager.Instance?.AddWatcher();
        }

        private void OnDisable()
        {
            CameraNetworkManager.Instance?.RemoveWatcher();
        }

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
                ShowFeed(null);
                return;
            }

            var cam = network.ActiveCamera;
            string name = cam != null ? cam.name : "NO SIGNAL";
            UITheme.SetText(cameraNameText, name.ToUpperInvariant());
            UITheme.StyleTitle(cameraNameText);

            ShowFeed(network.Feed);

            if (nightVisionToggle != null)
                nightVisionToggle.SetIsOnWithoutNotify(network.NightVisionEnabled);

            if (distortionOverlay != null)
            {
                float d = network.SignalDistortion;
                distortionOverlay.color = new Color(0.15f, 1f, 0.35f, d * 0.35f);
            }
        }

        /// <summary>
        /// Puts the network's picture on the panel, or takes it away. A monitor with no camera
        /// installed shows nothing rather than the last frame of a camera that has been picked
        /// back up.
        /// </summary>
        private void ShowFeed(Texture feed)
        {
            if (feedSurface == null)
                return;

            feedSurface.texture = feed;
            feedSurface.enabled = feed != null;
        }
    }
}
