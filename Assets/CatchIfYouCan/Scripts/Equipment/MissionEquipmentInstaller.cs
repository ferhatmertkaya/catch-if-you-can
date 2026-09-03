using System.Collections.Generic;
using CatchIfYouCan.Core;
using CatchIfYouCan.Player;
using UnityEngine;

namespace CatchIfYouCan.Equipment
{
    /// <summary>
    /// Puts the mission's loadout into the player's hands.
    ///
    /// <para>
    /// <b>Nothing did this.</b> The loadout was data and only data: <c>GiveStarterLoadout</c>
    /// filled a list of definitions, <c>InventorySlotSelector</c> read icons off it, and no code
    /// anywhere on the mission path ever turned a definition into an object. The only equipment
    /// a player actually held was the torch <see cref="PlayerFactory"/> builds into their hand.
    /// So the EMF detector, the UV lamp and the thermometer were implemented, catalogued,
    /// validated and unreachable - which means the evidence they produce was unreachable too,
    /// and with it the identification the mission is scored on.
    /// </para>
    ///
    /// <para>
    /// <see cref="EquipmentRuntimeFactory"/> already knew how to build every one of them. This
    /// is the call that was missing.
    /// </para>
    /// </summary>
    public static class MissionEquipmentInstaller
    {
        private const string LogTag = "[CIYC][Equipment] ";

        /// <summary>
        /// Fills the player's free slots from the loadout, in order, and says what did not fit.
        ///
        /// <para>
        /// The inventory holds <see cref="PlayerInventory.SlotCount"/> items and a loadout may
        /// name more; that is the design, not an error - the rest are chosen from at the van.
        /// It is reported at info level so that "why am I not carrying the thermometer" has an
        /// answer in the log rather than being a mystery.
        /// </para>
        /// </summary>
        public static int InstallLoadout(PlayerInventory inventory)
        {
            if (inventory == null)
            {
                CIYCLog.Error(LogTag + "No inventory to install a loadout into.");
                return 0;
            }

            EquipmentManager manager = EquipmentManager.Instance;
            if (manager == null || manager.Loadout == null || manager.Loadout.Count == 0)
            {
                CIYCLog.Warn(LogTag + "The mission started with an empty loadout, so the player " +
                             "carries only what PlayerFactory built.");
                return 0;
            }

            int installed = 0;
            var skipped = new List<string>();

            foreach (EquipmentDefinition definition in manager.Loadout)
            {
                if (definition == null)
                    continue;

                if (!EquipmentRuntimeFactory.HasRuntimePath(definition.Id))
                {
                    // Deliberately not built. An item with no runtime path becomes a
                    // DEV_PLACEHOLDER, and handing the player an inert grey box that looks like
                    // a thermometer is worse than not handing them one.
                    skipped.Add(definition.Id + " (no runtime implementation)");
                    continue;
                }

                if (AlreadyCarrying(inventory, definition.Id))
                {
                    skipped.Add(definition.Id + " (already carried)");
                    continue;
                }

                if (!inventory.HasFreeSlot)
                {
                    skipped.Add(definition.Id + " (no free slot)");
                    continue;
                }

                EquipmentBase item = Build(definition);
                if (item == null)
                {
                    skipped.Add(definition.Id + " (factory produced nothing)");
                    continue;
                }

                if (inventory.TryAddItem(item))
                {
                    installed++;
                }
                else
                {
                    skipped.Add(definition.Id + " (inventory refused it)");
                    Object.Destroy(item.gameObject);
                }
            }

            CIYCLog.Info(LogTag + "Loadout installed: " + installed + " of " +
                         manager.Loadout.Count + " item(s) carried" +
                         (skipped.Count > 0 ? "; not carried: " + string.Join(", ", skipped) : "") + ".");

            return installed;
        }

        /// <summary>
        /// Whether this player already has one of these - the torch, normally, which
        /// <see cref="PlayerFactory"/> puts in their hand before any loadout is consulted.
        /// Handing them a second is how this project ended up with two flashlights once already.
        /// </summary>
        private static bool AlreadyCarrying(PlayerInventory inventory, string equipmentId)
        {
            for (int i = 0; i < PlayerInventory.SlotCount; i++)
            {
                EquipmentBase held = inventory.GetSlot(i);
                if (held != null && held.Definition != null &&
                    string.Equals(held.Definition.Id, equipmentId, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// One instance from the definition's runtime template.
        ///
        /// <para>
        /// The template is switched off after it is built. <c>EquipmentRuntimeFactory</c> makes
        /// it as a live GameObject rather than a real prefab asset - there is nowhere else to
        /// put it in a code-built project - and a live template is an extra torch lying at the
        /// world origin that the player can walk up to and take.
        /// </para>
        /// </summary>
        private static EquipmentBase Build(EquipmentDefinition definition)
        {
            EquipmentRuntimeFactory.EnsureRuntimePrefab(definition);

            GameObject template = definition.Prefab;
            if (template == null)
            {
                CIYCLog.Error(LogTag + "No runtime template for '" + definition.Id + "'.");
                return null;
            }

            if (template.activeSelf && template.scene.IsValid())
                template.SetActive(false);

            var instance = Object.Instantiate(template);
            instance.name = definition.DisplayName;
            instance.SetActive(true);

            var equipment = instance.GetComponent<EquipmentBase>();
            if (equipment == null)
            {
                CIYCLog.Error(LogTag + "The runtime template for '" + definition.Id +
                              "' carries no EquipmentBase.");
                Object.Destroy(instance);
                return null;
            }

            return equipment;
        }
    }
}
