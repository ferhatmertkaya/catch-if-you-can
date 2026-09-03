using System;
using CatchIfYouCan.Procedural.Deterministic;

namespace CatchIfYouCan.Session
{
    /// <summary>
    /// The one place a host builds the config every peer will agree to, and the one place a
    /// session seed is rolled.
    ///
    /// <para>
    /// <b>The host rolls the seed exactly once.</b> That is the whole of NETWORKING.md §3 step
    /// 1, and it is not a style preference: the house is not replicated, it is regenerated from
    /// this number on every machine, so a second place that can mint one is a second house.
    /// The project has already had that bug - the mission-select screen used to roll its own,
    /// and because the menu scene has no MissionManager, the menu's seed was usually the live
    /// one.
    /// </para>
    ///
    /// <para>
    /// <see cref="MatchConfig.CreateAuthoritative"/> deliberately does not roll, so that no
    /// client-side call site can mint an authoritative seed by constructing a config. This is
    /// the counterpart: the only roller, and it refuses to roll unless this process is the
    /// host.
    /// </para>
    ///
    /// <para>
    /// Engine-free apart from the authority question, so the standalone determinism harness can
    /// exercise it.
    /// </para>
    /// </summary>
    public static class MatchConfigBuilder
    {
        /// <summary>
        /// Zero means "unset" throughout the deterministic contract, so it is never a seed.
        /// </summary>
        public const int UnsetSeed = 0;

        private static readonly Random Entropy = new Random();

        /// <summary>Why a build was refused, or that it was not.</summary>
        public enum BuildStatus
        {
            Built = 0,

            /// <summary>This process is not the host. Only a host rolls a seed.</summary>
            NotAuthoritative,

            /// <summary>No map or no content snapshot to describe.</summary>
            MissingContent,

            /// <summary>A seed was supplied and it was the reserved unset value.</summary>
            InvalidSeed,
        }

        /// <summary>
        /// Rolls a session seed. Never zero, because zero is the contract's "unset".
        ///
        /// <para>
        /// Not <c>UnityEngine.Random</c>: that is a single global stream shared with gameplay,
        /// so anything that draws from it before the roll shifts the session seed, and a
        /// session identity that depends on how much wandering happened in the menu is not an
        /// identity.
        /// </para>
        /// </summary>
        public static int RollSeed()
        {
            int seed;
            do
            {
                seed = Entropy.Next(int.MinValue, int.MaxValue);
            }
            while (seed == UnsetSeed);

            return seed;
        }

        /// <summary>
        /// Builds the authoritative config for a new session, rolling the seed.
        ///
        /// <para>
        /// Refuses on a client. A client that reaches this has a bug, and returning a config it
        /// would then broadcast would turn that bug into two different houses.
        /// </para>
        /// </summary>
        public static BuildStatus TryBuildAuthoritative(MapDefinition map, ContentSnapshot content,
                                                        out MatchConfig config)
        {
            return TryBuildAuthoritative(RollSeed(), map, content, out config);
        }

        /// <summary>
        /// The same, with a seed already chosen - replaying a known session, or a test that
        /// needs a fixed one. Still refuses on a client.
        /// </summary>
        public static BuildStatus TryBuildAuthoritative(int seed, MapDefinition map,
                                                        ContentSnapshot content,
                                                        out MatchConfig config)
        {
            config = default;

            if (!Core.SessionAuthority.IsHost)
                return BuildStatus.NotAuthoritative;

            if (seed == UnsetSeed)
                return BuildStatus.InvalidSeed;

            if (map == null || content == null)
                return BuildStatus.MissingContent;

            config = MatchConfig.CreateAuthoritative(seed, map, content);
            return BuildStatus.Built;
        }

        /// <summary>
        /// The config this peer offers when joining: everything the host will compare against,
        /// with the host's seed - which the client has been given and must never replace.
        ///
        /// <para>
        /// A client passes the seed it received back so the host can see it arrived intact. It
        /// does not roll one; a client that rolls is a client generating a different house and
        /// then failing a layout compare that will blame the generator.
        /// </para>
        /// </summary>
        public static MatchConfig ForJoinRequest(int seedFromHost, MapDefinition map,
                                                 ContentSnapshot content)
        {
            if (map == null || content == null)
                return default;

            return new MatchConfig(
                MultiplayerProtocol.Version,
                GenerationVersion.Current,
                seedFromHost,
                map.MapDefinitionId,
                content.ContentHash);
        }
    }
}
