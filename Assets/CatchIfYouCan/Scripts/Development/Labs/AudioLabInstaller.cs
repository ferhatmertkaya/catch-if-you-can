using CatchIfYouCan.Audio;
using UnityEngine;

namespace CatchIfYouCan.Development.Labs
{
    /// <summary>Room zones, portals, occlusion, reverb and the glass filter, with the lobby's own acoustic numbers.</summary>
    [AddComponentMenu("Catch If You Can/Development/AudioLabInstaller")]
    public sealed class AudioLabInstaller : DevelopmentLabInstaller
    {
        public override DevelopmentLab Lab => DevelopmentLab.Audio;

        private readonly System.Collections.Generic.List<RoomAudioZone> _zones =
            new System.Collections.Generic.List<RoomAudioZone>();

        protected override void BuildFixtures()
        {
            BuildFloor(Vector3.zero, new Vector2(30f, 20f));
            BuildMarker(PlayerSpawnMarkerName, new Vector3(0f, 0.05f, -7f));

            BuildThreeRoomsAndACorridor();
            BuildGlassPane();
            BuildEmitters();
            BuildFootstepSurfaces();
            BuildReadout();
        }

        /// <summary>
        /// Footstep surfaces in a row to walk across. Which clip a step plays is chosen by what
        /// is underfoot, and the handover from one surface to the next is where it goes wrong.
        /// </summary>
        private static void BuildFootstepSurfaces()
        {
            var surfaces = new[]
            {
                SurfaceType.Wood, SurfaceType.OldWood, SurfaceType.Carpet,
                SurfaceType.Tile, SurfaceType.Concrete, SurfaceType.Gravel,
            };

            for (int i = 0; i < surfaces.Length; i++)
            {
                BuildFootstepSurface("DEV_Surface_" + surfaces[i],
                                     new Vector3(-10f + i * 4f, 0f, -8f),
                                     new Vector2(3.6f, 3.6f), surfaces[i]);
            }
        }

        /// <summary>
        /// What the audio system currently thinks is true. Every one of these is a decision the
        /// mix makes that has no visible consequence in the room.
        /// </summary>
        private void BuildReadout()
        {
            Readout()
                .Line(() =>
                {
                    var reverb = Object.FindAnyObjectByType<ReverbZoneController>();
                    return "Reverb profile: " + (reverb != null ? reverb.CurrentProfileId : "-");
                })
                .Line(() =>
                {
                    var occlusion = Object.FindAnyObjectByType<AudioOcclusionController>();
                    return occlusion != null
                        ? "Occlusion: " + occlusion.TrackedSourceCount + " tracked, zone=" +
                          (occlusion.ListenerZoneName ?? "none")
                        : "Occlusion: no controller";
                })
                .Line(() =>
                {
                    var zones = Object.FindObjectsByType<RoomAudioZone>(FindObjectsSortMode.None);
                    var portals = Object.FindObjectsByType<AudioPortal>(FindObjectsSortMode.None);
                    return "Zones: " + zones.Length + "  portals: " + portals.Length;
                })
                .Line(() =>
                {
                    var surface = Object.FindAnyObjectByType<FootstepController>();
                    return surface != null ? "Footstep controller: present" : "Footstep controller: none";
                })
                .Button("Open all portals", () => SetPortals(1f))
                .Button("Close all portals", () => SetPortals(0f));
        }

        private static void SetPortals(float amount)
        {
            var portals = Object.FindObjectsByType<AudioPortal>(FindObjectsSortMode.None);
            for (int i = 0; i < portals.Length; i++)
                portals[i]?.SetOpenAmount(amount);
        }

        /// <summary>
        /// Three rooms off a corridor, each with a doorway and its own zone, and a portal in
        /// each doorway.
        ///
        /// <para>
        /// Occlusion, portals and reverb transitions are all about crossing a threshold, and
        /// none can be judged in one open room - the whole question is what happens between "in
        /// there" and "out here". Three rooms rather than two because a portal has two sides
        /// and a corridor between two portals is the case where the wrong one gets picked.
        /// </para>
        /// </summary>
        private void BuildThreeRoomsAndACorridor()
        {
            var centres = new[]
            {
                new Vector3(-9f, 0f, 4f), new Vector3(0f, 0f, 4f), new Vector3(9f, 0f, 4f),
            };
            var names = new[] { "A", "B", "C" };
            var profiles = new[] { "Bedroom", "Hallway", "Bathroom" };

            for (int i = 0; i < centres.Length; i++)
            {
                BuildRoomShell("DEV_Room" + names[i], centres[i], new Vector2(8f, 7f),
                               height: 3f, doorwayWidth: 1.2f);
                BuildLabel("ROOM " + names[i], centres[i] + new Vector3(0f, 2.4f, 0f));

                var zoneGo = new GameObject("DEV_Zone_" + names[i]);
                zoneGo.transform.position = centres[i];
                var box = zoneGo.AddComponent<BoxCollider>();
                box.size = new Vector3(8f, 3f, 7f);
                box.center = new Vector3(0f, 1.5f, 0f);
                box.isTrigger = true;

                var zone = zoneGo.AddComponent<RoomAudioZone>();
                WireLabField(zone, "reverbProfileId", profiles[i]);
                _zones.Add(zone);
            }

            // The corridor the three doorways open onto.
            BuildFloor(new Vector3(0f, 0f, 9f), new Vector2(28f, 3f), "DEV_CorridorFloor");
            BuildWall("DEV_Corridor_North", new Vector3(0f, 1.5f, 10.5f),
                      new Vector3(28f, 3f, 0.2f));
            BuildLabel("CORRIDOR", new Vector3(0f, 2.4f, 9f));

            for (int i = 0; i < _zones.Count; i++)
            {
                var portalGo = new GameObject("DEV_Portal_" + names[i]);
                portalGo.transform.position = centres[i] + new Vector3(0f, 1.2f, 3.5f);

                var portal = portalGo.AddComponent<AudioPortal>();
                WireLabField(portal, "roomA", _zones[i]);
                WireLabField(portal, "roomB", i == 1 ? _zones[0] : _zones[1]);
                BuildLabel("PORTAL " + names[i], portalGo.transform.position + new Vector3(0f, 0.5f, 0f));
            }
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
            "Floor 30x20, three 8x7 rooms off a corridor with 1.2 m doorways, " + _zones.Count +
            " audio zones and portals, a 4x3 glass pane, three 3D looping emitters " +
            "(no clip assigned), six footstep surfaces.";
    }
}
