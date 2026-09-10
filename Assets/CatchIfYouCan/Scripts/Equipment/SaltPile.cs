using System.Collections.Generic;
using CatchIfYouCan.Evidence;
using UnityEngine;

namespace CatchIfYouCan.Equipment
{
    /// <summary>
    /// A pile of salt on a floor, and the footprint the ghost leaves when it walks through one.
    ///
    /// <para>
    /// Neither half had ever run. The pile could not be poured - the salt item's
    /// <c>saltPilePrefab</c> was a serialized field nothing assigned, and the first line of the
    /// pour returned on it. Nothing anywhere called <c>NotifyGhostStep</c>, so a pile that did
    /// somehow exist would never have been stepped in. And <c>footprintPrefab</c> was another
    /// unassigned field, so a step that did somehow register left nothing behind. Three
    /// independent dead ends in one two-class mechanic.
    /// </para>
    ///
    /// <para>
    /// The footprint is deliberately invisible when it appears. Salt does not show you where
    /// the ghost went; salt plus a UV lamp does. It carries an
    /// <see cref="EvidenceReveal"/> that starts hidden and a trigger collider so the lamp's
    /// sweep can find it, and the lamp has to be held on it - which is the pairing the two
    /// items were always meant to have and never did.
    /// </para>
    /// </summary>
    [AddComponentMenu("Catch If You Can/Salt Pile")]
    public class SaltPile : MonoBehaviour
    {
        [Tooltip("How long a footprint lasts before it fades, in seconds.")]
        [SerializeField] private float footprintLifetime = 45f;

        [Tooltip("How close the ghost has to pass to disturb the pile, in metres.")]
        [SerializeField, Min(0.1f)] private float triggerRadius = 0.75f;

        private EquipmentVisualProfile _footprintVisual;
        private bool _triggered;

        public bool IsTriggered => _triggered;

        /// <summary>
        /// Every pile currently on a floor. A ghost step asks this rather than sweeping the
        /// scene: the step check runs whenever the ghost moves, and
        /// <c>FindObjectsByType&lt;SaltPile&gt;</c> at that rate walks every object in the
        /// house to find at most five of them.
        /// </summary>
        private static readonly List<SaltPile> Alive = new List<SaltPile>();

        /// <summary>Read-only view. Do not hold onto it across frames.</summary>
        public static IReadOnlyList<SaltPile> All => Alive;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => Alive.Clear();

        private void OnEnable()
        {
            if (!Alive.Contains(this))
                Alive.Add(this);
        }

        private void OnDisable()
        {
            Alive.Remove(this);
        }

        /// <summary>
        /// Pours a pile at a place the placement query has already accepted.
        ///
        /// <para>
        /// The visuals arrive as profiles rather than as construction in here, so replacing the
        /// DEV placeholder with real art is assigning a prefab to a profile and touches no
        /// gameplay code - the same contract every carried item has.
        /// </para>
        /// </summary>
        public static SaltPile Create(Vector3 position, Quaternion rotation,
                                      EquipmentVisualProfile pileVisual,
                                      EquipmentVisualProfile footprintVisual)
        {
            var go = new GameObject("SaltPile");
            go.transform.SetPositionAndRotation(position, rotation);

            EquipmentVisualFactory.Build(pileVisual, go.transform, "Pile", out _);

            var pile = go.AddComponent<SaltPile>();
            pile._footprintVisual = footprintVisual;
            return pile;
        }

        /// <summary>
        /// The ghost moved. Any pile it passed through takes a print.
        ///
        /// <para>
        /// Called by the ghost when it has actually covered ground, not per frame. Nothing
        /// called the old version of this at all, which is the reason a mechanic built out of
        /// two classes and an objective had never once fired.
        /// </para>
        /// </summary>
        public static void NotifyGhostStep(Vector3 from, Vector3 to)
        {
            for (int i = 0; i < Alive.Count; i++)
            {
                var pile = Alive[i];
                if (pile == null || pile._triggered)
                    continue;

                if (pile.OverlapsPoint(from) || pile.OverlapsPoint(to))
                    pile.TriggerFootprint(from, to);
            }
        }

        /// <summary>
        /// Leaves a print in the direction the ghost was travelling. Invisible: it is a
        /// disturbance in salt, and finding it is the UV lamp's job.
        /// </summary>
        public void TriggerFootprint(Vector3 fromPosition, Vector3 toPosition)
        {
            if (_triggered)
                return;

            _triggered = true;

            Vector3 heading = toPosition - fromPosition;
            Quaternion facing = heading.sqrMagnitude > 1e-6f
                ? Quaternion.LookRotation(heading.normalized, Vector3.up)
                : transform.rotation;

            var go = new GameObject("SaltFootprint");
            go.transform.SetPositionAndRotation((fromPosition + toPosition) * 0.5f, facing);

            EquipmentVisualFactory.Build(_footprintVisual, go.transform, "Print", out _);

            // The lamp finds traces with an overlap sphere, so a trace with no collider is a
            // trace no lamp can ever find.
            var trigger = go.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.3f;

            // Added rather than revealed. The old version called Reveal() the moment the print
            // appeared, which put the evidence on the floor in plain sight and made the UV lamp
            // decorative.
            go.AddComponent<EvidenceReveal>();

            if (footprintLifetime > 0f)
                Destroy(go, footprintLifetime);
        }

        public bool OverlapsPoint(Vector3 point) =>
            Vector3.Distance(transform.position, point) <= triggerRadius;
    }
}
