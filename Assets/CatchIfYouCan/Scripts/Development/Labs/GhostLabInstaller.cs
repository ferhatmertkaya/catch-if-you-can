using UnityEngine;

namespace CatchIfYouCan.Development.Labs
{
    /// <summary>Ghost state, perception and hunts in a fixed hand-built house, so a ghost bug is never confused with a generator bug.</summary>
    [AddComponentMenu("Catch If You Can/Development/GhostLabInstaller")]
    public sealed class GhostLabInstaller : DevelopmentLabInstaller
    {
        public override DevelopmentLab Lab => DevelopmentLab.Ghost;

        protected override void BuildFixtures()
        {
            BuildFloor(Vector3.zero, new Vector2(20f, 20f));
            BuildMarker("DEV_PlayerSpawn", new Vector3(0f, 0.05f, -6f));
            BuildMarker("DEV_GhostSpawn", new Vector3(0f, 0f, 6f));
        }

        protected override string DescribeState() => "Floor 20x20, player and ghost spawn markers.";
    }
}
