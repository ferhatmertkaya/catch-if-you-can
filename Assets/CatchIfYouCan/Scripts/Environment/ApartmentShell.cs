using System.Collections.Generic;
using UnityEngine;

namespace CatchIfYouCan.Environment
{
    /// <summary>
    /// The architectural shell of a room: floor, ceiling, and walls with openings cut into
    /// them.
    ///
    /// <para>
    /// <b>Blockout, and it says so.</b> The geometry is boxes, exactly as the lobby's own walls
    /// are boxes, and it is not the intended final art. What keeps it from looking like a
    /// primitive test scene is that the surfaces take the project's authored Victorian
    /// materials rather than flat runtime colours - <c>PrimitiveRoomFactory</c> makes its own
    /// grey ones and that is why generated houses read as grey.
    /// </para>
    ///
    /// <para>
    /// Every surface is built through this one type so a later art pass can replace the mesh
    /// for a wall, a floor or a doorway in one place without gameplay noticing. Nothing here
    /// knows about the ghost, equipment, evidence or the session.
    /// </para>
    /// </summary>
    public static class ApartmentShell
    {
        /// <summary>Wall thickness in metres. Thin enough to read as a partition wall.</summary>
        public const float WallThickness = 0.18f;

        /// <summary>Floor slab thickness, which is also the gap between storey floors.</summary>
        public const float SlabThickness = 0.3f;

        /// <summary>Inside height of one storey, floor to ceiling.</summary>
        public const float StoreyHeight = 2.9f;

        /// <summary>Floor-to-floor, which is what the upper storey is raised by.</summary>
        public const float StoreyPitch = StoreyHeight + SlabThickness;

        public const float DoorWidth = 1.05f;
        public const float DoorHeight = 2.15f;

        /// <summary>The materials a shell is dressed in. All optional; missing ones fall back.</summary>
        public sealed class Surfaces
        {
            public Material Wall;
            public Material Floor;
            public Material Ceiling;
            public Material Trim;
        }

        /// <summary>An opening in a wall run, measured along that run from its start.</summary>
        public readonly struct Opening
        {
            public readonly float CentreAlongRun;
            public readonly float Width;
            public readonly float Height;

            public Opening(float centreAlongRun, float width, float height)
            {
                CentreAlongRun = centreAlongRun;
                Width = width;
                Height = height;
            }

            public static Opening Door(float centreAlongRun) =>
                new Opening(centreAlongRun, DoorWidth, DoorHeight);
        }

        // ---- surfaces ---------------------------------------------------------------------

        /// <summary>A floor slab. Its top face sits at <paramref name="topY"/>.</summary>
        public static GameObject Floor(Transform parent, string name, Rect footprint, float topY,
                                       Surfaces surfaces)
        {
            return Box(parent, name,
                new Vector3(footprint.center.x, topY - SlabThickness * 0.5f, footprint.center.y),
                new Vector3(footprint.width, SlabThickness, footprint.height),
                surfaces?.Floor);
        }

        /// <summary>A ceiling slab directly above a floor whose top face is at <paramref name="floorY"/>.</summary>
        public static GameObject Ceiling(Transform parent, string name, Rect footprint, float floorY,
                                         Surfaces surfaces)
        {
            return Box(parent, name,
                new Vector3(footprint.center.x, floorY + StoreyHeight + SlabThickness * 0.5f,
                            footprint.center.y),
                new Vector3(footprint.width, SlabThickness, footprint.height),
                surfaces?.Ceiling);
        }

        /// <summary>
        /// A straight wall run from <paramref name="from"/> to <paramref name="to"/> in the XZ
        /// plane, with any number of openings cut out of it.
        ///
        /// <para>
        /// The cut is done the way the lobby's north wall is done - a segment either side of
        /// each opening and a header above it - rather than by boolean geometry, because three
        /// boxes are three things that cannot be wrong and a CSG operation done blind is a hole
        /// in the wrong place nobody can see from here.
        /// </para>
        /// </summary>
        public static void Wall(Transform parent, string name, Vector2 from, Vector2 to,
                                float floorY, Surfaces surfaces, params Opening[] openings)
        {
            Vector2 delta = to - from;
            float runLength = delta.magnitude;
            if (runLength < 0.01f)
                return;

            Vector2 dir = delta / runLength;
            float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);

            // Openings sorted along the run so the solid stretches between them can be walked
            // in order. An unsorted list produces overlapping segments and negative widths.
            var sorted = new List<Opening>(openings ?? new Opening[0]);
            sorted.Sort((a, b) => a.CentreAlongRun.CompareTo(b.CentreAlongRun));

            float cursor = 0f;
            int index = 0;

            foreach (Opening opening in sorted)
            {
                float start = opening.CentreAlongRun - opening.Width * 0.5f;
                float end = opening.CentreAlongRun + opening.Width * 0.5f;

                if (start > cursor + 0.01f)
                    Segment(parent, name + "_S" + index++, from, dir, rotation, cursor, start - cursor,
                            floorY, StoreyHeight, 0f, surfaces?.Wall);

                float headerHeight = StoreyHeight - opening.Height;
                if (headerHeight > 0.01f)
                    Segment(parent, name + "_Header" + index, from, dir, rotation, start,
                            opening.Width, floorY, headerHeight, opening.Height, surfaces?.Trim);

                cursor = end;
            }

            if (runLength > cursor + 0.01f)
                Segment(parent, name + "_S" + index, from, dir, rotation, cursor, runLength - cursor,
                        floorY, StoreyHeight, 0f, surfaces?.Wall);
        }

        private static void Segment(Transform parent, string name, Vector2 from, Vector2 dir,
                                    Quaternion rotation, float startAlong, float length,
                                    float floorY, float height, float baseOffset, Material material)
        {
            Vector2 centre = from + dir * (startAlong + length * 0.5f);
            var go = Box(parent, name,
                new Vector3(centre.x, floorY + baseOffset + height * 0.5f, centre.y),
                new Vector3(WallThickness, height, length),
                material);
            go.transform.localRotation = rotation;
        }

        /// <summary>
        /// A straight flight of stairs climbing one storey, as discrete steps.
        ///
        /// <para>
        /// Steps rather than a ramp because a CharacterController walks up steps of this size
        /// on its own, and because a ramp reads as a slope rather than as a staircase. The
        /// rise is <see cref="StoreyPitch"/> divided by the step count, so the flight always
        /// arrives exactly at the upper floor however the storey height is retuned.
        /// </para>
        /// </summary>
        public static GameObject Stairs(Transform parent, string name, Vector3 bottomCentre,
                                        float width, float run, int steps, float yawDegrees,
                                        Surfaces surfaces)
        {
            steps = Mathf.Max(2, steps);

            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = bottomCentre;
            root.transform.localRotation = Quaternion.Euler(0f, yawDegrees, 0f);

            float rise = StoreyPitch / steps;
            float tread = run / steps;

            for (int i = 0; i < steps; i++)
            {
                // Each step is a slab from the ground up to its own height, so the underside is
                // solid and the player cannot see or fall through the flight.
                float top = rise * (i + 1);
                Box(root.transform, "Step" + i,
                    new Vector3(0f, top * 0.5f, tread * (i + 0.5f)),
                    new Vector3(width, top, tread),
                    surfaces?.Trim);
            }

            return root;
        }

        // ---- one box, one place ------------------------------------------------------------

        /// <summary>
        /// Every surface in the shell goes through here, so the day these become authored
        /// meshes it is one method that changes rather than every wall in the apartment.
        /// </summary>
        public static GameObject Box(Transform parent, string name, Vector3 localCentre,
                                     Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localCentre;
            go.transform.localScale = size;

            if (material != null)
                go.GetComponent<Renderer>().sharedMaterial = material;

            return go;
        }
    }
}
