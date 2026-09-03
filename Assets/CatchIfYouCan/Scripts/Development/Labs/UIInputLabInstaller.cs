using UnityEngine;

namespace CatchIfYouCan.Development.Labs
{
    /// <summary>The touch HUD, safe areas and the raw input values, with a player present so the controls drive something.</summary>
    [AddComponentMenu("Catch If You Can/Development/UIInputLabInstaller")]
    public sealed class UIInputLabInstaller : DevelopmentLabInstaller
    {
        public override DevelopmentLab Lab => DevelopmentLab.UIInput;

        protected override void BuildFixtures()
        {
            BuildFloor(Vector3.zero, new Vector2(10f, 10f));
            BuildMarker("DEV_PlayerSpawn", new Vector3(0f, 0.05f, -2f));
        }

        protected override string DescribeState() => "Floor 10x10, spawn at (0, 0.05, -2).";
    }
}
