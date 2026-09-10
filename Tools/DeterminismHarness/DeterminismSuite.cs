using System;
using System.Collections.Generic;
using System.Text;
using CatchIfYouCan.Procedural;
using CatchIfYouCan.Procedural.Deterministic;
using CatchIfYouCan.Session;

namespace CatchIfYouCan.Tools
{
    /// <summary>
    /// The determinism suite, runnable without Unity.
    ///
    /// These are the same assertions as the Unity EditMode tests in
    /// Assets/CatchIfYouCan/Tests/EditMode. Both exist on purpose: this one runs in CI
    /// with no Unity licence and generates the golden table; the Unity one additionally
    /// proves that the real UnityEngine.Random-driven cosmetic systems cannot perturb
    /// generation inside the actual engine.
    /// </summary>
    public static class DeterminismSuite
    {
        /// <summary>
        /// The committed golden seed table. Regenerate with the "golden" command after a
        /// deliberate GenerationVersion bump - never to make a failing test pass.
        /// </summary>
        public static readonly int[] GoldenSeeds =
        {
            1, 7, 42, 1337, 184726392, 424242,
            999983, -1, -2147483648, 2147483647, 65536, 123456789
        };

        private static int _passed;
        private static int _failed;
        private static readonly List<string> Failures = new List<string>();

        public static bool RunAll()
        {
            _passed = 0;
            _failed = 0;
            Failures.Clear();

            Console.WriteLine("CATCH IF YOU CAN - determinism suite");
            Console.WriteLine($"generationVersion={GenerationVersion.Current}  algorithm={GenerationVersion.AlgorithmId}");
            Console.WriteLine();

            TestPcg32ReferenceVectors();
            TestA_RepeatedGeneration();
            TestB_InterleavedOtherContent();
            TestC_SimulatedTimingVariation();
            TestD_RetryPathIsClean();
            TestE_CosmeticRngDoesNotPerturb();
            TestF_GoldenSeeds();
            TestG_OrderPerturbation();
            TestStreamIsolation();
            TestDuplicateStableIdsRejected();
            TestValidationHolds();
            TestQuantizationContract();
            TestSessionHandshake();
            TestSessionCapacity();
            TestSessionMode();
            TestCharacterSelection();
            TestEquipmentOwnership();
            TestConnectionRating();
            TestReconnectPolicy();

            Console.WriteLine();
            Console.WriteLine($"passed: {_passed}   failed: {_failed}");
            if (_failed > 0)
            {
                Console.WriteLine();
                foreach (var f in Failures)
                    Console.WriteLine("  FAIL " + f);
            }

            return _failed == 0;
        }

        /// <summary>
        /// The bands a measured round trip falls in, and the two answers that are not
        /// measurements at all.
        /// </summary>
        private static void TestConnectionRating()
        {
            // The one thing a diagnostic must never do is report health it did not measure.
            Check("rating: no measurement is unknown, not good and not zero",
                ConnectionRating.Rate(ConnectionRating.NoMeasurement) ==
                ConnectionQuality.Unknown &&
                ConnectionRating.NoMeasurement < 0);

            Check("rating: an instant round trip is good, and is still a measurement",
                ConnectionRating.Rate(0) == ConnectionQuality.Good);

            Check("rating: each band ends where the next begins",
                ConnectionRating.Rate(ConnectionRating.GoodUpToMs) == ConnectionQuality.Good &&
                ConnectionRating.Rate(ConnectionRating.GoodUpToMs + 1) == ConnectionQuality.Fair &&
                ConnectionRating.Rate(ConnectionRating.FairUpToMs) == ConnectionQuality.Fair &&
                ConnectionRating.Rate(ConnectionRating.FairUpToMs + 1) == ConnectionQuality.Poor &&
                ConnectionRating.Rate(ConnectionRating.PoorUpToMs) == ConnectionQuality.Poor &&
                ConnectionRating.Rate(ConnectionRating.PoorUpToMs + 1) == ConnectionQuality.Lost);

            Check("rating: the bands are in order and do not overlap",
                ConnectionRating.GoodUpToMs < ConnectionRating.FairUpToMs &&
                ConnectionRating.FairUpToMs < ConnectionRating.PoorUpToMs);

            Check("rating: unknown and not-applicable are not measurements",
                !ConnectionRating.IsMeasured(ConnectionQuality.Unknown) &&
                !ConnectionRating.IsMeasured(ConnectionQuality.NotApplicable) &&
                ConnectionRating.IsMeasured(ConnectionQuality.Good) &&
                ConnectionRating.IsMeasured(ConnectionQuality.Lost));

            // Offline solo has no connection and is not a broken one.
            Check("rating: only a lost connection is unplayable",
                ConnectionRating.IsPlayable(ConnectionQuality.NotApplicable) &&
                ConnectionRating.IsPlayable(ConnectionQuality.Unknown) &&
                ConnectionRating.IsPlayable(ConnectionQuality.Poor) &&
                !ConnectionRating.IsPlayable(ConnectionQuality.Lost));

            Check("rating: offline and unmeasured read differently",
                ConnectionRating.Describe(ConnectionQuality.NotApplicable) !=
                ConnectionRating.Describe(ConnectionQuality.Unknown));

            var seen = new System.Collections.Generic.HashSet<string>();
            bool distinct = true;
            foreach (ConnectionQuality q in System.Enum.GetValues(typeof(ConnectionQuality)))
                if (!seen.Add(ConnectionRating.Describe(q)))
                    distinct = false;

            Check("rating: every quality has its own description", distinct);
        }

        /// <summary>
        /// When a dropped player tries again and how long their seat is held.
        ///
        /// <para>
        /// NOT PRODUCTION READY, and these checks are why that claim is honest rather than a
        /// disclaimer: the policy is exercised, the mechanism does not exist.
        /// </para>
        /// </summary>
        private static void TestReconnectPolicy()
        {
            Check("reconnect: the backoff doubles and then stops",
                ReconnectPolicy.BackoffSeconds(1) == 1 &&
                ReconnectPolicy.BackoffSeconds(2) == 2 &&
                ReconnectPolicy.BackoffSeconds(3) == 4 &&
                ReconnectPolicy.BackoffSeconds(4) == 8 &&
                ReconnectPolicy.BackoffSeconds(9) == ReconnectPolicy.MaxBackoffSeconds);

            Check("reconnect: a nonsensical attempt number does not produce a negative wait",
                ReconnectPolicy.BackoffSeconds(0) >= 1 &&
                ReconnectPolicy.BackoffSeconds(-4) >= 1);

            Check("reconnect: attempts run out at the limit",
                ReconnectPolicy.ShouldRetry(0) &&
                ReconnectPolicy.ShouldRetry(ReconnectPolicy.MaxAttempts - 1) &&
                !ReconnectPolicy.ShouldRetry(ReconnectPolicy.MaxAttempts) &&
                !ReconnectPolicy.ShouldRetry(ReconnectPolicy.MaxAttempts + 10));

            // The whole point of holding a seat: somebody who uses every attempt must still
            // have somewhere to land when the last one succeeds.
            Check("reconnect: the seat outlives the whole retry schedule",
                ReconnectPolicy.TotalBackoffSeconds(ReconnectPolicy.MaxAttempts) <
                ReconnectPolicy.SeatHeldSeconds);

            Check("reconnect: the seat expires at the limit and not before",
                !ReconnectPolicy.SeatExpired(ReconnectPolicy.SeatHeldSeconds - 1) &&
                ReconnectPolicy.SeatExpired(ReconnectPolicy.SeatHeldSeconds));

            // --- the schedule, walked ------------------------------------------------------
            Check("reconnect: the first moment after a drop is a wait, not an attempt",
                ReconnectPolicy.Next(0, 0) == ReconnectState.Waiting);

            Check("reconnect: the first attempt comes due after the first backoff",
                ReconnectPolicy.Next(0, ReconnectPolicy.BackoffSeconds(1)) ==
                ReconnectState.Retrying);

            Check("reconnect: attempt two waits out its own longer backoff",
                ReconnectPolicy.Next(1, 1) == ReconnectState.Waiting &&
                ReconnectPolicy.Next(1, 3) == ReconnectState.Retrying);

            Check("reconnect: running out of attempts gives up",
                ReconnectPolicy.Next(ReconnectPolicy.MaxAttempts, 20) == ReconnectState.GaveUp);

            // The seat is checked first on purpose: "attempt 3 of 4" is wrong in a way the
            // player cannot see once the host has filled their place.
            Check("reconnect: a lost seat outranks having attempts left",
                ReconnectPolicy.Next(0, ReconnectPolicy.SeatHeldSeconds) ==
                ReconnectState.SeatLost &&
                ReconnectPolicy.Next(ReconnectPolicy.MaxAttempts,
                                     ReconnectPolicy.SeatHeldSeconds) ==
                ReconnectState.SeatLost);

            Check("reconnect: both failures are terminal and neither is Connected",
                ReconnectPolicy.IsTerminal(ReconnectState.GaveUp) &&
                ReconnectPolicy.IsTerminal(ReconnectState.SeatLost) &&
                !ReconnectPolicy.IsTerminal(ReconnectState.Waiting) &&
                !ReconnectPolicy.IsTerminal(ReconnectState.Retrying) &&
                !ReconnectPolicy.IsTerminal(ReconnectState.Connected));

            Check("reconnect: the two failures are not told to the player the same way",
                ReconnectPolicy.Describe(ReconnectState.GaveUp) !=
                ReconnectPolicy.Describe(ReconnectState.SeatLost));

            // Nothing in the schedule may stall: every second from the drop to the seat
            // expiring must produce a state, and it must never go backwards to Connected.
            bool wellFormed = true;
            for (int attempts = 0; attempts <= ReconnectPolicy.MaxAttempts; attempts++)
                for (int t = 0; t <= ReconnectPolicy.SeatHeldSeconds; t++)
                    if (ReconnectPolicy.Next(attempts, t) == ReconnectState.Connected)
                        wellFormed = false;

            Check("reconnect: a dropped peer is never reported as connected", wellFormed);
        }

        /// <summary>
        /// Whose equipment is whose, and who may act on it.
        ///
        /// <para>
        /// Two players reaching for the same torch on the same frame is not an edge case, and
        /// the only way one of them loses is if exactly one machine decides. These are the
        /// answers that machine gives.
        /// </para>
        /// </summary>
        private static void TestEquipmentOwnership()
        {
            const int nobody = EquipmentOwnership.Nobody;
            const int solo = MultiplayerProtocol.LocalOnlyClientId;
            const int host = 0;
            const int guest = 3;

            // The trap this contract was built around: -1 is a real player, so an item the
            // solo player is carrying must not read as unowned.
            Check("ownership: nobody is not the offline player",
                EquipmentOwnership.Nobody != MultiplayerProtocol.LocalOnlyClientId);

            Check("ownership: nobody is not the first networked client either",
                EquipmentOwnership.Nobody != 0);

            Check("ownership: the offline player is a player",
                MultiplayerProtocol.IsPlayer(solo) && MultiplayerProtocol.IsPlayer(host) &&
                MultiplayerProtocol.IsPlayer(MultiplayerProtocol.MaxPlayers - 1));

            Check("ownership: nobody is not a player",
                !MultiplayerProtocol.IsPlayer(nobody) && !MultiplayerProtocol.IsPlayer(-5));

            Check("ownership: an unowned item is unowned and an owned one is not",
                !EquipmentOwnership.IsOwned(nobody) &&
                EquipmentOwnership.IsOwned(solo) &&
                EquipmentOwnership.IsOwned(host));

            // --- picking things up --------------------------------------------------------
            Check("ownership: an item on the floor is granted",
                EquipmentOwnership.Claim(EquipmentHold.InWorld, nobody, guest) ==
                EquipmentClaimVerdict.Granted);

            Check("ownership: a placed item is granted to somebody else - a camera can be moved",
                EquipmentOwnership.Claim(EquipmentHold.Placed, host, guest) ==
                EquipmentClaimVerdict.Granted);

            Check("ownership: an item somebody else is carrying is refused",
                EquipmentOwnership.Claim(EquipmentHold.Carried, host, guest) ==
                EquipmentClaimVerdict.CarriedBySomebodyElse);

            Check("ownership: claiming what you already carry is not a failure",
                EquipmentOwnership.Claim(EquipmentHold.Carried, guest, guest) ==
                EquipmentClaimVerdict.AlreadyYours);

            Check("ownership: a claim from nobody is refused as a routing bug",
                EquipmentOwnership.Claim(EquipmentHold.InWorld, nobody, nobody) ==
                EquipmentClaimVerdict.InvalidClaimant);

            // Offline is the whole game today, so it had better be ordinary.
            Check("ownership: the solo player picks up an item on the floor",
                EquipmentOwnership.Claim(EquipmentHold.InWorld, nobody, solo) ==
                EquipmentClaimVerdict.Granted);

            Check("ownership: the solo player keeps what they are carrying",
                EquipmentOwnership.Claim(EquipmentHold.Carried, solo, solo) ==
                EquipmentClaimVerdict.AlreadyYours);

            // --- what a verdict means -----------------------------------------------------
            Check("ownership: holding covers both granted and already-yours",
                EquipmentOwnership.Holds(EquipmentClaimVerdict.Granted) &&
                EquipmentOwnership.Holds(EquipmentClaimVerdict.AlreadyYours) &&
                !EquipmentOwnership.Holds(EquipmentClaimVerdict.CarriedBySomebodyElse) &&
                !EquipmentOwnership.Holds(EquipmentClaimVerdict.InvalidClaimant) &&
                !EquipmentOwnership.Holds(EquipmentClaimVerdict.NotAuthoritative));

            // The distinction a host broadcasts on. Already-yours holds but changes nothing,
            // and sending it would be an ownership update per frame the button is held.
            Check("ownership: only a grant changes who owns it",
                EquipmentOwnership.ChangesOwner(EquipmentClaimVerdict.Granted) &&
                !EquipmentOwnership.ChangesOwner(EquipmentClaimVerdict.AlreadyYours));

            // --- who may press the button -------------------------------------------------
            Check("ownership: a carried item answers only to its carrier",
                EquipmentOwnership.MayUse(EquipmentHold.Carried, guest, guest) &&
                !EquipmentOwnership.MayUse(EquipmentHold.Carried, guest, host));

            Check("ownership: an item nobody carries is a question of reach, not ownership",
                EquipmentOwnership.MayUse(EquipmentHold.InWorld, nobody, guest) &&
                EquipmentOwnership.MayUse(EquipmentHold.Placed, host, guest));

            Check("ownership: nobody cannot use anything",
                !EquipmentOwnership.MayUse(EquipmentHold.InWorld, nobody, nobody) &&
                !EquipmentOwnership.MayUse(EquipmentHold.Placed, host, nobody));

            Check("ownership: carried-by is exact, not merely owned",
                EquipmentOwnership.IsCarriedBy(EquipmentHold.Carried, guest, guest) &&
                !EquipmentOwnership.IsCarriedBy(EquipmentHold.Carried, guest, host) &&
                !EquipmentOwnership.IsCarriedBy(EquipmentHold.Placed, guest, guest) &&
                !EquipmentOwnership.IsCarriedBy(EquipmentHold.InWorld, nobody, nobody));

            // --- a contest, played out ----------------------------------------------------
            int owner = nobody;
            var hold = EquipmentHold.InWorld;

            var first = EquipmentOwnership.Claim(hold, owner, host);
            if (EquipmentOwnership.ChangesOwner(first))
            {
                owner = host;
                hold = EquipmentHold.Carried;
            }

            var second = EquipmentOwnership.Claim(hold, owner, guest);

            Check("contest: the first claim wins and the second is told why",
                first == EquipmentClaimVerdict.Granted &&
                second == EquipmentClaimVerdict.CarriedBySomebodyElse &&
                owner == host);

            // Dropped: belongs to nobody again, and the loser of the contest can have it.
            owner = nobody;
            hold = EquipmentHold.InWorld;

            Check("contest: once it is dropped the other player may take it",
                EquipmentOwnership.Claim(hold, owner, guest) == EquipmentClaimVerdict.Granted);

            Check("ownership: every verdict has its own description",
                DistinctOwnershipDescriptions());
        }

        private static bool DistinctOwnershipDescriptions()
        {
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (EquipmentClaimVerdict v in
                     System.Enum.GetValues(typeof(EquipmentClaimVerdict)))
                if (!seen.Add(EquipmentOwnership.Describe(v)))
                    return false;

            return true;
        }

        /// <summary>
        /// The compact character index and what a host does with the one it is handed.
        ///
        /// <para>
        /// An index arriving from another machine is a claim. Every case here is one a peer
        /// can produce - nothing chosen, a stale index from an older catalog, a number nobody
        /// could have meant - and none of them may come back as something that would index
        /// outside the catalog.
        /// </para>
        /// </summary>
        private static void TestCharacterSelection()
        {
            // --- the ordinary case --------------------------------------------------------
            Check("character: a valid index is accepted",
                CharacterSelection.Check(2, 4) == CharacterVerdict.Accepted);

            Check("character: an accepted index is used as sent",
                CharacterSelection.Resolve(2, 4) == 2);

            Check("character: the first and last entries are both reachable",
                CharacterSelection.Resolve(0, 4) == 0 &&
                CharacterSelection.Resolve(3, 4) == 3);

            // --- claims a peer can actually send ------------------------------------------
            Check("character: one past the end is out of range",
                CharacterSelection.Check(4, 4) == CharacterVerdict.OutOfRange);

            Check("character: an out-of-range claim becomes the default, not an exception",
                CharacterSelection.Resolve(4, 4) == CharacterSelection.Fallback);

            Check("character: a wildly out-of-range claim is substituted too",
                CharacterSelection.Resolve(4000, 4) == CharacterSelection.Fallback &&
                CharacterSelection.Resolve(int.MaxValue, 4) == CharacterSelection.Fallback);

            Check("character: a negative claim is substituted rather than clamped into a crash",
                CharacterSelection.Resolve(-7, 4) == CharacterSelection.Fallback &&
                CharacterSelection.Resolve(int.MinValue, 4) == CharacterSelection.Fallback);

            // Unset and hostile both end up as character zero, and the verdict is what tells
            // the host's log which of the two happened.
            Check("character: unset is its own verdict, not an out-of-range one",
                CharacterSelection.Check(CharacterSelection.Unset, 4) == CharacterVerdict.Unset);

            Check("character: unset resolves to the default",
                CharacterSelection.Resolve(CharacterSelection.Unset, 4) ==
                CharacterSelection.Fallback);

            Check("character: unset is not the same value as character zero",
                CharacterSelection.Unset != 0);

            // --- nothing to choose from ---------------------------------------------------
            Check("character: an empty catalog is reported as empty",
                CharacterSelection.Check(0, 0) == CharacterVerdict.EmptyCatalog &&
                CharacterSelection.Check(0, -1) == CharacterVerdict.EmptyCatalog);

            Check("character: an empty catalog resolves to unset, never to index zero",
                CharacterSelection.Resolve(0, 0) == CharacterSelection.Unset);

            // --- the encoding limit is a content limit ------------------------------------
            Check("character: a catalog past the encoding limit is reported",
                CharacterSelection.Check(0, CharacterSelection.MaxCharacters + 1) ==
                CharacterVerdict.CatalogTooLarge);

            Check("character: an index past the encoding limit resolves to the default",
                CharacterSelection.Resolve(CharacterSelection.MaxCharacters, 1000) ==
                CharacterSelection.Fallback);

            Check("character: the last nameable index still resolves to itself",
                CharacterSelection.Resolve(CharacterSelection.MaxCharacters - 1, 1000) ==
                CharacterSelection.MaxCharacters - 1);

            // --- the wire round trip ------------------------------------------------------
            bool roundTrips = true;
            for (int i = 0; i < CharacterSelection.MaxCharacters; i++)
                if (CharacterSelection.Decode(CharacterSelection.Encode(i)) != i)
                    roundTrips = false;

            Check("character: every nameable index survives the wire round trip", roundTrips);

            Check("character: unset survives the wire round trip",
                CharacterSelection.Decode(
                    CharacterSelection.Encode(CharacterSelection.Unset)) ==
                CharacterSelection.Unset);

            Check("character: an unencodable index becomes unset rather than another character",
                CharacterSelection.Encode(CharacterSelection.MaxCharacters) ==
                CharacterSelection.UnsetWire &&
                CharacterSelection.Encode(-3) == CharacterSelection.UnsetWire);

            Check("character: no byte decodes to something Resolve would refuse to place",
                AllBytesResolveInside(4));

            Check("character: only Accepted counts as accepted",
                CharacterSelection.IsAccepted(CharacterVerdict.Accepted) &&
                !CharacterSelection.IsAccepted(CharacterVerdict.Unset) &&
                !CharacterSelection.IsAccepted(CharacterVerdict.OutOfRange) &&
                !CharacterSelection.IsAccepted(CharacterVerdict.EmptyCatalog) &&
                !CharacterSelection.IsAccepted(CharacterVerdict.CatalogTooLarge));

            Check("character: every verdict has its own description",
                DistinctDescriptions());
        }

        /// <summary>
        /// Every one of the 256 bytes a peer could send, resolved against a real catalog size.
        /// None may come back outside it - a resolve that did would be an index into a live
        /// array from an untrusted number.
        /// </summary>
        private static bool AllBytesResolveInside(int catalogCount)
        {
            for (int b = 0; b <= 255; b++)
            {
                int resolved = CharacterSelection.Resolve(
                    CharacterSelection.Decode((byte)b), catalogCount);

                if (resolved < 0 || resolved >= catalogCount)
                    return false;
            }

            return true;
        }

        private static bool DistinctDescriptions()
        {
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (CharacterVerdict v in System.Enum.GetValues(typeof(CharacterVerdict)))
                if (!seen.Add(CharacterSelection.Describe(v)))
                    return false;

            return true;
        }

        /// <summary>
        /// The join handshake and mismatch protocol of Docs/NETWORKING.md §3 and §5.
        /// Transport-neutral, so it is testable here without Unity or a netcode package —
        /// which is the point of keeping it that way.
        /// </summary>
        private static void TestSessionHandshake()
        {
            var map = Map();
            var content = Content();
            var host = MatchConfig.CreateAuthoritative(424242, map, content);

            Check("handshake: capacity has one source and it is 8",
                MultiplayerProtocol.MaxPlayers == 8);

            Check("handshake: a session is viable with the host alone",
                MultiplayerProtocol.MinPlayers == 1);

            Check("handshake: identical configs admit",
                SessionCompatibility.CheckJoin(host, host, 1) == JoinVerdict.Admit);

            Check("handshake: a full lobby is refused before anything else is inspected",
                SessionCompatibility.CheckJoin(host, host, MultiplayerProtocol.MaxPlayers) == JoinVerdict.LobbyFull);

            Check("handshake: capacity admits up to but not beyond MaxPlayers",
                MultiplayerProtocol.HasCapacityFor(MultiplayerProtocol.MaxPlayers - 1) &&
                !MultiplayerProtocol.HasCapacityFor(MultiplayerProtocol.MaxPlayers));

            var otherProtocol = new MatchConfig(host.ProtocolVersion + 1, host.GenerationVersion,
                host.Seed, host.MapDefinitionId, host.ContentHash);
            Check("handshake: protocol mismatch is refused",
                SessionCompatibility.CheckJoin(host, otherProtocol, 1) == JoinVerdict.ProtocolMismatch);

            var otherGeneration = new MatchConfig(host.ProtocolVersion, host.GenerationVersion + 1,
                host.Seed, host.MapDefinitionId, host.ContentHash);
            Check("handshake: generation version mismatch is refused",
                SessionCompatibility.CheckJoin(host, otherGeneration, 1) == JoinVerdict.GenerationVersionMismatch);

            var otherContent = new MatchConfig(host.ProtocolVersion, host.GenerationVersion,
                host.Seed, host.MapDefinitionId, host.ContentHash ^ 0xFFUL);
            Check("handshake: content mismatch is refused",
                SessionCompatibility.CheckJoin(host, otherContent, 1) == JoinVerdict.ContentMismatch);

            var otherMap = new MatchConfig(host.ProtocolVersion, host.GenerationVersion,
                host.Seed, host.MapDefinitionId + "_x", host.ContentHash);
            Check("handshake: map mismatch is refused",
                SessionCompatibility.CheckJoin(host, otherMap, 1) == JoinVerdict.MapMismatch);

            var noSeed = new MatchConfig(host.ProtocolVersion, host.GenerationVersion,
                0, host.MapDefinitionId, host.ContentHash);
            Check("handshake: a config with no host-rolled seed is refused",
                SessionCompatibility.CheckJoin(host, noSeed, 1) == JoinVerdict.SeedMissing);

            // §5: protocol is checked before content, so a peer that disagrees about the
            // handshake layout is not reported as a content problem.
            var bothWrong = new MatchConfig(host.ProtocolVersion + 1, host.GenerationVersion,
                host.Seed, host.MapDefinitionId, host.ContentHash ^ 0xFFUL);
            Check("handshake: protocol outranks content when both differ",
                SessionCompatibility.CheckJoin(host, bothWrong, 1) == JoinVerdict.ProtocolMismatch);

            Check("handshake: every refusal aborts before generation",
                SessionCompatibility.AbortsBeforeGeneration(JoinVerdict.ContentMismatch) &&
                SessionCompatibility.AbortsBeforeGeneration(JoinVerdict.LobbyFull) &&
                !SessionCompatibility.AbortsBeforeGeneration(JoinVerdict.Admit));

            // Stage two: two peers that generated from the same admitted config must agree.
            var hostHash = HashFor(host.Seed);
            var peerHash = HashFor(host.Seed);
            Check("handshake: same config generates an agreeing layout",
                SessionCompatibility.CheckLayout(hostHash, peerHash, out _) == LayoutVerdict.Match);

            var divergent = HashFor(host.Seed + 1);
            var verdict = SessionCompatibility.CheckLayout(hostHash, divergent, out string diagnostic);
            Check("handshake: a divergent layout is caught",
                verdict == LayoutVerdict.Mismatch);
            Check("handshake: a divergent layout names the differing section",
                !string.IsNullOrEmpty(diagnostic), diagnostic);

            Check("handshake: config hash is stable across equal configs",
                host.ConfigHash() == MatchConfig.CreateAuthoritative(424242, map, content).ConfigHash());
            Check("handshake: config hash separates different seeds",
                host.ConfigHash() != MatchConfig.CreateAuthoritative(424243, map, content).ConfigHash());
        }

        // ------------------------------------------------------------------ helpers

        private static ContentSnapshot Content() => ContentSnapshot.CreateFallback();
        private static MapDefinition Map() => MapDefinition.HouseDefault;

        private static LayoutHash HashFor(int seed)
        {
            var layout = HouseLayoutBuilder.Generate(seed, Map(), Content(), out _);
            return LayoutHasher.Compute(layout);
        }

        /// <summary>
        /// The eight-player capacity contract, at every boundary that matters.
        ///
        /// <para>
        /// Capacity is the kind of rule that is obviously right when written and quietly wrong
        /// one edit later: an off-by-one admits a ninth player, and a clamp turns a malformed
        /// count into a silent success. These are the cases, spelled out, so a future change to
        /// MaxPlayers is either correct or loud.
        /// </para>
        ///
        /// <para>
        /// Every expectation is derived from <c>MultiplayerProtocol</c> rather than restated,
        /// except where the point of the check IS the literal - the contract says eight, and a
        /// test that reads the constant to check the constant proves nothing.
        /// </para>
        /// </summary>
        private static void TestSessionCapacity()
        {
            var map = Map();
            var content = Content();
            var host = MatchConfig.CreateAuthoritative(20250903, map, content);

            // --- the population ladder -------------------------------------------------
            //
            // The current count includes the host, so "current = 7, join one" is the eighth
            // player arriving and must be admitted.
            Check("capacity: an empty session admits the host",
                MultiplayerProtocol.HasCapacityFor(0));

            Check("capacity: 1 admits a second",
                MultiplayerProtocol.HasCapacityFor(1));

            Check("capacity: 2 admits a third",
                MultiplayerProtocol.HasCapacityFor(2));

            Check("capacity: 4 admits a fifth - four is no longer the limit",
                MultiplayerProtocol.HasCapacityFor(4));

            Check("capacity: 6 admits a seventh",
                MultiplayerProtocol.HasCapacityFor(6));

            Check("capacity: 7 admits the eighth",
                MultiplayerProtocol.HasCapacityFor(7));

            Check("capacity: 8 is full",
                !MultiplayerProtocol.HasCapacityFor(8));

            Check("capacity: 9 is full and stays full",
                !MultiplayerProtocol.HasCapacityFor(9));

            // A negative count cannot come from counting real players, so it means the caller
            // is confused. Refused rather than clamped: treating -1 as "plenty of room" would
            // admit peers into a session nobody can describe.
            Check("capacity: a negative population is refused rather than clamped",
                !MultiplayerProtocol.HasCapacityFor(-1));

            // --- the same ladder through the real handshake -----------------------------
            Check("capacity: the handshake admits the 2nd player",
                SessionCompatibility.CheckJoin(host, host, 1) == JoinVerdict.Admit);

            Check("capacity: the handshake admits the 5th player",
                SessionCompatibility.CheckJoin(host, host, 4) == JoinVerdict.Admit);

            Check("capacity: the handshake admits the 8th player",
                SessionCompatibility.CheckJoin(host, host, 7) == JoinVerdict.Admit);

            Check("capacity: the handshake refuses the 9th with LobbyFull",
                SessionCompatibility.CheckJoin(host, host, 8) == JoinVerdict.LobbyFull);

            // Capacity is tested before anything else, so a full session says it is full
            // rather than blaming a version the peer cannot do anything about.
            var wrongProtocol = new MatchConfig(host.ProtocolVersion + 1, host.GenerationVersion,
                host.Seed, host.MapDefinitionId, host.ContentHash);
            Check("capacity: a full session reports LobbyFull even when the peer is also incompatible",
                SessionCompatibility.CheckJoin(host, wrongProtocol, 8) == JoinVerdict.LobbyFull);

            // --- disconnect and rejoin --------------------------------------------------
            //
            // Capacity has no memory: it is a function of the current population. Walking the
            // sequence proves a seat freed by a departure is genuinely reusable.
            int population = MultiplayerProtocol.MaxPlayers;
            Check("rejoin: a full session refuses",
                !MultiplayerProtocol.HasCapacityFor(population));

            population--;
            Check("rejoin: one player leaves and the session is 7 of 8",
                population == 7 && MultiplayerProtocol.HasCapacityFor(population));

            Check("rejoin: the freed seat admits a new peer",
                SessionCompatibility.CheckJoin(host, host, population) == JoinVerdict.Admit);

            population++;
            Check("rejoin: the session is full again at 8",
                population == MultiplayerProtocol.MaxPlayers &&
                !MultiplayerProtocol.HasCapacityFor(population));

            // --- the host occupies a seat -----------------------------------------------
            //
            // Eight means one host plus seven clients. Reading it as host-plus-eight is the
            // single most likely misreading of this contract and it produces nine players.
            Check("topology: the maximum is 1 host and 7 clients, totalling 8",
                1 + 7 == MultiplayerProtocol.MaxPlayers);

            Check("topology: a host alone is a valid session, not a degenerate one",
                MultiplayerProtocol.MinPlayers == 1 && MultiplayerProtocol.HasCapacityFor(1));

            // --- protocol identity ------------------------------------------------------
            Check("protocol: the capacity change carries a protocol version that reflects it",
                MultiplayerProtocol.Version >= 2);

            Check("protocol: builds that disagree about the handshake refuse each other",
                SessionCompatibility.CheckJoin(host, wrongProtocol, 1)
                    == JoinVerdict.ProtocolMismatch);

            // Player capacity is not procedural generation. If this ever fails because
            // MaxPlayers moved, something has confused two unrelated contracts.
            Check("protocol: generation version is untouched by a capacity change",
                GenerationVersion.Current == host.GenerationVersion);

            Check("protocol: the tick rate is untouched by a capacity change",
                MultiplayerProtocol.ServerTickHz == 20);
        }

        /// <summary>
        /// The offline/online product contract: what each mode permits, and that the mode is a
        /// choice rather than something read off the weather.
        ///
        /// <para>
        /// These run in the engine-free harness because <c>SessionMode</c> is deliberately pure.
        /// That is the only reason this contract gets tested at all right now - the rest of the
        /// session layer needs Unity, and Unity is not available.
        /// </para>
        /// </summary>
        private static void TestSessionMode()
        {
            // --- offline is exactly one player, and needs nothing --------------------------
            Check("offline: exactly one player",
                SessionModeRules.MaxPlayers(SessionMode.Offline) == 1 &&
                SessionModeRules.MinPlayers(SessionMode.Offline) == 1);

            Check("offline: one player is a valid population",
                SessionModeRules.IsValidPopulation(SessionMode.Offline, 1));

            Check("offline: two players is not",
                !SessionModeRules.IsValidPopulation(SessionMode.Offline, 2));

            Check("offline: zero players is not",
                !SessionModeRules.IsValidPopulation(SessionMode.Offline, 0));

            // The whole point of the mode. Authentication, Lobby, Relay and the transport all
            // ask this before doing anything, which is what makes airplane mode a non-event:
            // the services are never attempted, so they cannot fail.
            Check("offline: online services are not permitted",
                !SessionModeRules.AllowsOnlineServices(SessionMode.Offline));

            Check("offline: a remote player cannot exist",
                !SessionModeRules.AllowsRemotePlayers(SessionMode.Offline));

            // --- online is one to eight ---------------------------------------------------
            Check("online: capacity is the protocol maximum and nothing else",
                SessionModeRules.MaxPlayers(SessionMode.Online) == MultiplayerProtocol.MaxPlayers);

            Check("online: a host alone is valid - 1 of 8 is waiting for friends",
                SessionModeRules.IsValidPopulation(SessionMode.Online, 1));

            Check("online: two through eight are valid",
                SessionModeRules.IsValidPopulation(SessionMode.Online, 2) &&
                SessionModeRules.IsValidPopulation(SessionMode.Online, 4) &&
                SessionModeRules.IsValidPopulation(SessionMode.Online, 7) &&
                SessionModeRules.IsValidPopulation(SessionMode.Online, 8));

            Check("online: nine is not a valid population",
                !SessionModeRules.IsValidPopulation(SessionMode.Online, 9));

            Check("online: zero is not a valid population - somebody has to be hosting",
                !SessionModeRules.IsValidPopulation(SessionMode.Online, 0));

            Check("online: services are permitted",
                SessionModeRules.AllowsOnlineServices(SessionMode.Online));

            Check("online: remote players may exist",
                SessionModeRules.AllowsRemotePlayers(SessionMode.Online));

            // --- the modes are actually different -----------------------------------------
            //
            // If these ever collapse into each other, mode has stopped meaning anything and
            // every rule above is decorative.
            Check("mode: offline and online are distinct capacities",
                SessionModeRules.MaxPlayers(SessionMode.Offline) !=
                SessionModeRules.MaxPlayers(SessionMode.Online));

            Check("mode: offline and online disagree about online services",
                SessionModeRules.AllowsOnlineServices(SessionMode.Offline) !=
                SessionModeRules.AllowsOnlineServices(SessionMode.Online));

            // Offline capacity is a product decision - one player - and must not silently
            // follow the protocol maximum if that changes again.
            Check("mode: offline capacity does not track the online maximum",
                SessionModeRules.MaxPlayers(SessionMode.Offline) == 1 &&
                MultiplayerProtocol.MaxPlayers != 1);
        }

        private static void Check(string name, bool condition, string detail = null)
        {
            if (condition)
            {
                _passed++;
                Console.WriteLine($"  ok    {name}");
            }
            else
            {
                _failed++;
                Failures.Add(name + (detail != null ? " - " + detail : ""));
                Console.WriteLine($"  FAIL  {name}{(detail != null ? " - " + detail : "")}");
            }
        }

        // ------------------------------------------------------------------ tests

        /// <summary>
        /// PCG32 must match the reference stream, otherwise "deterministic" only means
        /// "consistently wrong" and a future rewrite silently changes every stored seed.
        /// </summary>
        private static void TestPcg32ReferenceVectors()
        {
            var rng = new CiycRandom(42UL, 54UL);
            uint[] expected = { 0xA15C02B7u, 0x7B47F409u, 0xBA1D3330u, 0x83D2F293u, 0xBFA4784Bu, 0xCBED606Eu };
            bool ok = true;
            var actual = new uint[expected.Length];
            for (int i = 0; i < expected.Length; i++)
            {
                actual[i] = rng.NextUInt();
                if (actual[i] != expected[i])
                    ok = false;
            }

            Check("PCG32 matches published reference vectors (seed=42, seq=54)", ok,
                ok ? null : "got " + string.Join(",", Array.ConvertAll(actual, v => v.ToString("X8"))));
        }

        /// <summary>TEST A - same seed, 100 generations, one hash.</summary>
        private static void TestA_RepeatedGeneration()
        {
            bool allMatch = true;
            string detail = null;

            foreach (int seed in new[] { 42, 184726392, -7 })
            {
                var first = HashFor(seed);
                for (int i = 0; i < 100; i++)
                {
                    var again = HashFor(seed);
                    if (again.FinalHash != first.FinalHash)
                    {
                        allMatch = false;
                        detail = $"seed {seed} iteration {i}: {first.Final} vs {again.Final}";
                        break;
                    }
                }

                if (!allMatch) break;
            }

            Check("A: same seed generated 100x yields one hash", allMatch, detail);
        }

        /// <summary>TEST B - unrelated generation between runs must not perturb the result.</summary>
        private static void TestB_InterleavedOtherContent()
        {
            var a1 = HashFor(42);

            for (int i = 0; i < 25; i++)
                HashFor(1000 + i);

            var a2 = HashFor(42);

            // Also interleave a DIFFERENT map, which exercises different draw counts.
            HouseLayoutBuilder.Generate(777, MapDefinition.HouseTraining, Content(), out _);
            var a3 = HashFor(42);

            Check("B: interleaved unrelated generation leaves the hash unchanged",
                a1.FinalHash == a2.FinalHash && a1.FinalHash == a3.FinalHash,
                $"{a1.Final} / {a2.Final} / {a3.Final}");
        }

        /// <summary>
        /// TEST C - timing variation. Stage A reads no clock and no frame counter, so
        /// variable elapsed time between and during runs must be invisible.
        /// </summary>
        private static void TestC_SimulatedTimingVariation()
        {
            var baseline = HashFor(424242);
            bool ok = true;
            string detail = null;

            for (int frame = 0; frame < 10; frame++)
            {
                // Burn a varying amount of wall-clock time and work between generations.
                long spin = 0;
                for (int i = 0; i < frame * 5000; i++)
                    spin += i;
                System.Threading.Thread.Sleep(frame % 3);
                GC.KeepAlive(spin);

                var again = HashFor(424242);
                if (again.FinalHash != baseline.FinalHash)
                {
                    ok = false;
                    detail = $"frame {frame}: {baseline.Final} vs {again.Final}";
                    break;
                }
            }

            Check("C: variable elapsed time and work between runs changes nothing", ok, detail);
        }

        /// <summary>
        /// TEST D - the retry path. Each attempt is pure data, so attempt N must be
        /// reproducible on its own and must not be able to see attempt N-1.
        /// </summary>
        private static void TestD_RetryPathIsClean()
        {
            bool ok = true;
            string detail = null;

            foreach (int seed in new[] { 42, 424242, 999983 })
            {
                for (int attempt = 0; attempt < HouseLayoutBuilder.MaxAttempts; attempt++)
                {
                    // Built cold.
                    var cold = LayoutHasher.Compute(HouseLayoutBuilder.Build(seed, Map(), Content(), attempt));

                    // Built after every preceding attempt has already run in this process.
                    for (int prior = 0; prior < attempt; prior++)
                        HouseLayoutBuilder.Build(seed, Map(), Content(), prior);
                    var warm = LayoutHasher.Compute(HouseLayoutBuilder.Build(seed, Map(), Content(), attempt));

                    if (cold.FinalHash != warm.FinalHash)
                    {
                        ok = false;
                        detail = $"seed {seed} attempt {attempt}: cold {cold.Final} vs warm {warm.Final}";
                        break;
                    }
                }

                if (!ok) break;
            }

            Check("D: retry attempts are reproducible and uncontaminated by earlier attempts", ok, detail);

            // Distinct attempts should produce distinct layouts, or retrying is pointless.
            var a0 = LayoutHasher.Compute(HouseLayoutBuilder.Build(42, Map(), Content(), 0));
            var a1 = LayoutHasher.Compute(HouseLayoutBuilder.Build(42, Map(), Content(), 1));
            Check("D: consecutive attempts explore different layouts", a0.FinalHash != a1.FinalHash);
        }

        /// <summary>
        /// TEST E - cosmetic randomness. The harness has no UnityEngine.Random, so it
        /// hammers System.Random and unrelated CiycRandom streams instead; the Unity
        /// EditMode test covers UnityEngine.Random itself.
        /// </summary>
        private static void TestE_CosmeticRngDoesNotPerturb()
        {
            var baseline = HashFor(1337);

            var cosmetic = new Random(unchecked(Environment.TickCount * 7919));
            for (int i = 0; i < 5000; i++)
                cosmetic.NextDouble();

            var unrelatedStream = new CiycRandom(0xDEADBEEFUL, 999UL);
            for (int i = 0; i < 5000; i++)
                unrelatedStream.NextUInt();

            var after = HashFor(1337);

            Check("E: heavy cosmetic RNG consumption leaves generation untouched",
                baseline.FinalHash == after.FinalHash, $"{baseline.Final} vs {after.Final}");
        }

        /// <summary>TEST F - golden seeds.</summary>
        private static void TestF_GoldenSeeds()
        {
            var table = GoldenSeedTable.Entries;
            if (table.Length == 0)
            {
                Check("F: golden seed table is populated", false, "table is empty - run the 'golden' command");
                return;
            }

            Check("F: golden seed table has at least 10 entries", table.Length >= 10, $"has {table.Length}");

            bool ok = true;
            string detail = null;
            foreach (var entry in table)
            {
                if (entry.GenerationVersion != GenerationVersion.Current)
                {
                    ok = false;
                    detail = $"seed {entry.Seed} was recorded at generation version {entry.GenerationVersion}, current is {GenerationVersion.Current}";
                    break;
                }

                var map = MapDefinition.ById(entry.MapDefinitionId);
                var layout = HouseLayoutBuilder.Generate(entry.Seed, map, Content(), out _);
                var hash = LayoutHasher.Compute(layout);
                if (hash.Final != entry.ExpectedHash)
                {
                    ok = false;
                    detail = $"seed {entry.Seed} ({entry.MapDefinitionId}): expected {entry.ExpectedHash}, got {hash.Final}";
                    break;
                }
            }

            Check("F: every golden seed reproduces its recorded hash", ok, detail);
        }

        /// <summary>
        /// TEST G - order perturbation. The hash must canonicalise its input, so a layout
        /// whose collections arrive in a different order still hashes identically.
        /// </summary>
        private static void TestG_OrderPerturbation()
        {
            var layout = HouseLayoutBuilder.Generate(184726392, Map(), Content(), out _);
            var original = LayoutHasher.Compute(layout);

            var reversed = Permute(layout, reverse: true);
            var rotated = Permute(layout, reverse: false);

            var hashReversed = LayoutHasher.Compute(reversed);
            var hashRotated = LayoutHasher.Compute(rotated);

            Check("G: reversing every collection does not change the hash",
                original.FinalHash == hashReversed.FinalHash,
                $"{original.Final} vs {hashReversed.Final}");

            Check("G: rotating every collection does not change the hash",
                original.FinalHash == hashRotated.FinalHash,
                $"{original.Final} vs {hashRotated.Final}");
        }

        private static HouseLayout Permute(HouseLayout src, bool reverse)
        {
            return new HouseLayout(
                src.GenerationVersion, src.Seed, src.MapDefinitionId, src.ContentHash, src.Attempt,
                Perm(src.Rooms, reverse), Perm(src.Connections, reverse), Perm(src.Doors, reverse),
                Perm(src.Furniture, reverse), Perm(src.Props, reverse),
                Perm(src.HideSpots, reverse), Perm(src.EquipmentSpawns, reverse),
                Perm(src.EvidencePoints, reverse), Perm(src.GhostRoomCandidates, reverse),
                src.EntranceRoomId, src.GhostRoomId, src.WeatherIndex);
        }

        private static List<T> Perm<T>(IReadOnlyList<T> source, bool reverse)
        {
            var list = new List<T>(source);
            if (reverse)
            {
                list.Reverse();
            }
            else if (list.Count > 1)
            {
                var first = list[0];
                list.RemoveAt(0);
                list.Add(first);
            }

            return list;
        }

        /// <summary>
        /// Stream isolation: burning draws on one stream must not move another. This is the
        /// property that lets a designer add a prop without relocating the ghost room.
        /// </summary>
        private static void TestStreamIsolation()
        {
            var layoutA = HouseLayoutBuilder.Generate(42, Map(), Content(), out _);
            var hashA = LayoutHasher.Compute(layoutA);

            var propStream = CiycRandom.ForStream(42, CiycStream.Props);
            for (int i = 0; i < 1000; i++)
                propStream.NextUInt();

            var layoutB = HouseLayoutBuilder.Generate(42, Map(), Content(), out _);
            var hashB = LayoutHasher.Compute(layoutB);

            Check("stream isolation: draining the Props stream does not move the layout",
                hashA.FinalHash == hashB.FinalHash);

            // Different streams from the same seed must not be correlated.
            var s1 = CiycRandom.ForStream(42, CiycStream.Layout);
            var s2 = CiycRandom.ForStream(42, CiycStream.Props);
            bool identical = true;
            for (int i = 0; i < 32; i++)
            {
                if (s1.NextUInt() != s2.NextUInt())
                {
                    identical = false;
                    break;
                }
            }

            Check("stream isolation: two streams from one seed produce different sequences", !identical);
        }

        /// <summary>
        /// Duplicate stable ids must be REJECTED, not tie-broken.
        ///
        /// Two entries sharing an id make the sort comparator non-total, and List.Sort is an
        /// unstable introsort - so their relative order would depend on the authoring order,
        /// and two clients with the same assets ordered differently would silently generate
        /// different houses from the same seed.
        /// </summary>
        private static void TestDuplicateStableIdsRejected()
        {
            var goodRooms = new List<RoomArchetype>
            {
                new RoomArchetype("ARCH_A", RoomCategory.Entrance, new Vec3i(6000, 3000, 6000), 1, Quantize.Weight(1f)),
                new RoomArchetype("ARCH_B", RoomCategory.Hallway, new Vec3i(6000, 3000, 6000), 1, Quantize.Weight(1f)),
            };
            var goodProps = new List<PropArchetype>
            {
                new PropArchetype("PROP_A", PropKind.Prop, new Vec3i(500, 500, 500), Quantize.Weight(1f), null),
                new PropArchetype("PROP_B", PropKind.Prop, new Vec3i(500, 500, 500), Quantize.Weight(1f), null),
            };

            // Baseline: unique ids must still construct.
            bool baselineOk = true;
            try { var _ = new ContentSnapshot(goodRooms, goodProps); }
            catch (Exception) { baselineOk = false; }
            Check("duplicate ids: unique content still constructs", baselineOk);

            // Duplicate PROP id -> reject.
            var dupProps = new List<PropArchetype>(goodProps)
            {
                new PropArchetype("PROP_A", PropKind.Furniture, new Vec3i(900, 900, 900), Quantize.Weight(2f), null)
            };
            Check("duplicate ids: duplicate prop id is rejected",
                ThrowsDuplicate(() => new ContentSnapshot(goodRooms, dupProps), out string propMsg),
                propMsg);
            Check("duplicate ids: prop error names the offending id",
                propMsg != null && propMsg.Contains("PROP_A"), propMsg);

            // Duplicate ROOM id -> reject.
            var dupRooms = new List<RoomArchetype>(goodRooms)
            {
                new RoomArchetype("ARCH_B", RoomCategory.Bedroom, new Vec3i(6000, 3000, 6000), 1, Quantize.Weight(1f))
            };
            Check("duplicate ids: duplicate room id is rejected",
                ThrowsDuplicate(() => new ContentSnapshot(dupRooms, goodProps), out string roomMsg),
                roomMsg);
            Check("duplicate ids: room error names the offending id",
                roomMsg != null && roomMsg.Contains("ARCH_B"), roomMsg);

            // The rejection must not depend on WHERE in the input the duplicate sits,
            // which is precisely the input-order sensitivity being defended against.
            var dupFirst = new List<PropArchetype>
            {
                new PropArchetype("PROP_A", PropKind.Furniture, new Vec3i(900, 900, 900), Quantize.Weight(2f), null),
                goodProps[0],
                goodProps[1],
            };
            Check("duplicate ids: rejected regardless of input position",
                ThrowsDuplicate(() => new ContentSnapshot(goodRooms, dupFirst), out _));

            // Non-throwing helper used by editor tooling.
            var found = ContentSnapshot.FindDuplicateIds(new[] { "b", "a", "b", "c", "a", "b" });
            bool helperOk = found.Count == 2 && found[0] == "a" && found[1] == "b";
            Check("duplicate ids: FindDuplicateIds reports each duplicate once, in order",
                helperOk, helperOk ? null : string.Join(",", found));

            Check("duplicate ids: FindDuplicateIds returns empty for unique input",
                ContentSnapshot.FindDuplicateIds(new[] { "a", "b", "c" }).Count == 0);

            // The real content set must itself be clean, or generation is already broken.
            bool fallbackOk = true;
            try { var _ = ContentSnapshot.CreateFallback(); }
            catch (DuplicateStableIdException) { fallbackOk = false; }
            Check("duplicate ids: the shipped fallback content set has unique ids", fallbackOk);
        }

        private static bool ThrowsDuplicate(Func<ContentSnapshot> act, out string message)
        {
            message = null;
            try
            {
                var _ = act();
                return false;
            }
            catch (DuplicateStableIdException ex)
            {
                message = ex.Message;
                return true;
            }
            catch (Exception ex)
            {
                message = "wrong exception type: " + ex.GetType().Name;
                return false;
            }
        }

        private static void TestValidationHolds()
        {
            int valid = 0;
            int total = 0;
            string firstFailure = null;

            for (int seed = 1; seed <= 200; seed++)
            {
                total++;
                var layout = HouseLayoutBuilder.Generate(seed, Map(), Content(), out var validation);
                if (validation.IsValid)
                    valid++;
                else if (firstFailure == null)
                    firstFailure = $"seed {seed}: {validation}";

                if (layout.Rooms.Count == 0 && firstFailure == null)
                    firstFailure = $"seed {seed} produced no rooms";
            }

            Check($"generation succeeds for all 200 sampled seeds ({valid}/{total})", valid == total, firstFailure);
        }

        private static void TestQuantizationContract()
        {
            Check("quantization: 1.5 m round-trips to 1500 mm", Quantize.Millimetres(1.5f) == 1500);
            Check("quantization: negative values are symmetric",
                Quantize.Millimetres(-1.5f) == -1500 && Quantize.Millimetres(-0.0005f) == -1);
            Check("quantization: rotation index wraps into range",
                Quantize.RotationIndex(-1) == 3 && Quantize.RotationIndex(7) == 3);

            // A stable hasher must not depend on the runtime's string hashing.
            var h1 = Fnv1a64.Create();
            h1.WriteString("HOUSE_DEFAULT_A");
            var h2 = Fnv1a64.Create();
            h2.WriteString("HOUSE_DEFAULT_A");
            Check("hashing: FNV-1a is stable for equal input", h1.Value == h2.Value);
            Check("hashing: FNV-1a separates concatenation boundaries", DistinctBoundary());
        }

        private static bool DistinctBoundary()
        {
            var a = Fnv1a64.Create();
            a.WriteString("ab");
            a.WriteString("c");

            var b = Fnv1a64.Create();
            b.WriteString("a");
            b.WriteString("bc");

            return a.Value != b.Value;
        }

        // ------------------------------------------------------------------ commands

        public static void PrintGoldenSeeds()
        {
            var sb = new StringBuilder();
            sb.AppendLine("// AUTO-GENERATED by: dotnet run --project Tools/DeterminismHarness golden");
            sb.AppendLine("// Do not edit by hand. Regenerate only after a deliberate GenerationVersion bump.");
            sb.AppendLine();
            sb.AppendLine("namespace CatchIfYouCan.Procedural.Deterministic");
            sb.AppendLine("{");
            sb.AppendLine("    public static class GoldenSeedTable");
            sb.AppendLine("    {");
            sb.AppendLine("        public readonly struct Entry");
            sb.AppendLine("        {");
            sb.AppendLine("            public readonly int GenerationVersion;");
            sb.AppendLine("            public readonly string MapDefinitionId;");
            sb.AppendLine("            public readonly int Seed;");
            sb.AppendLine("            public readonly string ExpectedHash;");
            sb.AppendLine();
            sb.AppendLine("            public Entry(int generationVersion, string mapDefinitionId, int seed, string expectedHash)");
            sb.AppendLine("            {");
            sb.AppendLine("                GenerationVersion = generationVersion;");
            sb.AppendLine("                MapDefinitionId = mapDefinitionId;");
            sb.AppendLine("                Seed = seed;");
            sb.AppendLine("                ExpectedHash = expectedHash;");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public static readonly Entry[] Entries =");
            sb.AppendLine("        {");

            foreach (var map in new[] { MapDefinition.HouseDefault, MapDefinition.HouseTraining })
            {
                foreach (int seed in GoldenSeeds)
                {
                    var layout = HouseLayoutBuilder.Generate(seed, map, Content(), out _);
                    var hash = LayoutHasher.Compute(layout);
                    sb.AppendLine($"            new Entry({GenerationVersion.Current}, \"{map.MapDefinitionId}\", {seed}, \"{hash.Final}\"),");
                }
            }

            sb.AppendLine("        };");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            Console.Write(sb.ToString());
        }

        public static void PrintReport(int seed)
        {
            var layout = HouseLayoutBuilder.Generate(seed, Map(), Content(), out var validation);
            var hash = LayoutHasher.Compute(layout);
            Console.WriteLine(hash.ToReport());
            Console.WriteLine($"rooms={layout.Rooms.Count} connections={layout.Connections.Count} doors={layout.Doors.Count} " +
                              $"furniture={layout.Furniture.Count} props={layout.Props.Count} hideSpots={layout.HideSpots.Count} " +
                              $"ghostRoom={layout.GhostRoomId} attempt={layout.Attempt} valid={validation.IsValid}");
            if (!validation.IsValid)
                Console.WriteLine("validation: " + validation);
        }
    }
}
