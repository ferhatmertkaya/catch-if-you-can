using System.Collections.Generic;
using CatchIfYouCan.Evidence;

namespace CatchIfYouCan.Ghost
{
    /// <summary>
    /// Checks that the ghost roster is something a player could actually solve.
    ///
    /// <para>
    /// The failures this catches are not crashes. A ghost declaring an evidence type no device
    /// can observe is a ghost that cannot be identified; two ghosts with the same three
    /// evidence types are two ghosts that cannot be told apart; a ghost with a repeated
    /// evidence type has two findings instead of three. All three run perfectly, and all three
    /// mean a player gathers everything the game offers and still cannot answer the question
    /// the game asked.
    /// </para>
    ///
    /// <para>
    /// Cross-checked against <see cref="EvidenceAuthority"/> rather than against a second list
    /// of what is detectable. One table decides what can be proved; this asks it.
    /// </para>
    /// </summary>
    public static class GhostCatalogValidator
    {
        /// <summary>Everything wrong with the roster, as text. Empty means solvable.</summary>
        public static List<string> Validate(IReadOnlyList<GhostDefinition> ghosts)
        {
            var issues = new List<string>();
            if (ghosts == null || ghosts.Count == 0)
            {
                issues.Add("the ghost roster is empty");
                return issues;
            }

            var seenIds = new HashSet<string>(System.StringComparer.Ordinal);
            var seenSignatures = new Dictionary<string, string>(System.StringComparer.Ordinal);

            for (int i = 0; i < ghosts.Count; i++)
            {
                var ghost = ghosts[i];
                if (ghost == null)
                {
                    issues.Add("roster slot " + i + " is null");
                    continue;
                }

                if (string.IsNullOrEmpty(ghost.Id))
                {
                    issues.Add("roster slot " + i + " has no id");
                    continue;
                }

                if (!seenIds.Add(ghost.Id))
                    issues.Add("'" + ghost.Id + "' appears more than once");

                if (!GhostIds.IsCanonical(ghost.Id))
                    issues.Add("'" + ghost.Id + "' is not in the canonical roster");

                CheckEvidence(ghost, issues);

                // Two ghosts exhibiting the same three things are one ghost with two names, and
                // the player who gathers all three has no way to choose between them.
                string signature = Signature(ghost);
                if (seenSignatures.TryGetValue(signature, out string other))
                {
                    issues.Add("'" + ghost.Id + "' and '" + other +
                               "' exhibit the same three evidence types and cannot be told apart");
                }
                else
                {
                    seenSignatures[signature] = ghost.Id;
                }
            }

            return issues;
        }

        private static void CheckEvidence(GhostDefinition ghost, List<string> issues)
        {
            EvidenceType[] triple = { ghost.Evidence1, ghost.Evidence2, ghost.Evidence3 };

            for (int i = 0; i < triple.Length; i++)
            {
                // A ghost cannot exhibit something nothing in the game can measure.
                if (!EvidenceAuthority.IsSupported(triple[i]))
                {
                    issues.Add("'" + ghost.Id + "' exhibits " + triple[i] +
                               ", which has no supported observation path");
                }

                for (int j = i + 1; j < triple.Length; j++)
                {
                    if (triple[i] == triple[j])
                    {
                        issues.Add("'" + ghost.Id + "' lists " + triple[i] +
                                   " twice, so it has two findings rather than three");
                    }
                }
            }
        }

        /// <summary>
        /// The ghost's evidence triple in a canonical order, so that two ghosts listing the
        /// same three types in a different order are still recognised as the same ghost.
        /// </summary>
        private static string Signature(GhostDefinition ghost)
        {
            int a = (int)ghost.Evidence1;
            int b = (int)ghost.Evidence2;
            int c = (int)ghost.Evidence3;

            // Three values; a sort network is shorter and allocates nothing.
            if (a > b) (a, b) = (b, a);
            if (b > c) (b, c) = (c, b);
            if (a > b) (a, b) = (b, a);

            return a + "/" + b + "/" + c;
        }

        /// <summary>
        /// A stable fingerprint of the whole roster: ids and the evidence each one exhibits.
        ///
        /// <para>
        /// <b>Not folded into <c>ContentSnapshot.ContentHash</c>.</b> Doing that would change
        /// what an existing ContentHash means, which is a deterministic-contract semantic
        /// change and would silently make every current build incompatible with itself. This is
        /// here so that a future <c>MultiplayerProtocol.Version</c> bump can include ghost
        /// content in the join handshake deliberately, in one place, with the version change
        /// that makes it honest.
        /// </para>
        ///
        /// <para>
        /// FNV-1a over sorted entries, in integer arithmetic only, so it is engine-free and
        /// gives the same answer on every platform - which is the whole point of a
        /// compatibility hash.
        /// </para>
        /// </summary>
        public static ulong ComputeCatalogHash(IReadOnlyList<GhostDefinition> ghosts)
        {
            if (ghosts == null || ghosts.Count == 0)
                return 0UL;

            var entries = new List<string>(ghosts.Count);
            for (int i = 0; i < ghosts.Count; i++)
            {
                var ghost = ghosts[i];
                if (ghost != null && !string.IsNullOrEmpty(ghost.Id))
                    entries.Add(ghost.Id + ":" + Signature(ghost));
            }

            // Sorted by ordinal, so the hash does not depend on the order the roster happened
            // to be built in.
            entries.Sort(System.StringComparer.Ordinal);

            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;

            ulong hash = offset;
            for (int i = 0; i < entries.Count; i++)
            {
                string entry = entries[i];
                for (int c = 0; c < entry.Length; c++)
                {
                    hash ^= entry[c];
                    hash *= prime;
                }

                // A separator, so "ab" + "c" and "a" + "bc" are different rosters.
                hash ^= 0x1FUL;
                hash *= prime;
            }

            return hash;
        }
    }
}
