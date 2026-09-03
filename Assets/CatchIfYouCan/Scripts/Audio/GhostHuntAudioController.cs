using CatchIfYouCan.Core;
using CatchIfYouCan.Ghost;
using CatchIfYouCan.Interaction;
using CatchIfYouCan.Player;
using UnityEngine;

namespace CatchIfYouCan.Audio
{
    [RequireComponent(typeof(GhostController))]
    public class GhostHuntAudioController : MonoBehaviour
    {
        [SerializeField] private HuntController hunt;
        [SerializeField] private string warningId = "Ghost.Hunt.Warning";
        [SerializeField] private string activeId = "Ghost.Hunt.Active";
        [SerializeField] private string nearId = "Ghost.Hunt.Near";
        [SerializeField] private string endId = "Ghost.Hunt.End";
        [SerializeField] private string movementBedId = "Ghost.Hunt.MovementBed";
        [SerializeField] private float nearDistance = 8f;
        [SerializeField] private float movementBedVolume = 0.7f;

        private GhostController _ghost;
        private GhostAudioIdentity _identity;
        private Transform _player;
        private HideSpot _playerHide;
        private HuntPhase _phase = HuntPhase.Idle;
        private AudioSource _movementBed;
        private float _pulseTimer;

        private enum HuntPhase { Idle, Warning, Active, Near, Ending }

        private void Awake()
        {
            _ghost = GetComponent<GhostController>();
            hunt = hunt != null ? hunt : GetComponent<HuntController>();
            _movementBed = gameObject.AddComponent<AudioSource>();
            _movementBed.loop = true;
            _movementBed.spatialBlend = 1f;
            _movementBed.maxDistance = 35f;
            _movementBed.volume = 0f;
        }

        private void Start()
        {
            _identity = GhostIdentityAudio.Resolve(_ghost?.Definition?.DisplayName);
            _player = Core.LocalPlayerService.RootTransform;
        }

        private void OnEnable()
        {
            GameEvents.OnHuntStarted += OnHuntStarted;
            GameEvents.OnHuntEnded += OnHuntEnded;
        }

        private void OnDisable()
        {
            GameEvents.OnHuntStarted -= OnHuntStarted;
            GameEvents.OnHuntEnded -= OnHuntEnded;
            StopMovementBed();
        }

        private void Update()
        {
            if (_phase == HuntPhase.Idle) return;
            UpdatePhase();
            UpdateMovementBed();
            TickTensionPulse();
        }

        private void OnHuntStarted()
        {
            _phase = hunt != null && hunt.PreWarningActive ? HuntPhase.Warning : HuntPhase.Active;
            if (_phase == HuntPhase.Warning)
                AudioManager.Instance?.PlayEvent(warningId, transform.position, 0.75f);
            else
                AudioManager.Instance?.PlayEvent(activeId, transform.position, 0.8f);
            StartMovementBed();
        }

        private void OnHuntEnded()
        {
            _phase = HuntPhase.Ending;
            AudioManager.Instance?.PlayEvent(endId, transform.position, 0.65f);
            StopMovementBed();
            Invoke(nameof(ResetPhase), 2f);
        }

        private void ResetPhase() => _phase = HuntPhase.Idle;

        private void UpdatePhase()
        {
            if (_player == null) return;
            float dist = Vector3.Distance(transform.position, _player.position);
            bool los = HasLineOfSight();
            bool hidden = IsPlayerHidden();

            if (hunt != null && hunt.PreWarningActive)
            {
                _phase = HuntPhase.Warning;
                return;
            }

            if (dist <= nearDistance && los && !hidden)
                _phase = HuntPhase.Near;
            else if (_phase != HuntPhase.Ending)
                _phase = HuntPhase.Active;
        }

        private void TickTensionPulse()
        {
            _pulseTimer -= Time.deltaTime;
            if (_pulseTimer > 0f) return;
            _pulseTimer = _phase switch
            {
                HuntPhase.Warning => 1.8f,
                HuntPhase.Near => 0.9f,
                HuntPhase.Active => 1.4f,
                _ => 3f
            };

            string id = _phase switch
            {
                HuntPhase.Warning => warningId,
                HuntPhase.Near => nearId,
                HuntPhase.Active => _identity.HuntEventId,
                _ => activeId
            };

            float scale = _phase == HuntPhase.Near ? 0.95f : 0.65f;
            if (_phase != HuntPhase.Ending)
                AudioManager.Instance?.PlayEvent(id, transform.position, scale);
        }

        private void StartMovementBed()
        {
            var clip = AudioEventResolve.ResolveClip(movementBedId);
            if (clip == null) return;
            _movementBed.clip = clip;
            _movementBed.volume = movementBedVolume;
            if (!_movementBed.isPlaying)
                _movementBed.Play();
        }

        private void StopMovementBed()
        {
            _movementBed.Stop();
            _movementBed.volume = 0f;
        }

        private void UpdateMovementBed()
        {
            if (!_movementBed.isPlaying || _player == null) return;
            float dist = Vector3.Distance(transform.position, _player.position);
            float proximity = 1f - Mathf.Clamp01(dist / 25f);
            bool hidden = IsPlayerHidden();
            float targetVol = movementBedVolume * (0.45f + proximity * 0.55f);
            if (hidden) targetVol *= 0.55f;
            _movementBed.volume = Mathf.Lerp(_movementBed.volume, targetVol, Time.deltaTime * 4f);
            _movementBed.transform.position = transform.position;
        }

        private bool HasLineOfSight()
        {
            if (_player == null) return false;
            Vector3 origin = transform.position + Vector3.up * 1.4f;
            Vector3 target = _player.position + Vector3.up * 1.2f;
            return !Physics.Linecast(origin, target, ~0, QueryTriggerInteraction.Ignore);
        }

        private bool IsPlayerHidden()
        {
            if (_player == null) return false;
            var spots = FindObjectsByType<HideSpot>();
            for (int i = 0; i < spots.Length; i++)
            {
                if (spots[i] != null && spots[i].PlayerHidden)
                    return true;
            }
            return false;
        }
    }
}
