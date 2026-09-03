using CatchIfYouCan.Core;
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
            BuildFloor(Vector3.zero, new Vector2(30f, 30f));
            BuildMetreGrid(Vector3.zero, 30);
            BuildMarker(PlayerSpawnMarkerName, new Vector3(0f, 0.05f, -6f));

            // The real HUD, not a lab copy. What this lab is for is the shipping HUD's own
            // safe-area behaviour and its own touch targets.
            CiycServices.EnsureCore();

            BuildHeadingPosts();
            BuildSlopeAndStep();
        }

        /// <summary>
        /// Posts at the four compass points, far enough out to be aimed at. A look stick is
        /// judged by whether you can put the crosshair on something and keep it there, which
        /// needs something to put it on.
        /// </summary>
        private static void BuildHeadingPosts()
        {
            var headings = new[] { "N", "E", "S", "W" };
            var offsets = new[]
            {
                new Vector3(0f, 0f, 12f), new Vector3(12f, 0f, 0f),
                new Vector3(0f, 0f, -12f), new Vector3(-12f, 0f, 0f),
            };

            for (int i = 0; i < headings.Length; i++)
            {
                BuildWall("DEV_Post_" + headings[i], offsets[i] + new Vector3(0f, 1.5f, 0f),
                          new Vector3(0.3f, 3f, 0.3f));
                BuildLabel(headings[i], offsets[i] + new Vector3(0f, 3.4f, 0f));
            }
        }

        /// <summary>
        /// A ramp and a stair. Auto-run and the joystick deadzone both behave differently the
        /// moment the ground is not flat, and a flat lab hides it.
        /// </summary>
        private static void BuildSlopeAndStep()
        {
            var ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.name = "DEV_Ramp_20deg";
            ramp.transform.position = new Vector3(5f, 0.5f, 4f);
            ramp.transform.localScale = new Vector3(4f, 0.2f, 8f);
            ramp.transform.rotation = Quaternion.Euler(-20f, 0f, 0f);
            BuildLabel("RAMP 20 deg", new Vector3(5f, 2f, 4f));

            for (int i = 0; i < 5; i++)
            {
                float height = 0.18f * (i + 1);
                var step = GameObject.CreatePrimitive(PrimitiveType.Cube);
                step.name = "DEV_Step_" + (i + 1);
                step.transform.position = new Vector3(-5f, height * 0.5f, 2f + i * 0.6f);
                step.transform.localScale = new Vector3(3f, height, 0.6f);
            }

            BuildLabel("STEPS 0.18 m", new Vector3(-5f, 1.4f, 3.2f));
        }

        protected override string DescribeState() =>
            "Floor 30x30, 1 m grid, four heading posts at 12 m, a 20 degree ramp and five " +
            "0.18 m steps. The runtime HUD is the shipping one.";
    }
}
