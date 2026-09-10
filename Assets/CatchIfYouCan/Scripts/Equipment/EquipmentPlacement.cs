using UnityEngine;

namespace CatchIfYouCan.Equipment
{
    /// <summary>Which surfaces an item will accept. Flags, because most accept more than one.</summary>
    [System.Flags]
    public enum PlacementSurface
    {
        None = 0,
        Floor = 1 << 0,
        Wall = 1 << 1,
        Ceiling = 1 << 2,

        FloorAndWall = Floor | Wall,
        Any = Floor | Wall | Ceiling,
    }

    /// <summary>What to look for, and what counts as a legal answer.</summary>
    public struct PlacementQuery
    {
        /// <summary>Where the look ray starts - the camera, not the item.</summary>
        public Vector3 Origin;
        public Vector3 Direction;

        /// <summary>How far the player can reach, in metres.</summary>
        public float MaxRange;

        /// <summary>Which surfaces this item will sit on.</summary>
        public PlacementSurface Allowed;

        /// <summary>What counts as a surface, and what counts as being in the way.</summary>
        public LayerMask SurfaceMask;

        /// <summary>Half the item's size, for the clearance test. Zero skips the test.</summary>
        public Vector3 HalfExtents;

        /// <summary>
        /// How far off the surface the item is held before its clearance is tested, in metres.
        /// Without it the surface the item is resting on is itself an overlap.
        /// </summary>
        public float SurfaceSkin;

        /// <summary>
        /// Which way is "forward" for the player, used to orient a floor placement. A projector
        /// on the floor should face the way the player was looking, not an arbitrary axis.
        /// </summary>
        public Vector3 PlayerForward;

        /// <summary>
        /// Steepest surface still counted as floor rather than wall, in degrees from level.
        /// </summary>
        public float MaxFloorAngle;
    }

    /// <summary>Where an item would go, or why it would not go there.</summary>
    public readonly struct PlacementResult
    {
        public readonly bool IsValid;
        public readonly EquipmentActionStatus Status;
        public readonly string Detail;

        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly Vector3 SurfaceNormal;
        public readonly PlacementSurface Surface;

        public PlacementResult(bool isValid, EquipmentActionStatus status, string detail,
                               Vector3 position, Quaternion rotation, Vector3 normal,
                               PlacementSurface surface)
        {
            IsValid = isValid;
            Status = status;
            Detail = detail;
            Position = position;
            Rotation = rotation;
            SurfaceNormal = normal;
            Surface = surface;
        }

        public static PlacementResult Invalid(EquipmentActionStatus status, string detail) =>
            new PlacementResult(false, status, detail, Vector3.zero, Quaternion.identity,
                                Vector3.up, PlacementSurface.None);

        public override string ToString() =>
            IsValid ? "valid on " + Surface + " at " + Position.ToString("F2")
                    : "invalid: " + Status + (string.IsNullOrEmpty(Detail) ? "" : " - " + Detail);
    }

    /// <summary>
    /// Where a placeable item would go if you placed it now.
    ///
    /// <para>
    /// Four items place things - the grid projector on a wall or a floor, the video camera, the
    /// salt on a floor, and whatever comes next - and without this each would invent its own
    /// raycast, its own idea of what counts as a wall, its own orientation convention and its
    /// own reason for saying no. Four implementations of "can I put this here" is four
    /// different answers to the same question.
    /// </para>
    ///
    /// <para>
    /// It answers with a reason, because "you are looking at nothing", "that is a wall and this
    /// only goes on floors", "that is too far away" and "there is a table in the way" are four
    /// different things and a player can only fix the one they are told about.
    /// </para>
    ///
    /// <para>
    /// <b>No allocation.</b> This runs every frame while a preview is up, so the raycast and
    /// the clearance test use shared buffers and non-allocating physics calls.
    /// </para>
    /// </summary>
    public static class EquipmentPlacement
    {
        /// <summary>Shared, because Evaluate runs per frame and must not produce garbage.</summary>
        private static readonly RaycastHit[] HitBuffer = new RaycastHit[8];
        private static readonly Collider[] OverlapBuffer = new Collider[8];

        /// <summary>Sensible defaults for an item that has not been tuned yet.</summary>
        public static PlacementQuery DefaultQuery(Vector3 origin, Vector3 direction,
                                                  PlacementSurface allowed) =>
            new PlacementQuery
            {
                Origin = origin,
                Direction = direction,
                MaxRange = 3.5f,
                Allowed = allowed,
                SurfaceMask = ~0,
                HalfExtents = Vector3.zero,
                SurfaceSkin = 0.01f,
                PlayerForward = Vector3.forward,
                MaxFloorAngle = 40f,
            };

        /// <summary>
        /// Casts along the look, classifies what it found, orients the item against it and
        /// checks it fits.
        /// </summary>
        public static PlacementResult Evaluate(in PlacementQuery query)
        {
            if (query.Direction.sqrMagnitude < 1e-6f)
                return PlacementResult.Invalid(
                    EquipmentActionStatus.NoValidSurface, "no aim direction");

            var ray = new Ray(query.Origin, query.Direction.normalized);

            // Non-allocating, and sorted by hand: RaycastNonAlloc does not promise an order,
            // and the nearest surface is the one being pointed at.
            int count = Physics.RaycastNonAlloc(ray, HitBuffer, query.MaxRange,
                                                query.SurfaceMask, QueryTriggerInteraction.Ignore);
            if (count == 0)
                return PlacementResult.Invalid(
                    EquipmentActionStatus.NoValidSurface, "not pointing at a surface within reach");

            int nearest = 0;
            for (int i = 1; i < count; i++)
                if (HitBuffer[i].distance < HitBuffer[nearest].distance)
                    nearest = i;

            Vector3 point = HitBuffer[nearest].point;
            Vector3 normal = HitBuffer[nearest].normal;

            var surface = Classify(normal, query.MaxFloorAngle);
            if (surface == PlacementSurface.None)
                return PlacementResult.Invalid(
                    EquipmentActionStatus.NoValidSurface, "that surface is not flat enough");

            if ((query.Allowed & surface) == 0)
            {
                return PlacementResult.Invalid(
                    EquipmentActionStatus.NoValidSurface,
                    "this goes on " + Describe(query.Allowed) + ", not on a " +
                    surface.ToString().ToLowerInvariant());
            }

            Quaternion rotation = Orient(surface, normal, query.PlayerForward);

            // Held off the surface by its own half-height plus a skin, so the thing it is
            // standing on is not counted as the thing that is in the way.
            float lift = query.HalfExtents.y + query.SurfaceSkin;
            Vector3 position = point + normal * lift;

            if (query.HalfExtents.sqrMagnitude > 0f)
            {
                // Shrunk slightly so a surface exactly touching it does not read as an overlap.
                Vector3 probe = query.HalfExtents * 0.95f;
                int overlaps = Physics.OverlapBoxNonAlloc(position, probe, OverlapBuffer,
                                                          rotation, query.SurfaceMask,
                                                          QueryTriggerInteraction.Ignore);
                for (int i = 0; i < overlaps; i++)
                {
                    var collider = OverlapBuffer[i];
                    if (collider == null || collider == HitBuffer[nearest].collider)
                        continue;

                    return PlacementResult.Invalid(
                        EquipmentActionStatus.Blocked,
                        collider.name + " is in the way");
                }
            }

            return new PlacementResult(true, EquipmentActionStatus.Success, null,
                                       position, rotation, normal, surface);
        }

        /// <summary>Floor, wall or ceiling, by how far the normal is from straight up.</summary>
        private static PlacementSurface Classify(Vector3 normal, float maxFloorAngle)
        {
            float fromUp = Vector3.Angle(normal, Vector3.up);

            if (fromUp <= maxFloorAngle)
                return PlacementSurface.Floor;

            if (fromUp >= 180f - maxFloorAngle)
                return PlacementSurface.Ceiling;

            // Everything between is a wall, including a surface leaning far enough that
            // standing something on it would slide.
            return PlacementSurface.Wall;
        }

        /// <summary>
        /// How the item sits against what it found.
        ///
        /// <para>
        /// On a floor the item stands up and faces the way the player was looking. On a wall it
        /// points <b>away</b> from the wall, which is the only useful direction for anything
        /// that projects or watches, and keeps its roll level against world up so it does not
        /// arrive tilted.
        /// </para>
        /// </summary>
        private static Quaternion Orient(PlacementSurface surface, Vector3 normal,
                                         Vector3 playerForward)
        {
            if (surface == PlacementSurface.Wall || surface == PlacementSurface.Ceiling)
            {
                Vector3 up = Vector3.up;
                if (Mathf.Abs(Vector3.Dot(normal, up)) > 0.99f)
                    up = Mathf.Abs(playerForward.y) < 0.99f ? playerForward : Vector3.forward;

                return Quaternion.LookRotation(normal, up);
            }

            Vector3 forward = Vector3.ProjectOnPlane(playerForward, normal);
            if (forward.sqrMagnitude < 1e-6f)
                forward = Vector3.ProjectOnPlane(Vector3.forward, normal);
            if (forward.sqrMagnitude < 1e-6f)
                forward = Vector3.forward;

            return Quaternion.LookRotation(forward.normalized, normal);
        }

        private static string Describe(PlacementSurface allowed)
        {
            if (allowed == PlacementSurface.Floor) return "floors";
            if (allowed == PlacementSurface.Wall) return "walls";
            if (allowed == PlacementSurface.FloorAndWall) return "floors and walls";
            if (allowed == PlacementSurface.Any) return "any surface";
            return allowed.ToString();
        }
    }
}
