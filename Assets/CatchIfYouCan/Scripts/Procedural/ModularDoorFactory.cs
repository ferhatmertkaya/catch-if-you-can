using CatchIfYouCan.Content;
using CatchIfYouCan.Interaction;
using UnityEngine;

namespace CatchIfYouCan.Procedural
{
    /// <summary>
    /// The leaf that hangs in a generated doorway, and the hinge it swings on.
    ///
    /// <para>
    /// <b>Not a stand-in.</b> The old fallback built two cubes with no material, which under URP
    /// is a magenta frame and an invisible blocker across every threshold in the house - and
    /// that is the whole reason <see cref="ProceduralHouseGenerator"/> would rather leave an
    /// opening empty than fabricate something. This is the other answer to the same rule: a real
    /// door, at the exact size of the hole, wearing a material the project owns, refused outright
    /// if that material is missing.
    /// </para>
    ///
    /// <para>
    /// <b>One per connection, not one per wall.</b> A doorway between two rooms is one hole that
    /// both rooms build a wall around. The lining is per wall - a real doorway has one in each
    /// room - but the leaf is the part there is only one of, so it is built here, from the
    /// layout's own door list, rather than in <see cref="ModularRoomBuilder"/>.
    /// </para>
    ///
    /// <para>
    /// <b>The hinge is a wrapper, not the leaf's own pivot.</b> Rotating the leaf about its own
    /// centre swings half of it into the wall. The hinge sits on the opening's left jamb and the
    /// leaf hangs off it, offset by half its width, so the swing is about the edge - which is
    /// what a door does.
    /// </para>
    /// </summary>
    public static class ModularDoorFactory
    {
        /// <summary>Gap between the leaf and the lining, total, across the opening.</summary>
        public const float LeafClearance = 0.06f;

        /// <summary>Thickness of the leaf itself.</summary>
        public const float LeafThickness = 0.045f;

        /// <summary>How far the leaf swings when opened.</summary>
        public const float OpenAngle = 92f;

        /// <summary>
        /// Builds the door, or refuses and says why.
        /// </summary>
        /// <returns>The component that answers E, or null when nothing was built.</returns>
        public static InteractiveDoor Build(Transform parent, Vector3 position, Quaternion rotation,
                                            ModularInteriorCatalog catalog)
        {
            Material leafMaterial = catalog != null ? catalog.DoorLeafMaterial : null;

            if (leafMaterial == null)
            {
                Core.CIYCLog.Error("[CIYC][House][Door] Kein DoorLeafMaterial im " +
                                   "ModularInteriorCatalog. Die Oeffnung bleibt LEER statt ein " +
                                   "Blatt ohne Material zu bekommen - das waere unter URP eine " +
                                   "magenta Flaeche im Tuerrahmen, und der Collider davor " +
                                   "waere die unsichtbare Wand in jeder Schwelle des Hauses.");
                return null;
            }

            float leafWidth = ModularRoomBuilder.DoorWidth - LeafClearance;
            float leafHeight = ModularRoomBuilder.DoorHeight - 0.04f;

            var root = new GameObject("Door_Generated");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, rotation);

            // On the left jamb. The leaf's own left edge sits here, so the swing is about the
            // edge rather than about the middle of the leaf.
            var hinge = new GameObject("Hinge");
            hinge.transform.SetParent(root.transform, false);
            hinge.transform.localPosition =
                new Vector3(-(ModularRoomBuilder.DoorWidth * 0.5f) + LeafClearance * 0.5f, 0f, 0f);

            // A PANEL, not a wall piece: the texture IS a door - stiles, rails, panels, a
            // keyhole - so it is stretched once across the leaf instead of tiled by the metre,
            // which would put one and a bit doors across a 1.2 m leaf.
            var leaf = new GameObject("Leaf");
            leaf.transform.SetParent(hinge.transform, false);
            leaf.transform.localPosition = new Vector3(leafWidth * 0.5f, 0f, 0f);

            var filter = leaf.AddComponent<MeshFilter>();
            filter.sharedMesh = StructuralMeshFactory.Panel(leafWidth, leafHeight, LeafThickness);

            var renderer = leaf.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = leafMaterial;

            // Collision follows the LEAF, so it moves with the swing: closed it blocks the
            // threshold, open it stands against the wall and the doorway is clear. A collider on
            // the root would be a blocker that never moves, which is the old bug exactly.
            var box = leaf.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, leafHeight * 0.5f, 0f);
            box.size = new Vector3(leafWidth, leafHeight, LeafThickness);

            var door = root.AddComponent<InteractiveDoor>();
            door.Configure(hinge.transform, OpenAngle);

            // Und dieselbe Tuer laesst sich ZIEHEN. Beide sitzen auf demselben Scharnier und
            // streiten sich nicht: die schwingende Tuer schreibt es nur, solange sie animiert,
            // und das Ziehen meldet ihr beim Loslassen, wo das Blatt stehen geblieben ist. Was
            // sie NICHT bekommt, ist ein zweiter Scharnierbegriff - beide bekommen denselben
            // Transform und denselben Grenzwinkel, weil zwei Antworten auf "wie weit geht die
            // Tuer auf" genau dann auseinandergehen, wenn jemand einen davon aendert.
            var drag = root.AddComponent<Interaction.DraggableDoor>();
            drag.Configure(hinge.transform, 1, OpenAngle);

            // MEASURED, not claimed. The two things a door can get wrong without anybody being
            // able to say which - standing off the floor and cutting the ceiling - are the two
            // numbers printed here, in the room's own space.
            Bounds worldBounds = renderer.bounds;
            Core.CIYCLog.Info("[CIYC][House][Door] leaf=" + leafWidth.ToString("F2") + " x " +
                              leafHeight.ToString("F2") + " x " + LeafThickness.ToString("F3") +
                              " material=" + leafMaterial.name +
                              " at=" + position.ToString("F2") +
                              " yaw=" + rotation.eulerAngles.y.ToString("F0") +
                              " bottomY=" + worldBounds.min.y.ToString("F3") +
                              " topY=" + worldBounds.max.y.ToString("F3") +
                              " opening=" + ModularRoomBuilder.DoorWidth.ToString("F2") + " x " +
                              ModularRoomBuilder.DoorHeight.ToString("F2") +
                              " ceiling=" + ModularRoomBuilder.CeilingClearance.ToString("F2"));

            if (worldBounds.max.y - position.y > ModularRoomBuilder.CeilingClearance + 0.01f)
            {
                Core.CIYCLog.Error("[CIYC][House][Door] Das Blatt reicht " +
                                   (worldBounds.max.y - position.y).ToString("F2") +
                                   " m hoch und damit ueber die lichte Hoehe " +
                                   ModularRoomBuilder.CeilingClearance.ToString("F2") +
                                   " m - es schneidet die Decke.");
            }

            return door;
        }
    }
}
