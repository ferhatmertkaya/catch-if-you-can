using System;
using System.Collections.Generic;
using System.Text;

namespace CatchIfYouCan.Procedural.Deterministic
{
    /// <summary>
    /// Thrown when authored content contains two entries that resolve to the same stable id.
    ///
    /// This is a hard failure by design. A stable id IS content identity: it selects which
    /// prop a seed produces and it is what the content hash is built from. Two entries
    /// sharing an id make the ordering comparator non-total, and
    /// <see cref="List{T}.Sort"/> is an unstable introsort - so their relative order becomes
    /// an artifact of the input order, and two clients with the same assets in a different
    /// authoring order would silently generate different houses from the same seed.
    ///
    /// The alternative - adding a tie-break key - would produce a stable ordering while
    /// leaving the real defect (two things claiming one identity) in the project, hidden.
    /// Rejecting the content is the honest fix.
    /// </summary>
    public sealed class DuplicateStableIdException : Exception
    {
        /// <summary>The duplicated ids, in canonical order.</summary>
        public IReadOnlyList<string> DuplicateIds { get; }

        public DuplicateStableIdException(string kind, IReadOnlyList<string> duplicateIds)
            : base(BuildMessage(kind, duplicateIds))
        {
            DuplicateIds = duplicateIds ?? Array.Empty<string>();
        }

        private static string BuildMessage(string kind, IReadOnlyList<string> duplicateIds)
        {
            var sb = new StringBuilder();
            sb.Append("Duplicate ").Append(kind).Append(" stable id(s): ");

            if (duplicateIds != null)
            {
                for (int i = 0; i < duplicateIds.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append('\'').Append(duplicateIds[i]).Append('\'');
                }
            }

            sb.Append(". A stable id is content identity: it selects which asset a seed ")
              .Append("produces and it is what the content hash is built from, so two entries ")
              .Append("cannot share one. Fix by setting a unique StableId on each affected ")
              .Append("definition asset (leaving it empty falls back to the display name, ")
              .Append("which is not guaranteed unique). Generation is refused until the ids ")
              .Append("are distinct.");

            return sb.ToString();
        }
    }
}
