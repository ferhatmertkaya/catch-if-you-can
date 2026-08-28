using UnityEngine;
using CatchIfYouCan.Player;

namespace CatchIfYouCan.Interaction
{
    public class HideSpot : MonoBehaviour, IInteractable
    {
        [SerializeField] private Transform entryPoint;
        [SerializeField] private Transform cameraPoint;
        [SerializeField] private Transform exitPoint;
        [SerializeField] private float detectionModifier = 0.25f;
        [SerializeField] private string hidePrompt = "Hide";
        [SerializeField] private string exitPrompt = "Exit Hide Spot";
        [SerializeField] private float distance = 2f;
        [SerializeField] private float transitionSpeed = 4f;

        private bool _playerHidden;
        private Transform _hiddenPlayer;
        private PlayerController _playerController;
        private PlayerLook _playerLook;
        private Camera _playerCamera;

        public bool PlayerHidden => _playerHidden;
        public float DetectionModifier => _playerHidden ? detectionModifier : 1f;

        public string Prompt => _playerHidden ? exitPrompt : hidePrompt;
        public float HoldDuration => 0.25f;
        public InteractionType InteractionType => _playerHidden ? InteractionType.Use : InteractionType.Hide;
        public float Distance => distance;

        private void Awake()
        {
            if (entryPoint == null)
                entryPoint = transform;
            if (exitPoint == null)
                exitPoint = entryPoint;
            if (cameraPoint == null)
                cameraPoint = entryPoint;
        }

        private void Update()
        {
            if (!_playerHidden || _hiddenPlayer == null)
                return;

            _hiddenPlayer.position = Vector3.Lerp(
                _hiddenPlayer.position,
                entryPoint.position,
                transitionSpeed * Time.deltaTime);

            if (_playerCamera != null)
            {
                _playerCamera.transform.position = Vector3.Lerp(
                    _playerCamera.transform.position,
                    cameraPoint.position,
                    transitionSpeed * Time.deltaTime);
                _playerCamera.transform.rotation = Quaternion.Slerp(
                    _playerCamera.transform.rotation,
                    cameraPoint.rotation,
                    transitionSpeed * Time.deltaTime);
            }
        }

        public bool CanInteract(GameObject interactor)
        {
            if (_playerHidden)
                return _hiddenPlayer != null && interactor.transform == _hiddenPlayer;

            return !_playerHidden;
        }

        public void Interact(GameObject interactor)
        {
            if (_playerHidden)
                UnhidePlayer();
            else
                HidePlayer(interactor);
        }

        private void HidePlayer(GameObject interactor)
        {
            _hiddenPlayer = interactor.transform;
            _playerController = interactor.GetComponent<PlayerController>();
            _playerLook = interactor.GetComponentInChildren<PlayerLook>();
            _playerCamera = interactor.GetComponentInChildren<Camera>();

            _playerHidden = true;

            if (_playerController != null)
                _playerController.SetHidden(true);

            if (_playerLook != null)
                _playerLook.AllowLook = false;

            _hiddenPlayer.position = entryPoint.position;
            _hiddenPlayer.rotation = entryPoint.rotation;

            if (_playerCamera != null)
            {
                _playerCamera.transform.SetPositionAndRotation(
                    cameraPoint.position,
                    cameraPoint.rotation);
            }
        }

        private void UnhidePlayer()
        {
            if (_hiddenPlayer == null)
                return;

            _playerHidden = false;

            if (_playerController != null)
            {
                _playerController.SetHidden(false);
                _playerController.Teleport(exitPoint.position, exitPoint.rotation);
            }
            else
            {
                _hiddenPlayer.SetPositionAndRotation(exitPoint.position, exitPoint.rotation);
            }

            if (_playerLook != null)
                _playerLook.AllowLook = true;

            _hiddenPlayer = null;
            _playerController = null;
            _playerLook = null;
            _playerCamera = null;
        }
    }
}
