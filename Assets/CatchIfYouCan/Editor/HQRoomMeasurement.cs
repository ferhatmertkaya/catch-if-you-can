using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CatchIfYouCan.EditorTools
{
    /// <summary>
    /// One measurement of the hand-built lobby, shared by the tool that reports it and the tool
    /// that acts on it.
    ///
    /// <para>
    /// Shared deliberately. Two copies of "how tall is this room" would agree on the day they
    /// were written and drift the first time either was corrected - and the failure would be
    /// silent, because the applier would scale by a factor the audit never proposed. This
    /// repository has already shipped two flashlights and two inventories; a second measurement
    /// is the same mistake with numbers.
    /// </para>
    /// </summary>
    public static class HQRoomMeasurement
    {
        /// <summary>Roots whose name starts with this are the hand-placed pack pieces.</summary>
        public const string RoomPrefix = "HQ_";

        /// <summary>The slab under the room, placed by hand beside the pack pieces.</summary>
        public const string FloorName = "FLOOR_Lobby_01";

        /// <summary>One renderer of the room, with the world box it occupies.</summary>
        public struct Piece
        {
            public Transform Transform;
            public Bounds World;
            public string Source;
        }

        /// <summary>What a measurement found, and whether it found enough to be used.</summary>
        public struct Result
        {
            public bool HasClearHeight;
            public float FloorTop;
            public float CeilingBottom;
            public float ClearHeight;
            public int FlatLow;
            public int FlatHigh;
            public Bounds Room;
            public List<Piece> Pieces;
        }

        /// <summary>
        /// The room's roots, by name rather than by selection, so the same set is measured every
        /// time and two runs can be compared.
        /// </summary>
        public static List<GameObject> CollectRoots()
        {
            var roots = new List<GameObject>();
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var all = scene.GetRootGameObjects();

            for (int i = 0; i < all.Length; i++)
            {
                string n = all[i].name;
                if (n.StartsWith(RoomPrefix) || n == FloorName)
                    roots.Add(all[i]);
            }
            return roots;
        }

        /// <summary>Every renderer under the given objects, with its world box.</summary>
        public static List<Piece> CollectPieces(IList<GameObject> roots)
        {
            var pieces = new List<Piece>();
            for (int r = 0; r < roots.Count; r++)
            {
                if (roots[r] == null)
                    continue;
                var renderers = roots[r].GetComponentsInChildren<MeshRenderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    MeshRenderer mr = renderers[i];
                    if (mr == null)
                        continue;
                    Bounds b = mr.bounds;
                    if (b.size == Vector3.zero)
                        continue;

                    var mf = mr.GetComponent<MeshFilter>();
                    string mesh = mf != null && mf.sharedMesh != null ? mf.sharedMesh.name : "?";
                    pieces.Add(new Piece
                    {
                        Transform = mr.transform,
                        World = b,
                        Source = mesh + "   in " + NearestNamedAncestor(mr.transform)
                    });
                }
            }
            return pieces;
        }

        /// <summary>
        /// The clear height, from the top of the flat things low down to the underside of the
        /// flat things high up.
        ///
        /// <para>
        /// Nothing is decided by NAME. This pack numbers its prefabs, so a name says nothing
        /// about whether a piece is a floor. A floor piece is wide, flat and below the room's
        /// middle; a ceiling piece is wide, flat and above it.
        /// </para>
        /// <para>
        /// World bounds are correct here and are not CLAUDE.md mistake 12. That mistake divided a
        /// wanted size by a world AABB to get a LOCAL scale, double-applying every ancestor. What
        /// is wanted here is a world height in metres, and the factor built from it is a RATIO of
        /// two world heights, out of which the ancestor chain cancels.
        /// </para>
        /// </summary>
        public static Result Measure(List<Piece> pieces)
        {
            var result = new Result { Pieces = pieces };
            if (pieces == null || pieces.Count == 0)
                return result;

            Bounds room = pieces[0].World;
            for (int i = 1; i < pieces.Count; i++)
                room.Encapsulate(pieces[i].World);
            result.Room = room;

            float mid = (room.min.y + room.max.y) * 0.5f;
            float floorTop = float.NegativeInfinity;
            float ceilingBottom = float.PositiveInfinity;

            for (int i = 0; i < pieces.Count; i++)
            {
                Bounds b = pieces[i].World;
                bool flat = b.size.y < Mathf.Min(b.size.x, b.size.z) * 0.5f;
                if (!flat)
                    continue;

                if (b.center.y < mid)
                {
                    result.FlatLow++;
                    floorTop = Mathf.Max(floorTop, b.max.y);
                }
                else
                {
                    result.FlatHigh++;
                    ceilingBottom = Mathf.Min(ceilingBottom, b.min.y);
                }
            }

            result.FloorTop = floorTop;
            result.CeilingBottom = ceilingBottom;
            result.HasClearHeight = result.FlatLow > 0 && result.FlatHigh > 0
                                    && ceilingBottom > floorTop;
            result.ClearHeight = result.HasClearHeight ? ceilingBottom - floorTop : 0f;
            return result;
        }

        /// <summary>Convenience: collect and measure in one step.</summary>
        public static Result MeasureRoom()
        {
            return Measure(CollectPieces(CollectRoots()));
        }

        /// <summary>
        /// Lists every distinct mesh with the largest size it is placed at, which is where a door
        /// or a window height is READ OFF rather than inferred from a name.
        /// </summary>
        public static void AppendSources(Result r, StringBuilder sb)
        {
            var bySource = new Dictionary<string, Bounds>();
            var count = new Dictionary<string, int>();

            for (int i = 0; i < r.Pieces.Count; i++)
            {
                string s = r.Pieces[i].Source;
                if (bySource.TryGetValue(s, out Bounds have))
                {
                    if (r.Pieces[i].World.size.magnitude > have.size.magnitude)
                        bySource[s] = r.Pieces[i].World;
                    count[s] = count[s] + 1;
                }
                else
                {
                    bySource[s] = r.Pieces[i].World;
                    count[s] = 1;
                }
            }

            foreach (var kv in bySource)
            {
                Vector3 s = kv.Value.size;
                sb.AppendLine("    " + s.x.ToString("F2").PadLeft(6) + " x " +
                              s.y.ToString("F2").PadLeft(6) + " x " +
                              s.z.ToString("F2").PadLeft(6) + " m   x" +
                              count[kv.Key].ToString().PadLeft(3) + "   " + kv.Key);
            }
        }

        /// <summary>
        /// The first ancestor whose name says which placed piece this is - the wrapper the room
        /// was built out of, rather than a numbered child inside a vendor prefab.
        /// </summary>
        public static string NearestNamedAncestor(Transform t)
        {
            for (Transform p = t; p != null; p = p.parent)
            {
                if (p.parent == null || p.name.StartsWith(RoomPrefix))
                    return p.name;
            }
            return t.name;
        }
    }
}
