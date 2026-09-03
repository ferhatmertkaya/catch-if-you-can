using UnityEngine;

namespace CatchIfYouCan.Development.Labs
{
    /// <summary>Forward+ behaviour, the mirror, post-processing, and the shader board that catches stripping before a device build does.</summary>
    [AddComponentMenu("Catch If You Can/Development/LightingLabInstaller")]
    public sealed class LightingLabInstaller : DevelopmentLabInstaller
    {
        public override DevelopmentLab Lab => DevelopmentLab.Lighting;

        protected override void BuildFixtures()
        {
            BuildFloor(Vector3.zero, new Vector2(20f, 14f));
            BuildMarker("DEV_PlayerSpawn", new Vector3(0f, 0.05f, -5f));
        }

        protected override string DescribeState() => "Floor 20x14, spawn at (0, 0.05, -5).";
    }
}
