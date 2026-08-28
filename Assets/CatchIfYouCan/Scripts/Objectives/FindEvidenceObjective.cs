using CatchIfYouCan.Core;
using CatchIfYouCan.Evidence;

namespace CatchIfYouCan.Objectives
{
    public class FindEvidenceObjective : ObjectiveBase
    {
        private readonly EvidenceType _target;
        private readonly int _requiredCount;
        private int _foundCount;

        public FindEvidenceObjective(string id, string description, EvidenceType target, int requiredCount, bool optional)
            : base(id, description, optional)
        {
            _target = target;
            _requiredCount = UnityEngine.Mathf.Max(1, requiredCount);
        }

        public override void Activate()
        {
            GameEvents.OnEvidenceDetected += HandleEvidence;
        }

        public override void Deactivate()
        {
            GameEvents.OnEvidenceDetected -= HandleEvidence;
        }

        private void HandleEvidence(EvidenceType type)
        {
            if (type != _target)
                return;

            _foundCount++;
            SetProgress(_foundCount / (float)_requiredCount);
        }
    }
}
