using UnityEngine;
using CatchIfYouCan.Core;
using CatchIfYouCan.Evidence;

namespace CatchIfYouCan.Equipment
{
    public class CameraNetworkManager : Utilities.SingletonBehaviour<CameraNetworkManager>
    {
        [SerializeField] private float signalDistortionBase = 0.15f;
        [SerializeField] private KeyCode nextCameraKey = KeyCode.RightBracket;
        [SerializeField] private KeyCode previousCameraKey = KeyCode.LeftBracket;
        [SerializeField] private KeyCode nightVisionKey = KeyCode.N;

        private readonly System.Collections.Generic.List<VideoCameraEquipment> _cameras =
            new System.Collections.Generic.List<VideoCameraEquipment>();

        private int _activeIndex = -1;
        private bool _nightVisionEnabled;

        public VideoCameraEquipment ActiveCamera =>
            _activeIndex >= 0 && _activeIndex < _cameras.Count ? _cameras[_activeIndex] : null;

        public bool NightVisionEnabled => _nightVisionEnabled;
        public float SignalDistortion { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            ServiceLocator.Register(this);
        }

        protected override void OnDestroy()
        {
            if (Instance == this)
                ServiceLocator.Unregister<CameraNetworkManager>();
            base.OnDestroy();
        }

        private void Update()
        {
            if (_cameras.Count == 0)
                return;

            if (Input.GetKeyDown(nextCameraKey))
                SelectNext();
            if (Input.GetKeyDown(previousCameraKey))
                SelectPrevious();
            if (Input.GetKeyDown(nightVisionKey))
                ToggleNightVision();

            UpdateSignalDistortion();
            ApplyFeedSettings();
        }

        public void RegisterCamera(VideoCameraEquipment camera)
        {
            if (camera == null || _cameras.Contains(camera))
                return;

            _cameras.Add(camera);
            if (_activeIndex < 0)
                _activeIndex = 0;
        }

        public void UnregisterCamera(VideoCameraEquipment camera)
        {
            if (camera == null)
                return;

            int index = _cameras.IndexOf(camera);
            if (index < 0)
                return;

            _cameras.RemoveAt(index);
            if (_cameras.Count == 0)
                _activeIndex = -1;
            else if (_activeIndex >= _cameras.Count)
                _activeIndex = _cameras.Count - 1;
        }

        public void SelectNext()
        {
            if (_cameras.Count == 0)
                return;

            _activeIndex = (_activeIndex + 1) % _cameras.Count;
        }

        public void SelectPrevious()
        {
            if (_cameras.Count == 0)
                return;

            _activeIndex = (_activeIndex - 1 + _cameras.Count) % _cameras.Count;
        }

        public void ToggleNightVision()
        {
            _nightVisionEnabled = !_nightVisionEnabled;
        }

        private void UpdateSignalDistortion()
        {
            float distortion = signalDistortionBase;
            foreach (var cam in _cameras)
            {
                if (cam == null)
                    continue;

                distortion += cam.LocalDistortionContribution;
            }

            SignalDistortion = Mathf.Clamp01(distortion);
        }

        private void ApplyFeedSettings()
        {
            for (int i = 0; i < _cameras.Count; i++)
            {
                var cam = _cameras[i];
                if (cam == null)
                    continue;

                bool activeFeed = i == _activeIndex;
                cam.SetFeedActive(activeFeed, _nightVisionEnabled, SignalDistortion);
            }
        }
    }
}
