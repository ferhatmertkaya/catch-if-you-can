using CatchIfYouCan.Core;
using CatchIfYouCan.Evidence;

namespace CatchIfYouCan.Objectives
{
    public class RecordEVPObjective : ObjectiveBase
    {
        public RecordEVPObjective(string id, string description, bool optional)
            : base(id, description, optional)
        {
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
            if (type == EvidenceType.EVPResponse || type == EvidenceType.ParabolicAnomaly)
                MarkComplete();
        }
    }
}
