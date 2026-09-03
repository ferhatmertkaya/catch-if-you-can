using UnityEngine;

namespace CatchIfYouCan.Development.Labs
{
    /// <summary>Holding, using, dropping and picking equipment back up, with nothing else in the room to explain a result away.</summary>
    [AddComponentMenu("Catch If You Can/Development/EquipmentLabInstaller")]
    public sealed class EquipmentLabInstaller : DevelopmentLabInstaller
    {
        public override DevelopmentLab Lab => DevelopmentLab.Equipment;

        protected override void BuildFixtures()
        {
            BuildFloor(Vector3.zero, new Vector2(12f, 12f));
            BuildMarker("DEV_PlayerSpawn", new Vector3(0f, 0.05f, -3f));
        }

        protected override string DescribeState() => "Floor 12x12, spawn at (0, 0.05, -3).";
    }
}
