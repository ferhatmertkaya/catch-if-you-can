using CatchIfYouCan.Core;
using CatchIfYouCan.Procedural.Deterministic;

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

        /// <summary>
        /// Which product this is. Chosen, never inferred.
        ///
        /// <para>
        /// Ask this rather than testing the state. An online session that is still connecting,
        /// or that failed, is Online - and code that decides "offline" from a state that is not
        /// yet Connected will initialise nothing, show nothing, and blame the wrong thing.
        /// </para>
        /// </summary>
        public static SessionMode Mode => _current.Mode;

        /// <summary>
        /// True when the player chose offline.
        ///
        /// <para>
        /// This used to read <c>State == SessionState.Offline</c>, which conflated two
        /// different facts: "the player chose single player" and "no session has connected
        /// yet". Every online session passes through the second on its way up, so anything
        /// gated on it would have behaved as offline during connection.
        /// </para>
        /// </summary>
        public static bool IsOffline => _current.Mode == SessionMode.Offline;

        /// <summary>True when the player chose online, whatever the connection is doing.</summary>
        public static bool IsOnline => _current.Mode == SessionMode.Online;

        /// <summary>
        /// Whether an online service may be initialised right now.
        ///
        /// <para>
        /// Authentication, Lobby, Relay and the transport all ask this before doing anything.
        /// Offline it is false, which is what makes airplane mode a non-event: the services are
        /// not attempted, so they cannot fail.
        /// </para>
        /// </summary>
        public static bool AllowsOnlineServices => SessionModeRules.AllowsOnlineServices(Mode);

        /// <summary>
        /// The most players this session can hold. One offline, the protocol maximum online.
        ///
        /// <para>
        /// A player-count readout asks for this rather than writing "/ 8". The eight lives in
        /// <see cref="MultiplayerProtocol.MaxPlayers"/> and nowhere else.
        /// </para>
        /// </summary>
        public static int MaxPlayers => _current.MaxPlayers;

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

            // Mode is fixed for a session's life. Replacing a live session with one of the
            // other mode is the silent fallback this contract forbids - an online session that
            // failed becoming an offline one, or an offline mission being promoted because a
            // connection appeared. Ending the session is explicit and goes through Reset.
            if (_current != null &&
                _current.State != SessionState.Offline &&
                _current.Mode != session.Mode)
            {
                CIYCLog.Error(
                    "Refused to replace a live " + _current.Mode + " session with an " +
                    session.Mode + " one. Session mode is chosen once and does not change; " +
                    "end the current session first.");
                return;
            }

            _current = session;
            SessionAuthority.Provider = authority;

            CIYCLog.Info("Session installed: " + session.Mode + " " + session.Role +
                         " (" + session.State + ")");
        }

        /// <summary>
        /// Ends a session deliberately and returns to offline with local authority.
        ///
        /// <para>
        /// <b>The named way, and the only way, to leave a live online session.</b> Ending one
        /// is a decision somebody made - the host left, the player quit to the menu, the
        /// mission finished - and it is stated here with a reason that reaches the log.
        /// </para>
        ///
        /// <para>
        /// This is deliberately not the failure path. An online session that could not
        /// authenticate, could not allocate a relay or lost its connection stays Online and
        /// Failed, so the player is told that online failed rather than quietly discovering
        /// they are in single player. Turning a failure into an offline session is the silent
        /// fallback the mode contract forbids.
        /// </para>
        /// </summary>
        public static void EndSession(string reason)
        {
            if (_current != null && _current.Mode == SessionMode.Online)
            {
                CIYCLog.Info("Online session ended: " +
                             (string.IsNullOrEmpty(reason) ? "no reason given" : reason));
            }

            _current = new OfflineSession();
            SessionAuthority.Provider = null;
        }

        /// <summary>
        /// Returns to the starting state: no session, local authority.
        ///
        /// <para>
        /// For a domain reload and for a process that has nothing live. It <b>refuses</b> to
        /// discard a live online session, because a bare Reset in a failure handler is exactly
        /// how an online session becomes an offline one without anybody deciding that it
        /// should. Ending one on purpose goes through <see cref="EndSession"/>, which says why.
        /// </para>
        /// </summary>
        public static void Reset()
        {
            if (_current != null &&
                _current.Mode == SessionMode.Online &&
                _current.State != SessionState.Offline)
            {
                CIYCLog.Error(
                    "Refused to reset a live " + _current.State + " online session to offline. " +
                    "A failed online session stays online and failed so the player can be told; " +
                    "ending one on purpose goes through EndSession(reason).");
                return;
            }

            _current = new OfflineSession();
            SessionAuthority.Provider = null;
        }

        /// <summary>
        /// A fresh process has no session, whatever the last one was doing.
        ///
        /// <para>
        /// Assigns directly rather than going through <see cref="Reset"/>, which would refuse
        /// if a live online session were still held in a static from the previous play session.
        /// Entering play mode is not a fallback; there is nothing to fall back from.
        /// </para>
        /// </summary>
        [UnityEngine.RuntimeInitializeOnLoadMethod(
            UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            _current = new OfflineSession();
            SessionAuthority.Provider = null;
        }
    }
}
