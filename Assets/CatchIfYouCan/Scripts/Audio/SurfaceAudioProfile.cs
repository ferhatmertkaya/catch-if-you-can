using UnityEngine;

namespace CatchIfYouCan.Audio
{
    public enum SurfaceType
    {
        Wood,
        OldWood,
        Carpet,
        Tile,
        Concrete,
        Metal,
        Grass,
        Gravel,
        Mud
    }

    [CreateAssetMenu(fileName = "SurfaceAudioProfile", menuName = "Catch If You Can/Surface Audio Profile")]
    public class SurfaceAudioProfile : ScriptableObject
    {
        [System.Serializable]
        public class SurfaceEvents
        {
            public SurfaceType Surface;
            public string WalkEventId;
            public string RunEventId;
            public string CrouchEventId;
        }

        [SerializeField] private SurfaceEvents[] surfaces =
        {
            new SurfaceEvents { Surface = SurfaceType.Wood, WalkEventId = "Player.Footstep.Wood.Walk", RunEventId = "Player.Footstep.Wood.Run", CrouchEventId = "Player.Footstep.Wood.Crouch" },
            new SurfaceEvents { Surface = SurfaceType.OldWood, WalkEventId = "Player.Footstep.OldWood.Walk", RunEventId = "Player.Footstep.OldWood.Run", CrouchEventId = "Player.Footstep.OldWood.Crouch" },
            new SurfaceEvents { Surface = SurfaceType.Carpet, WalkEventId = "Player.Footstep.Carpet.Walk", RunEventId = "Player.Footstep.Carpet.Run", CrouchEventId = "Player.Footstep.Carpet.Crouch" },
            new SurfaceEvents { Surface = SurfaceType.Tile, WalkEventId = "Player.Footstep.Tile.Walk", RunEventId = "Player.Footstep.Tile.Run", CrouchEventId = "Player.Footstep.Tile.Crouch" },
            new SurfaceEvents { Surface = SurfaceType.Concrete, WalkEventId = "Player.Footstep.Concrete.Walk", RunEventId = "Player.Footstep.Concrete.Run", CrouchEventId = "Player.Footstep.Concrete.Crouch" },
            new SurfaceEvents { Surface = SurfaceType.Metal, WalkEventId = "Player.Footstep.Metal.Walk", RunEventId = "Player.Footstep.Metal.Run", CrouchEventId = "Player.Footstep.Metal.Crouch" },
            new SurfaceEvents { Surface = SurfaceType.Grass, WalkEventId = "Player.Footstep.Grass.Walk", RunEventId = "Player.Footstep.Grass.Run", CrouchEventId = "Player.Footstep.Grass.Crouch" },
            new SurfaceEvents { Surface = SurfaceType.Gravel, WalkEventId = "Player.Footstep.Gravel.Walk", RunEventId = "Player.Footstep.Gravel.Run", CrouchEventId = "Player.Footstep.Gravel.Crouch" },
            new SurfaceEvents { Surface = SurfaceType.Mud, WalkEventId = "Player.Footstep.Mud.Walk", RunEventId = "Player.Footstep.Mud.Run", CrouchEventId = "Player.Footstep.Mud.Crouch" }
        };

        [SerializeField] private string defaultWalk = "Player.Footstep.Wood.Walk";
        [SerializeField] private string defaultRun = "Player.Footstep.Wood.Run";
        [SerializeField] private string defaultCrouch = "Player.Footstep.Carpet.Crouch";

        public string GetEventId(SurfaceType surface, FootstepGait gait)
        {
            for (int i = 0; i < surfaces.Length; i++)
            {
                if (surfaces[i].Surface != surface) continue;
                return gait switch
                {
                    FootstepGait.Run => surfaces[i].RunEventId,
                    FootstepGait.Crouch => surfaces[i].CrouchEventId,
                    _ => surfaces[i].WalkEventId
                };
            }

            return gait switch
            {
                FootstepGait.Run => defaultRun,
                FootstepGait.Crouch => defaultCrouch,
                _ => defaultWalk
            };
        }
    }

    public enum FootstepGait
    {
        Walk,
        Run,
        Crouch
    }
}
