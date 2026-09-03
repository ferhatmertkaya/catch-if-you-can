using System;
using CatchIfYouCan.Procedural.Deterministic;

namespace CatchIfYouCan.Session
{
    /// <summary>Where a session is in its life. What the HUD and the lab display.</summary>
    public enum SessionState
    {
        /// <summary>No session. Single player lives here and works completely.</summary>
        Offline = 0,

        /// <summary>Signing in, allocating, or connecting.</summary>
        Connecting,

        /// <summary>Host or client, in a session.</summary>
        Connected,

        /// <summary>Leaving, or being dropped.</summary>
        Disconnecting,

        /// <summary>Ended by a fault rather than by a person. <see cref="IMultiplayerSession.LastError"/> says which.</summary>
        Failed,
    }

    /// <summary>What this peer is.</summary>
    public enum SessionRole
    {
        /// <summary>No session. Single player: the local player owns everything.</summary>
        SinglePlayer = 0,
        Host,
        Client,
    }

    /// <summary>
    /// The only multiplayer surface gameplay is allowed to see.
    ///
    /// <para>
    /// <b>No gameplay component knows what Relay is.</b> Not the ghost, not the equipment, not
    /// the mission, not the HUD. They ask this: am I the host, who is here, what is the match
    /// config, has the layout been agreed. Underneath, an implementation may use Netcode for
    /// GameObjects, a transport, a relay allocation and a lobby - or, today, nothing at all.
    /// </para>
    ///
    /// <para>
    /// That boundary is the whole reason this interface exists rather than gameplay calling a
    /// networking singleton. A service the ghost can reach into is a service the ghost will
    /// reach into, and then swapping it means touching the ghost.
    /// </para>
    ///
    /// <para>
    /// The dependency runs one way. This interface knows the deterministic contract types -
    /// <see cref="MatchConfig"/>, <see cref="JoinVerdict"/>, <see cref="LayoutHash"/> - and
    /// they know nothing about it. An implementation depends inward on both; nothing depends
    /// outward on an implementation.
    /// </para>
    /// </summary>
    public interface IMultiplayerSession
    {
        /// <summary>
        /// Which product this session is, chosen once and fixed for its lifetime.
        ///
        /// <para>
        /// Distinct from <see cref="State"/> and <see cref="Role"/>, which say what is
        /// happening. An online session that has not connected yet is
        /// <see cref="SessionMode.Online"/> in <see cref="SessionState.Connecting"/>, and one
        /// that failed is Online in <see cref="SessionState.Failed"/> - not offline. Reading
        /// the mode off the state is the inference this contract exists to forbid.
        /// </para>
        /// </summary>
        SessionMode Mode { get; }

        SessionState State { get; }
        SessionRole Role { get; }

        /// <summary>True when this process decides. True in single player, where there is no argument.</summary>
        bool IsHost { get; }

        /// <summary>How many players are in the session, including this one. One when offline.</summary>
        int PlayerCount { get; }

        /// <summary>
        /// The most players this session can hold, derived from the mode.
        ///
        /// <para>
        /// One offline; <see cref="MultiplayerProtocol.MaxPlayers"/> online. It is exposed here
        /// so that a player-count readout has a denominator to ask for rather than a literal to
        /// hard-code - "3 / 8" should get its 8 from the session, not from the UI.
        /// </para>
        /// </summary>
        int MaxPlayers { get; }

        /// <summary>
        /// The agreed configuration, once there is one. Its seed is what every peer generates
        /// from; a client must never replace it.
        /// </summary>
        MatchConfig Config { get; }

        /// <summary>
        /// Why the session last failed or refused somebody, or null. Carries the precise
        /// <see cref="JoinVerdict"/> cause for the log rather than the vaguer player text.
        /// </summary>
        string LastError { get; }

        /// <summary>Raised on any change to <see cref="State"/>, for the HUD and the lab.</summary>
        event Action<SessionState> StateChanged;

        /// <summary>Raised when a peer joins or leaves, with the new count.</summary>
        event Action<int> PlayerCountChanged;
    }

    /// <summary>
    /// What single player is: one player, who is the host, with no session at all.
    ///
    /// <para>
    /// A real implementation rather than a null object. Gameplay that asks "am I the host"
    /// offline should get "yes" - it is - and not a null reference or a special case at every
    /// call site. It is also what keeps single player working while multiplayer does not
    /// exist: every gate in the game asks the same questions and gets the same answers it
    /// always did.
    /// </para>
    /// </summary>
    public sealed class OfflineSession : IMultiplayerSession
    {
        /// <summary>
        /// Offline, always, for this object's whole life.
        ///
        /// <para>
        /// A connection appearing does not turn this into an online session. The player chose
        /// offline; reclassifying their solo mission because Wi-Fi came back would be a
        /// surprise, and the reverse - quietly telling someone who chose online that they are
        /// playing single player - is worse than an error message.
        /// </para>
        /// </summary>
        public SessionMode Mode => SessionMode.Offline;

        public SessionState State => SessionState.Offline;
        public SessionRole Role => SessionRole.SinglePlayer;
        public bool IsHost => true;
        public int PlayerCount => 1;

        /// <summary>One. Offline solo is exactly one local player, by contract.</summary>
        public int MaxPlayers => SessionModeRules.MaxPlayers(SessionMode.Offline);
        public MatchConfig Config => default;
        public string LastError => null;

        /// <summary>Never raised. Nothing about an offline session changes.</summary>
        public event Action<SessionState> StateChanged
        {
            add { }
            remove { }
        }

        /// <summary>Never raised. There is one player and there always will be.</summary>
        public event Action<int> PlayerCountChanged
        {
            add { }
            remove { }
        }
    }
}
