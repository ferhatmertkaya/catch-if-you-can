using UnityEngine;
using CatchIfYouCan.Evidence;

namespace CatchIfYouCan.Equipment
{
    public class SaltPile : MonoBehaviour
    {
        [SerializeField] private float footprintLifetime = 45f;
        [SerializeField] private GameObject footprintPrefab;
        [SerializeField] private float triggerRadius = 0.75f;

        private bool _triggered;

        public bool IsTriggered => _triggered;

        public void TriggerFootprint(Vector3 fromPosition, Vector3 toPosition)
        {
            if (_triggered || footprintPrefab == null)
                return;

            _triggered = true;
            var midpoint = (fromPosition + toPosition) * 0.5f;
            var footprint = Instantiate(footprintPrefab, midpoint, Quaternion.LookRotation(toPosition - fromPosition, Vector3.up));
            var reveal = footprint.GetComponent<EvidenceReveal>();
            if (reveal != null)
                reveal.Reveal();

            Destroy(footprint, footprintLifetime);
        }

        public bool OverlapsPoint(Vector3 point)
        {
            return Vector3.Distance(transform.position, point) <= triggerRadius;
        }
    }
}
