using CatchIfYouCan.Procedural.Deterministic;

namespace CatchIfYouCan.Session
{
    /// <summary>
    /// What a transport is asked when something wants to know how a connection is doing.
    ///
    /// <para>
    /// One method, and it may say no. A transport that has not measured a peer yet returns
    /// false rather than zero, because zero is a reading and "I have not measured" is not.
    /// </para>
    /// </summary>
    public interface IConnectionProbe
    {
        /// <summary>
        /// The last measured round trip to this peer, in milliseconds.
        /// </summary>
        /// <returns>
        /// False when there is no measurement for this peer - not yet taken, peer gone,
        /// transport not running. The out value is ignored when this is false.
        /// </returns>
        bool TryGetRoundTripMs(int clientId, out int roundTripMs);
    }

    /// <summary>
    /// How the connection is doing, for a HUD, a lobby row and the network lab.
    ///
    /// <para>
    /// <b>It reports what it knows and nothing else.</b> With no transport installed - which
    /// is every build today - it says <see cref="ConnectionQuality.Unknown"/> online and
    /// <see cref="ConnectionQuality.NotApplicable"/> offline. It never returns zero
    /// milliseconds, because a confident "0 ms" on a game that has never sent a packet is
    /// worse than no readout at all: it is the readout somebody trusts while debugging why
    /// nothing arrives.
    /// </para>
    ///
    /// <para>
    /// Offline and unmeasured are deliberately different answers. Offline solo has no
    /// connection, so it has no latency and never will; an online session that has not
    /// measured yet will. A single "unknown" for both would put "measuring" in the corner of a
    /// single-player screen forever.
    /// </para>
    ///
    /// <para>
    /// The bands live in <see cref="ConnectionRating"/>, which is pure and tested. This is the
    /// seam a transport plugs into, and it is the whole of it.
    /// </para>
    /// </summary>
    public static class ConnectionDiagnostics
    {
        private static IConnectionProbe _probe;

        /// <summary>Whether anything can measure. False in every build today.</summary>
        public static bool HasProbe => _probe != null;

        /// <summary>
        /// Installs the transport's measurement. Called by the networking layer, not by
        /// gameplay and not by a HUD.
        /// </summary>
        public static void InstallProbe(IConnectionProbe probe)
        {
            if (probe == null)
            {
                Core.CIYCLog.Error("ConnectionDiagnostics was handed a null probe. " +
                                   "Use ClearProbe to remove one.");
                return;
            }

            _probe = probe;
        }

        /// <summary>
        /// Removes a probe, if it is the one that is installed. Takes it rather than clearing
        /// blindly so a transport shutting down cannot remove one that replaced it.
        /// </summary>
        public static void ClearProbe(IConnectionProbe probe)
        {
            if (probe != null && !ReferenceEquals(_probe, probe))
                return;

            _probe = null;
        }

        /// <summary>
        /// The measured round trip to a peer, in milliseconds.
        /// </summary>
        /// <returns>
        /// False offline, false with no probe, and false when the probe has no measurement.
        /// <paramref name="roundTripMs"/> is <see cref="ConnectionRating.NoMeasurement"/> in
        /// all three cases, which is deliberately not a number anything would render.
        /// </returns>
        public static bool TryGetRoundTripMs(int clientId, out int roundTripMs)
        {
            roundTripMs = ConnectionRating.NoMeasurement;

            if (MultiplayerSessionService.Mode != SessionMode.Online)
                return false;

            if (_probe == null)
                return false;

            if (!_probe.TryGetRoundTripMs(clientId, out int measured) || measured < 0)
                return false;

            roundTripMs = measured;
            return true;
        }

        /// <summary>
        /// The word to show for this peer.
        ///
        /// <para>
        /// Offline is <see cref="ConnectionQuality.NotApplicable"/> and says so, rather than
        /// pretending to measure something that does not exist.
        /// </para>
        /// </summary>
        public static ConnectionQuality QualityFor(int clientId)
        {
            if (MultiplayerSessionService.Mode != SessionMode.Online)
                return ConnectionQuality.NotApplicable;

            return TryGetRoundTripMs(clientId, out int ms)
                ? ConnectionRating.Rate(ms)
                : ConnectionQuality.Unknown;
        }

        /// <summary>
        /// This peer's own quality - what a corner-of-the-screen readout shows.
        ///
        /// <para>
        /// A host measures nothing about itself and correctly reports
        /// <see cref="ConnectionQuality.Unknown"/> rather than
        /// <see cref="ConnectionQuality.Good"/>: the host's own latency to itself is not a
        /// fact about the session, and showing it as perfect is how a host concludes the
        /// network is fine while everybody else is at 900 ms.
        /// </para>
        /// </summary>
        public static ConnectionQuality LocalQuality =>
            QualityFor(MultiplayerProtocol.LocalOnlyClientId);

        /// <summary>A fresh process has measured nothing and has no transport.</summary>
        [UnityEngine.RuntimeInitializeOnLoadMethod(
            UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => _probe = null;
    }
}
