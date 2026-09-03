using System.Collections.Generic;
using CatchIfYouCan.Core;
using CatchIfYouCan.Evidence;
using CatchIfYouCan.Ghost;
using UnityEngine;

namespace CatchIfYouCan.Equipment
{
    /// <summary>
    /// The photo camera: point it at something and press the button once.
    ///
    /// <para>
    /// Its camera had never existed. <c>viewCamera</c> was a serialized field with a
    /// <c>GetComponentInChildren</c> fallback, and nothing anywhere built one - so every path
    /// through this class returned early and the item did nothing at all. It builds its own
    /// now, for the same reason the torch builds its beam.
    /// </para>
    ///
    /// <para>
    /// GhostOrb is deliberately not wired to this. It is an unused EvidenceType and attaching
    /// it here would be filling in a matrix rather than designing an item.
    /// </para>
    /// </summary>
    [AddComponentMenu("Catch If You Can/Photo Camera")]
    public class PhotoCameraEquipment : HeldEquipmentBase
    {
        [Header("Lens")]
        [SerializeField, Range(10f, 90f)] private float minZoom = 40f;
        [SerializeField, Range(5f, 60f)] private float maxZoom = 15f;
        [SerializeField, Min(0.1f)] private float zoomStep = 5f;
        [SerializeField, Min(1f)] private float subjectRange = 25f;
        [SerializeField] private LayerMask subjectMask = ~0;
        [SerializeField] private LayerMask occluderMask = ~0;

        [Header("Capture")]
        [SerializeField, Min(64)] private int renderWidth = 512;
        [SerializeField, Min(64)] private int renderHeight = 512;

        [Tooltip("Seconds between shots. A touch button can report twice from one press, and " +
                 "without this that is two photographs and two durability points.")]
        [SerializeField, Min(0f)] private float shutterCooldown = 0.6f;

        [Tooltip("How far off centre a subject can be and still count as framed, as a fraction " +
                 "of the frame from the middle. Beyond this it is in shot but not the shot.")]
        [SerializeField, Range(0.05f, 0.75f)] private float framingRadius = 0.4f;

        [Header("Night vision")]
        [SerializeField, Min(0f)] private float nightVisionRange = 8f;
        [SerializeField, Min(0f)] private float nightVisionIntensity = 1.6f;
        [SerializeField] private Color nightVisionColor = new Color(0.55f, 0.85f, 0.6f);

        private Camera _lens;
        private Light _nightVision;
        private Transform _photoOrigin;
        private float _currentZoom;
        private float _shutterTimer;
        private bool _nightVisionOn;

        // One capture target for every camera in the game, made on demand and never remade.
        // A fresh RenderTexture per shot is a graphics allocation per shutter press.
        private static RenderTexture _sharedTarget;
        private static readonly RaycastHit[] HitBuffer = new RaycastHit[16];
        private static readonly List<Transform> Candidates = new List<Transform>(16);

        /// <summary>
        /// Where the lens is. Systems that want to know what the camera can see ask this
        /// rather than guessing from the item's root.
        /// </summary>
        public Transform PhotoOrigin => _photoOrigin != null ? _photoOrigin : transform;

        /// <summary>Current field of view. Lower is more zoomed in.</summary>
        public float Zoom => _currentZoom;

        public bool NightVisionOn => _nightVisionOn;

        /// <summary>Seconds until the shutter is ready again.</summary>
        public float ShutterCooldown => Mathf.Max(0f, _shutterTimer);

        /// <summary>Field of view, and whether the lamp is on.</summary>
        public override string HudReadout =>
            Mathf.RoundToInt(_currentZoom) + "\u00B0" + (_nightVisionOn ? " NV" : "");

        /// <summary>
        /// The lens controls. All three were public methods with nothing on screen to call
        /// them: on a phone there is no scroll wheel and there was no button either, so the
        /// camera could only ever be fired at whatever it happened to be pointed at.
        /// </summary>
        public override void CollectActions(System.Collections.Generic.List<EquipmentAction> into)
        {
            into.Add(new EquipmentAction("ZOOM +", ZoomIn, _currentZoom > maxZoom));
            into.Add(new EquipmentAction("ZOOM -", ZoomOut, _currentZoom < minZoom));
            into.Add(new EquipmentAction(_nightVisionOn ? "NV OFF" : "NV ON", ToggleNightVision));
        }

        protected override float GetInterferenceMultiplier() => 0.3f;

        /// <summary>Taking a photograph does wear the camera, unlike flicking a switch.</summary>
        protected override float DurabilityLossPerUse => durabilityLossPerUse;

        /// <summary>
        /// The lens and the night-vision lamp. A mesh cannot be either, so they are built here
        /// and everything else comes from the visual profile.
        /// </summary>
        protected override void BuildCarried()
        {
            if (CarriedRoot != null)
                return;

            base.BuildCarried();

            var origin = new GameObject("PhotoOrigin");
            _photoOrigin = origin.transform;
            _photoOrigin.SetParent(CarriedRoot, false);
            _photoOrigin.localPosition = new Vector3(0f, CarriedLength, 0f);
            // The carried transform's +Y is its length, so the lens is turned to look along it.
            _photoOrigin.localRotation = Quaternion.Euler(90f, 0f, 0f);

            _lens = origin.AddComponent<Camera>();
            _lens.fieldOfView = minZoom;
            _lens.nearClipPlane = 0.05f;
            _lens.farClipPlane = Mathf.Max(subjectRange * 1.5f, 30f);
            // Off until the shutter fires. A second camera rendering every frame on a mobile
            // forward+ renderer is the most expensive thing this item could possibly do.
            _lens.enabled = false;

            _currentZoom = minZoom;

            var lampGo = new GameObject("NightVision");
            lampGo.transform.SetParent(_photoOrigin, false);

            _nightVision = lampGo.AddComponent<Light>();
            _nightVision.type = LightType.Spot;
            _nightVision.range = nightVisionRange;
            _nightVision.spotAngle = minZoom;
            _nightVision.color = nightVisionColor;
            _nightVision.intensity = nightVisionIntensity;
            _nightVision.shadows = LightShadows.None;
            _nightVision.enabled = false;
        }

        /// <summary>Steps the zoom in. Called by the HUD; there is no scroll wheel on a phone.</summary>
        public void ZoomIn() => SetZoom(_currentZoom - zoomStep);

        public void ZoomOut() => SetZoom(_currentZoom + zoomStep);

        public void ToggleNightVision() => SetNightVision(!_nightVisionOn);

        private void SetZoom(float fieldOfView)
        {
            _currentZoom = Mathf.Clamp(fieldOfView, maxZoom, minZoom);

            if (_lens != null)
                _lens.fieldOfView = _currentZoom;
            if (_nightVision != null)
                _nightVision.spotAngle = _currentZoom;
        }

        private void SetNightVision(bool on)
        {
            _nightVisionOn = on;
            ApplyNightVision();
        }

        private void ApplyNightVision()
        {
            bool burning = _nightVisionOn && LifecycleState == EquipmentLifecycleState.Equipped;

            if (_nightVision != null)
                _nightVision.enabled = burning;

            SetDeviceActive(burning);
        }

        protected override void OnLifecycleStateChanged(EquipmentLifecycleState from,
                                                        EquipmentLifecycleState to)
        {
            ApplyNightVision();
        }

        protected override void TickEquipped(float deltaTime)
        {
            if (_shutterTimer > 0f)
                _shutterTimer -= deltaTime;
        }

        protected override void OnUse()
        {
            // One photograph per deliberate press. A touch button can report twice from one
            // press, and without this that is two photographs and two durability points.
            if (_shutterTimer > 0f || _lens == null)
                return;

            _shutterTimer = shutterCooldown;
            Capture();
        }

        private void Capture()
        {
            var subject = FindFramedSubject(out float distance, out float visibility,
                                            out float centering, out bool eventCaptured);
            int stars = ScorePhoto(distance, visibility, centering, eventCaptured);

            var photo = new PhotoResult
            {
                Stars = stars,
                DistanceToSubject = distance,
                VisibilityScore = visibility,
                CenteringScore = centering,
                CapturedEvent = eventCaptured,
                CapturePosition = PhotoOrigin.position,
                SubjectPosition = subject != null
                    ? subject.position
                    : PhotoOrigin.position + PhotoOrigin.forward * 3f,
                Caption = BuildCaption(stars, subject),
                Thumbnail = RenderThumbnail()
            };

            if (ServiceLocator.TryGet<EvidenceManager>(out var manager))
                manager.AddPhoto(photo);
        }

        /// <summary>
        /// What is actually in the frame, rather than what a single ray down the middle hit.
        ///
        /// <para>
        /// The old version cast one ray straight ahead, so a ghost filling half the viewfinder
        /// but not dead centre was not in the photograph at all. Candidates now come from the
        /// registries - the ghost, the EMF sources, the revealed traces - and are kept if they
        /// project inside the frame and nothing solid is between.
        /// </para>
        /// </summary>
        private Transform FindFramedSubject(out float distance, out float visibility,
                                            out float centering, out bool eventCaptured)
        {
            distance = subjectRange;
            visibility = 0f;
            centering = 0f;
            eventCaptured = false;

            Transform best = null;
            float bestScore = float.MinValue;
            Vector3 origin = PhotoOrigin.position;

            GatherCandidates();

            for (int i = 0; i < Candidates.Count; i++)
            {
                var candidate = Candidates[i];
                if (candidate == null)
                    continue;

                Vector3 viewport = _lens.WorldToViewportPoint(candidate.position);
                if (viewport.z <= 0f || viewport.x < 0f || viewport.x > 1f ||
                    viewport.y < 0f || viewport.y > 1f)
                    continue;

                float offCentre = Vector2.Distance(new Vector2(viewport.x, viewport.y),
                                                   new Vector2(0.5f, 0.5f));
                if (offCentre > framingRadius)
                    continue;

                float d = Vector3.Distance(origin, candidate.position);
                if (d > subjectRange)
                    continue;

                // Something in the way is something in the photograph instead.
                if (Physics.Linecast(origin, candidate.position, occluderMask.value))
                    continue;

                float vis = 1f - Mathf.Clamp01(d / subjectRange);
                float centred = 1f - offCentre / framingRadius;
                bool evt = candidate.GetComponentInParent<EMFSpot>() != null
                           || candidate.GetComponentInParent<EvidenceReveal>() != null
                           || candidate.GetComponentInParent<GhostController>() != null;

                float score = vis * 0.45f + centred * 0.45f + (evt ? 0.25f : 0f);
                if (score <= bestScore)
                    continue;

                bestScore = score;
                best = candidate;
                distance = d;
                visibility = vis;
                centering = centred;
                eventCaptured = evt;
            }

            return best;
        }

        /// <summary>
        /// Everything worth photographing, from the registries plus whatever the centre ray
        /// happens to hit - so a photograph of a piece of furniture is still a photograph.
        /// </summary>
        private void GatherCandidates()
        {
            Candidates.Clear();

            var ghost = GhostController.Active;
            if (ghost != null)
                Candidates.Add(ghost.transform);

            int hits = Physics.RaycastNonAlloc(
                new Ray(PhotoOrigin.position, PhotoOrigin.forward), HitBuffer,
                subjectRange, subjectMask.value, QueryTriggerInteraction.Collide);

            for (int i = 0; i < hits; i++)
            {
                var hit = HitBuffer[i];
                if (hit.transform != null && !Candidates.Contains(hit.transform))
                    Candidates.Add(hit.transform);
            }
        }

        private static int ScorePhoto(float distance, float visibility, float centering,
                                      bool eventCaptured)
        {
            float score = visibility * 0.4f + centering * 0.4f;
            if (distance <= 6f) score += 0.15f;
            if (eventCaptured) score += 0.2f;

            if (score >= 0.75f) return 3;
            if (score >= 0.45f) return 2;
            if (score >= 0.2f) return 1;
            return 0;
        }

        /// <summary>
        /// One frame, into a shared target.
        ///
        /// <para>
        /// A RenderTexture was created and destroyed on every shutter press. The Texture2D is
        /// still made per photo because the photo keeps it - that is the picture - but the
        /// render target is graphics memory and there is no reason to have more than one.
        /// </para>
        /// </summary>
        private Texture2D RenderThumbnail()
        {
            if (_sharedTarget == null || _sharedTarget.width != renderWidth ||
                _sharedTarget.height != renderHeight)
            {
                if (_sharedTarget != null)
                    _sharedTarget.Release();

                _sharedTarget = new RenderTexture(renderWidth, renderHeight, 24)
                {
                    name = "CIYC_PhotoCapture"
                };
            }

            var previousActive = RenderTexture.active;

            _lens.targetTexture = _sharedTarget;
            _lens.enabled = true;
            _lens.Render();
            _lens.enabled = false;

            RenderTexture.active = _sharedTarget;
            var texture = new Texture2D(renderWidth, renderHeight, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, renderWidth, renderHeight), 0, 0);
            texture.Apply();

            _lens.targetTexture = null;
            RenderTexture.active = previousActive;
            return texture;
        }

        private static string BuildCaption(int stars, Transform subject)
        {
            string target = subject != null ? subject.name : "Unknown";
            return target + " - " + stars + " star capture";
        }
    }
}
