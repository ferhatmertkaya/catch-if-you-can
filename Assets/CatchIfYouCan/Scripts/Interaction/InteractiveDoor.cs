using UnityEngine;
using CatchIfYouCan.Core;
using CatchIfYouCan.Player;

namespace CatchIfYouCan.Interaction
{
    public class InteractiveDoor : MonoBehaviour, IInteractable
    {
        [Header("Hinge")]
        [SerializeField] private Transform hinge;
        [SerializeField] private float minAngle;
        [SerializeField] private float maxAngle = 95f;
        [SerializeField] private float openSpeed = 120f;
        [SerializeField] private float closeSpeed = 90f;

        [Header("Interaction")]
        [SerializeField] private string openPrompt = "Open Door";
        [SerializeField] private string closePrompt = "Close Door";
        [SerializeField] private string lockedPrompt = "Locked";
        [SerializeField] private float distance = 2.5f;
        [SerializeField] private bool startOpen;
        [SerializeField] private bool locked;

        [Header("Noise")]
        [SerializeField] private float openNoise = 0.45f;
        [SerializeField] private float closeNoise = 0.35f;
        [SerializeField] private float ghostOpenNoise = 0.7f;

        [Header("Ghost Control")]
        [SerializeField] private bool ghostControllable = true;
        [SerializeField] private float ghostOpenChance = 0.15f;

        private float _currentAngle;
        private float _targetAngle;
        private bool _isOpen;
        private bool _moving;

        public bool IsOpen => _isOpen;
        public bool IsLocked => locked;

        public string Prompt
        {
            get
            {
                if (locked)
                    return lockedPrompt;
                return _isOpen ? closePrompt : openPrompt;
            }
        }

        public float HoldDuration => 0f;
        public InteractionType InteractionType => _isOpen ? InteractionType.Close : InteractionType.Open;
        public float Distance => distance;

        private void Awake()
        {
            if (hinge == null)
                hinge = transform;

            _isOpen = startOpen;
            _targetAngle = _isOpen ? maxAngle : minAngle;
            _currentAngle = _targetAngle;
            ApplyAngle(_currentAngle);
        }

        private void Update()
        {
            UpdateRotation();
            TryGhostInteraction();
        }

        public bool CanInteract(GameObject interactor)
        {
            return !locked || _isOpen;
        }

        public void Interact(GameObject interactor)
        {
            if (locked && !_isOpen)
                return;

            ToggleDoor(interactor != null);
        }

        public void SetLocked(bool value) => locked = value;

        public void ForceOpenByGhost()
        {
            if (!ghostControllable || locked)
                return;

            SetOpen(true, false);
            GameEvents.NoiseGenerated(ghostOpenNoise, transform.position);
        }

        private void ToggleDoor(bool emitNoise)
        {
            SetOpen(!_isOpen, emitNoise);
        }

        private void SetOpen(bool open, bool emitNoise)
        {
            _isOpen = open;
            _targetAngle = open ? maxAngle : minAngle;
            _moving = true;

            if (emitNoise)
            {
                float noise = open ? openNoise : closeNoise;
                GameEvents.NoiseGenerated(noise, transform.position);
            }

            if (open)
                GameEvents.DoorOpened();
        }

        private void UpdateRotation()
        {
            if (Mathf.Approximately(_currentAngle, _targetAngle))
            {
                _moving = false;
                return;
            }

            float speed = _isOpen ? openSpeed : closeSpeed;
            _currentAngle = Mathf.MoveTowards(_currentAngle, _targetAngle, speed * Time.deltaTime);
            ApplyAngle(_currentAngle);
        }

        private void ApplyAngle(float angle)
        {
            hinge.localRotation = Quaternion.Euler(0f, angle, 0f);
        }

        private void TryGhostInteraction()
        {
            if (!ghostControllable || locked || _moving)
                return;

            if (Random.value > ghostOpenChance * Time.deltaTime)
                return;

            ForceOpenByGhost();
        }

        public bool TryInteractFromLookDirection(Vector3 origin, Vector3 forward)
        {
            Vector3 toDoor = (hinge.position - origin).normalized;
            if (Vector3.Dot(forward, toDoor) < 0.55f)
                return false;

            if (!CanInteract(null))
                return false;

            Interact(null);
            return true;
        }
    }
}
