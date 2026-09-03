using CatchIfYouCan.Core;
using CatchIfYouCan.Equipment;
using CatchIfYouCan.Evidence;

namespace CatchIfYouCan.Objectives
{
    public class SaltGhostObjective : ObjectiveBase
    {
        public SaltGhostObjective(string id, string description, bool optional)
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
            if (type == EvidenceType.PhysicalDisturbance || type == EvidenceType.UVTraces)
            {
                // From the registry rather than a scene sweep. This runs on an evidence
                // confirmation, which is rare - but FindObjectsByType walks every object in
                // the house to find at most five piles, and the registry already has them.
                var piles = SaltPile.All;
                for (int i = 0; i < piles.Count; i++)
                {
                    if (piles[i] != null && piles[i].IsTriggered)
                    {
                        MarkComplete();
                        return;
                    }
                }
            }
        }
    }
}
