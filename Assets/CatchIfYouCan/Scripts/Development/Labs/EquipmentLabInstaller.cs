using CatchIfYouCan.Equipment;
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
