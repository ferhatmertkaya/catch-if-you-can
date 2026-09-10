using UnityEngine;

namespace CatchIfYouCan.Development.Labs
{
    /// <summary>A shell only. No netcode package is installed, so this lab exists to be filled in later rather than to pretend it works now.</summary>
    [AddComponentMenu("Catch If You Can/Development/NetworkLabInstaller")]
    public sealed class NetworkLabInstaller : DevelopmentLabInstaller
    {
        /// <summary>
        /// One pad per place in a full session, derived rather than declared.
        ///
        /// <para>
        /// This was a serialized four with a comment calling it "the intended party size" - a
        /// second capacity constant, and exactly the kind that goes stale silently. The
        /// contract moved to eight and this would still have laid out four pads, so the lab
        /// built to test an eight-player session would have quietly disagreed with it.
        /// </para>
        ///
        /// <para>
        /// Read as a property rather than cached in a field so it cannot drift from the
        /// protocol between a domain reload and a scene build.
        /// </para>
        /// </summary>
        private static int PlayerPads =>
            Procedural.Deterministic.MultiplayerProtocol.MaxPlayers;

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
            float angle = index / Mathf.Max(1f, PlayerPads) * Mathf.PI * 2f;
            return new Vector3(Mathf.Sin(angle) * 5f, 0.05f, Mathf.Cos(angle) * 5f);
        }

        /// <summary>
        /// One pad per intended player, in a fixed ring. Fixed rather than generated because
        /// spawn ordering is one of the things that has to be identical on every client, and
        /// a ring indexed by player number is the simplest thing that can be checked by eye.
        /// </summary>
        private void BuildPads()
        {
            for (int i = 0; i < PlayerPads; i++)
            {
                var position = PadPosition(i);

                var pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                // One-based and zero-padded, so the pads sort and read the way the spec names
                // them: Spawn_01 through Spawn_08.
                pad.name = "DEV_NetworkSpawn_" + (i + 1).ToString("00");
                pad.transform.position = new Vector3(position.x, 0.02f, position.z);
                pad.transform.localScale = new Vector3(1.2f, 0.02f, 1.2f);

                var collider = pad.GetComponent<Collider>();
                if (collider != null)
                    Destroy(collider);

                // Pad 1 is the host's place. It is one of the eight, not a ninth - the host
                // occupies a seat in MaxPlayers rather than sitting outside it.
                string who = i == 0 ? "HOST" : "CLIENT " + i;
                BuildLabel(who + "\n" + (i + 1) + " / " + PlayerPads,
                           position + new Vector3(0f, 0.3f, 0f));
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
                .Line(() => "Session mode: " + Session.MultiplayerSessionService.Mode +
                            "   capacity: " + Session.MultiplayerSessionService.Current.PlayerCount +
                            " / " + Session.MultiplayerSessionService.MaxPlayers)
                .Line(() => "Registered players: " + Player.PlayerPresence.Count +
                            "   pads: " + PlayerPads + " (1 host + " + (PlayerPads - 1) + " clients)")
                .Line(() => "Transport: none.  Session: none.  Authority: local only.")
                .Line(() => "Character: " +
                            (Character.CharacterService.LocalCharacterId ?? "default") +
                            "  (index " + Character.CharacterService.LocalCharacterIndex +
                            " in the catalog, which is what a compact encoding would send)")

                // Whether online is reachable at all, said out loud. A lab that showed a
                // session mode and nothing else would leave "why does online do nothing" to
                // be discovered by pressing it.
                .Line(() => "Online provider: " +
                            (Session.SessionLauncher.HasOnlineProvider
                                ? "installed"
                                : "none - BeginOnline refuses with NoOnlineProvider"))

                // The readout that must never say 0 ms. Offline it says so; online with no
                // probe it says it has not measured, which is the honest answer either way.
                .Line(() => "Connection: " +
                            Procedural.Deterministic.ConnectionRating.Describe(
                                Session.ConnectionDiagnostics.LocalQuality) +
                            "   probe: " +
                            (Session.ConnectionDiagnostics.HasProbe ? "installed" : "none"))

                // Ownership exists whether or not anybody else does. Offline every item is
                // the one local player's, which is what these numbers should show.
                .Line(() => "Equipment: " + Equipment.EquipmentBase.Alive.Count +
                            " alive, " + OwnedItems() + " owned by a player")

                .Line(() => "Reconnect: policy only, NOT PRODUCTION READY (" +
                            Procedural.Deterministic.ReconnectPolicy.MaxAttempts +
                            " attempts, seat held " +
                            Procedural.Deterministic.ReconnectPolicy.SeatHeldSeconds + "s)");
        }

        /// <summary>
        /// How many live items belong to somebody. Counted rather than assumed, because the
        /// interesting failure is an item that is being carried and belongs to nobody.
        /// </summary>
        private static int OwnedItems()
        {
            var alive = Equipment.EquipmentBase.Alive;
            int owned = 0;

            for (int i = 0; i < alive.Count; i++)
                if (alive[i] != null &&
                    Procedural.Deterministic.EquipmentOwnership.IsOwned(alive[i].OwnerClientId))
                    owned++;

            return owned;
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
            "Floor 24x24, 1 m grid, " + PlayerPads + " spawn pads in a fixed ring " +
            "(1 host + " + (PlayerPads - 1) + " clients). " +
            "NETWORKING NOT INSTALLED - this lab builds a room, not a session.";
    }
}
