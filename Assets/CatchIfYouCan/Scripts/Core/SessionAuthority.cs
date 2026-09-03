namespace CatchIfYouCan.Core
{
    /// <summary>
    /// Who decides, for the whole game. One question, one answer, one place to change it.
    ///
    /// <para>
    /// <b>This is a seam, not a networking layer.</b> There is still no netcode in this
    /// project. What this does is put the authority question at the call sites that will have
    /// to ask it, so the answer changes in one file instead of across the ghost, the equipment,
    /// the interactables and the evidence system. Today every answer is yes, because there is
    /// one player and they are the host.
    /// </para>
    ///
    /// <para>
    /// V3 introduced this as <c>EquipmentAuthority</c>, which was the right shape under the
    /// wrong name: <c>CanConfirmEvidence</c> was never an equipment question, and the ghost
    /// needs the same answer. Rather than add a second authority - which is precisely the
    /// mistake this repository has made twice, with two flashlights and two inventories -
    /// the provider lives here and <c>EquipmentAuthority</c> forwards to it. There is one
    /// provider. Setting it once changes every gate.
    /// </para>
    ///
    /// <para>
    /// The split follows <c>Docs/NETWORKING.md</c> §4. Owner-predicted actions - moving,
    /// looking, switching a torch on, changing a question, aiming a placement - deliberately do
    /// not ask, because making a player wait a round trip to press a button is what makes a
    /// game feel broken. Everything that changes what another player can see does ask.
    /// </para>
    /// </summary>
    public static class SessionAuthority
    {
        /// <summary>
        /// What decides. Implemented by a netcode layer later; until then, by
        /// <see cref="LocalAuthority"/>.
        /// </summary>
        public interface IAuthorityProvider
        {
            /// <summary>Whether this process owns world state.</summary>
            bool IsHost { get; }

            /// <summary>
            /// Whether this process may run the one authoritative ghost simulation: roaming,
            /// investigating, hunting, choosing a target, deciding an interaction.
            ///
            /// <para>
            /// Separate from <see cref="IsHost"/> so that a lab, a replay or a test can run
            /// ghost logic without claiming to own everything else.
            /// </para>
            /// </summary>
            bool CanSimulateGhost { get; }

            /// <summary>Whether this process may install or remove an object in the room.</summary>
            bool CanChangeWorldState(UnityEngine.Object subject);

            /// <summary>Whether this process may turn an observation into confirmed evidence.</summary>
            bool CanConfirmEvidence { get; }
        }

        /// <summary>
        /// One player, who is also the host. Every answer is yes, and that is correct rather
        /// than a stub: in a single-player process the local player really does own the world.
        /// </summary>
        public sealed class LocalAuthority : IAuthorityProvider
        {
            public bool IsHost => true;
            public bool CanSimulateGhost => true;
            public bool CanChangeWorldState(UnityEngine.Object subject) => true;
            public bool CanConfirmEvidence => true;
        }

        private static IAuthorityProvider _provider = new LocalAuthority();

        /// <summary>
        /// The current authority. Setting it to null restores the local one, so a networking
        /// layer shutting down cannot leave the game unable to do anything.
        /// </summary>
        public static IAuthorityProvider Provider
        {
            get => _provider;
            set => _provider = value ?? new LocalAuthority();
        }

        public static bool IsHost => _provider.IsHost;

        public static bool CanSimulateGhost => _provider.CanSimulateGhost;

        public static bool CanChangeWorldState(UnityEngine.Object subject) =>
            _provider.CanChangeWorldState(subject);

        public static bool CanConfirmEvidence => _provider.CanConfirmEvidence;

        [UnityEngine.RuntimeInitializeOnLoadMethod(
            UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => _provider = new LocalAuthority();
    }
}
