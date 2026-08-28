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

        private void Start()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;
        }

        private void Update()
        {
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

            _bobPhase += Time.deltaTime * (1f + NormalizedFear * 2f);
            float bob = Mathf.Sin(_bobPhase * 2f) * maxBreathingBob * NormalizedFear;
            targetCamera.transform.localPosition = new Vector3(0f, bob, 0f);
        }

        private void HandleHuntStarted() => _huntActive = true;
        private void HandleHuntEnded() => _huntActive = false;
    }
}
