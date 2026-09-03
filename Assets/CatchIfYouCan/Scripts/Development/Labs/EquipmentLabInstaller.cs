using CatchIfYouCan.Equipment;
using CatchIfYouCan.Evidence;
using UnityEngine;

namespace CatchIfYouCan.Development.Labs
{
    /// <summary>Holding, using, dropping and picking equipment back up, with nothing else in the room to explain a result away.</summary>
    [AddComponentMenu("Catch If You Can/Development/EquipmentLabInstaller")]
    public sealed class EquipmentLabInstaller : DevelopmentLabInstaller
    {
        public override DevelopmentLab Lab => DevelopmentLab.Equipment;

        private int _implemented;
        private int _placeholders;

        protected override void BuildFixtures()
        {
            BuildFloor(Vector3.zero, new Vector2(18f, 12f));
            BuildMetreGrid(Vector3.zero, 12);
            BuildMarker(PlayerSpawnMarkerName, new Vector3(0f, 0.05f, -4f));

            // A back wall to throw things at. A dropped torch that lands on open floor tells
            // you it falls; one that bounces off a wall first tells you what it does when it
            // hits something, which is the case that actually goes wrong.
            BuildWall("DEV_DropWall", new Vector3(0f, 1.5f, 5.5f), new Vector3(18f, 3f, 0.2f));

            BuildBench();
            BuildDropZone();
            BuildEvidenceTargets();
            BuildReadout();

            // The same starter kit the investigation gives the player, so the HUD row in the
            // lab shows what it shows in the game.
            EquipmentManager.Instance?.GiveStarterLoadout();
        }

        /// <summary>
        /// One plinth per catalogue entry, in catalogue order, each labelled with its id and
        /// with whether it is a real item or a DEV_PLACEHOLDER. Side by side is the point:
        /// "the thermometer looks fine" stops being sayable when it is standing next to the
        /// torch and is visibly a grey box.
        /// </summary>
        private void BuildBench()
        {
            var all = EquipmentDefinitionFactory.All();
            if (all == null || all.Length == 0)
                return;

            var bench = new GameObject("DEV_EquipmentBench");
            float spacing = 1.2f;
            float start = -(all.Length - 1) * spacing * 0.5f;

            for (int i = 0; i < all.Length; i++)
            {
                var definition = all[i];
                if (definition == null)
                    continue;

                bool real = definition.Prefab != null || definition.Id == "flashlight";
                if (real)
                    _implemented++;
                else
                    _placeholders++;

                var plinth = BuildPlinth(
                    definition.Id + (real ? "" : "\nDEV_PLACEHOLDER"),
                    new Vector3(start + i * spacing, 0f, 3f), bench.transform);

                if (definition.Prefab != null)
                {
                    var item = Instantiate(definition.Prefab,
                                           plinth.position + new Vector3(0f, 0.55f, 0f),
                                           Quaternion.identity, plinth);
                    item.name = "DEV_" + definition.Id;
                }
            }
        }

        /// <summary>
        /// The three things equipment is supposed to find: an EMF source, a UV trace and a
        /// generic evidence target. Without them the detectors in this lab are held, switched
        /// on, and pointed at nothing, which proves only that they turn on.
        /// </summary>
        private static void BuildEvidenceTargets()
        {
            // The EMF spot destroys itself when it decays, which is what it does in the game.
            // A long duration rather than an infinite one, so the lab shows the decay too.
            var emfGo = new GameObject("DEV_EMFSpot");
            emfGo.transform.position = new Vector3(-4f, 1f, 0f);
            emfGo.AddComponent<EMFSpot>().Initialize(1f, 600f, 0.001f, 4f);
            BuildLabel("EMF SOURCE\n4 m radius", new Vector3(-4f, 1.4f, 0f));

            BuildUvTarget(new Vector3(4f, 1f, 0f));

            // A button that raises evidence directly, for the case where the question is what
            // the journal and the objectives do with a find rather than how it was found.
            BuildLabel("EVIDENCE TARGETS", new Vector3(0f, 2.2f, 0f));
        }

        /// <summary>
        /// A handprint that is invisible until a UV light is pointed at it. Built with the
        /// project's own authored materials, so if MAT_UVEvidence was stripped from the build
        /// this fixture is the thing that says so.
        /// </summary>
        private static void BuildUvTarget(Vector3 at)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "DEV_UVTarget";
            quad.transform.position = at;
            quad.transform.localScale = new Vector3(0.6f, 0.6f, 1f);

            var reveal = quad.AddComponent<EvidenceReveal>();
            WireLabField(reveal, "targetRenderer", quad.GetComponent<Renderer>());
            WireLabField(reveal, "revealedMaterial", Art.RuntimeMaterialFactory.GetUVEvidence());
            WireLabField(reveal, "revealLifetime", 600f);

            BuildLabel("UV TRACE", at + new Vector3(0f, 0.5f, 0f));
        }

        /// <summary>
        /// Battery, durability and what the player is actually holding. All three are numbers
        /// that decide behaviour and none of them are visible in the room.
        /// </summary>
        private void BuildReadout()
        {
            Readout()
                .Line(() =>
                {
                    var inventory = Core.LocalPlayerService.GetPlayerComponent<Player.PlayerInventory>();
                    if (inventory == null)
                        return "Held: no player";

                    var item = inventory.GetSelectedItem();
                    return "Held: slot " + inventory.SelectedIndex + " = " +
                           (item != null ? item.DeviceId : "empty");
                })
                .Line(() =>
                {
                    var item = Core.LocalPlayerService
                        .GetPlayerComponent<Player.PlayerInventory>()?.GetSelectedItem();
                    if (item == null)
                        return "Battery: -";

                    return "Battery: " + (item.BatteryPercent * 100f).ToString("F0") + "%  " +
                           "Durability: " + item.Durability.ToString("F0") + "/" +
                           item.MaxDurability.ToString("F0") +
                           "  active=" + item.IsActive;
                })
                .Line(() =>
                {
                    var manager = EquipmentManager.Instance;
                    return "Loadout: " + (manager != null ? manager.Loadout.Count : 0) + " items";
                })
                .Line(() =>
                {
                    var evidence = EvidenceManager.Instance;
                    return "Evidence found: " +
                           (evidence != null ? evidence.FoundEvidence.Count : 0);
                })
                // No "drain the battery" button. The only way to reach BatteryLevel from
                // outside is its compiler-generated backing field, and reflecting on a name
                // the compiler chose is the kind of thing that works until it does not. The
                // battery is watched here and run down by using the item, as it is in the game.
                .Button("Register EMFSurge", () =>
                    EvidenceManager.Instance?.RegisterEvidence(EvidenceType.EMFSurge))
                .Button("Reset evidence", () => EvidenceManager.Instance?.ResetMission());
        }

        /// <summary>A marked square to drop into, so "where did it land" has an answer.</summary>
        private static void BuildDropZone()
        {
            var pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pad.name = "DEV_DropZone";
            pad.transform.position = new Vector3(0f, 0.005f, 0f);
            pad.transform.localScale = new Vector3(2f, 0.01f, 2f);

            var collider = pad.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            BuildLabel("DROP ZONE", new Vector3(0f, 0.1f, 0f), pad.transform);
        }

        protected override string DescribeState() =>
            "Floor 18x12, 1 m grid, drop wall at z=5.5, drop zone at the origin, bench of " +
            _implemented + " implemented and " + _placeholders + " placeholder items.";
    }
}
