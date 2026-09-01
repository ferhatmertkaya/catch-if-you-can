using UnityEngine;

namespace CatchIfYouCan.Audio
{
    /// <summary>
    /// Marks what a surface is made of, so footsteps do not have to guess from its name.
    ///
    /// <para>
    /// The footstep system used to read <c>collider.name</c> and test it against a list of
    /// substrings on every step. That allocates a lowercased string per step, breaks the moment
    /// somebody renames an object, and quietly resolves anything unrecognised to wood. A component
    /// is the cheapest thing that cannot be wrong: one <c>GetComponent</c> on the collider that
    /// was hit, cached per collider so repeat steps on the same floor cost nothing.
    /// </para>
    ///
    /// <para>
    /// Put it on the collider, or on any parent of it. Only the interactive room's floor carries
    /// one today; everything else falls back to the profile's default, which is what keeps this
    /// from turning into a surface database before there is anything to put in one.
    /// </para>
    /// </summary>
    [AddComponentMenu("Catch If You Can/Footstep Surface")]
    public sealed class FootstepSurface : MonoBehaviour
    {
        [Tooltip("What this surface sounds like underfoot.")]
        [SerializeField] private SurfaceType surface = SurfaceType.Wood;

        [Tooltip("Clear for an exterior surface, so footsteps lose the close indoor colouring.")]
        [SerializeField] private bool indoor = true;

        public SurfaceType Surface => surface;
        public bool Indoor => indoor;
    }
}
