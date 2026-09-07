using UnityEngine;

namespace CatchIfYouCan.Art
{
    /// <summary>
    /// The one rule for giving a runtime primitive its surface, and the one thing to do when
    /// there is no surface to give it.
    ///
    /// <para>
    /// <c>GameObject.CreatePrimitive</c> does not arrive bare. It arrives carrying Unity's
    /// built-in default material, which is a Built-in-pipeline shader - and under URP that draws
    /// solid magenta. So skipping the assignment does not leave a plain grey box; it leaves the
    /// loudest possible wrong one, and one that three different people will explain three
    /// different ways. This project has spent real time on exactly that.
    /// </para>
    /// <para>
    /// The answer is to switch the RENDERER off and say so. A hidden object is a bug somebody
    /// reports precisely; a magenta object is a bug somebody argues about. The collider stays,
    /// so an invisible floor still holds the player up.
    /// </para>
    /// <para>
    /// One implementation, called from every site that makes a primitive, so the rule cannot
    /// hold in one generator and quietly not in another - which is how the diagnostic floor, the
    /// van and the apartment shell each ended up able to ship magenta while the room factory
    /// could not.
    /// </para>
    /// </summary>
    public static class PrimitiveSurface
    {
        /// <summary>
        /// Puts <paramref name="material"/> on the object, or hides it and logs why.
        ///
        /// <para>
        /// <paramref name="expected"/> says what the caller wanted, so the log names the missing
        /// thing rather than only the object that is now invisible.
        /// </para>
        /// </summary>
        /// <returns>True if the object is visible with a material.</returns>
        public static bool Apply(GameObject go, Material material, string expected)
        {
            if (go == null)
                return false;

            var renderer = go.GetComponent<Renderer>();
            if (renderer == null)
                return false;

            if (material != null)
            {
                renderer.sharedMaterial = material;
                renderer.enabled = true;
                return true;
            }

            renderer.enabled = false;
            Core.CIYCLog.Error("[CIYC][WorldMaterial] object=" + go.name +
                               " expected=" + (string.IsNullOrEmpty(expected) ? "<unnamed>" : expected) +
                               " material=<none> renderer=disabled reason=a primitive with no " +
                               "material of its own draws Unity's built-in default, which is a " +
                               "Built-in-pipeline shader and magenta under URP");
            return false;
        }
    }
}
