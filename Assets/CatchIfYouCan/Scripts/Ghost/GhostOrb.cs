using UnityEngine;
using CatchIfYouCan.Evidence;

namespace CatchIfYouCan.Ghost
{
    public class GhostOrb : MonoBehaviour
    {
        [SerializeField] private ParticleSystem orbParticles;
        [SerializeField] private bool cameraOnlyVisibility;
        [SerializeField] private float driftSpeed = 0.4f;
        [SerializeField] private float lifetime = 12f;

        private Camera _targetCamera;
        private float _spawnTime;
        private Renderer[] _renderers;

        private void Awake()
        {
            if (orbParticles == null)
                orbParticles = GetComponentInChildren<ParticleSystem>();

            _renderers = GetComponentsInChildren<Renderer>(true);
            _spawnTime = Time.time;
        }

        private void Start()
        {
            _targetCamera = Camera.main;
            ApplyVisibilityMode();
        }

        private void Update()
        {
            transform.position += Vector3.up * Mathf.Sin(Time.time * 2f) * driftSpeed * Time.deltaTime;

            if (cameraOnlyVisibility)
                FaceCamera();

            if (lifetime > 0f && Time.time - _spawnTime >= lifetime)
                Destroy(gameObject);
        }

        public void Configure(EvidenceType evidenceType, float scale, bool camOnly)
        {
            cameraOnlyVisibility = camOnly;
            transform.localScale = Vector3.one * scale;

            if (orbParticles != null)
            {
                var main = orbParticles.main;
                main.startColor = GetColorForEvidence(evidenceType);
                orbParticles.Play();
            }

            ApplyVisibilityMode();
        }

        private void ApplyVisibilityMode()
        {
            if (!cameraOnlyVisibility)
            {
                SetRenderersEnabled(true);
                return;
            }

            SetRenderersEnabled(_targetCamera != null &&
                                Vector3.Dot(_targetCamera.transform.forward,
                                    (transform.position - _targetCamera.transform.position).normalized) > 0.2f);
        }

        private void FaceCamera()
        {
            if (_targetCamera == null) return;
            transform.LookAt(_targetCamera.transform.position);
        }

        private void SetRenderersEnabled(bool enabled)
        {
            if (_renderers == null) return;
            for (int i = 0; i < _renderers.Length; i++)
                _renderers[i].enabled = enabled;
        }

        private static Color GetColorForEvidence(EvidenceType type)
        {
            switch (type)
            {
                case EvidenceType.GhostOrb: return new Color(0.4f, 0.85f, 1f, 0.8f);
                case EvidenceType.EMFSurge: return new Color(0.2f, 1f, 0.3f, 0.7f);
                case EvidenceType.SpectralGrid: return new Color(1f, 0.3f, 0.9f, 0.75f);
                default: return new Color(0.7f, 0.9f, 1f, 0.6f);
            }
        }
    }
}
