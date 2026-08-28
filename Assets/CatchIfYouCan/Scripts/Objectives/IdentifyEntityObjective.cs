using CatchIfYouCan.Core;
using CatchIfYouCan.Ghost;

namespace CatchIfYouCan.Objectives
{
    public class IdentifyEntityObjective : ObjectiveBase
    {
        private readonly GhostDefinition _target;

        public IdentifyEntityObjective(string id, string description, GhostDefinition target, bool optional)
            : base(id, description, optional)
        {
            _target = target;
        }

        public override void Activate()
        {
            GameEvents.OnEntityDiscovered += HandleDiscovered;
        }

        public override void Deactivate()
        {
            GameEvents.OnEntityDiscovered -= HandleDiscovered;
        }

        private void HandleDiscovered(GhostDefinition ghost)
        {
            if (ghost == null || _target == null)
                return;

            if (ghost == _target || string.Equals(ghost.Id, _target.Id))
                MarkComplete();
        }
    }
}
