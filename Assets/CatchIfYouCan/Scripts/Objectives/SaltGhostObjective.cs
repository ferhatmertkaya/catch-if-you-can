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
                var piles = UnityEngine.Object.FindObjectsByType<SaltPile>(UnityEngine.FindObjectsSortMode.None);
                for (int i = 0; i < piles.Length; i++)
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
