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

        /// <summary>Whether the ghost is currently manifested. What a peer is told, not what it guesses.</summary>
        public bool IsManifestationVisible => _manifestVisible;

        /// <summary>Where this ghost's position and state come from.</summary>
        public enum GhostDriveMode
        {
            /// <summary>This process simulates it, subject to <c>SessionAuthority.CanSimulateGhost</c>.</summary>
            HostSimulation = 0,

            /// <summary>Another machine decides; this one draws what it is told.</summary>
            RemoteState,
        }

        /// <summary>
        /// How this ghost is driven. <see cref="GhostDriveMode.HostSimulation"/> unless
        /// something has said otherwise, which is what single player is.
        /// </summary>
        public GhostDriveMode Drive { get; private set; }

        /// <summary>
        /// Every ghost currently in the scene. There is normally one, and equipment that needs
        /// it was each calling FindAnyObjectByType - the thermometer did it every frame, per
        /// frame, forever. A ghost knows when it exists.
        /// </summary>
        private static readonly System.Collections.Generic.List<GhostController> Alive =
            new System.Collections.Generic.List<GhostController>();

        /// <summary>The ghost, or null. Null is normal outside an investigation.</summary>
        public static GhostController Active => Alive.Count > 0 ? Alive[0] : null;

        /// <summary>All of them, for a lab that deliberately spawns more than one.</summary>
        public static System.Collections.Generic.IReadOnlyList<GhostController> All => Alive;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => Alive.Clear();

        private void OnEnable()
        {
            if (!Alive.Contains(this))
                Alive.Add(this);
        }

        private void OnDisable()
        {
            Alive.Remove(this);
        }

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

            // Exactly one peer runs the ghost. Everything below the presentation line - the
            // state machine, room awareness, the footsteps that disturb salt - is a decision,
            // and four machines each making it independently is four different ghosts wearing
            // the same transform.
            //
            // In single player this is always true, so nothing changes.
            if (Drive == GhostDriveMode.HostSimulation && Core.SessionAuthority.CanSimulateGhost)
            {
                _stateMachine.Tick(Time.deltaTime);
                UpdateRoomAwareness();
                UpdateFootsteps();

                // When a manifestation ends is a decision as much as when it starts, and it
                // runs on the same clock as the roll that began it. A remote-driven ghost is
                // told whether it is visible; expiring it locally would hide a ghost the host
                // is still showing everybody else, because the end time it would compare
                // against was never set on this machine.
                UpdateManifestationVisibility();
            }
        }

        [Header("Traces")]
        [Tooltip("How far the ghost must travel before it counts as having taken a step, in " +
                 "metres. The step is what disturbs salt.")]
        [SerializeField, Min(0.05f)] private float stepDistance = 0.55f;

        private Vector3 _lastStepPosition;
        private bool _hasStepped;

        /// <summary>
        /// Tells the salt where the ghost walked.
        ///
        /// <para>
        /// Nothing used to. <c>SaltFootprintUtility.NotifyGhostStep</c> existed, was correct,
        /// and had no caller anywhere in the project - so salt was a mechanic made of a pile
        /// that could not be poured, a step that was never reported and a footprint that was
        /// never built. This is the missing caller.
        /// </para>
        ///
        /// <para>
        /// On distance covered rather than per frame: a step is a step, and a stationary ghost
        /// standing in a pile should not grind it.
        /// </para>
        /// </summary>
        private void UpdateFootsteps()
        {
            Vector3 here = transform.position;

            if (!_hasStepped)
            {
                _hasStepped = true;
                _lastStepPosition = here;
                return;
            }

            if ((here - _lastStepPosition).sqrMagnitude < stepDistance * stepDistance)
                return;

            Vector3 from = _lastStepPosition;
            _lastStepPosition = here;
            Equipment.SaltPile.NotifyGhostStep(from, here);
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

        /// <summary>
        /// Cuts an active hunt short. The warding relic's whole purpose, asked for through the
        /// ghost rather than taken.
        ///
        /// <para>
        /// A public request rather than a <c>FindAnyObjectByType&lt;HuntController&gt;</c> from
        /// outside: the relic used to sweep the scene for the component and call into it, which
        /// is reaching past the ghost to operate one of its parts. Returns false when there was
        /// no hunt to end, so a ward does not spend a charge on nothing.
        /// </para>
        /// </summary>
        public bool TryEndHunt()
        {
            if (_hunt == null || (!_hunt.IsHunting && !_hunt.PreWarningActive))
                return false;

            _hunt.ForceEndHunt();
            return true;
        }

        /// <summary>Whether a hunt or its pre-warning is running right now.</summary>
        public bool IsHuntImminentOrActive =>
            _hunt != null && (_hunt.IsHunting || _hunt.PreWarningActive);
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

        /// <summary>
        /// Hands this ghost over to replicated state, permanently.
        ///
        /// <para>
        /// One-way, like the player's equivalent: a ghost that is somebody else's simulation
        /// is somebody else's for its whole life. The agent is switched off because a second
        /// pathfinder fighting a received position is a ghost that stutters between where it
        /// is and where it thinks it should walk.
        /// </para>
        /// </summary>
        public void DriveFromRemoteState()
        {
            Drive = GhostDriveMode.RemoteState;

            if (_agent != null)
            {
                if (_agent.isOnNavMesh)
                    _agent.ResetPath();

                _agent.enabled = false;
            }
        }

        /// <summary>
        /// Takes a state that was decided on another machine, for presentation.
        ///
        /// <para>
        /// Not <c>ForceState</c>, which runs the entry decisions belonging to the state -
        /// picking a roam target, performing an interaction. A client that ran those would be
        /// a second ghost making its own choices behind the same transform.
        /// </para>
        /// </summary>
        public void AdoptReplicatedState(GhostState replicated)
        {
            if (Drive != GhostDriveMode.RemoteState)
                return;

            if (_stateMachine != null)
                _stateMachine.AdoptReplicatedState(replicated);
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
