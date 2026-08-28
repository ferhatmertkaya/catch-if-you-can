using CatchIfYouCan.Core;
using CatchIfYouCan.Ghost;
using UnityEngine;

namespace CatchIfYouCan.Audio
{
    [RequireComponent(typeof(GhostController))]
    public class GhostAudioController : MonoBehaviour
    {
        [SerializeField] private float presenceIntervalMin = 8f;
        [SerializeField] private float presenceIntervalMax = 22f;
        [SerializeField] private float movementThreshold = 0.35f;

        private GhostController _ghost;
        private GhostAudioIdentity _identity;
        private GhostState _lastState = GhostState.Dormant;
        private float _presenceTimer;
        private float _moveTimer;
        private Vector3 _lastPos;
        private bool _huntActive;

        public bool IsHuntActive => _huntActive;

        private void Awake()
        {
            _ghost = GetComponent<GhostController>();
            _lastPos = transform.position;
        }

        private void Start()
        {
            RefreshIdentity();
            _presenceTimer = Random.Range(presenceIntervalMin, presenceIntervalMax);
        }

        private void OnEnable()
        {
            GameEvents.OnHuntStarted += HandleHuntStarted;
            GameEvents.OnHuntEnded += HandleHuntEnded;
            GameEvents.OnGhostActivityChanged += HandleActivity;
        }

        private void OnDisable()
        {
            GameEvents.OnHuntStarted -= HandleHuntStarted;
            GameEvents.OnHuntEnded -= HandleHuntEnded;
            GameEvents.OnGhostActivityChanged -= HandleActivity;
        }

        private void Update()
        {
            if (_ghost == null || _ghost.Definition == null) return;
            TickStateChange();
            TickPresence();
            TickMovementAudio();
            _lastPos = transform.position;
        }

        private void RefreshIdentity()
        {
            string name = _ghost?.Definition?.DisplayName;
            _identity = GhostIdentityAudio.Resolve(name);
        }

        private void TickStateChange()
        {
            var state = _ghost.CurrentState;
            if (state == _lastState) return;
            OnGhostStateChanged(_lastState, state);
            _lastState = state;
        }

        private void OnGhostStateChanged(GhostState from, GhostState to)
        {
            switch (to)
            {
                case GhostState.Manifesting:
                    AudioManager.Instance?.PlayEvent(_identity.PresenceEventId, transform.position, 0.55f);
                    break;
                case GhostState.Interacting:
                    AudioManager.Instance?.PlayEvent("Ghost.Interact.Subtle", transform.position, 0.45f);
                    break;
                case GhostState.Event:
                    AudioManager.Instance?.PlayEvent("Ghost.Event.Pulse", transform.position, 0.6f);
                    break;
                case GhostState.Hunting:
                    AudioManager.Instance?.PlayEvent(_identity.HuntEventId, transform.position, 0.75f);
                    break;
            }
        }

        private void TickPresence()
        {
            if (_huntActive || _ghost.CurrentState == GhostState.Dormant) return;
            _presenceTimer -= Time.deltaTime;
            if (_presenceTimer > 0f) return;
            _presenceTimer = Random.Range(presenceIntervalMin, presenceIntervalMax);
            float activity = GhostActivitySystem.Instance != null ? GhostActivitySystem.Instance.Normalized : 0.3f;
            AudioManager.Instance?.PlayEvent(_identity.PresenceEventId, transform.position, 0.25f + activity * 0.35f);
        }

        private void TickMovementAudio()
        {
            float speed = Vector3.Distance(transform.position, _lastPos) / Mathf.Max(Time.deltaTime, 0.001f);
            if (speed < movementThreshold) return;

            _moveTimer -= Time.deltaTime;
            if (_moveTimer > 0f) return;
            _moveTimer = _huntActive ? 0.35f : 0.9f;
            float scale = _huntActive ? 0.85f : 0.45f;
            AudioManager.Instance?.PlayEvent(_identity.MovementEventId, transform.position, scale);
        }

        private void HandleHuntStarted()
        {
            _huntActive = true;
            AudioManager.Instance?.PlayEvent(_identity.HuntEventId, transform.position, 0.8f);
        }

        private void HandleHuntEnded() => _huntActive = false;

        private void HandleActivity(float activity)
        {
            if (activity > 0.7f && Random.value < 0.04f)
                AudioManager.Instance?.PlayEvent(_identity.PresenceEventId, transform.position, 0.4f);
        }
    }
}
