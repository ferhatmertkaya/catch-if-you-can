using UnityEngine;

namespace CatchIfYouCan.Development.Labs
{
    /// <summary>A shell only. No netcode package is installed, so this lab exists to be filled in later rather than to pretend it works now.</summary>
    [AddComponentMenu("Catch If You Can/Development/NetworkLabInstaller")]
    public sealed class NetworkLabInstaller : DevelopmentLabInstaller
    {
        public override DevelopmentLab Lab => DevelopmentLab.Network;

        protected override void BuildFixtures()
        {
            BuildFloor(Vector3.zero, new Vector2(20f, 20f));
            for (int i = 0; i < 4; i++)
            {
                float angle = i * 90f * Mathf.Deg2Rad;
                BuildMarker("DEV_NetworkSpawn_" + i,
                            new Vector3(Mathf.Sin(angle) * 4f, 0.05f, Mathf.Cos(angle) * 4f));
            }
        }

        protected override string DescribeState() => "Floor 20x20, four spawn pads. NETWORKING NOT INSTALLED.";
    }
}
