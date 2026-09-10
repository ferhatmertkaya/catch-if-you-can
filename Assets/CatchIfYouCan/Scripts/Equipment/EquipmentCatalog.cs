using UnityEngine;

namespace CatchIfYouCan.Equipment
{
    /// <summary>
    /// Every piece of equipment in the game, and the id-to-definition lookup everything else
    /// uses.
    ///
    /// <para>
    /// The definitions were built in code, in <see cref="EquipmentDefinitionFactory"/>, and
    /// <c>GetById</c> rebuilt all eleven of them on every call and threw ten away. Worse, each
    /// call handed back a <b>different</b> ScriptableObject instance for the same id, so
    /// nothing could ever compare two definitions by reference and the battery charge written
    /// onto one was invisible to the next caller.
    /// </para>
    ///
    /// <para>
    /// An asset fixes both: there is one definition per id and it is the same object every
    /// time. It is an explicit ordered list rather than a folder scan, because a scan answers
    /// "whatever happened to be imported", which is a different set on two machines and
    /// therefore a different set on two clients - the exact thing a content hash exists to
    /// catch. The order is also what a compact network encoding would index into.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "EquipmentCatalog", menuName = "Catch If You Can/Equipment Catalog")]
    public sealed class EquipmentCatalog : ScriptableObject
    {
        [Tooltip("Order is meaningful: it is the index a compact network encoding would use.")]
        [SerializeField] private EquipmentDefinition[] equipment = new EquipmentDefinition[0];

        public EquipmentDefinition[] Equipment => equipment;
        public int Count => equipment != null ? equipment.Length : 0;

        public EquipmentDefinition Resolve(string equipmentId)
        {
            if (equipment == null || string.IsNullOrEmpty(equipmentId))
                return null;

            for (int i = 0; i < equipment.Length; i++)
            {
                if (equipment[i] != null &&
                    string.Equals(equipment[i].Id, equipmentId, System.StringComparison.Ordinal))
                    return equipment[i];
            }

            return null;
        }

        public int IndexOf(string equipmentId)
        {
            if (equipment == null)
                return -1;

            for (int i = 0; i < equipment.Length; i++)
                if (equipment[i] != null &&
                    string.Equals(equipment[i].Id, equipmentId, System.StringComparison.Ordinal))
                    return i;

            return -1;
        }
    }
}
