using UnityEngine;

namespace CatchIfYouCan.Development.Labs
{
    /// <summary>Room zones, portals, occlusion, reverb and the glass filter, with the lobby's own acoustic numbers.</summary>
    [AddComponentMenu("Catch If You Can/Development/AudioLabInstaller")]
    public sealed class AudioLabInstaller : DevelopmentLabInstaller
    {
        public override DevelopmentLab Lab => DevelopmentLab.Audio;

        protected override void BuildFixtures()
        {
            BuildFloor(Vector3.zero, new Vector2(24f, 12f));
            BuildMarker("DEV_PlayerSpawn", new Vector3(0f, 0.05f, -4f));
        }

        protected override string DescribeState() => "Floor 24x12, spawn at (0, 0.05, -4).";
    }
}
