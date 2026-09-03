using UnityEngine;
using CatchIfYouCan.Core;

namespace CatchIfYouCan.Ghost
{
    [RequireComponent(typeof(GhostController))]
    public class GhostPerception : MonoBehaviour
    {
        [SerializeField] private float visionRange = 18f;
        [SerializeField] private float visionAngle = 110f;
        [SerializeField] private float hearingRange = 14f;
        [SerializeField] private LayerMask obstructionMask = ~0;
        [SerializeField] private float huntConfirmMin = 0.6f;
        [SerializeField] private float huntConfirmMax = 1f;
        [SerializeField] private Transform eyePoint;

        private Transform _player;
        private GhostController _controller;
        private float _losConfirmTimer;
        private bool _hadLosLastFrame;
        private float _confirmDuration;

        public Vector3 LastKnownPlayerPosition { get; private set; }
        public bool HasLineOfSight { get; private set; }
        public bool HuntKillConfirmed { get; private set; }
        public float LastNoiseIntensity { get; private set; }

        private void Awake()
        {
            _controller = GetComponent<GhostController>();
            if (eyePoint == null) eyePoint = transform;
            _confirmDuration = Random.Range(huntConfirmMin, huntConfirmMax);
        }

        private void Start()
        {
            // The ghost is spawned before the player in the investigation bootstrap, so
            // this can legitimately find nothing here. Re-resolved on demand rather than
            // leaving the ghost permanently unable to perceive anyone.
            BindPlayer();
        }

        [Tooltip("Seconds between re-checking which player is nearest. A ghost does not need " +
                 "to re-decide who it is looking at sixty times a second, and eight players " +
                 "make that a real cost rather than a theoretical one.")]
        [SerializeField, Min(0.05f)] private float targetRefreshInterval = 0.25f;

        private float _targetTimer;

        /// <summary>
        /// Picks whoever is nearest out of everyone in the house.
        ///
        /// <para>
        /// This used to bind <c>LocalPlayerService.RootTransform</c> once and keep it forever.
        /// That service holds exactly one player - the one on this machine - so with a second
        /// player in the house the ghost would roam toward the host, hunt the host and treat
        /// everybody else as furniture. In single player the registry holds exactly one entry
        /// and the answer is identical to what it was.
        /// </para>
        /// </summary>
        private bool BindPlayer()
        {
            _targetTimer -= Time.deltaTime;
            if (_player != null && _targetTimer > 0f)
                return true;

            _targetTimer = targetRefreshInterval;

            var nearest = Player.PlayerPresence.Nearest(eyePoint != null
                ? eyePoint.position
                : transform.position);

            if (nearest == null)
                return _player != null;

            if (_player != nearest.transform)
            {
                _player = nearest.transform;
                LastKnownPlayerPosition = _player.position;
            }

            return true;
        }

        private void Update()
        {
            // Perception is a host decision: it feeds hunting, investigating and who gets
            // killed. A client running it would reach its own conclusions about its own local
            // player and act on them.
            if (!Core.SessionAuthority.CanSimulateGhost)
                return;

            // Retried until a player exists, so a ghost spawned first is not blind for the
            // rest of the mission.
            if (!BindPlayer()) return;

            UpdateLineOfSight();
            UpdateHuntConfirmation();
            PollNoise();
        }

        /// <summary>
        /// Where the ghost sees from. Asked for as a method because the factory used to reach
        /// in and set the private field by reflection - which compiles, reviews clean, and
        /// fails silently the next time somebody renames the field.
        /// </summary>
        public void SetEyePoint(Transform eye)
        {
            if (eye != null)
                eyePoint = eye;
        }

        public void SetPlayer(Transform player)
        {
            _player = player;
            if (_player != null)
                LastKnownPlayerPosition = _player.position;
        }

        public bool CanSeePlayer()
        {
            return HasLineOfSight;
        }

        public Vector3 GetPlayerPosition()
        {
            return _player != null ? _player.position : LastKnownPlayerPosition;
        }

        private void UpdateLineOfSight()
        {
            HasLineOfSight = false;
            HuntKillConfirmed = false;

            Vector3 origin = eyePoint.position;
            Vector3 target = _player.position + Vector3.up * 1.1f;
            Vector3 toTarget = target - origin;
            float dist = toTarget.magnitude;

            if (dist > visionRange) return;

            Vector3 forward = transform.forward;
            float angle = Vector3.Angle(forward, toTarget.normalized);
            if (angle > visionAngle * 0.5f) return;

            if (Physics.Raycast(origin, toTarget.normalized, out RaycastHit hit, dist, obstructionMask,
                    QueryTriggerInteraction.Ignore))
            {
                if (!hit.transform.IsChildOf(_player) && hit.transform != _player)
                    return;
            }

            HasLineOfSight = true;
            LastKnownPlayerPosition = _player.position;
        }

        private void UpdateHuntConfirmation()
        {
            if (_controller == null || _controller.CurrentState != GhostState.Hunting)
            {
                _losConfirmTimer = 0f;
                _hadLosLastFrame = false;
                return;
            }

            if (HasLineOfSight)
            {
                if (!_hadLosLastFrame)
                    _losConfirmTimer = 0f;

                _losConfirmTimer += Time.deltaTime;
                if (_losConfirmTimer >= _confirmDuration)
                    HuntKillConfirmed = true;
            }
            else
            {
                _losConfirmTimer = 0f;
            }

            _hadLosLastFrame = HasLineOfSight;
        }

        private void PollNoise()
        {
            if (_controller?.Definition == null) return;

            var noiseMgr = AI.NoiseManager.Instance;
            if (noiseMgr == null) return;

            float sensitivity = _controller.Definition.SoundSensitivity;
            if (noiseMgr.TryGetLoudestNoise(transform.position, sensitivity, hearingRange, out var noise))
            {
                LastNoiseIntensity = noise.Intensity;
                LastKnownPlayerPosition = noise.Position;
            }
        }

        public void ResetHuntConfirmation()
        {
            _losConfirmTimer = 0f;
            _hadLosLastFrame = false;
            _confirmDuration = Random.Range(huntConfirmMin, huntConfirmMax);
            HuntKillConfirmed = false;
        }

        private void OnDrawGizmosSelected()
        {
            if (eyePoint == null) return;
            Gizmos.color = HasLineOfSight ? Color.red : Color.yellow;
            Gizmos.DrawWireSphere(eyePoint.position, 0.3f);
            Gizmos.DrawLine(eyePoint.position, LastKnownPlayerPosition);
        }
    }
}
