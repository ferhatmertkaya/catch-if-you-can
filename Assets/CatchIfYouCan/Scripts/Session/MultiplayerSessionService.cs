using CatchIfYouCan.Core;

namespace CatchIfYouCan.Session
{
    /// <summary>
    /// Where gameplay finds the session, and the one place a networking implementation is
    /// installed.
    ///
    /// <para>
    /// Offline until something installs otherwise, which is what single player is: one player
    /// who is the host. Nothing in the game has to special-case the absence of a network,
    /// because there is always a session object and offline is a real answer rather than a
    /// null.
    /// </para>
    ///
    /// <para>
    /// <b>Installing a session is also what sets the authority.</b> These were always the same
    /// fact stated twice - "I am the host" and "I decide" - and keeping them in two places is
    /// how they end up disagreeing during a disconnect. <see cref="Install"/> sets both;
    /// <see cref="Reset"/> puts both back.
    /// </para>
    /// </summary>
    public static class MultiplayerSessionService
    {
        private static IMultiplayerSession _current = new OfflineSession();

        /// <summary>The session. Never null.</summary>
        public static IMultiplayerSession Current => _current;

        /// <summary>Shorthand for the question almost every caller is actually asking.</summary>
        public static bool IsHost => _current.IsHost;

        /// <summary>True when there is no session at all. Single player.</summary>
        public static bool IsOffline => _current.State == SessionState.Offline;

        /// <summary>
        /// Installs a live session and the authority that goes with it.
        ///
        /// <para>
        /// The authority provider is taken alongside the session rather than derived from it,
        /// because "who is host" and "what may this process do" are not always the same
        /// question - a host that is mid-teardown is still the host and may no longer confirm
        /// evidence.
        /// </para>
        /// </summary>
        public static void Install(IMultiplayerSession session,
                                   SessionAuthority.IAuthorityProvider authority)
        {
            if (session == null)
            {
                Reset();
                return;
            }

            _current = session;
            SessionAuthority.Provider = authority;

            CIYCLog.Info("Session installed: " + session.Role + " (" + session.State + ")");
        }

        /// <summary>
        /// Back to single player: offline session, local authority.
        ///
        /// <para>
        /// Called when a session ends for any reason, including badly. A game that has lost its
        /// connection must still be a game, and the safe state is the one where the local
        /// player owns everything - not one where nothing in the world will answer.
        /// </para>
        /// </summary>
        public static void Reset()
        {
            _current = new OfflineSession();
            SessionAuthority.Provider = null;
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(
            UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => Reset();
    }
}
