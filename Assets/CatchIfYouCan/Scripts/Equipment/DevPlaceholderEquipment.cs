using UnityEngine;

namespace CatchIfYouCan.Equipment
{
    /// <summary>
    /// A deliberately non-functional stand-in for an equipment ID that has no implementation yet.
    ///
    /// <para>
    /// This exists so that an unimplemented item is obvious rather than silently wrong. The
    /// runtime factory used to fall back to the flashlight for every ID it did not recognise,
    /// so a thermometer, an EVP recorder and a spirit box all became torches - and a torch that
    /// works is far harder to notice than a box labelled DEV_PLACEHOLDER that does nothing.
    /// </para>
    ///
    /// <para>
    /// It draws no evidence, carries no interference and refuses to be used. Anything that ships
    /// with one of these in the player's hands is not finished.
    /// </para>
    /// </summary>
    public sealed class DevPlaceholderEquipment : EquipmentBase
    {
        private bool _warned;

        protected override float GetInterferenceMultiplier() => 0f;

        protected override void OnUse()
        {
            if (_warned)
                return;

            _warned = true;
            Debug.LogWarning(
                $"[Equipment] '{DeviceId}' is a DEV_PLACEHOLDER with no implementation. " +
                "Using it does nothing.", this);
        }
    }
}
