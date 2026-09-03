using UnityEngine;

namespace CatchIfYouCan.Development.Labs
{
    /// <summary>The rig: crouch, strafe, blink, head follow, the flashlight arm. Neutral room, known scale, nothing to distract the eye.</summary>
    [AddComponentMenu("Catch If You Can/Development/CharacterLabInstaller")]
    public sealed class CharacterLabInstaller : DevelopmentLabInstaller
    {
        /// <summary>Capsule height of the player, in metres. PlayerFactory's own number.</summary>
        private const float PlayerCapsuleHeight = 1.86f;

        /// <summary>Eye height of the player, in metres. PlayerFactory's own number.</summary>
        private const float PlayerEyeHeight = 1.68f;

        protected override void BuildFixtures()
        {
            BuildFloor(Vector3.zero, new Vector2(14f, 14f));
            BuildMetreGrid(Vector3.zero, 14);
            BuildHumanReference(new Vector3(2f, 0f, 0f));
            BuildLabel("1.86 m REFERENCE", new Vector3(2f, PlayerCapsuleHeight + 0.15f, 0f));
            BuildMarker(PlayerSpawnMarkerName, new Vector3(0f, 0.05f, -3f));

            BuildEyeLine();
            BuildCrouchGate();
            BuildStrafeLane();
        }

        public override DevelopmentLab Lab => DevelopmentLab.Character;

        /// <summary>
        /// A rail at exactly eye height. The camera is either level with it or it is not, which
        /// turns "the camera feels like it is in his neck" into something you can look at.
        /// </summary>
        private static void BuildEyeLine()
        {
            BuildWall("DEV_EyeHeightRail", new Vector3(-4f, PlayerEyeHeight, 0f),
                      new Vector3(0.04f, 0.04f, 10f));
            BuildLabel("EYE 1.68 m", new Vector3(-4f, PlayerEyeHeight + 0.12f, 0f));
        }

        /// <summary>
        /// A bar low enough that standing does not fit under it and crouching does. Whether the
        /// crouch actually lowers the capsule, rather than only the camera, is otherwise a
        /// matter of opinion.
        /// </summary>
        private static void BuildCrouchGate()
        {
            const float clearance = 1.3f;
            BuildWall("DEV_CrouchGate_L", new Vector3(3f, clearance * 0.5f, -3f),
                      new Vector3(0.2f, clearance, 0.2f));
            BuildWall("DEV_CrouchGate_R", new Vector3(5f, clearance * 0.5f, -3f),
                      new Vector3(0.2f, clearance, 0.2f));
            BuildWall("DEV_CrouchGate_Bar", new Vector3(4f, clearance, -3f),
                      new Vector3(2.2f, 0.1f, 0.2f));
            BuildLabel("CROUCH GATE 1.3 m", new Vector3(4f, clearance + 0.25f, -3f));
        }

        /// <summary>
        /// Two rails a stride apart to walk sideways between. The strafe lean only reads
        /// against something straight.
        /// </summary>
        private static void BuildStrafeLane()
        {
            BuildWall("DEV_StrafeRail_N", new Vector3(0f, 0.6f, 4f), new Vector3(10f, 0.06f, 0.06f));
            BuildWall("DEV_StrafeRail_S", new Vector3(0f, 0.6f, 2.6f), new Vector3(10f, 0.06f, 0.06f));
            BuildLabel("STRAFE LANE", new Vector3(0f, 0.9f, 3.3f));
        }

        protected override string DescribeState() =>
            "Floor 14x14, 1 m grid, 1.86 m reference, eye-height rail at 1.68 m, " +
            "1.3 m crouch gate, strafe lane.";
    }
}
