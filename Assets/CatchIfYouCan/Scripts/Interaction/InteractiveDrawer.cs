using UnityEngine;
using CatchIfYouCan.Core;
using CatchIfYouCan.Player;

namespace CatchIfYouCan.Interaction
{
    public class InteractiveDrawer : MonoBehaviour, IInteractable
    {
        [SerializeField] private Transform slideTarget;
        [SerializeField] private Vector3 closedLocalPosition;
        [SerializeField] private Vector3 openLocalOffset = new Vector3(0f, 0f, 0.35f);
        [SerializeField] private float slideSpeed = 1.2f;
        [SerializeField] private string openPrompt = "Open Drawer";
        [SerializeField] private string closePrompt = "Close Drawer";
        [SerializeField] private float distance = 2f;
        [SerializeField] private float openNoise = 0.25f;
        [SerializeField] private bool startOpen;

        private Vector3 _openLocalPosition;
        private Vector3 _targetLocalPosition;
        private bool _isOpen;

        public string Prompt => _isOpen ? closePrompt : openPrompt;
        public float HoldDuration => 0f;
        public InteractionType InteractionType => _isOpen ? InteractionType.Close : InteractionType.Open;
        public float Distance => distance;

        private void Awake()
        {
            if (slideTarget == null)
                slideTarget = transform;

            if (closedLocalPosition == Vector3.zero)
                closedLocalPosition = slideTarget.localPosition;

            _openLocalPosition = closedLocalPosition + openLocalOffset;
            _isOpen = startOpen;
            _targetLocalPosition = _isOpen ? _openLocalPosition : closedLocalPosition;
            slideTarget.localPosition = _targetLocalPosition;
        }

        private void Update()
        {
            slideTarget.localPosition = Vector3.MoveTowards(
                slideTarget.localPosition,
                _targetLocalPosition,
                slideSpeed * Time.deltaTime);
        }

        public bool CanInteract(GameObject interactor) => true;

        public void Interact(GameObject interactor)
        {
            _isOpen = !_isOpen;
            _targetLocalPosition = _isOpen ? _openLocalPosition : closedLocalPosition;

            GameEvents.NoiseGenerated(openNoise, transform.position);

            PlayerNoiseEmitter noise = interactor != null
                ? interactor.GetComponent<PlayerNoiseEmitter>()
                : null;
            noise?.EmitCustomNoise(openNoise);
        }
    }
}
