using UnityEngine;

namespace CatchIfYouCan.Core
{
    /// <summary>
    /// Says, in the console, which version of this code is actually running.
    ///
    /// <para>
    /// This exists because of a specific and expensive failure: three separate faults were
    /// reported as still broken across several rounds, all three had already been fixed, and
    /// neither side could tell whether the fixes were even on the machine that was running the
    /// game. Every hour spent re-deriving a bug that no longer exists in the code being edited is
    /// an hour spent on nothing. One line at start-up ends that question.
    /// </para>
    ///
    /// <para>
    /// <see cref="Stamp"/> is bumped by hand in the same commit as the change it describes, so it
    /// names what is in the build rather than what the repository happens to have. If the console
    /// does not print the stamp you are expecting, the build is old - that is the whole feature.
    /// </para>
    /// </summary>
    public static class BuildStamp
    {
        /// <summary>What is in this build. Bumped by hand, alongside the change it names.</summary>
        public const string Stamp = "2026-09-02 / door closed-loop sizing, live arm trim, " +
                                    "head follows the idle scan, mirror flip live";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Announce()
        {
            Debug.Log("[CIYC] Build: " + Stamp);
        }
    }
}
