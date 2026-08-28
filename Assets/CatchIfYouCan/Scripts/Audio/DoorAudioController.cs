using CatchIfYouCan.Core;
using CatchIfYouCan.Interaction;
using UnityEngine;

namespace CatchIfYouCan.Audio
{
    [RequireComponent(typeof(InteractiveDoor))]
    public class DoorAudioController : MonoBehaviour
    {
        [SerializeField] private string handleId = "Env.Door.Handle";
        [SerializeField] private string latchId = "Env.Door.Latch";
        [SerializeField] private string openSlowId = "Env.Door.Open.Slow";
        [SerializeField] private string openFastId = "Env.Door.Open.Fast";
        [SerializeField] private string closeSlowId = "Env.Door.Close.Slow";
        [SerializeField] private string closeFastId = "Env.Door.Close.Fast";
        [SerializeField] private string slamId = "Env.Door.Slam";
        [SerializeField] private string creakId = "Env.Door.Creak";
        [SerializeField] private string ghostMoveId = "Env.Door.GhostMove";
        [SerializeField] private float fastVelocityThreshold = 90f;

        private InteractiveDoor _door;
        private float _lastAngle;
        private bool _wasOpen;
        private AudioPortal _portal;

        private void Awake()
        {
            _door = GetComponent<InteractiveDoor>();
            _portal = GetComponent<AudioPortal>();
        }

        private void OnEnable()
        {
            GameEvents.OnDoorOpened += HandleDoorOpened;
        }

        private void OnDisable()
        {
            GameEvents.OnDoorOpened -= HandleDoorOpened;
        }

        private void Update()
        {
            if (_door == null) return;
            float velocity = Mathf.Abs(GetAngle() - _lastAngle) / Mathf.Max(Time.deltaTime, 0.001f);
            _lastAngle = GetAngle();

            if (_door.IsOpen != _wasOpen)
            {
                PlayTransition(_door.IsOpen, velocity);
                _wasOpen = _door.IsOpen;
            }
            else if (velocity > 5f)
            {
                MaybeCreak(velocity);
            }

            _portal?.SetOpenAmount(_door.IsOpen ? 1f : 0f);
        }

        private void HandleDoorOpened()
        {
            if (_door == null || transform.position == Vector3.zero) return;
            AudioManager.Instance?.PlayEvent(latchId, transform.position, 0.45f);
        }

        public void PlayGhostMove()
        {
            AudioManager.Instance?.PlayEvent(ghostMoveId, transform.position, 0.7f);
        }

        public void PlaySlam()
        {
            AudioManager.Instance?.PlayEvent(slamId, transform.position, 0.9f);
        }

        private void PlayTransition(bool opening, float velocity)
        {
            AudioManager.Instance?.PlayEvent(handleId, transform.position, 0.4f);
            bool fast = velocity >= fastVelocityThreshold;
            string id = opening
                ? (fast ? openFastId : openSlowId)
                : (fast ? closeFastId : closeSlowId);
            float scale = fast ? 0.85f : 0.6f;
            AudioManager.Instance?.PlayEvent(id, transform.position, scale);
            if (fast && !opening)
                PlaySlam();
        }

        private void MaybeCreak(float velocity)
        {
            if (Random.value > velocity * 0.002f) return;
            AudioManager.Instance?.PlayEvent(creakId, transform.position, 0.35f);
        }

        private float GetAngle()
        {
            return transform.localEulerAngles.y;
        }
    }
}
