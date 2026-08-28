using UnityEngine;

namespace CatchIfYouCan.Ghost
{
    public class GhostStateMachine : MonoBehaviour
    {
        private GhostController _owner;
        private GhostState _state = GhostState.Dormant;
        private float _stateTimer;
        private float _stateDuration;

        public GhostState Current => _state;
        public float StateTime => _stateTimer;

        public void Initialize(GhostController owner)
        {
            _owner = owner;
            EnterState(GhostState.Dormant);
        }

        public void Tick(float deltaTime)
        {
            _stateTimer += deltaTime;
            EvaluateTransitions();
            RunStateLogic(deltaTime);
        }

        public void ForceState(GhostState newState)
        {
            if (_state == newState) return;
            EnterState(newState);
        }

        private void EnterState(GhostState newState)
        {
            _state = newState;
            _stateTimer = 0f;
            _stateDuration = GetDurationForState(newState);
            _owner?.OnStateEntered(newState);
        }

        private float GetDurationForState(GhostState state)
        {
            var def = _owner?.Definition;
            float activity = def != null ? def.Activity : 0.5f;

            switch (state)
            {
                case GhostState.Dormant:
                    return Random.Range(5f, 12f);
                case GhostState.Roaming:
                    return Random.Range(8f, 18f) * (1.1f - activity * 0.3f);
                case GhostState.Investigating:
                    return Random.Range(6f, 14f);
                case GhostState.Manifesting:
                    return Random.Range(2f, 5f);
                case GhostState.Interacting:
                    return Random.Range(3f, 7f);
                case GhostState.Event:
                    return Random.Range(2f, 4f);
                case GhostState.Hunting:
                    return 999f;
                case GhostState.Searching:
                    return Random.Range(10f, 20f) * (1.2f - (def?.SearchAbility ?? 0.5f) * 0.4f);
                case GhostState.Cooldown:
                    return Random.Range(6f, 12f);
                default:
                    return 10f;
            }
        }

        private void EvaluateTransitions()
        {
            if (_owner == null) return;

            switch (_state)
            {
                case GhostState.Dormant:
                    if (_stateTimer >= _stateDuration * 0.5f && _owner.ShouldWake())
                        EnterState(GhostState.Roaming);
                    break;

                case GhostState.Roaming:
                    if (_owner.WantsToHunt())
                    {
                        EnterState(GhostState.Hunting);
                        break;
                    }
                    if (_owner.HasNoiseTarget())
                        EnterState(GhostState.Investigating);
                    else if (_stateTimer >= _stateDuration)
                    {
                        float response = _owner.Definition != null ? _owner.Definition.ResponseFrequency : 0.3f;
                        EnterState(Random.value < response ? GhostState.Interacting : GhostState.Cooldown);
                    }
                    break;

                case GhostState.Investigating:
                    if (_owner.WantsToHunt())
                    {
                        EnterState(GhostState.Hunting);
                        break;
                    }
                    if (_owner.ReachedInvestigationPoint() || _stateTimer >= _stateDuration)
                        EnterState(GhostState.Roaming);
                    break;

                case GhostState.Manifesting:
                case GhostState.Event:
                case GhostState.Interacting:
                    if (_stateTimer >= _stateDuration)
                        EnterState(GhostState.Roaming);
                    break;

                case GhostState.Hunting:
                    if (!_owner.IsHuntActive())
                        EnterState(GhostState.Searching);
                    break;

                case GhostState.Searching:
                    if (_owner.WantsToHunt())
                    {
                        EnterState(GhostState.Hunting);
                        break;
                    }
                    if (_stateTimer >= _stateDuration)
                        EnterState(GhostState.Cooldown);
                    break;

                case GhostState.Cooldown:
                    if (_stateTimer >= _stateDuration)
                        EnterState(GhostState.Dormant);
                    break;
            }
        }

        private void RunStateLogic(float deltaTime)
        {
            switch (_state)
            {
                case GhostState.Roaming:
                    _owner.DoRoam(deltaTime);
                    break;
                case GhostState.Investigating:
                    _owner.DoInvestigate(deltaTime);
                    break;
                case GhostState.Hunting:
                    _owner.DoHunt(deltaTime);
                    break;
                case GhostState.Searching:
                    _owner.DoSearch(deltaTime);
                    break;
                case GhostState.Manifesting:
                    _owner.DoManifest(deltaTime);
                    break;
                case GhostState.Interacting:
                    _owner.DoInteract(deltaTime);
                    break;
            }
        }
    }
}
