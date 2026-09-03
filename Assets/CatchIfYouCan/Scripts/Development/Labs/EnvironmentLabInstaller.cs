using UnityEngine;

namespace CatchIfYouCan.Development.Labs
{
    /// <summary>Prop sizing, import orientation and vertex cost, against a known grid and a known human.</summary>
    [AddComponentMenu("Catch If You Can/Development/EnvironmentLabInstaller")]
    public sealed class EnvironmentLabInstaller : DevelopmentLabInstaller
    {
        public override DevelopmentLab Lab => DevelopmentLab.Environment;

        protected override void BuildFixtures()
        {
            BuildFloor(Vector3.zero, new Vector2(16f, 16f));
            BuildMetreGrid(Vector3.zero, 16);
            BuildHumanReference(new Vector3(-3f, 0f, 0f));
            BuildMarker("DEV_PlayerSpawn", new Vector3(0f, 0.05f, -5f));
        }

        protected override string DescribeState() => "Floor 16x16, 1 m grid, 1.86 m reference.";
    }
}
