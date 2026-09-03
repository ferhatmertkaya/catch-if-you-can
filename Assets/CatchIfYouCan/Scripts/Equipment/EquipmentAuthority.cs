namespace CatchIfYouCan.Equipment
{
    /// <summary>
    /// Who is allowed to change the world, asked at the two places where equipment does.
    ///
    /// <para>
    /// <b>This is a seam, not a networking layer.</b> There is no netcode in this project and
    /// V3 does not add any - no Netcode for GameObjects, no transport, no relay, no lobby, no
    /// authentication. What this does is put the question in the code, at the call sites that
    /// will have to ask it, so that the answer can change in one file instead of in eleven
    /// items. Today the answer is always yes, because there is one player and they are the
    /// host.
    /// </para>
    ///
    /// <para>
    /// The split follows <c>Docs/NETWORKING.md</c> §4, which already commits to it:
    /// </para>
    ///
    /// <list type="bullet">
    /// <item><description>
    /// <b>Held-slot actions are owner-predicted.</b> Switching a torch on, changing a question,
    /// zooming a lens - these run locally and are corrected if the host disagrees, because
    /// making a player wait a round trip to press a button is what makes a game feel broken.
    /// They deliberately do not ask this class.
    /// </description></item>
    /// <item><description>
    /// <b>Putting something into the room is host state.</b> A placed camera is an object every
    /// player can see and interact with, so a client asks and the host decides. That is
    /// <see cref="CanChangeWorldState"/>.
    /// </description></item>
    /// <item><description>
    /// <b>Evidence is confirmed by the host, never claimed by a client.</b> §6 is explicit:
    /// clients never assert evidence. That is <see cref="CanConfirmEvidence"/>, and it is why
    /// <see cref="Evidence.EvidenceValidator"/> was built as a boundary in phase AH rather than
    /// as a rule inside each device.
    /// </description></item>
    /// </list>
    ///
    /// <para>
    /// Nothing here knows what a network is. When netcode arrives, step 7 of the build order
    /// replaces <see cref="Provider"/> with one that asks it; no item changes.
    /// </para>
    /// </summary>
    public static class EquipmentAuthority
    {
        /// <summary>
        /// What decides. Implemented by a netcode layer later; until then, by
        /// <see cref="LocalAuthority"/>.
        /// </summary>
        public interface IAuthorityProvider
        {
            /// <summary>Whether this process is the one that owns world state.</summary>
            bool IsHost { get; }

            /// <summary>Whether this process may install or remove an object in the room.</summary>
            bool CanChangeWorldState(EquipmentBase equipment);

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
            public bool CanChangeWorldState(EquipmentBase equipment) => true;
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

        public static bool CanChangeWorldState(EquipmentBase equipment) =>
            _provider.CanChangeWorldState(equipment);

        public static bool CanConfirmEvidence => _provider.CanConfirmEvidence;

        [UnityEngine.RuntimeInitializeOnLoadMethod(
            UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => _provider = new LocalAuthority();
    }
}
