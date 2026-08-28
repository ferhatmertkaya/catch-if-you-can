using CatchIfYouCan.Core;

namespace CatchIfYouCan.Objectives
{
    public class SurviveHuntObjective : ObjectiveBase
    {
        private bool _huntActive;

        public SurviveHuntObjective(string id, string description, bool optional)
            : base(id, description, optional)
        {
        }

        public override void Activate()
        {
            GameEvents.OnHuntStarted += HandleHuntStarted;
            GameEvents.OnHuntEnded += HandleHuntEnded;
            GameEvents.OnPlayerDied += HandlePlayerDied;
        }

        public override void Deactivate()
        {
            GameEvents.OnHuntStarted -= HandleHuntStarted;
            GameEvents.OnHuntEnded -= HandleHuntEnded;
            GameEvents.OnPlayerDied -= HandlePlayerDied;
        }

        private void HandleHuntStarted()
        {
            _huntActive = true;
        }

        private void HandleHuntEnded()
        {
            if (_huntActive)
                MarkComplete();
            _huntActive = false;
        }

        private void HandlePlayerDied()
        {
            _huntActive = false;
            Progress = 0f;
            IsComplete = false;
        }
    }
}
