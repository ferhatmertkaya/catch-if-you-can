using CatchIfYouCan.Core;

namespace CatchIfYouCan.Session
{
    /// <summary>
    /// What the player picked on the play screen. The whole menu, as a value.
    ///
    /// <para>
    /// PLAY splits into solo and online, and online splits into hosting and joining. Those are
    /// the only three ways a session begins. There is no fourth that happens by itself.
    /// </para>
    /// </summary>
    public enum SessionChoice
    {
        /// <summary>Solo, on this device, with nothing outside it involved.</summary>
        OfflineSolo = 0,

        /// <summary>Online, hosting: this device rolls the seed and decides.</summary>
        OnlineHost,

        /// <summary>Online, joining a session somebody else is hosting.</summary>
        OnlineJoin,
    }

    /// <summary>Why a launch was refused, or that it was not.</summary>
    public enum LaunchStatus
    {
        /// <summary>The session was installed and the chosen mode is live.</summary>
        Started = 0,

        /// <summary>A session is already running. End it first; a launch does not replace one.</summary>
        SessionAlreadyLive,

        /// <summary><see cref="SessionLauncher.BeginOnline"/> was handed the solo choice.</summary>
        NotAnOnlineChoice,

        /// <summary>
        /// Online was chosen and no networking layer is installed to serve it. Reported as
        /// itself, never as an offline session.
        /// </summary>
        NoOnlineProvider,

        /// <summary>
        /// A provider exists and declined - sign-in failed, no allocation, lobby full, the
        /// wrong build. <see cref="LaunchResult.Detail"/> carries what it said.
        /// </summary>
        OnlineProviderRefused,

        /// <summary>
        /// A provider returned something that is not an online session. A bug in the provider,
        /// refused rather than installed: the alternative is a player who chose online being
        /// put into single player without being told.
        /// </summary>
        OnlineProviderReturnedWrongMode,
    }

    /// <summary>What a launch attempt did, and what to tell the player if it did nothing.</summary>
    public readonly struct LaunchResult
    {
        public readonly LaunchStatus Status;

        /// <summary>The mode now live. The mode that was asked for when nothing started.</summary>
        public readonly SessionMode Mode;

        /// <summary>The specific cause, for the log and the network lab. Null on success.</summary>
        public readonly string Detail;

        public LaunchResult(LaunchStatus status, SessionMode mode, string detail)
        {
            Status = status;
            Mode = mode;
            Detail = detail;
        }

        /// <summary>True only when a session is now live in <see cref="Mode"/>.</summary>
        public bool Started => Status == LaunchStatus.Started;
    }

    /// <summary>
    /// What a networking layer implements so that online becomes reachable.
    ///
    /// <para>
    /// The seam, and deliberately the whole of it. Everything an online launch needs -
    /// signing in, allocating, connecting, becoming host or client - happens behind this one
    /// call, so the menu asks for online without knowing that Authentication, Lobby, Relay or
    /// a transport exist. Nothing implements it yet, which is why online currently refuses
    /// with <see cref="LaunchStatus.NoOnlineProvider"/> instead of quietly starting solo.
    /// </para>
    /// </summary>
    public interface IOnlineSessionProvider
    {
        /// <summary>
        /// Begins an online session, or explains why not.
        ///
        /// <para>
        /// Returning true must mean the returned session's <see cref="IMultiplayerSession.Mode"/>
        /// is <see cref="SessionMode.Online"/>, including while it is still connecting and
        /// including if it then fails. A failed online session is online and failed; it is not
        /// an offline one.
        /// </para>
        /// </summary>
        /// <param name="choice">Hosting or joining. Never <see cref="SessionChoice.OfflineSolo"/>.</param>
        /// <param name="joinTarget">
        /// What the player is joining - a code, a lobby id, whatever the provider uses. Null
        /// when hosting; the provider decides what it means.
        /// </param>
        bool TryBegin(SessionChoice choice, string joinTarget,
                      out IMultiplayerSession session,
                      out SessionAuthority.IAuthorityProvider authority,
                      out string error);
    }

    /// <summary>
    /// The one place a session begins, because somebody chose it.
    ///
    /// <para>
    /// <b>Nothing here runs at boot.</b> There is no <c>RuntimeInitializeOnLoadMethod</c> on
    /// this class and nothing calls it from a scene load. The process starts with the offline
    /// session <see cref="MultiplayerSessionService"/> holds by default, no online service is
    /// attempted, and airplane mode is a non-event. A session exists because a person pressed
    /// something.
    /// </para>
    ///
    /// <para>
    /// Before this existed, <c>MultiplayerSessionService.Install</c> had no callers at all:
    /// online was unreachable in the running game and offline was a default that nobody had
    /// chosen. Those are different bugs with the same symptom - a build that looks like it
    /// works because the only mode reachable is the one that needs nothing.
    /// </para>
    ///
    /// <para>
    /// <b>An online launch that fails does not become an offline one.</b> Every refusal path
    /// returns a status and leaves the current session alone. Falling back would hand a player
    /// who chose online a single-player mission and no error, which is the failure mode
    /// <c>Docs/MULTIPLAYER_RUNTIME_ARCHITECTURE.md</c> §7b exists to forbid.
    /// </para>
    /// </summary>
    public static class SessionLauncher
    {
        private static IOnlineSessionProvider _onlineProvider;

        /// <summary>
        /// Whether a networking layer is installed. False in every build today.
        ///
        /// <para>
        /// The play screen asks this to decide whether to offer online at all. Offering a
        /// button that can only fail is worse than not offering it.
        /// </para>
        /// </summary>
        public static bool HasOnlineProvider => _onlineProvider != null;

        /// <summary>
        /// Installs the networking layer that will serve online launches.
        ///
        /// <para>
        /// Called by the netcode layer when it initialises, not by gameplay and not by the
        /// menu. Registering a provider does not start anything: online still waits for the
        /// player to choose it.
        /// </para>
        /// </summary>
        public static void RegisterOnlineProvider(IOnlineSessionProvider provider)
        {
            if (provider == null)
            {
                CIYCLog.Error("SessionLauncher was handed a null online provider. " +
                              "Use ClearOnlineProvider to remove one.");
                return;
            }

            if (_onlineProvider != null && !ReferenceEquals(_onlineProvider, provider))
            {
                CIYCLog.Error("An online session provider is already registered. " +
                              "There is one networking layer; a second is the duplicate " +
                              "implementation this project keeps having to delete.");
                return;
            }

            _onlineProvider = provider;
        }

        /// <summary>
        /// Removes a provider, if it is the one that is registered.
        ///
        /// <para>
        /// Takes the provider rather than clearing blindly so that a layer shutting down
        /// cannot unregister a different one that has since replaced it.
        /// </para>
        /// </summary>
        public static void ClearOnlineProvider(IOnlineSessionProvider provider)
        {
            if (provider != null && !ReferenceEquals(_onlineProvider, provider))
                return;

            _onlineProvider = null;
        }

        /// <summary>
        /// Starts single player: one local player who is the host, with local authority.
        ///
        /// <para>
        /// This installs what the process already holds, which is the point - the mission
        /// begins because the player chose solo, not because nothing else happened. It touches
        /// no service, allocates nothing and cannot fail for a reason outside this device.
        /// </para>
        /// </summary>
        public static LaunchResult BeginOfflineSolo()
        {
            if (IsLive)
                return Refused(LaunchStatus.SessionAlreadyLive, SessionMode.Offline,
                               "a " + MultiplayerSessionService.Mode + " session is " +
                               MultiplayerSessionService.Current.State);

            MultiplayerSessionService.Install(CreateOfflineSession(),
                                              new SessionAuthority.LocalAuthority());

            CIYCLog.Info("Session choice: offline solo.");
            return new LaunchResult(LaunchStatus.Started, SessionMode.Offline, null);
        }

        /// <summary>
        /// Starts online, hosting or joining, through the registered networking layer.
        ///
        /// <para>
        /// With no layer registered this returns <see cref="LaunchStatus.NoOnlineProvider"/>
        /// and changes nothing. That is the honest answer for a build with no netcode in it,
        /// and it is deliberately not a silent offline session.
        /// </para>
        /// </summary>
        public static LaunchResult BeginOnline(SessionChoice choice, string joinTarget)
        {
            if (choice == SessionChoice.OfflineSolo)
                return Refused(LaunchStatus.NotAnOnlineChoice, SessionMode.Online,
                               "BeginOnline was asked for OfflineSolo; call BeginOfflineSolo");

            if (IsLive)
                return Refused(LaunchStatus.SessionAlreadyLive, SessionMode.Online,
                               "a " + MultiplayerSessionService.Mode + " session is " +
                               MultiplayerSessionService.Current.State);

            IOnlineSessionProvider provider = _onlineProvider;
            if (provider == null)
                return Refused(LaunchStatus.NoOnlineProvider, SessionMode.Online,
                               "no networking layer is installed in this build");

            if (!provider.TryBegin(choice, joinTarget,
                                   out IMultiplayerSession session,
                                   out SessionAuthority.IAuthorityProvider authority,
                                   out string error))
            {
                return Refused(LaunchStatus.OnlineProviderRefused, SessionMode.Online,
                               string.IsNullOrEmpty(error) ? "the networking layer declined"
                                                           : error);
            }

            // A provider that hands back an offline session, or none, has a bug. Installing it
            // would put somebody who chose online into single player silently, which is the one
            // outcome worse than telling them online failed.
            if (session == null || session.Mode != SessionMode.Online)
                return Refused(LaunchStatus.OnlineProviderReturnedWrongMode, SessionMode.Online,
                               session == null ? "the provider returned no session"
                                               : "the provider returned a " + session.Mode +
                                                 " session for an online launch");

            MultiplayerSessionService.Install(session, authority);

            CIYCLog.Info("Session choice: " + choice + ".");
            return new LaunchResult(LaunchStatus.Started, SessionMode.Online, null);
        }

        /// <summary>
        /// Whether something is running that a launch would have to trample.
        ///
        /// <para>
        /// Asks the state rather than the mode: the offline session the process holds by
        /// default reports <see cref="SessionState.Offline"/> and is not something anybody
        /// chose, so choosing solo over it is fine. An online session in any other state is.
        /// </para>
        /// </summary>
        private static bool IsLive =>
            MultiplayerSessionService.Current.State != SessionState.Offline;

        /// <summary>
        /// The only place this class makes an offline session.
        ///
        /// <para>
        /// One call site by construction, so that a fallback cannot be added to a failure path
        /// without it being obvious. The architecture guard counts them.
        /// </para>
        /// </summary>
        private static IMultiplayerSession CreateOfflineSession() => new OfflineSession();

        /// <summary>A refusal, logged, changing nothing.</summary>
        private static LaunchResult Refused(LaunchStatus status, SessionMode mode, string detail)
        {
            CIYCLog.Error("Refused to start a " + mode + " session: " + status + " (" +
                          detail + ").");
            return new LaunchResult(status, mode, detail);
        }
    }
}
