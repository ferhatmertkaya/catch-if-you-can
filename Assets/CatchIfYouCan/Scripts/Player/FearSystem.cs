using UnityEngine;
using CatchIfYouCan.Core;

namespace CatchIfYouCan.Player
{
    public class FearSystem : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] private float darknessFearRate = 2.5f;
        [SerializeField] private float ghostNearFearRate = 8f;
        [SerializeField] private float huntFearRate = 18f;
        [SerializeField] private float whisperFearRate = 4f;
        [SerializeField] private float calmRate = 3f;

        [Header("Detection")]
        [SerializeField] private Light playerFlashlight;
        [SerializeField] private float ghostNearDistance = 6f;
        [SerializeField] private LayerMask ghostLayer;
        [SerializeField] private string ghostTag = "Ghost";

        [Header("Effects")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private float maxVignetteStrength = 0.35f;
        [SerializeField] private float maxBreathingBob = 0.015f;
        [SerializeField] private float effectLerpSpeed = 3f;

        private float _fear;
        private bool _huntActive;
        private bool _whisperActive;
        private float _currentVignette;
        private float _bobPhase;

        // The camera this component is bobbing, and the local position it was authored with.
        // Only MainCamera is ever written; CameraRoot, which carries the eye height and forward
        // offset, is never touched by this component.
        private Camera _bobbedCamera;
        private Vector3 _cameraBaseLocalPosition;

        public float Fear => _fear;
        public float NormalizedFear => _fear / 100f;

        private void OnEnable()
        {
            GameEvents.OnHuntStarted += HandleHuntStarted;
            GameEvents.OnHuntEnded += HandleHuntEnded;
        }

        private void OnDisable()
        {
            GameEvents.OnHuntStarted -= HandleHuntStarted;
            GameEvents.OnHuntEnded -= HandleHuntEnded;
        }

        private void Update()
        {
            // Resolved here rather than latched in Start. The camera is built in the same
            // frame as this component, and Start could run before the player was registered;
            // a latch that lost that race stayed null for the rest of the session.
            if (targetCamera == null)
                targetCamera = LocalPlayerService.ResolveViewCamera();

            float delta = 0f;

            if (_huntActive)
                delta += huntFearRate * Time.deltaTime;
            if (_whisperActive)
                delta += whisperFearRate * Time.deltaTime;
            if (IsInDarkness())
                delta += darknessFearRate * Time.deltaTime;
            if (IsGhostNear())
                delta += ghostNearFearRate * Time.deltaTime;

            if (delta <= 0f)
                delta -= calmRate * Time.deltaTime;

            SetFear(_fear + delta);
            ApplySubtleEffects();
        }

        public void SetWhisperActive(bool active) => _whisperActive = active;

        public void AddFear(float amount) => SetFear(_fear + amount);

        public void SetFear(float value)
        {
            float clamped = Mathf.Clamp(value, 0f, 100f);
            if (Mathf.Approximately(clamped, _fear))
                return;

            _fear = clamped;
            GameEvents.FearChanged(_fear);
        }

        /// <summary>
        /// Hands the fear system the light the player is actually carrying, so
        /// <see cref="IsInDarkness"/> can tell whether they are standing in their own beam.
        /// Set by whoever builds the player, because the torch is created at runtime and there
        /// is no serialized reference to give it.
        /// </summary>
        public void SetFlashlight(Light light)
        {
            playerFlashlight = light;
        }

        private bool IsInDarkness()
        {
            if (playerFlashlight != null && playerFlashlight.enabled && playerFlashlight.intensity > 0.05f)
                return false;

            return RenderSettings.ambientIntensity < 0.35f;
        }

        private bool IsGhostNear()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, ghostNearDistance, ghostLayer);
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].CompareTag(ghostTag))
                    return true;
            }

            return false;
        }

        private void ApplySubtleEffects()
        {
            float targetStrength = NormalizedFear * maxVignetteStrength;
            _currentVignette = Mathf.Lerp(_currentVignette, targetStrength, effectLerpSpeed * Time.deltaTime);

            if (targetCamera == null)
                return;

            // Capture the camera's authored local position the first time it is seen, and again
            // if the camera is ever swapped. Without this the write below was an absolute
            // assignment to (0, bob, 0), which silently discarded any offset the camera was built
            // or tuned with - it only looked harmless because the authored value happened to be
            // zero and fear starts at zero.
            if (_bobbedCamera != targetCamera)
            {
                _bobbedCamera = targetCamera;
                _cameraBaseLocalPosition = targetCamera.transform.localPosition;
            }

            _bobPhase += Time.deltaTime * (1f + NormalizedFear * 2f);
            float bob = Mathf.Sin(_bobPhase * 2f) * maxBreathingBob * NormalizedFear;

            // Baseline plus offset, never a read-modify-write of the current value, so the bob
            // cannot accumulate: at fear zero this restores the authored position exactly.
            targetCamera.transform.localPosition =
                _cameraBaseLocalPosition + new Vector3(0f, bob, 0f);
        }

        private void HandleHuntStarted() => _huntActive = true;
        private void HandleHuntEnded() => _huntActive = false;
    }
}
