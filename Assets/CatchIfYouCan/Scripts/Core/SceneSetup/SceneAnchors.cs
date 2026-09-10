using UnityEngine;

namespace CatchIfYouCan.Core.SceneSetup
{
    /// <summary>
    /// The WORLD / VanAnchor / HouseAnchor trio that Training and Investigation both
    /// generate around.
    ///
    /// The positions are the ones the generator has always assumed: the van sits 14 m back
    /// from the house, and the house sits at the origin of the world root. They are applied
    /// only to anchors this creates, so an anchor moved by hand in the scene is left where
    /// the author put it.
    /// </summary>
    internal static class SceneAnchors
    {
        private static readonly Vector3 VanOffset = new Vector3(0f, 0f, -14f);

        internal static void EnsureWorldAnchors(ref Transform worldRoot, ref Transform vanAnchor,
                                                ref Transform houseAnchor)
        {
            if (worldRoot == null)
            {
                var existing = GameObject.Find("WORLD");
                worldRoot = existing != null ? existing.transform : new GameObject("WORLD").transform;
            }

            vanAnchor = EnsureAnchor(worldRoot, vanAnchor, "VanAnchor", VanOffset);
            houseAnchor = EnsureAnchor(worldRoot, houseAnchor, "HouseAnchor", Vector3.zero);
        }

        private static Transform EnsureAnchor(Transform parent, Transform assigned, string name,
                                              Vector3 localPositionIfCreated)
        {
            if (assigned != null)
                return assigned;

            var existing = parent.Find(name);
            if (existing != null)
                return existing;

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPositionIfCreated;
            return go.transform;
        }
    }
}
