using CatchIfYouCan.Ghost;
using UnityEngine;

namespace CatchIfYouCan.Development.Labs
{
    /// <summary>Ghost state, perception and hunts in a fixed hand-built house, so a ghost bug is never confused with a generator bug.</summary>
    [AddComponentMenu("Catch If You Can/Development/GhostLabInstaller")]
    public sealed class GhostLabInstaller : DevelopmentLabInstaller
    {
        [Tooltip("Ghost id spawned on entry. Empty spawns nothing, which is what you want " +
                 "when the question is about the room rather than the ghost.")]
        [SerializeField] private string ghostId = "the_wanderer";

        public override DevelopmentLab Lab => DevelopmentLab.Ghost;

        private string _spawned = "nothing";

        protected override void BuildFixtures()
        {
            BuildFloor(Vector3.zero, new Vector2(26f, 26f));
            BuildMarker(PlayerSpawnMarkerName, new Vector3(0f, 0.05f, -10f));

            BuildFourRoomHouse();
            SpawnGhost();
        }

        /// <summary>
        /// Four rooms in a square, each with a doorway onto the hall. Hand-built and fixed on
        /// purpose: the procedural generator produces a different house every seed, and a
        /// ghost that behaves oddly in a house nobody has seen before is two bugs at once.
        /// Every room gets a spawn marker so a hunt can be started from a known distance.
        /// </summary>
        private void BuildFourRoomHouse()
        {
            var offsets = new[]
            {
                new Vector3(-6f, 0f, 6f), new Vector3(6f, 0f, 6f),
                new Vector3(-6f, 0f, -2f), new Vector3(6f, 0f, -2f),
            };
            var names = new[] { "NW", "NE", "SW", "SE" };

            for (int i = 0; i < offsets.Length; i++)
            {
                BuildRoomShell("DEV_Room_" + names[i], offsets[i], new Vector2(8f, 6f),
                               height: 3f, doorwayWidth: 1.4f);
                BuildMarker("DEV_GhostSpawn_" + names[i], offsets[i]);
                BuildLabel(names[i], offsets[i] + new Vector3(0f, 2.4f, 0f));
            }

            BuildMarker("DEV_GhostRoom", offsets[0]);
        }

        private void SpawnGhost()
        {
            if (string.IsNullOrEmpty(ghostId))
                return;

            GhostDefinition definition = null;
            var all = GhostDefinitionFactory.CreateAllDefaultGhosts();
            for (int i = 0; all != null && i < all.Length; i++)
            {
                if (all[i] != null &&
                    string.Equals(all[i].Id, ghostId, System.StringComparison.Ordinal))
                {
                    definition = all[i];
                    break;
                }
            }

            if (definition == null)
            {
                Core.CIYCLog.Warn("Ghost lab: no ghost definition with id '" + ghostId + "'.");
                return;
            }

            // Through the real factory, not a lab-local ghost. Whether the bundled model
            // loads at all is one of the things this lab is for, and a lab that built its
            // own capsule would answer that question wrong every time.
            var ghost = GhostFactory.Create(definition, new Vector3(-6f, 0f, 6f));
            _spawned = ghost != null ? ghostId : "nothing (factory returned null)";
        }

        protected override string DescribeState() =>
            "Floor 26x26, four 8x6 rooms with 1.4 m doorways and a spawn marker each, " +
            "ghost spawned: " + _spawned + ".";
    }
}
