using UnityEngine;
using CatchIfYouCan.Evidence;

namespace CatchIfYouCan.Equipment
{
    public class VideoCameraEquipment : EquipmentBase
    {
        [SerializeField] private Camera feedCamera;
        [SerializeField] private Light nightVisionLight;
        [SerializeField] private float distortionPerDistance = 0.02f;
        [SerializeField] private float maxDistortionContribution = 0.35f;

        public float LocalDistortionContribution { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            if (feedCamera == null)
                feedCamera = GetComponentInChildren<Camera>();
        }

        protected override void OnPlaced()
        {
            SetDeviceActive(true);
            RegisterWithNetwork();
        }

        protected override void OnEquipped()
        {
            UnregisterFromNetwork();
        }

        private void OnDisable()
        {
            UnregisterFromNetwork();
        }

        protected override void TickEquipped(float deltaTime)
        {
            if (!IsPlaced)
                return;

            float distance = HandAnchor != null
                ? Vector3.Distance(transform.position, HandAnchor.position)
                : 0f;

            LocalDistortionContribution = Mathf.Min(maxDistortionContribution, distance * distortionPerDistance);
        }

        private void RegisterWithNetwork()
        {
            if (CameraNetworkManager.Instance != null)
                CameraNetworkManager.Instance.RegisterCamera(this);
        }

        private void UnregisterFromNetwork()
        {
            if (CameraNetworkManager.Instance != null)
                CameraNetworkManager.Instance.UnregisterCamera(this);
        }

        public void SetFeedActive(bool activeFeed, bool nightVision, float signalDistortion)
        {
            if (feedCamera != null)
                feedCamera.enabled = activeFeed;

            if (nightVisionLight != null)
                nightVisionLight.enabled = activeFeed && nightVision;

            LocalDistortionContribution = activeFeed
                ? Mathf.Max(LocalDistortionContribution, signalDistortion * 0.5f)
                : LocalDistortionContribution;
        }
    }
}
