using UnityEngine;

namespace CatchIfYouCan.Equipment
{
    public class SaltEquipment : EquipmentBase
    {
        [SerializeField] private SaltPile saltPilePrefab;
        [SerializeField] private int maxPiles = 5;
        [SerializeField] private float pileSpacing = 1.5f;

        private int _placedCount;

        protected override void OnUse()
        {
            TryPlaceSaltPile();
        }

        private void TryPlaceSaltPile()
        {
            if (saltPilePrefab == null || HandAnchor == null || _placedCount >= maxPiles)
                return;

            var pos = HandAnchor.position + HandAnchor.forward * 1.1f;
            if (IsTooCloseToExisting(pos))
                return;

            Instantiate(saltPilePrefab, pos, Quaternion.identity);
            _placedCount++;
            ApplyDurabilityLoss(2f);
        }

        private bool IsTooCloseToExisting(Vector3 position)
        {
            var piles = FindObjectsByType<SaltPile>();
            foreach (var pile in piles)
            {
                if (pile != null && Vector3.Distance(pile.transform.position, position) < pileSpacing)
                    return true;
            }

            return false;
        }
    }

    public static class SaltFootprintUtility
    {
        public static void NotifyGhostStep(Vector3 from, Vector3 to)
        {
            var piles = Object.FindObjectsByType<SaltPile>();
            foreach (var pile in piles)
            {
                if (pile == null || pile.IsTriggered)
                    continue;

                if (pile.OverlapsPoint(from) || pile.OverlapsPoint(to))
                    pile.TriggerFootprint(from, to);
            }
        }
    }
}
