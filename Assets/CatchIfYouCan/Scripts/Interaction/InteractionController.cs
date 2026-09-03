using System;
using UnityEngine;
using CatchIfYouCan.Core;
using CatchIfYouCan.Input;

namespace CatchIfYouCan.Interaction
{
    public class InteractionController : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField] private float interactDistance = 2.75f;
        [SerializeField] private LayerMask interactMask = ~0;
        [Header("Outline (Optional)")]
        [SerializeField] private bool useOutline;
        [SerializeField] private Renderer[] outlineRenderers;

        public IInteractable CurrentTarget { get; private set; }
        public float HoldProgress { get; private set; }

        public event Action<IInteractable> OnTargetChanged;
        public event Action<string, InteractionType, float> OnPromptChanged;

        private MobileInputController _input;
        private IInteractable _heldTarget;
        private float _holdTimer;

        private void Update()
        {
            // Both of these used to be latched once, the camera in Awake and the input in
            // Start, and both can legitimately not exist yet at that moment: the factory
            // assigns the camera immediately after AddComponent returns, and the input
            // singleton is created alongside the HUD. Losing either race left interaction
            // dead for the whole session with nothing on screen to say why. Resolved per
            // frame until they answer, then cached.
            if (viewCamera == null)
                viewCamera = LocalPlayerService.ResolveViewCamera();

            if (_input == null)
                _input = MobileInputController.Instance;

            if (_input == null || viewCamera == null)
                return;

            ScanForTarget();
            ProcessHoldInteraction();
        }

        private void ScanForTarget()
        {
            Ray ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
            IInteractable found = null;

            if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactMask, QueryTriggerInteraction.Collide))
                found = hit.collider.GetComponentInParent<IInteractable>();

            if (found != null && !found.CanInteract(gameObject))
                found = null;

            if (found != CurrentTarget)
            {
                SetOutline(false);
                CurrentTarget = found;
                SetOutline(found != null);
                HoldProgress = 0f;
                _holdTimer = 0f;
                _heldTarget = null;
                OnTargetChanged?.Invoke(CurrentTarget);
                BroadcastPrompt();
            }
            else if (CurrentTarget != null)
            {
                BroadcastPrompt();
            }
        }

        private void ProcessHoldInteraction()
        {
            if (CurrentTarget == null)
            {
                HoldProgress = 0f;
                _holdTimer = 0f;
                _heldTarget = null;
                return;
            }

            if (!_input.InteractPressed && !_input.InteractHeld)
            {
                HoldProgress = 0f;
                _holdTimer = 0f;
                _heldTarget = null;
                return;
            }

            if (CurrentTarget.HoldDuration <= 0f)
            {
                if (_input.InteractPressed)
                    CurrentTarget.Interact(gameObject);
                return;
            }

            if (_heldTarget != CurrentTarget)
            {
                _heldTarget = CurrentTarget;
                _holdTimer = 0f;
            }

            if (!_input.InteractHeld)
                return;

            _holdTimer += Time.deltaTime;
            HoldProgress = Mathf.Clamp01(_holdTimer / CurrentTarget.HoldDuration);

            if (_holdTimer >= CurrentTarget.HoldDuration)
            {
                CurrentTarget.Interact(gameObject);
                _holdTimer = 0f;
                HoldProgress = 0f;
                _heldTarget = null;
            }
        }

        private void BroadcastPrompt()
        {
            if (CurrentTarget == null)
            {
                OnPromptChanged?.Invoke(string.Empty, InteractionType.Use, 0f);
                return;
            }

            OnPromptChanged?.Invoke(CurrentTarget.Prompt, CurrentTarget.InteractionType, HoldProgress);
        }

        private void SetOutline(bool enabled)
        {
            if (!useOutline || outlineRenderers == null)
                return;

            for (int i = 0; i < outlineRenderers.Length; i++)
            {
                if (outlineRenderers[i] == null)
                    continue;

                outlineRenderers[i].enabled = enabled;
            }
        }
    }
}
