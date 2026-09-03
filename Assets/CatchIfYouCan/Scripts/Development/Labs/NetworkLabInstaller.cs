using UnityEngine;

namespace CatchIfYouCan.Development.Labs
{
    /// <summary>A shell only. No netcode package is installed, so this lab exists to be filled in later rather than to pretend it works now.</summary>
    [AddComponentMenu("Catch If You Can/Development/NetworkLabInstaller")]
    public sealed class NetworkLabInstaller : DevelopmentLabInstaller
    {
        [Tooltip("How many player pads to lay out. Four is the intended party size.")]
        [SerializeField, Min(1)] private int playerPads = 4;

        public override DevelopmentLab Lab => DevelopmentLab.Network;

        protected override void BuildFixtures()
        {
            BuildFloor(Vector3.zero, new Vector2(24f, 24f));
            BuildMetreGrid(Vector3.zero, 24);

            BuildPads();
            BuildNoticeBoard();
            BuildReadout();

            // Pad 0 doubles as the local player's spawn, so the lab is walkable while the
            // rest of it is a plan rather than a system.
            BuildMarker(PlayerSpawnMarkerName, PadPosition(0));
        }

        private Vector3 PadPosition(int index)
        {
            float angle = index / Mathf.Max(1f, playerPads) * Mathf.PI * 2f;
            return new Vector3(Mathf.Sin(angle) * 5f, 0.05f, Mathf.Cos(angle) * 5f);
        }

        /// <summary>
        /// One pad per intended player, in a fixed ring. Fixed rather than generated because
        /// spawn ordering is one of the things that has to be identical on every client, and
        /// a ring indexed by player number is the simplest thing that can be checked by eye.
        /// </summary>
        private void BuildPads()
        {
            for (int i = 0; i < playerPads; i++)
            {
                var position = PadPosition(i);

                var pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pad.name = "DEV_NetworkSpawn_" + i;
                pad.transform.position = new Vector3(position.x, 0.02f, position.z);
                pad.transform.localScale = new Vector3(1.2f, 0.02f, 1.2f);

                var collider = pad.GetComponent<Collider>();
                if (collider != null)
                    Destroy(collider);

                BuildLabel("PLAYER " + i, position + new Vector3(0f, 0.3f, 0f));
            }
        }

        /// <summary>
        /// The panel version of the notice board, plus what the local session actually is. One
        /// player, no transport, no session - stated rather than implied by an empty room.
        /// </summary>
        private void BuildReadout()
        {
            Readout()
                .Line(() => "NETWORKING NOT INSTALLED")
                .Line(() => "No NGO, Relay, Lobby, Authentication or Multiplayer Services package")
                .Line(() => "Local players: " + (Core.LocalPlayerService.HasPlayer ? 1 : 0) +
                            " of " + playerPads + " pads")
                .Line(() => "Transport: none.  Session: none.  Authority: local only.")
                .Line(() => "Character: " +
                            (Character.CharacterService.LocalCharacterId ?? "default") +
                            "  (index " +
                            (Character.CharacterService.Catalog()?.IndexOf(
                                Character.CharacterService.LocalCharacterId) ?? -1) +
                            " in the catalog, which is what a compact encoding would send)");
        }

        /// <summary>
        /// A sign saying what is not here. A lab that looks finished and does nothing is worse
        /// than no lab: someone opens it, sees a room and four pads, and concludes networking
        /// is partly working.
        /// </summary>
        private static void BuildNoticeBoard()
        {
            BuildWall("DEV_NoticeBoard", new Vector3(0f, 1.6f, 9f), new Vector3(8f, 3f, 0.2f));
            BuildLabel("NETWORKING NOT INSTALLED", new Vector3(0f, 2.6f, 8.85f), null, 0.05f);
            BuildLabel("No NGO, Relay, Lobby or Authentication package is in this project.\n" +
                       "This lab is the room those systems will be tested in, and nothing more.",
                       new Vector3(0f, 1.6f, 8.85f), null, 0.025f);
        }

        protected override string DescribeState() =>
            "Floor 24x24, 1 m grid, " + playerPads + " spawn pads in a fixed ring. " +
            "NETWORKING NOT INSTALLED - this lab builds a room, not a session.";
    }
}
