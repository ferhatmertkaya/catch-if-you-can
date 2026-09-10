using CatchIfYouCan.Character;
using CatchIfYouCan.Player;
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
            BuildObservationCamera();
            BuildMirror();
            BuildReadout();
        }

        /// <summary>
        /// A fixed camera looking back at the spawn, rendering to a screen on the wall.
        ///
        /// <para>
        /// This is a first-person game, so the one thing you cannot see is the character. Every
        /// question about the rig - does the crouch read, is the arm in the right place, does
        /// the walk cycle match the speed - is a question about a body the player's own camera
        /// is inside of.
        /// </para>
        /// </summary>
        private static void BuildObservationCamera()
        {
            var target = new RenderTexture(512, 512, 16) { name = "DEV_ObservationRT" };

            var camGo = new GameObject("DEV_ObservationCamera");
            camGo.transform.position = new Vector3(0f, 1.4f, 1.2f);
            camGo.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            var cam = camGo.AddComponent<Camera>();
            cam.targetTexture = target;
            cam.fieldOfView = 50f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 30f;

            var screen = GameObject.CreatePrimitive(PrimitiveType.Quad);
            screen.name = "DEV_ObservationScreen";
            screen.transform.position = new Vector3(-3f, 1.6f, 3f);
            screen.transform.localScale = new Vector3(2f, 2f, 1f);

            var shader = Art.CiycShaders.Find(Art.CiycShaders.Unlit)
                         ?? Art.CiycShaders.FindLit();
            if (shader != null)
            {
                var mat = new Material(shader);
                mat.mainTexture = target;
                screen.GetComponent<Renderer>().sharedMaterial = mat;
            }

            BuildLabel("THIRD-PERSON VIEW", new Vector3(-3f, 2.75f, 3f));
        }

        /// <summary>
        /// The project's own mirror, at the same size the lobby uses. The mirror is the other
        /// way to see the character, and it is also the fixture that catches a stripped shader
        /// before the lobby does.
        /// </summary>
        private static void BuildMirror()
        {
            var go = new GameObject("DEV_Mirror");
            go.transform.position = new Vector3(3f, 0f, 3f);
            go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            go.AddComponent<Art.MirrorCorner>();

            BuildLabel("MIRROR", new Vector3(3f, 2.4f, 3f));
        }

        /// <summary>
        /// The rig's own numbers, plus the controls that make a pose hold still long enough to
        /// look at. A pose that only exists while you are moving cannot be inspected.
        /// </summary>
        private void BuildReadout()
        {
            var readout = Readout();

            readout
                .Line(() =>
                {
                    var character = CharacterService.Resolve();
                    return "Character: " + (character != null ? character.Id : "none") +
                           "  (catalog " + (CharacterService.Catalog()?.Count ?? 0) + ")";
                })
                .Line(() =>
                {
                    var motion = Core.LocalPlayerService.GetPlayerComponent<PlayerBodyMotion>();
                    if (motion == null)
                        return "Body motion: none";

                    return "Crouch drop: " + motion.FullCrouchDrop.ToString("F3") + " m" +
                           "  measured head drop: " + motion.MeasuredHeadDrop.ToString("F3") + " m";
                })
                .Line(() =>
                {
                    var controller = Core.LocalPlayerService.GetPlayerComponent<PlayerController>();
                    return controller != null
                        ? "Speed: " + controller.CurrentSpeed.ToString("F2") + " m/s"
                        : "Speed: no player";
                })
                .Line(() =>
                {
                    var motion = Core.LocalPlayerService.GetPlayerComponent<PlayerBodyMotion>();
                    if (motion == null || !motion.TryGetGrip(out var palm, out _, out _))
                        return "Grip: not measured";

                    return "Grip palm: " + palm.ToString("F2");
                })
                .Button("Next character", CycleCharacter)
                .Button("Freeze pose (disable body motion)", () => SetPoseFrozen(true))
                .Button("Unfreeze pose", () => SetPoseFrozen(false));
        }

        /// <summary>Steps through the catalog, so a second character can be looked at.</summary>
        private static void CycleCharacter()
        {
            var catalog = CharacterService.Catalog();
            if (catalog == null || catalog.Count == 0)
                return;

            int next = (catalog.IndexOf(CharacterService.LocalCharacterId) + 1) % catalog.Count;
            var character = catalog.Characters[next];
            if (character != null)
                CharacterService.SetLocalCharacter(character.Id);

            Core.CIYCLog.Info("Character lab: selected '" + CharacterService.LocalCharacterId +
                              "'. Respawn the player to see it.");
        }

        /// <summary>
        /// Turns the procedural body layer off and on. Off is the underlying animation clip
        /// with nothing added, which is the only way to tell which of the two put a limb
        /// somewhere unexpected.
        /// </summary>
        private static void SetPoseFrozen(bool frozen)
        {
            var motion = Core.LocalPlayerService.GetPlayerComponent<PlayerBodyMotion>();
            if (motion != null)
                motion.enabled = !frozen;
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
