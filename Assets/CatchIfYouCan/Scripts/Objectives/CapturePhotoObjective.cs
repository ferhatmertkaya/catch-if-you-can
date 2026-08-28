using CatchIfYouCan.Core;

namespace CatchIfYouCan.Objectives
{
    public class CapturePhotoObjective : ObjectiveBase
    {
        private readonly int _requiredStars;
        private int _photosTaken;

        public CapturePhotoObjective(string id, string description, int requiredStars, bool optional)
            : base(id, description, optional)
        {
            _requiredStars = requiredStars;
        }

        public override void Activate()
        {
            GameEvents.OnPhotoTaken += HandlePhotoTaken;
        }

        public override void Deactivate()
        {
            GameEvents.OnPhotoTaken -= HandlePhotoTaken;
        }

        private void HandlePhotoTaken(int stars)
        {
            if (stars >= _requiredStars)
            {
                _photosTaken++;
                SetProgress(_photosTaken >= 1 ? 1f : 0f);
            }
        }
    }
}
