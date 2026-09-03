using UnityEngine;

namespace CatchIfYouCan.Development.Labs
{
    /// <summary>The rig: crouch, strafe, blink, head follow, the flashlight arm. Neutral room, known scale, nothing to distract the eye.</summary>
    [AddComponentMenu("Catch If You Can/Development/CharacterLabInstaller")]
    public sealed class CharacterLabInstaller : DevelopmentLabInstaller
    {
        public override DevelopmentLab Lab => DevelopmentLab.Character;

        protected override void BuildFixtures()
        {
            BuildFloor(Vector3.zero, new Vector2(10f, 10f));
            BuildMetreGrid(Vector3.zero, 10);
            BuildHumanReference(new Vector3(2f, 0f, 0f));
            BuildMarker("DEV_PlayerSpawn", new Vector3(0f, 0.05f, -2f));
        }

        protected override string DescribeState() => "Floor 10x10, 1 m grid, 1.86 m reference, spawn at (0, 0.05, -2).";
    }
}
