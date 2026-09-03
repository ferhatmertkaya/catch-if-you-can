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
            BuildFloor(Vector3.zero, new Vector2(30f, 20f));
            BuildMarker(PlayerSpawnMarkerName, new Vector3(0f, 0.05f, -7f));

            BuildTwoRoomsAndADoorway();
            BuildGlassPane();
            BuildEmitters();
        }

        /// <summary>
        /// Two rooms sharing a doorway. Occlusion, portals and reverb transitions are all
        /// about crossing a threshold, and none of them can be judged in one open room - the
        /// whole question is what happens between "in there" and "out here".
        /// </summary>
        private static void BuildTwoRoomsAndADoorway()
        {
            BuildRoomShell("DEV_RoomA", new Vector3(-6f, 0f, 3f), new Vector2(10f, 8f),
                           height: 3f, doorwayWidth: 1.2f);
            BuildRoomShell("DEV_RoomB", new Vector3(6f, 0f, 3f), new Vector2(10f, 8f),
                           height: 3f, doorwayWidth: 1.2f);
            BuildLabel("ROOM A", new Vector3(-6f, 2.4f, 3f));
            BuildLabel("ROOM B", new Vector3(6f, 2.4f, 3f));
        }

        /// <summary>
        /// A pane to stand behind. The lobby's exterior audio comes through glass, and a
        /// filter you can only hear in the lobby is a filter you cannot tune.
        /// </summary>
        private static void BuildGlassPane()
        {
            BuildWall("DEV_GlassPane", new Vector3(0f, 1.5f, -4f), new Vector3(4f, 3f, 0.06f));
            BuildLabel("GLASS", new Vector3(0f, 3.2f, -4f));
        }

        /// <summary>
        /// Three silent looping sources at known distances. Silent because the lab is not
        /// about which clip plays; what is being measured is falloff, occlusion and reverb,
        /// and the clip is whatever you drop on the source while it runs.
        /// </summary>
        private static void BuildEmitters()
        {
            BuildEmitter("DEV_Emitter_RoomA", new Vector3(-6f, 1.5f, 3f));
            BuildEmitter("DEV_Emitter_RoomB", new Vector3(6f, 1.5f, 3f));
            BuildEmitter("DEV_Emitter_BehindGlass", new Vector3(0f, 1.5f, -6f));
        }

        private static void BuildEmitter(string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.position = position;

            var source = go.AddComponent<AudioSource>();
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 1f;
            source.maxDistance = 25f;
            source.loop = true;
            source.playOnAwake = false;

            BuildLabel(name.Replace("DEV_Emitter_", ""), position + new Vector3(0f, 0.4f, 0f));
        }

        protected override string DescribeState() =>
            "Floor 30x20, two 10x8 rooms with 1.2 m doorways, a 4x3 glass pane, three " +
            "3D looping emitters (no clip assigned).";
    }
}
