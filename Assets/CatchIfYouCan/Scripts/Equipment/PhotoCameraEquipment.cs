using UnityEngine;
using CatchIfYouCan.Core;
using CatchIfYouCan.Evidence;

namespace CatchIfYouCan.Equipment
{
    public class PhotoCameraEquipment : EquipmentBase
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Light nightVisionLight;
        [SerializeField] private float minZoom = 40f;
        [SerializeField] private float maxZoom = 15f;
        [SerializeField] private float zoomSpeed = 8f;
        [SerializeField] private LayerMask subjectMask = ~0;
        [SerializeField] private int renderWidth = 512;
        [SerializeField] private int renderHeight = 512;

        private float _currentZoom;
        private bool _nightVisionOn;

        protected override void Awake()
        {
            base.Awake();
            if (viewCamera == null)
                viewCamera = GetComponentInChildren<Camera>();

            if (viewCamera != null)
                _currentZoom = viewCamera.fieldOfView;
        }

        protected override void OnEquipped()
        {
            SetDeviceActive(true);
        }

        protected override void OnUse()
        {
            CapturePhoto();
        }

        protected override void TickEquipped(float deltaTime)
        {
            if (!IsEquipped || viewCamera == null)
                return;

            float scroll = UnityEngine.Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.001f)
            {
                _currentZoom = Mathf.Clamp(_currentZoom - scroll * zoomSpeed, maxZoom, minZoom);
                viewCamera.fieldOfView = _currentZoom;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.N))
                SetNightVision(!_nightVisionOn);
        }

        private void SetNightVision(bool enabled)
        {
            _nightVisionOn = enabled;
            if (nightVisionLight != null)
                nightVisionLight.enabled = enabled;
        }

        private void CapturePhoto()
        {
            if (viewCamera == null || HandAnchor == null)
                return;

            var subject = FindBestSubject(out float distance, out float visibility, out float centering, out bool eventCaptured);
            int stars = ScorePhoto(distance, visibility, centering, eventCaptured);

            var photo = new PhotoResult
            {
                Stars = stars,
                DistanceToSubject = distance,
                VisibilityScore = visibility,
                CenteringScore = centering,
                CapturedEvent = eventCaptured,
                CapturePosition = HandAnchor.position,
                SubjectPosition = subject != null ? subject.position : HandAnchor.position + HandAnchor.forward * 3f,
                Caption = BuildCaption(stars, subject),
                Thumbnail = RenderThumbnail()
            };

            if (Core.ServiceLocator.TryGet<EvidenceManager>(out var manager))
                manager.AddPhoto(photo);
        }

        private Transform FindBestSubject(out float distance, out float visibility, out float centering, out bool eventCaptured)
        {
            distance = 10f;
            visibility = 0.2f;
            centering = 0f;
            eventCaptured = false;
            Transform best = null;
            float bestScore = float.MinValue;

            var hits = Physics.RaycastAll(viewCamera.transform.position, viewCamera.transform.forward, 25f, subjectMask);
            foreach (var hit in hits)
            {
                if (hit.transform == null)
                    continue;

                float d = hit.distance;
                float vis = 1f - Mathf.Clamp01(d / 20f);
                var viewport = viewCamera.WorldToViewportPoint(hit.point);
                float center = 1f - Vector2.Distance(new Vector2(viewport.x, viewport.y), new Vector2(0.5f, 0.5f));
                bool evt = hit.collider.GetComponentInParent<EMFSpot>() != null
                           || hit.collider.GetComponentInParent<EvidenceReveal>() != null;

                float score = vis * 0.45f + center * 0.45f + (evt ? 0.25f : 0f);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = hit.transform;
                    distance = d;
                    visibility = vis;
                    centering = center;
                    eventCaptured = evt;
                }
            }

            return best;
        }

        private static int ScorePhoto(float distance, float visibility, float centering, bool eventCaptured)
        {
            float score = visibility * 0.4f + centering * 0.4f;
            if (distance <= 6f) score += 0.15f;
            if (eventCaptured) score += 0.2f;

            if (score >= 0.75f) return 3;
            if (score >= 0.45f) return 2;
            if (score >= 0.2f) return 1;
            return 0;
        }

        private Texture2D RenderThumbnail()
        {
            var rt = new RenderTexture(renderWidth, renderHeight, 24);
            viewCamera.targetTexture = rt;
            viewCamera.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(renderWidth, renderHeight, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, renderWidth, renderHeight), 0, 0);
            tex.Apply();

            viewCamera.targetTexture = null;
            RenderTexture.active = null;
            Destroy(rt);
            return tex;
        }

        private static string BuildCaption(int stars, Transform subject)
        {
            string target = subject != null ? subject.name : "Unknown";
            return $"{target} — {stars} star capture";
        }
    }
}
