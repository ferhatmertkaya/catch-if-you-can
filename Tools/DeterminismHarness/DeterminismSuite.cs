using System;
using System.Collections.Generic;
using System.Text;
using CatchIfYouCan.Procedural;
using CatchIfYouCan.Procedural.Deterministic;

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
        /// The join handshake and mismatch protocol of Docs/NETWORKING.md §3 and §5.
        /// Transport-neutral, so it is testable here without Unity or a netcode package —
        /// which is the point of keeping it that way.
        /// </summary>
        private static void TestSessionHandshake()
        {
            var map = Map();
            var content = Content();
            var host = MatchConfig.CreateAuthoritative(424242, map, content);

            Check("handshake: capacity has one source and it is 4",
                MultiplayerProtocol.MaxPlayers == 4);

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
