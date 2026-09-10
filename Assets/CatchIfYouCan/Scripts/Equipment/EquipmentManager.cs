using System.Collections.Generic;
using UnityEngine;
using CatchIfYouCan.Utilities;

namespace CatchIfYouCan.Equipment
{
    /// <summary>
    /// The loadout: which equipment the player brought, as data. Not what they are holding.
    ///
    /// <para>
    /// This used to be a second held-item state machine running alongside
    /// <see cref="Player.PlayerInventory"/>. Both owned a hand anchor, both equipped and
    /// unequipped, both raised EquipmentChanged, and both believed they knew what was in the
    /// player's hand - while only the inventory was ever given the real torch. Two answers
    /// to "what am I holding" is not a redundancy, it is a bug waiting for the second item
    /// to exist.
    /// </para>
    ///
    /// <para>
    /// The inventory won because it is the one the runtime player actually uses, and it
    /// already owns slots, hand-anchor binding, selection and dropping. What is left here is
    /// the part the inventory never did: the shop's and the mission screen's idea of what
    /// the player owns for this run.
    /// </para>
    /// </summary>
    public class EquipmentManager : SingletonBehaviour<EquipmentManager>
    {
        private readonly List<EquipmentDefinition> _loadout = new List<EquipmentDefinition>();

        /// <summary>What the player brought on this run, in order. Read-only to callers.</summary>
        public IReadOnlyList<EquipmentDefinition> Loadout => _loadout;

        protected override void Awake()
        {
            base.Awake();
            Core.ServiceLocator.Register(this);
        }

        protected override void OnDestroy()
        {
            if (Instance == this)
                Core.ServiceLocator.Unregister<EquipmentManager>();
            base.OnDestroy();
        }

        public void SetLoadout(IEnumerable<EquipmentDefinition> definitions)
        {
            _loadout.Clear();
            if (definitions == null)
                return;

            foreach (var def in definitions)
            {
                if (def != null)
                    _loadout.Add(def);
            }
        }

        public EquipmentDefinition GetLoadoutSlot(int index) =>
            index >= 0 && index < _loadout.Count ? _loadout[index] : null;

        public bool LoadoutContains(string equipmentId)
        {
            for (int i = 0; i < _loadout.Count; i++)
                if (_loadout[i] != null &&
                    string.Equals(_loadout[i].Id, equipmentId, System.StringComparison.Ordinal))
                    return true;

            return false;
        }

        /// <summary>
        /// The default kit. Sets the loadout only - it does not put anything in anyone's
        /// hand, because whose hand that would be is the inventory's business.
        /// </summary>
        public void GiveStarterLoadout()
        {
            var starter = new List<EquipmentDefinition>();

            // The four the vertical slice is built on. Together they can produce a complete
            // evidence combination, which is what the mission is scored on - three of them fit
            // in the inventory at once and the fourth is swapped for at the van.
            AddIfFound(starter, EquipmentIds.Flashlight);
            AddIfFound(starter, EquipmentIds.EmfDetector);
            AddIfFound(starter, EquipmentIds.UvLight);
            AddIfFound(starter, EquipmentIds.Thermometer);

            SetLoadout(starter);
        }

        private static void AddIfFound(List<EquipmentDefinition> into, string id)
        {
            var definition = EquipmentDefinitionFactory.GetById(id);
            if (definition != null)
                into.Add(definition);
        }
    }
}
