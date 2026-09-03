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

        private readonly System.Collections.Generic.List<Interaction.LightController> _lights =
            new System.Collections.Generic.List<Interaction.LightController>();

        private string _spawned = "nothing";
        private bool _navMesh;

        protected override void BuildFixtures()
        {
            BuildFloor(Vector3.zero, new Vector2(26f, 26f));
            BuildMarker(PlayerSpawnMarkerName, new Vector3(0f, 0.05f, -10f));

            BuildFourRoomHouse();
            BuildRoomFixtures();
            BuildNavMesh();
            SpawnGhost();
            BuildReadout();
        }

        /// <summary>
        /// A door and a switchable light in each room. A ghost's interactions are almost all
        /// with doors and lights, so a house with neither can only be walked around in.
        /// </summary>
        private void BuildRoomFixtures()
        {
            var offsets = new[]
            {
                new Vector3(-6f, 0f, 6f), new Vector3(6f, 0f, 6f),
                new Vector3(-6f, 0f, -2f), new Vector3(6f, 0f, -2f),
            };

            for (int i = 0; i < offsets.Length; i++)
            {
                var doorAt = offsets[i] + new Vector3(-0.7f, 0f, 3f);
                var hinge = new GameObject("DEV_RoomDoor_" + i);
                hinge.transform.position = doorAt;

                var leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leaf.name = "Leaf";
                leaf.transform.SetParent(hinge.transform, false);
                leaf.transform.localPosition = new Vector3(0.7f, 1f, 0f);
                leaf.transform.localScale = new Vector3(1.4f, 2f, 0.08f);
                hinge.AddComponent<Interaction.InteractiveDoor>();

                var lightGo = new GameObject("DEV_RoomLight_" + i);
                lightGo.transform.position = offsets[i] + new Vector3(0f, 2.6f, 0f);
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = 8f;
                light.intensity = 1.6f;
                light.color = new Color(1f, 0.88f, 0.7f);

                var controller = lightGo.AddComponent<Interaction.LightController>();
                WireLabField(controller, "lights", new[] { light });
                _lights.Add(controller);
            }
        }

        /// <summary>
        /// Bakes navigation over the fixtures. A ghost that cannot path is a ghost that stands
        /// still, and a lab where it stands still for that reason wastes an afternoon.
        /// </summary>
        private void BuildNavMesh()
        {
            var builder = Object.FindAnyObjectByType<Procedural.NavMeshRuntimeBuilder>();
            if (builder == null)
                builder = new GameObject("DEV_NavMesh").AddComponent<Procedural.NavMeshRuntimeBuilder>();

            _navMesh = builder != null;
        }

        /// <summary>
        /// State, distance and evidence. Everything a ghost does is decided by a state machine
        /// that is invisible from inside the room.
        /// </summary>
        private void BuildReadout()
        {
            Readout()
                .Line(() =>
                {
                    var controller = Object.FindAnyObjectByType<GhostController>();
                    return controller != null
                        ? "Ghost: " + controller.CurrentState
                        : "Ghost: none in scene";
                })
                .Line(() =>
                {
                    var controller = Object.FindAnyObjectByType<GhostController>();
                    var player = Core.LocalPlayerService.RootTransform;
                    if (controller == null || player == null)
                        return "Distance: -";

                    return "Distance: " +
                           Vector3.Distance(controller.transform.position, player.position)
                               .ToString("F1") + " m";
                })
                .Line(() =>
                {
                    var evidence = Evidence.EvidenceManager.Instance;
                    if (evidence == null)
                        return "Evidence: no manager";

                    return "Evidence: " + string.Join(", ", evidence.FoundEvidence);
                })
                .Line(() => "NavMesh builder present: " + _navMesh)
                .Button("Make noise at player", () =>
                {
                    var player = Core.LocalPlayerService.RootTransform;
                    if (player != null)
                        Core.GameEvents.NoiseGenerated(1f, player.position);
                })
                .Button("Toggle all lights", () =>
                {
                    for (int i = 0; i < _lights.Count; i++)
                        _lights[i]?.Toggle();
                })
                .Button("Reset evidence", () => Evidence.EvidenceManager.Instance?.ResetMission());
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
            "a door and a switchable light per room, navmesh builder " +
            (_navMesh ? "present" : "MISSING") + ", ghost spawned: " + _spawned + ".";
    }
}
