using UnityEngine;

namespace CatchIfYouCan.Evidence
{
    public class EvidenceReveal : MonoBehaviour
    {
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private Material hiddenMaterial;
        [SerializeField] private Material revealedMaterial;
        [SerializeField] private float revealLifetime = 30f;
        [SerializeField] private bool hideUntilRevealed = true;

        private float _revealTimer;
        private bool _revealed;

        public bool IsRevealed => _revealed;

        private void Awake()
        {
            if (targetRenderer == null)
                targetRenderer = GetComponentInChildren<Renderer>();

            if (hideUntilRevealed && targetRenderer != null && hiddenMaterial != null)
                targetRenderer.sharedMaterial = hiddenMaterial;
        }

        private void Update()
        {
            if (!_revealed || revealLifetime <= 0f)
                return;

            _revealTimer -= Time.deltaTime;
            if (_revealTimer <= 0f)
                Hide();
        }

        public void Reveal()
        {
            if (_revealed || targetRenderer == null || revealedMaterial == null)
                return;

            _revealed = true;
            _revealTimer = revealLifetime;
            targetRenderer.sharedMaterial = revealedMaterial;
        }

        public void Hide()
        {
            _revealed = false;
            if (targetRenderer != null && hiddenMaterial != null)
                targetRenderer.sharedMaterial = hiddenMaterial;
        }
    }
}
