using UnityEngine;

namespace CatchIfYouCan.Development.Labs
{
    /// <summary>One of every IInteractable in a row, so prompt text, hold duration and reach can be compared side by side.</summary>
    [AddComponentMenu("Catch If You Can/Development/InteractionLabInstaller")]
    public sealed class InteractionLabInstaller : DevelopmentLabInstaller
    {
        public override DevelopmentLab Lab => DevelopmentLab.Interaction;

        protected override void BuildFixtures()
        {
            BuildFloor(Vector3.zero, new Vector2(16f, 8f));
            BuildMarker("DEV_PlayerSpawn", new Vector3(0f, 0.05f, -3f));
        }

        protected override string DescribeState() => "Floor 16x8, spawn at (0, 0.05, -3).";
    }
}
