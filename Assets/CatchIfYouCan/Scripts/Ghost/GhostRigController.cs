using UnityEngine;

namespace CatchIfYouCan.Ghost
{
    /// <summary>Drives rigged ghost/monster animations based on GhostState.</summary>
    [DisallowMultipleComponent]
    public class GhostRigController : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private bool disableRootMotion = true;

        [Header("Clip name fallbacks")]
        [SerializeField] private string idleClip = "Idle";
        [SerializeField] private string walkClip = "Walk";
        [SerializeField] private string runClip = "Run";
        [SerializeField] private string manifestClip = "Roar";
        [SerializeField] private string attackClip = "Punch";

        private GhostController _ghost;
        private GhostState _lastState = GhostState.Dormant;
        private int _currentHash;

        private void Awake()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            if (animator != null && disableRootMotion)
                animator.applyRootMotion = false;

            _ghost = GetComponent<GhostController>();
        }

        private void Update()
        {
            if (animator == null || _ghost == null)
                return;

            var state = _ghost.CurrentState;
            if (state == _lastState)
                return;

            _lastState = state;
            PlayForState(state);
        }

        public void BindAnimator(Animator target)
        {
            animator = target;
            if (animator != null && disableRootMotion)
                animator.applyRootMotion = false;
        }

        private void PlayForState(GhostState state)
        {
            switch (state)
            {
                case GhostState.Hunting:
                    TryPlay(runClip, walkClip, "Run", "Walk2_Action", "Walk1_Action");
                    break;
                case GhostState.Roaming:
                case GhostState.Investigating:
                    TryPlay(walkClip, "Walk1_Action", "Walk2_Action", "Walk");
                    break;
                case GhostState.Manifesting:
                    TryPlay(manifestClip, "Roar_Action", "Wave", "Idle1_Action", idleClip);
                    break;
                case GhostState.Interacting:
                case GhostState.Event:
                case GhostState.Searching:
                    TryPlay(attackClip, "Punch_Action", "Punch", "Weapon");
                    break;
                default:
                    TryPlay(idleClip, "Idle1_Action", "Idle2_Action", "Idle", "Sleep_loop_Action");
                    break;
            }
        }

        private void TryPlay(params string[] clipNames)
        {
            for (int i = 0; i < clipNames.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(clipNames[i]))
                    continue;

                int hash = Animator.StringToHash(clipNames[i]);
                if (HasState(hash))
                {
                    if (_currentHash == hash)
                        return;

                    _currentHash = hash;
                    animator.CrossFade(hash, 0.15f);
                    return;
                }
            }
        }

        private bool HasState(int hash)
        {
            if (animator.runtimeAnimatorController == null)
                return false;

            for (int i = 0; i < animator.layerCount; i++)
            {
                if (animator.HasState(i, hash))
                    return true;
            }

            return false;
        }
    }
}
