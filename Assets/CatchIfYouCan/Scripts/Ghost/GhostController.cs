using UnityEngine;
using UnityEngine.AI;
using CatchIfYouCan.Core;

namespace CatchIfYouCan.Ghost
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class GhostController : MonoBehaviour
    {
        [SerializeField] private GhostDefinition definition;
        [SerializeField] private Transform[] roomWaypoints;
        [SerializeField] private Renderer[] manifestationRenderers;
        [SerializeField] private float investigationReachDistance = 1.5f;

        private NavMeshAgent _agent;
        private GhostStateMachine _stateMachine;
        private GhostPerception _perception;
        private HuntController _hunt;
        private GhostInteractionBrain _interaction;

        private Vector3 _roamTarget;
        private Vector3 _investigateTarget;
        private int _currentRoomIndex;
        private bool _manifestVisible;
        private float _manifestEndTime;
        private bool _initialized;

        public GhostDefinition Definition => definition;
        public GhostState CurrentState => _stateMachine != null ? _stateMachine.Current : GhostState.Dormant;
        public NavMeshAgent Agent => _agent;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _stateMachine = EnsureComponent<GhostStateMachine>();
            _perception = EnsureComponent<GhostPerception>();
            _hunt = EnsureComponent<HuntController>();
            _interaction = EnsureComponent<GhostInteractionBrain>();
            EnsureComponent<GhostEvidenceManager>();
            SetManifestationVisible(false);
        }

        private T EnsureComponent<T>() where T : Component
        {
            var c = GetComponent<T>();
            return c != null ? c : gameObject.AddComponent<T>();
        }

        public void Initialize(GhostDefinition def)
        {
            definition = def;
            ApplyDefinitionStats();
            _stateMachine.Initialize(this);
            _initialized = true;
            PickRoamTarget();
        }

        private void Start()
        {
            if (!_initialized && definition != null)
                Initialize(definition);
        }

        private void Update()
        {
            if (definition == null) return;
            _stateMachine.Tick(Time.deltaTime);
            UpdateManifestationVisibility();
            UpdateRoomAwareness();
        }

        public void OnStateEntered(GhostState state)
        {
            switch (state)
            {
                case GhostState.Hunting:
                    _agent.speed = definition.Speed * 1.35f;
                    _perception.ResetHuntConfirmation();
                    break;
                case GhostState.Roaming:
                    _agent.speed = definition.Speed * 0.75f;
                    PickRoamTarget();
                    break;
                case GhostState.Manifesting:
                    SetManifestationVisible(true);
                    _manifestEndTime = Time.time + Random.Range(2f, 4f);
                    break;
                case GhostState.Interacting:
                    _interaction.TryRandomInteraction();
                    break;
                case GhostState.Cooldown:
                    SetManifestationVisible(false);
                    _agent.ResetPath();
                    break;
            }
        }

        public bool ShouldWake()
        {
            var activity = GhostActivitySystem.Instance;
            if (activity == null) return Random.value < definition.RoamFrequency;
            return activity.Normalized > 0.05f || Random.value < definition.RoamFrequency * 0.5f;
        }

        public bool WantsToHunt()
        {
            if (_hunt.IsHunting || _hunt.PreWarningActive) return true;
            var activity = GhostActivitySystem.Instance;
            if (activity == null) return false;
            return activity.ShouldConsiderHunt(definition.GetEffectiveHuntThreshold(activity.Normalized));
        }

        public bool IsHuntActive() => _hunt.IsHunting;

        public bool HasNoiseTarget() =>
            _perception.LastNoiseIntensity > 0.15f * definition.SoundSensitivity;

        public bool ReachedInvestigationPoint()
        {
            if (!_agent.pathPending && _agent.remainingDistance <= investigationReachDistance)
                return true;
            return Vector3.Distance(transform.position, _investigateTarget) <= investigationReachDistance;
        }

        public void DoRoam(float deltaTime)
        {
            if (!_agent.hasPath || _agent.remainingDistance <= investigationReachDistance)
                PickRoamTarget();
            _agent.SetDestination(_roamTarget);
        }

        public void DoInvestigate(float deltaTime)
        {
            _investigateTarget = _perception.LastKnownPlayerPosition;
            _agent.SetDestination(_investigateTarget);
        }

        public void DoHunt(float deltaTime)
        {
            if (!_hunt.IsHunting && !_hunt.PreWarningActive)
            {
                TryBeginHunt();
                return;
            }

            _agent.SetDestination(_perception.GetPlayerPosition());
            if (!_perception.HuntKillConfirmed) return;

            var gm = GameManager.Instance;
            if (gm != null && gm.Invincible) return;

            GameEvents.PlayerDied();
            if (Missions.MissionManager.Instance?.ActiveMission != null)
                Missions.MissionManager.Instance.FailActiveMission();
            else
                gm?.FailMission();
            _hunt.ForceEndHunt();
        }

        public void DoSearch(float deltaTime)
        {
            if (!_agent.hasPath || _agent.remainingDistance <= investigationReachDistance)
            {
                _investigateTarget = _perception.LastKnownPlayerPosition +
                    Random.insideUnitSphere * (4f + definition.SearchAbility * 6f);
                _agent.SetDestination(_investigateTarget);
            }
        }

        public void DoManifest(float deltaTime)
        {
            if (Time.time >= _manifestEndTime)
                _stateMachine.ForceState(GhostState.Roaming);
        }

        public void DoInteract(float deltaTime) => _agent.ResetPath();
        public void RequestLightFlicker() => _interaction.TryLightInteraction();
        public void RequestDoorInteraction(bool slam) => _interaction.TryDoorInteraction(slam);
        public void RequestObjectThrow() => _interaction.TryObjectThrow();
        public bool TryBeginHunt() => _hunt.TryStartHunt();
        public void NotifyDirectorEvent(HorrorEventType type) => _stateMachine.ForceState(GhostState.Event);

        public void OnHuntStarted()
        {
            _stateMachine.ForceState(GhostState.Hunting);
            SetManifestationVisible(true);
        }

        public void OnHuntEnded()
        {
            _perception.ResetHuntConfirmation();
            SetManifestationVisible(false);
        }

        public void RequestManifestation(float duration, bool forceVisible)
        {
            _manifestEndTime = Time.time + duration;
            SetManifestationVisible(forceVisible || Random.value < definition.ManifestationChance);
            _stateMachine.ForceState(GhostState.Manifesting);
        }

        public void RequestInteraction(HorrorEventType type)
        {
            _interaction.ExecuteHorrorInteraction(type);
            _stateMachine.ForceState(GhostState.Event);
        }

        public void SetManifestationRenderers(Renderer[] renderers)
        {
            manifestationRenderers = renderers;
        }

        public void EnsureManifestationRenderers()
        {
            if (manifestationRenderers != null && manifestationRenderers.Length > 0)
                return;

            manifestationRenderers = GetComponentsInChildren<Renderer>(true);
        }

        public void SetManifestationVisible(bool visible)
        {
            _manifestVisible = visible;
            if (manifestationRenderers == null) return;
            for (int i = 0; i < manifestationRenderers.Length; i++)
            {
                if (manifestationRenderers[i] != null)
                    manifestationRenderers[i].enabled = visible;
            }
        }

        public string GetCurrentRoomName()
        {
            if (roomWaypoints == null || roomWaypoints.Length == 0 ||
                _currentRoomIndex < 0 || _currentRoomIndex >= roomWaypoints.Length ||
                roomWaypoints[_currentRoomIndex] == null)
                return "Unknown";
            return roomWaypoints[_currentRoomIndex].name;
        }

        private void ApplyDefinitionStats()
        {
            if (definition == null) return;
            _agent.speed = definition.Speed;
            _agent.acceleration = definition.Speed * 4f;
            _agent.angularSpeed = 240f;
            _agent.updateRotation = true;
            _agent.stoppingDistance = investigationReachDistance;
        }

        private void PickRoamTarget()
        {
            if (roomWaypoints != null && roomWaypoints.Length > 0)
            {
                _currentRoomIndex = Random.Range(0, roomWaypoints.Length);
                _roamTarget = roomWaypoints[_currentRoomIndex].position;
            }
            else
            {
                Vector2 circle = Random.insideUnitCircle * 12f;
                _roamTarget = transform.position + new Vector3(circle.x, 0f, circle.y);
            }

            if (NavMesh.SamplePosition(_roamTarget, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                _roamTarget = hit.position;
        }

        private void UpdateRoomAwareness()
        {
            if (roomWaypoints == null || roomWaypoints.Length == 0) return;

            float best = float.MaxValue;
            int bestIdx = _currentRoomIndex;
            for (int i = 0; i < roomWaypoints.Length; i++)
            {
                if (roomWaypoints[i] == null) continue;
                float d = Vector3.Distance(transform.position, roomWaypoints[i].position);
                if (d < best) { best = d; bestIdx = i; }
            }
            _currentRoomIndex = bestIdx;
        }

        private void UpdateManifestationVisibility()
        {
            if (_manifestVisible && CurrentState != GhostState.Hunting &&
                CurrentState != GhostState.Manifesting && Time.time >= _manifestEndTime)
                SetManifestationVisible(false);
        }
    }
}
