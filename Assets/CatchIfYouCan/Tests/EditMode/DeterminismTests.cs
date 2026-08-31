using System;
using System.Collections.Generic;
using CatchIfYouCan.Procedural;
using CatchIfYouCan.Procedural.Deterministic;
using NUnit.Framework;
using UnityEngine;

namespace CatchIfYouCan.Tests
{
    /// <summary>
    /// Determinism suite (T1-T7 / A-G in Docs/DETERMINISM.md), running inside Unity.
    ///
    /// The same assertions also run outside Unity via Tools/DeterminismHarness, which is
    /// what CI uses since it needs no licence. This copy exists because two things can only
    /// be proven in the engine: that UnityEngine.Random genuinely cannot perturb generation,
    /// and that the core still behaves identically when compiled by Unity's toolchain
    /// (Mono in the editor, IL2CPP on device).
    /// </summary>
    public class DeterminismTests
    {
        private static MapDefinition Map => MapDefinition.HouseDefault;
        private static ContentSnapshot Content() => ContentSnapshot.CreateFallback();

        private static LayoutHash HashFor(int seed, MapDefinition map = null)
        {
            var layout = HouseLayoutBuilder.Generate(seed, map ?? Map, Content(), out _);
            return LayoutHasher.Compute(layout);
        }

        // ------------------------------------------------------------------ RNG contract

        [Test]
        public void Pcg32_MatchesPublishedReferenceVectors()
        {
            // If this fails, "deterministic" only means "consistently wrong": the stream has
            // silently changed and every stored seed now names a different house.
            var rng = new CiycRandom(42UL, 54UL);
            uint[] expected = { 0xA15C02B7u, 0x7B47F409u, 0xBA1D3330u, 0x83D2F293u, 0xBFA4784Bu, 0xCBED606Eu };

            for (int i = 0; i < expected.Length; i++)
                Assert.AreEqual(expected[i], rng.NextUInt(), $"PCG32 diverged at draw {i}.");
        }

        [Test]
        public void NextUInt_Bounded_IsWithinRangeAndUnbiased()
        {
            var rng = CiycRandom.ForStream(1234, CiycStream.Props);
            var counts = new int[7];
            const int draws = 70000;

            for (int i = 0; i < draws; i++)
            {
                uint v = rng.NextUInt(7);
                Assert.Less(v, 7u);
                counts[v]++;
            }

            // Rejection sampling, so each bucket should land near draws/7 (10000).
            for (int i = 0; i < counts.Length; i++)
                Assert.That(counts[i], Is.InRange(9400, 10600), $"bucket {i} looks biased: {counts[i]}");
        }

        [Test]
        public void NextFloat_StaysInUnitInterval()
        {
            var rng = CiycRandom.ForStream(99, CiycStream.Furniture);
            for (int i = 0; i < 20000; i++)
            {
                float v = rng.NextFloat();
                Assert.That(v, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
            }
        }

        [Test]
        public void Shuffle_IsAPermutation()
        {
            var rng = CiycRandom.ForStream(7, CiycStream.Layout);
            var items = new List<int>();
            for (int i = 0; i < 64; i++)
                items.Add(i);

            rng.Shuffle(items);
            items.Sort();

            for (int i = 0; i < 64; i++)
                Assert.AreEqual(i, items[i], "Shuffle lost or duplicated an element.");
        }

        // ------------------------------------------------------------------ TEST A

        [Test]
        public void A_SameSeed_ProducesSameHash_100Times()
        {
            foreach (int seed in new[] { 42, 184726392, -7 })
            {
                var first = HashFor(seed);
                for (int i = 0; i < 100; i++)
                    Assert.AreEqual(first.FinalHash, HashFor(seed).FinalHash,
                        $"seed {seed} diverged on iteration {i}.");
            }
        }

        // ------------------------------------------------------------------ TEST B

        [Test]
        public void B_GeneratingOtherContentBetweenRuns_DoesNotChangeHash()
        {
            var before = HashFor(42);

            for (int i = 0; i < 25; i++)
                HashFor(1000 + i);

            HouseLayoutBuilder.Generate(777, MapDefinition.HouseTraining, Content(), out _);

            Assert.AreEqual(before.FinalHash, HashFor(42).FinalHash);
        }

        // ------------------------------------------------------------------ TEST C

        [Test]
        public void C_FrameAndTimingVariation_DoesNotChangeHash()
        {
            var baseline = HashFor(424242);

            for (int frame = 0; frame < 10; frame++)
            {
                // Stage A reads no clock and no frame counter, so burning time and frames
                // between runs must be invisible to it.
                long spin = 0;
                for (int i = 0; i < frame * 5000; i++)
                    spin += i;
                GC.KeepAlive(spin);

                Assert.AreEqual(baseline.FinalHash, HashFor(424242).FinalHash,
                    $"hash changed after {frame} frames of unrelated work.");
            }
        }

        // ------------------------------------------------------------------ TEST D

        [Test]
        public void D_RetryAttempts_AreReproducibleAndUncontaminated()
        {
            foreach (int seed in new[] { 42, 424242, 999983 })
            {
                for (int attempt = 0; attempt < HouseLayoutBuilder.MaxAttempts; attempt++)
                {
                    var cold = LayoutHasher.Compute(HouseLayoutBuilder.Build(seed, Map, Content(), attempt));

                    for (int prior = 0; prior < attempt; prior++)
                        HouseLayoutBuilder.Build(seed, Map, Content(), prior);

                    var warm = LayoutHasher.Compute(HouseLayoutBuilder.Build(seed, Map, Content(), attempt));

                    Assert.AreEqual(cold.FinalHash, warm.FinalHash,
                        $"seed {seed} attempt {attempt} was contaminated by earlier attempts.");
                }
            }
        }

        [Test]
        public void D_ConsecutiveAttempts_ExploreDifferentLayouts()
        {
            var a0 = LayoutHasher.Compute(HouseLayoutBuilder.Build(42, Map, Content(), 0));
            var a1 = LayoutHasher.Compute(HouseLayoutBuilder.Build(42, Map, Content(), 1));
            Assert.AreNotEqual(a0.FinalHash, a1.FinalHash, "Retrying would be pointless.");
        }

        // ------------------------------------------------------------------ TEST E

        [Test]
        public void E_UnityEngineRandom_CannotPerturbGeneration()
        {
            // The whole point of the migration. UnityEngine.Random is a process-global
            // stream shared with roughly a hundred cosmetic call sites whose draw COUNT
            // depends on frame rate; generation must be completely blind to it.
            var baseline = HashFor(1337);

            UnityEngine.Random.InitState(12345);
            for (int i = 0; i < 5000; i++)
            {
                UnityEngine.Random.value.ToString();
                UnityEngine.Random.Range(0, 100);
                var _ = UnityEngine.Random.insideUnitSphere;
            }

            var afterSeeding = HashFor(1337);
            Assert.AreEqual(baseline.FinalHash, afterSeeding.FinalHash);

            // And again from a completely different global state.
            UnityEngine.Random.InitState(987654321);
            for (int i = 0; i < 5000; i++)
                UnityEngine.Random.Range(0f, 1f);

            Assert.AreEqual(baseline.FinalHash, HashFor(1337).FinalHash);
        }

        [Test]
        public void E_GenerationDoesNotAdvanceUnityEngineRandom()
        {
            // The converse: generation must not consume from the cosmetic stream either,
            // or it would make cosmetic systems depend on how many rooms were generated.
            UnityEngine.Random.InitState(4242);
            var stateBefore = UnityEngine.Random.state;

            HashFor(31337);

            var stateAfter = UnityEngine.Random.state;
            Assert.AreEqual(JsonUtility.ToJson(stateBefore), JsonUtility.ToJson(stateAfter),
                "Generation consumed draws from UnityEngine.Random.");
        }

        // ------------------------------------------------------------------ TEST F

        [Test]
        public void F_GoldenSeeds_ReproduceRecordedHashes()
        {
            Assert.GreaterOrEqual(GoldenSeedTable.Entries.Length, 10,
                "The golden table must cover at least 10 seeds.");

            foreach (var entry in GoldenSeedTable.Entries)
            {
                Assert.AreEqual(GenerationVersion.Current, entry.GenerationVersion,
                    $"Golden entry for seed {entry.Seed} was recorded at generation version " +
                    $"{entry.GenerationVersion}. Regenerate the table deliberately, in the same " +
                    "commit as the version bump.");

                var map = MapDefinition.ById(entry.MapDefinitionId);
                var layout = HouseLayoutBuilder.Generate(entry.Seed, map, Content(), out _);
                var hash = LayoutHasher.Compute(layout);

                Assert.AreEqual(entry.ExpectedHash, hash.Final,
                    $"Seed {entry.Seed} on {entry.MapDefinitionId} no longer reproduces its " +
                    $"recorded layout.\n{hash.ToReport()}");
            }
        }

        // ------------------------------------------------------------------ TEST G

        [Test]
        public void G_PerturbingCollectionOrder_DoesNotChangeHash()
        {
            var layout = HouseLayoutBuilder.Generate(184726392, Map, Content(), out _);
            var original = LayoutHasher.Compute(layout);

            Assert.AreEqual(original.FinalHash, LayoutHasher.Compute(Permute(layout, true)).FinalHash,
                "Reversing collection order changed the hash; the hasher is not canonicalising.");
            Assert.AreEqual(original.FinalHash, LayoutHasher.Compute(Permute(layout, false)).FinalHash,
                "Rotating collection order changed the hash; the hasher is not canonicalising.");
        }

        private static HouseLayout Permute(HouseLayout src, bool reverse) =>
            new HouseLayout(
                src.GenerationVersion, src.Seed, src.MapDefinitionId, src.ContentHash, src.Attempt,
                Perm(src.Rooms, reverse), Perm(src.Connections, reverse), Perm(src.Doors, reverse),
                Perm(src.Furniture, reverse), Perm(src.Props, reverse),
                Perm(src.HideSpots, reverse), Perm(src.EquipmentSpawns, reverse),
                Perm(src.EvidencePoints, reverse), Perm(src.GhostRoomCandidates, reverse),
                src.EntranceRoomId, src.GhostRoomId, src.WeatherIndex);

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

        // ------------------------------------------------------------------ streams

        [Test]
        public void StreamsAreIsolated()
        {
            var before = HashFor(42);

            var propStream = CiycRandom.ForStream(42, CiycStream.Props);
            for (int i = 0; i < 1000; i++)
                propStream.NextUInt();

            Assert.AreEqual(before.FinalHash, HashFor(42).FinalHash);
        }

        [Test]
        public void DifferentStreams_FromOneSeed_AreUncorrelated()
        {
            var a = CiycRandom.ForStream(42, CiycStream.Layout);
            var b = CiycRandom.ForStream(42, CiycStream.Props);

            bool anyDifference = false;
            for (int i = 0; i < 32 && !anyDifference; i++)
                anyDifference = a.NextUInt() != b.NextUInt();

            Assert.IsTrue(anyDifference, "Two streams produced the same sequence.");
        }

        // ------------------------------------------------------------------ content identity

        [Test]
        public void DuplicatePropStableId_IsRejected()
        {
            // Two entries sharing an id make the sort comparator non-total, and List.Sort is
            // an unstable introsort - so their relative order would follow the authoring
            // order, and two clients with the same assets ordered differently would silently
            // generate different houses from the same seed.
            var rooms = UniqueRooms();
            var props = UniqueProps();
            props.Add(new PropArchetype("PROP_A", PropKind.Furniture,
                new Vec3i(900, 900, 900), Quantize.Weight(2f), null));

            var ex = Assert.Throws<DuplicateStableIdException>(() => new ContentSnapshot(rooms, props));
            StringAssert.Contains("PROP_A", ex.Message);
            CollectionAssert.Contains(ex.DuplicateIds, "PROP_A");
        }

        [Test]
        public void DuplicateRoomStableId_IsRejected()
        {
            var rooms = UniqueRooms();
            rooms.Add(new RoomArchetype("ARCH_B", RoomCategory.Bedroom,
                new Vec3i(6000, 3000, 6000), 1, Quantize.Weight(1f)));

            var ex = Assert.Throws<DuplicateStableIdException>(() => new ContentSnapshot(rooms, UniqueProps()));
            StringAssert.Contains("ARCH_B", ex.Message);
        }

        [Test]
        public void DuplicateIsRejected_RegardlessOfInputPosition()
        {
            // The whole point: rejection must not itself depend on input order.
            var duplicate = new PropArchetype("PROP_A", PropKind.Furniture,
                new Vec3i(900, 900, 900), Quantize.Weight(2f), null);

            var first = new List<PropArchetype> { duplicate };
            first.AddRange(UniqueProps());

            var last = UniqueProps();
            last.Add(duplicate);

            Assert.Throws<DuplicateStableIdException>(() => new ContentSnapshot(UniqueRooms(), first));
            Assert.Throws<DuplicateStableIdException>(() => new ContentSnapshot(UniqueRooms(), last));
        }

        [Test]
        public void UniqueStableIds_StillConstruct()
        {
            Assert.DoesNotThrow(() => new ContentSnapshot(UniqueRooms(), UniqueProps()));
            Assert.DoesNotThrow(() => ContentSnapshot.CreateFallback());
        }

        [Test]
        public void FindDuplicateIds_ReportsEachDuplicateOnceInOrder()
        {
            var found = ContentSnapshot.FindDuplicateIds(new[] { "b", "a", "b", "c", "a", "b" });
            Assert.AreEqual(2, found.Count);
            Assert.AreEqual("a", found[0]);
            Assert.AreEqual("b", found[1]);

            Assert.IsEmpty(ContentSnapshot.FindDuplicateIds(new[] { "a", "b", "c" }));
            Assert.IsEmpty(ContentSnapshot.FindDuplicateIds(new string[0]));
        }

        [Test]
        public void ContentHash_IsIndependentOfAuthoringOrder()
        {
            // With duplicates rejected the sort key is total, so shuffling the input must
            // leave the content hash - and therefore every layout - untouched.
            var forward = new ContentSnapshot(UniqueRooms(), UniqueProps());

            var reversedRooms = UniqueRooms();
            reversedRooms.Reverse();
            var reversedProps = UniqueProps();
            reversedProps.Reverse();
            var backward = new ContentSnapshot(reversedRooms, reversedProps);

            Assert.AreEqual(forward.ContentHash, backward.ContentHash,
                "Content hash depends on authoring order; the id ordering is not total.");
        }

        private static List<RoomArchetype> UniqueRooms() => new List<RoomArchetype>
        {
            new RoomArchetype("ARCH_A", RoomCategory.Entrance, new Vec3i(6000, 3000, 6000), 1, Quantize.Weight(1f)),
            new RoomArchetype("ARCH_B", RoomCategory.Hallway, new Vec3i(6000, 3000, 6000), 1, Quantize.Weight(1f)),
        };

        private static List<PropArchetype> UniqueProps() => new List<PropArchetype>
        {
            new PropArchetype("PROP_A", PropKind.Prop, new Vec3i(500, 500, 500), Quantize.Weight(1f), null),
            new PropArchetype("PROP_B", PropKind.Prop, new Vec3i(500, 500, 500), Quantize.Weight(1f), null),
        };

        // ------------------------------------------------------------------ layout sanity

        [Test]
        public void EverySampledSeed_ProducesAValidLayout()
        {
            for (int seed = 1; seed <= 200; seed++)
            {
                var layout = HouseLayoutBuilder.Generate(seed, Map, Content(), out var validation);
                Assert.IsTrue(validation.IsValid, $"seed {seed}: {validation}");
                Assert.GreaterOrEqual(layout.Rooms.Count, Map.MinRooms, $"seed {seed} produced too few rooms.");
                Assert.AreNotEqual(-1, layout.GhostRoomId, $"seed {seed} has no ghost room.");
            }
        }

        [Test]
        public void PropsNeverBlockADoorway()
        {
            // The occupancy grid reserves an approach zone in front of every door. If this
            // regresses, players get soft-locked out of rooms.
            for (int seed = 1; seed <= 50; seed++)
            {
                var layout = HouseLayoutBuilder.Generate(seed, Map, Content(), out _);
                AssertNoPropInDoorway(layout, layout.Furniture);
                AssertNoPropInDoorway(layout, layout.Props);
            }
        }

        private static void AssertNoPropInDoorway(HouseLayout layout, IReadOnlyList<LayoutProp> props)
        {
            for (int i = 0; i < props.Count; i++)
            {
                var prop = props[i];
                Assert.IsTrue(layout.TryGetRoom(prop.RoomId, out var room),
                    $"prop {prop.PropInstanceId} references missing room {prop.RoomId}.");

                var local = prop.PositionMm - room.PositionMm;
                int halfZ = room.SizeMm.Z / 2;
                int halfX = room.SizeMm.X / 2;

                Assert.Less(System.Math.Abs(local.X), halfX, "prop escaped the room on X.");
                Assert.Less(System.Math.Abs(local.Z), halfZ, "prop escaped the room on Z.");
            }
        }

        [Test]
        public void LayoutDiff_NamesTheFirstRealDifference()
        {
            var a = HouseLayoutBuilder.Generate(42, Map, Content(), out _);
            var b = HouseLayoutBuilder.Generate(43, Map, Content(), out _);

            Assert.IsFalse(LayoutDiff.TryDescribeFirstDifference(a, a, out _),
                "A layout must not differ from itself.");

            Assert.IsTrue(LayoutDiff.TryDescribeFirstDifference(a, b, out string description));
            Assert.IsNotEmpty(description);
        }

        [Test]
        public void SectionHashes_LocaliseADivergence()
        {
            var layout = HouseLayoutBuilder.Generate(42, Map, Content(), out _);
            var original = LayoutHasher.Compute(layout);

            // Move one prop and confirm only the prop section reacts.
            var movedProps = new List<LayoutProp>(layout.Props);
            if (movedProps.Count > 0)
            {
                var p = movedProps[0];
                movedProps[0] = new LayoutProp(p.PropInstanceId, p.PropDefinitionId, p.Kind, p.RoomId,
                    p.Slot, p.PositionMm + new Vec3i(100, 0, 0), p.RotationIndex);

                var mutated = new HouseLayout(
                    layout.GenerationVersion, layout.Seed, layout.MapDefinitionId, layout.ContentHash,
                    layout.Attempt, layout.Rooms, layout.Connections, layout.Doors, layout.Furniture,
                    movedProps, layout.HideSpots, layout.EquipmentSpawns, layout.EvidencePoints,
                    layout.GhostRoomCandidates, layout.EntranceRoomId, layout.GhostRoomId, layout.WeatherIndex);

                var mutatedHash = LayoutHasher.Compute(mutated);

                Assert.AreNotEqual(original.PropsHash, mutatedHash.PropsHash);
                Assert.AreEqual(original.RoomsHash, mutatedHash.RoomsHash, "Rooms section should be unaffected.");
                Assert.AreEqual(original.DoorsHash, mutatedHash.DoorsHash, "Doors section should be unaffected.");
                StringAssert.Contains("Props", original.DescribeDifference(mutatedHash));
            }
        }

        // ------------------------------------------------------------------ quantization

        [Test]
        public void Quantization_IsSymmetricAndExact()
        {
            Assert.AreEqual(1500, Quantize.Millimetres(1.5f));
            Assert.AreEqual(-1500, Quantize.Millimetres(-1.5f));
            Assert.AreEqual(0, Quantize.Millimetres(0f));
            Assert.AreEqual(3, Quantize.RotationIndex(-1));
            Assert.AreEqual(3, Quantize.RotationIndex(7));
        }

        [Test]
        public void Fnv1a_IsStable_AndNotStringGetHashCode()
        {
            var a = Fnv1a64.Create();
            a.WriteString("HOUSE_DEFAULT_A");

            var b = Fnv1a64.Create();
            b.WriteString("HOUSE_DEFAULT_A");

            Assert.AreEqual(a.Value, b.Value);

            // Length prefixing: "ab"+"c" must not collide with "a"+"bc".
            var c = Fnv1a64.Create();
            c.WriteString("ab");
            c.WriteString("c");

            var d = Fnv1a64.Create();
            d.WriteString("a");
            d.WriteString("bc");

            Assert.AreNotEqual(c.Value, d.Value);
        }
    }
}
