using UnityEngine;

namespace CatchIfYouCan.Equipment
{
    /// <summary>
    /// Marks the child that <see cref="EquipmentVisualFactory"/> built, so an item that was
    /// CLONED can find the visual it already has.
    ///
    /// <para>
    /// <b>This exists because a reference does not survive Instantiate and a GameObject does.</b>
    /// <c>HeldEquipmentBase.CarriedRoot</c> is an auto-property and <c>_dropCollider</c> is a
    /// non-public field, so Unity serializes neither - but the child object and the collider
    /// component they point at are both copied by <c>Instantiate</c> like everything else. On
    /// the clone the objects are therefore present and the references are null, which is the
    /// one combination <c>BuildCarried</c>'s <c>if (CarriedRoot != null) return;</c> guard reads
    /// as "nothing has been built yet".
    /// </para>
    ///
    /// <para>
    /// Every equipment item in this project is a clone - <c>EquipmentRuntimeFactory</c> builds
    /// one live template per id and <c>Instantiate</c>s it - so every item built a SECOND visual
    /// and a SECOND drop capsule in <c>Start</c>, exactly on top of the first. Two coincident
    /// copies of the same mesh look like one object, which is why it was never visible as
    /// doubling; what it did show up as was an item that could not be picked up.
    /// </para>
    ///
    /// <para>
    /// A name would not do: the visual is named after the item's display name, and so is
    /// anything else a subclass might hang there. A component is the identity that survives the
    /// copy, which is the whole point.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EquipmentVisualRoot : MonoBehaviour
    {
    }
}
