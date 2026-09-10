using System.Collections.Generic;
using CatchIfYouCan.Core;
using CatchIfYouCan.Ghost;
using UnityEngine;

namespace CatchIfYouCan.Equipment
{
    /// <summary>
    /// The monitor the placed video cameras feed into: which one is showing, how bad the
    /// picture is, and the one render that produces it.
    ///
    /// <para>
    /// It used to poll <c>Input.GetKeyDown</c> for three keyboard keys on a project whose
    /// primary target is a phone, and to "show" a feed by enabling a Camera that had no target
    /// texture - which does not display anything, it draws the room over the top of the
    /// player's view. It also drove every registered camera every frame whether or not anybody
    /// had the monitor open.
    /// </para>
    ///
    /// <para>
    /// Now: no input polling - the monitor UI calls the same public methods it always called,
    /// and there is nowhere else the selection can come from. One render target for the whole
    /// game, owned here, handed out to whichever camera is selected, and released the moment
    /// the last camera leaves. And nothing renders at all unless a monitor says it is watching,
    /// so four cameras in a house cost four transforms.
    /// </para>
    /// </summary>
    public class CameraNetworkManager : Utilities.SingletonBehaviour<CameraNetworkManager>
    {
        [Header("Signal")]
        [Tooltip("Distortion every feed has before any interference is added, 0 to 1.")]
        [SerializeField, Range(0f, 1f)] private float signalDistortionBase = 0.15f;

        [Header("Feed")]
        [SerializeField, Min(64)] private int feedWidth = 512;
        [SerializeField, Min(64)] private int feedHeight = 288;

        [Tooltip("Feed frames per second. A security monitor is not sixty hertz, and each " +
                 "frame is a full render of the room on a mobile GPU.")]
        [SerializeField, Range(1f, 30f)] private float feedFrameRate = 12f;

        private readonly List<VideoCameraEquipment> _cameras = new List<VideoCameraEquipment>();

        private int _activeIndex = -1;
        private bool _nightVisionEnabled;
        private int _watchers;
        private float _frameTimer;
        private bool _settingsDirty = true;
        private RenderTexture _feed;

        public VideoCameraEquipment ActiveCamera =>
            _activeIndex >= 0 && _activeIndex < _cameras.Count ? _cameras[_activeIndex] : null;

        public bool NightVisionEnabled => _nightVisionEnabled;
        public float SignalDistortion { get; private set; }

        /// <summary>How many cameras are installed, for the monitor's readout.</summary>
        public int CameraCount => _cameras.Count;

        /// <summary>
        /// The picture. Null until a monitor is open and a camera is installed - a monitor
        /// showing nothing should show nothing rather than a stale last frame.
        /// </summary>
        public RenderTexture Feed => _watchers > 0 ? _feed : null;

        protected override void Awake()
        {
            base.Awake();
            ServiceLocator.Register(this);
        }

        protected override void OnDestroy()
        {
            if (Instance == this)
                ServiceLocator.Unregister<CameraNetworkManager>();

            ReleaseFeed();
            base.OnDestroy();
        }

        /// <summary>
        /// A monitor opening. Counted rather than set, because the equipment lab and the game's
        /// own monitor can both be up, and the first one to close should not switch off the
        /// picture the other is looking at.
        /// </summary>
        public void AddWatcher()
        {
            _watchers++;
            _settingsDirty = true;
        }

        public void RemoveWatcher()
        {
            _watchers = Mathf.Max(0, _watchers - 1);
            if (_watchers == 0)
                _settingsDirty = true;
        }

        private void Update()
        {
            if (_cameras.Count == 0)
                return;

            UpdateSignalDistortion();

            // Only when something changed. Every camera in the house was being told its feed
            // state every frame, to tell it the same thing it was told last frame; the three
            // things that can change it - the selection, night vision, and whether anyone is
            // watching - all push it themselves.
            if (_settingsDirty)
            {
                _settingsDirty = false;
                ApplyFeedSettings();
            }

            if (_watchers <= 0)
                return;

            _frameTimer -= Time.deltaTime;
            if (_frameTimer > 0f)
                return;

            _frameTimer = 1f / Mathf.Max(1f, feedFrameRate);
            RenderActiveFeed();
        }

        public void RegisterCamera(VideoCameraEquipment camera)
        {
            if (camera == null || _cameras.Contains(camera))
                return;

            _cameras.Add(camera);
            if (_activeIndex < 0)
                _activeIndex = 0;
            _settingsDirty = true;
        }

        public void UnregisterCamera(VideoCameraEquipment camera)
        {
            if (camera == null)
                return;

            int index = _cameras.IndexOf(camera);
            if (index < 0)
                return;

            // Whatever it was pointed at, it is not pointed at it any more.
            if (camera.Lens != null)
                camera.Lens.targetTexture = null;
            camera.SetFeedActive(false, false);

            _cameras.RemoveAt(index);
            if (_cameras.Count == 0)
            {
                _activeIndex = -1;
                // Nothing left to show. Holding half a megabyte of graphics memory for a
                // network with no cameras in it is the leak this is here to not have.
                ReleaseFeed();
            }
            else if (_activeIndex >= _cameras.Count)
            {
                _activeIndex = _cameras.Count - 1;
            }

            _settingsDirty = true;
        }

        public void SelectNext()
        {
            if (_cameras.Count == 0)
                return;

            SelectIndex((_activeIndex + 1) % _cameras.Count);
        }

        public void SelectPrevious()
        {
            if (_cameras.Count == 0)
                return;

            SelectIndex((_activeIndex - 1 + _cameras.Count) % _cameras.Count);
        }

        public void ToggleNightVision()
        {
            _nightVisionEnabled = !_nightVisionEnabled;
            _settingsDirty = true;
        }

        private void SelectIndex(int index)
        {
            if (index == _activeIndex)
                return;

            // The camera being switched away from gives the target back before the next one
            // takes it. Two cameras holding one RenderTexture is one of them rendering into
            // nothing and neither of them knowing which.
            var previous = ActiveCamera;
            if (previous != null && previous.Lens != null)
                previous.Lens.targetTexture = null;

            _activeIndex = index;
            _settingsDirty = true;
        }

        private void UpdateSignalDistortion()
        {
            float distortion = signalDistortionBase;
            for (int i = 0; i < _cameras.Count; i++)
            {
                var cam = _cameras[i];
                if (cam != null)
                    distortion += cam.LocalDistortionContribution;
            }

            SignalDistortion = Mathf.Clamp01(distortion);
        }

        private void ApplyFeedSettings()
        {
            bool watching = _watchers > 0;

            for (int i = 0; i < _cameras.Count; i++)
            {
                var cam = _cameras[i];
                if (cam == null)
                    continue;

                cam.SetFeedActive(watching && i == _activeIndex, _nightVisionEnabled);
            }
        }

        /// <summary>
        /// One frame of the selected camera, into the one shared target.
        ///
        /// <para>
        /// Rendered by hand rather than by leaving the Camera enabled, because that is the only
        /// way to bracket it: the camera-only ghost orbs are switched on immediately before the
        /// render and off immediately after, so they exist in the picture and nowhere else. It
        /// is also the only way to render at twelve hertz instead of at the frame rate.
        /// </para>
        /// </summary>
        private void RenderActiveFeed()
        {
            var cam = ActiveCamera;
            if (cam == null || cam.Lens == null)
                return;

            EnsureFeed();
            if (_feed == null)
                return;

            var lens = cam.Lens;
            lens.targetTexture = _feed;

            GhostOrb.SetVisibleTo(lens, true);
            lens.Render();
            GhostOrb.SetVisibleTo(lens, false);
        }

        private void EnsureFeed()
        {
            if (_feed != null && _feed.width == feedWidth && _feed.height == feedHeight)
                return;

            ReleaseFeed();

            _feed = new RenderTexture(feedWidth, feedHeight, 24)
            {
                name = "CIYC_CameraFeed"
            };
        }

        private void ReleaseFeed()
        {
            if (_feed == null)
                return;

            // Both, and in this order. Release frees the GPU surface; the object itself is
            // still a managed object holding a native one until it is destroyed.
            _feed.Release();
            Destroy(_feed);
            _feed = null;
        }
    }
}
