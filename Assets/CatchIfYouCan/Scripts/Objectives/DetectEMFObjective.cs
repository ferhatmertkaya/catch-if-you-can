using CatchIfYouCan.Core;
using CatchIfYouCan.Evidence;

namespace CatchIfYouCan.Objectives
{
    public class DetectEMFObjective : ObjectiveBase
    {
        public DetectEMFObjective(string id, string description, bool optional)
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
            if (type == EvidenceType.EMFSurge)
                MarkComplete();
        }
    }
}
