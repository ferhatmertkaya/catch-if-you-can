using CatchIfYouCan.Electronics;
using CatchIfYouCan.Evidence;
using CatchIfYouCan.Ghost;
using UnityEngine;

namespace CatchIfYouCan.Equipment
{
    /// <summary>
    /// The video camera: install it in a room, walk away, and watch what happens there.
    ///
    /// <para>
    /// Nothing about it worked. There was no case for <c>video_camera</c> in the runtime
    /// factory, so the id fell through to the unknown-id branch and the item a player would
    /// have been handed was a DEV_PLACEHOLDER box. It derived from <see cref="EquipmentBase"/>,
    /// so it could not be carried. Its <c>feedCamera</c> and <c>nightVisionLight</c> were
    /// serialized fields with <c>GetComponentInChildren</c> fallbacks and nothing anywhere
    /// built either, so every path that used them did nothing. And when the network did enable
    /// that camera it had no target texture, which does not show a feed - it renders the room
    /// over the top of the player's view.
    /// </para>
    ///
    /// <para>
    /// It builds its own lens and lamp now, places through the shared placement system as the
    /// same object, and renders only when a monitor is actually being watched - see
    /// <see cref="CameraNetworkManager"/>. Four cameras in a house are not four render passes.
    /// </para>
    ///
    /// <para>
    /// This is the item that makes ghost orbs worth anything. <see cref="GhostOrb"/> has always
    /// carried a <c>cameraOnlyVisibility</c> flag and the evidence manager has always spawned
    /// orbs with it set - the phenomenon was designed to be seen down a feed and there was no
    /// feed to see it down. Wiring the two together is finishing an existing design, not
    /// filling in a matrix.
    /// </para>
    /// </summary>
    [AddComponentMenu("Catch If You Can/Video Camera")]
    public class VideoCameraEquipment : PlaceableEquipmentBase
    {
        [Header("Lens")]
        [Tooltip("Field of view of the feed, in degrees. Wide: this is a security camera "
                 + "covering a room, not a viewfinder.")]
        [SerializeField, Range(30f, 110f)] private float fieldOfView = 75f;

        [Tooltip("How far down the room the feed sees, in metres.")]
        [SerializeField, Min(2f)] private float viewRange = 14f;

        [Header("Night vision")]
        [SerializeField, Min(0f)] private float nightVisionRange = 10f;
        [SerializeField, Min(0f)] private float nightVisionIntensity = 1.2f;
        [SerializeField] private Color nightVisionColor = new Color(0.55f, 0.85f, 0.6f);

        [Header("Signal")]
        [Tooltip("How far away other running electronics still spoil the picture, in metres.")]
        [SerializeField, Min(0f)] private float interferenceRange = 8f;

        [Tooltip("Most of the picture one camera can spoil, 0 to 1. A ceiling, so a house full "
                 + "of running devices does not add up to a blank screen.")]
        [SerializeField, Range(0f, 1f)] private float maxDistortionContribution = 0.35f;

        [Header("Evidence")]
        [Tooltip("What can hide an orb from the lens.")]
        [SerializeField] private LayerMask occluderMask = ~0;

        [Tooltip("Seconds between looking for orbs in frame. The feed itself renders more "
                 + "often than this; finding one is not a per-frame question.")]
        [SerializeField, Min(0.05f)] private float scanInterval = 0.25f;

        private Camera _lens;
        private Light _nightVision;
        private Transform _feedOrigin;
        private float _scanTimer;
        private bool _feedSelected;
        private bool _nightVisionOn;

        /// <summary>How much of the picture this camera is spoiling, 0 to 1.</summary>
        public float LocalDistortionContribution { get; private set; }

        /// <summary>The lens, for the network to render. Null until the item has been built.</summary>
        public Camera Lens => _lens;

        /// <summary>Where the camera looks from.</summary>
        public Transform FeedOrigin => _feedOrigin != null ? _feedOrigin : transform;

        /// <summary>Whether this is the camera the monitor is currently showing.</summary>
        public bool IsSelectedFeed => _feedSelected;

        protected override float GetInterferenceMultiplier() => 0.5f;

        /// <summary>Installing a camera does not wear it out.</summary>
        protected override float DurabilityLossPerUse => 0f;

        /// <summary>
        /// The lens and the night-vision lamp. A mesh can be neither, so they are built here -
        /// the same reason the torch builds its beam and the photo camera builds its lens.
        /// </summary>
        protected override void BuildCarried()
        {
            if (CarriedRoot != null)
                return;

            base.BuildCarried();

            var origin = new GameObject("FeedOrigin");
            _feedOrigin = origin.transform;
            _feedOrigin.SetParent(CarriedRoot, false);
            _feedOrigin.localPosition = new Vector3(0f, CarriedLength, 0f);
            // The carried transform's +Y is its length by the shared convention, so the lens is
            // turned to look along it. A wall placement turns the whole item to put that axis
            // along the wall's normal, which is what aims an installed camera into the room.
            _feedOrigin.localRotation = Quaternion.Euler(90f, 0f, 0f);

            _lens = origin.AddComponent<Camera>();
            _lens.fieldOfView = fieldOfView;
            _lens.nearClipPlane = 0.05f;
            _lens.farClipPlane = viewRange;
            // Off, and it stays off. The network renders it by hand, one frame at a time, only
            // while a monitor is open. An enabled second camera on a mobile forward+ renderer
            // is the most expensive thing a placed item could possibly do, and there can be
            // four of these in a house.
            _lens.enabled = false;

            var lampGo = new GameObject("NightVision");
            lampGo.transform.SetParent(_feedOrigin, false);

            _nightVision = lampGo.AddComponent<Light>();
            _nightVision.type = LightType.Spot;
            _nightVision.range = nightVisionRange;
            _nightVision.spotAngle = fieldOfView;
            _nightVision.color = nightVisionColor;
            _nightVision.intensity = nightVisionIntensity;
            _nightVision.shadows = LightShadows.None;
            _nightVision.enabled = false;
        }

        /// <summary>Switching it on in the hand does nothing but arm it; a camera works placed.</summary>
        protected override void OnUse() => SetDeviceActive(!DeviceActive);

        protected override void OnLifecycleStateChanged(EquipmentLifecycleState from,
                                                        EquipmentLifecycleState to)
        {
            if (to != EquipmentLifecycleState.Placed)
            {
                // Off the wall it is not a camera in the network, and it is not lighting a room
                // it is no longer pointed at.
                UnregisterFromNetwork();
                _feedSelected = false;
                ApplyNightVision();
            }
        }

        protected override void OnPlacedInWorld(in PlacementResult result)
        {
            SetDeviceActive(true);
            RegisterWithNetwork();
        }

        protected override void OnPickedUpFromPlacement()
        {
            base.OnPickedUpFromPlacement();
            UnregisterFromNetwork();
        }

        protected override void OnDestroy()
        {
            UnregisterFromNetwork();
            base.OnDestroy();
        }

        protected override void TickEquipped(float deltaTime)
        {
            // The placement preview, when one is up.
            base.TickEquipped(deltaTime);

            if (!IsPlaced || !IsActive)
            {
                LocalDistortionContribution = 0f;
                return;
            }

            // What actually spoils a picture is other electronics running near it, which is a
            // question the registry answers without a scene sweep. It used to be the distance
            // from the camera to the player's hand anchor, which is not a property of anything.
            float interference = ElectronicDeviceRegistry.InterferenceAt(
                FeedOrigin.position, interferenceRange, this);

            LocalDistortionContribution =
                Mathf.Min(maxDistortionContribution, interference * 0.5f);

            _scanTimer -= deltaTime;
            if (_scanTimer > 0f)
                return;

            _scanTimer = scanInterval;
            ScanForOrbs();
        }

        /// <summary>
        /// Looks for an orb in frame, and reports it if there is one.
        ///
        /// <para>
        /// Only while this is the selected feed: an orb nobody is watching has not been seen,
        /// and a house of cameras quietly proving evidence at each other is the shape of every
        /// back-door this migration has closed. It also has to be a real orb, really in the
        /// frustum, with nothing between - and then the validator still decides, against the
        /// ghost's own profile.
        /// </para>
        /// </summary>
        private void ScanForOrbs()
        {
            if (!_feedSelected || _lens == null)
                return;

            var orbs = GhostOrb.All;
            Vector3 origin = FeedOrigin.position;

            for (int i = 0; i < orbs.Count; i++)
            {
                var orb = orbs[i];
                if (orb == null || !orb.CameraOnly)
                    continue;

                Vector3 point = orb.transform.position;

                Vector3 viewport = _lens.WorldToViewportPoint(point);
                if (viewport.z <= 0f || viewport.x < 0f || viewport.x > 1f ||
                    viewport.y < 0f || viewport.y > 1f)
                    continue;

                float distance = Vector3.Distance(origin, point);
                if (distance > viewRange)
                    continue;

                if (Physics.Linecast(origin, point, occluderMask.value))
                    continue;

                // Nearer is a clearer sighting, and a picture full of interference is a worse
                // one - so the same distortion the player is looking at is the strength this
                // reports.
                float clarity = (1f - distance / viewRange) *
                                (1f - Mathf.Clamp01(LocalDistortionContribution));

                Observe(EvidenceType.GhostOrb, clarity);
                return;
            }
        }

        /// <summary>
        /// Called by the network. Says whether this is the camera being watched and whether
        /// night vision is on; it does not render anything, because the network renders.
        /// </summary>
        public void SetFeedActive(bool selected, bool nightVision)
        {
            _feedSelected = selected;
            _nightVisionOn = nightVision;
            ApplyNightVision();
        }

        private void ApplyNightVision()
        {
            if (_nightVision == null)
                return;

            // The lamp burns only for the feed being watched. A dark house with four cameras
            // in it should be dark.
            _nightVision.enabled = _feedSelected && _nightVisionOn && IsPlaced && IsActive;
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
    }
}
