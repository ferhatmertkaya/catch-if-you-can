using UnityEngine;

namespace CatchIfYouCan.Equipment
{
    /// <summary>
    /// Marks a renderer that is an EFFECT's screen footprint rather than part of the object's
    /// body.
    ///
    /// <para>
    /// Some effects are drawn by giving a fragment shader pixels to run on. The DOTS projector's
    /// <see cref="SpectralGridProjection"/> is one: its dots are computed per pixel from the
    /// depth buffer, so the mesh it draws is an eleven-metre box around the lens that exists
    /// only to cover the part of the screen the effect can reach. It is not the device. It is
    /// not even visible as a box.
    /// </para>
    ///
    /// <para>
    /// <b>Everything that asks "how big is this item" would otherwise get eleven metres.</b>
    /// That is not hypothetical: the lobby lays each item down by measuring its box and lifting
    /// it until its underside rests on the floor, and with the volume measured in, the 0.25 m
    /// projector was lifted 5.19 m and ended up at y = 6.49 - above the lobby's three-metre
    /// ceiling, in a room whose floor is at zero. From inside the game that is not a floating
    /// object, it is an item that never spawned, which is the same report as a build failure and
    /// a different bug (CLAUDE.md mistakes 20, 27 and 28).
    /// </para>
    ///
    /// <para>
    /// It is a COMPONENT rather than a name, a tag or a layer because a component is the one
    /// thing that survives <c>Instantiate</c> - every equipment item in this project reaches the
    /// world as a clone of a living template, and a private field or an auto-property does not
    /// come with it (mistake 30). It carries no state and no behaviour: it is a fact about the
    /// object, and the measuring code asks for it.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Catch If You Can/Effect Volume")]
    public sealed class EffectVolume : MonoBehaviour
    {
        /// <summary>
        /// True when this transform is an effect volume, or sits under one.
        ///
        /// <para>
        /// The parent chain is walked by hand rather than through
        /// <c>GetComponentInParent</c>'s include-inactive overload, because an effect volume is
        /// switched off for most of its life and the two overloads differ in exactly that case.
        /// </para>
        /// </summary>
        public static bool Encloses(Transform t)
        {
            for (Transform p = t; p != null; p = p.parent)
            {
                if (p.GetComponent<EffectVolume>() != null)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Marks <paramref name="go"/>, and answers with the marker either way.
        ///
        /// <para>
        /// <c>GetComponent</c> first, always: this runs on a template AND on every clone of it,
        /// and the clone arrives already carrying the component while the field that pointed at
        /// it arrives null. <c>AddComponent</c> on a <c>[DisallowMultipleComponent]</c> type
        /// that is already there returns null, and the line after would dereference it - which
        /// is mistake 27, exactly, in the class this one exists to serve.
        /// </para>
        /// </summary>
        public static EffectVolume Mark(GameObject go)
        {
            if (go == null)
                return null;

            EffectVolume existing = go.GetComponent<EffectVolume>();
            return existing != null ? existing : go.AddComponent<EffectVolume>();
        }
    }
}
