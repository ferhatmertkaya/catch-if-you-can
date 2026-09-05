using System.Collections.Generic;
using UnityEngine;

namespace CatchIfYouCan.Procedural
{
    /// <summary>
    /// Builds the house's structural geometry as CIYC-owned meshes.
    ///
    /// <para>
    /// The pack that was meant to supply these walls turned out not to be a modular kit: its
    /// pivots sit up to 29 m from their own meshes, it has no floors, no ceilings and no
    /// stairs, and its UVs are normalised per piece so no two walls share a texture scale.
    /// Docs/HQ_MODULAR_MIGRATION.md has the measurements. What it does have is materials and a
    /// few genuinely compatible inserts, and those arrive in a later phase. The structure
    /// itself is generated here, at exactly the size the layout asks for, which is the one way
    /// to hit 6 x 3 x 6 without scaling anyone's art.
    /// </para>
    ///
    /// <para>
    /// Nothing here decides anything. It is handed a width, a height and an opening and returns
    /// a mesh; it reads no layout, draws no random number, and holds no state beyond a cache
    /// keyed by the dimensions it was asked for. Two walls of the same size share one mesh -
    /// a house of forty walls allocates a handful of them, not forty.
    /// </para>
    /// </summary>
    public static class StructuralMeshFactory
    {
        /// <summary>
        /// UVs run at one unit per metre. A neutral material tiles sensibly at that scale, and
        /// when the pack's materials arrive the density is rescaled per material rather than
        /// per mesh - the pack normalises UVs per piece, so its numbers cannot be inherited.
        /// </summary>
        public const float UvUnitsPerMetre = 1f;

        /// <summary>
        /// Where a wall's geometry sits relative to its transform: centred on X across its
        /// width, rising from y = 0 to its height, centred on Z across its thickness. A wall
        /// therefore goes exactly on the line it stands on, with no half-height correction at
        /// the call site - the kind of correction that is right in three places and wrong in
        /// the fourth.
        /// </summary>
        public static Mesh SolidWall(float width, float height, float thickness)
        {
            var key = new Key(Kind.SolidWall, width, height, thickness, 0f, 0f, 0f);
            if (_cache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            var mesh = Begin("CIYC_Wall_" + Fmt(width) + "x" + Fmt(height));
            AddBox(new Vector3(-width * 0.5f, 0f, -thickness * 0.5f),
                   new Vector3(width * 0.5f, height, thickness * 0.5f));
            Finish(mesh);

            _cache[key] = mesh;
            return mesh;
        }

        /// <summary>
        /// A wall with a real hole in it: left section, right section, header above the
        /// opening, and nothing where the opening is. Not a solid wall with something drawn
        /// over it - the player has to be able to walk through, and a collider that matched a
        /// painted-on doorway would be an invisible wall across every threshold in the house.
        /// </summary>
        public static Mesh WallWithOpening(float width, float height, float thickness,
            float openingWidth, float openingHeight, float openingBottom)
        {
            var sections = Sections(width, height, thickness, openingWidth, openingHeight, openingBottom);
            if (!sections.HasOpening)
                return SolidWall(width, height, thickness);

            var key = new Key(Kind.OpeningWall, width, height, thickness,
                openingWidth, openingHeight, openingBottom);
            if (_cache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            var mesh = Begin("CIYC_WallOpening_" + Fmt(width) + "x" + Fmt(height) +
                             "_" + Fmt(openingWidth) + "x" + Fmt(openingHeight));

            AddBox(sections.Left.min, sections.Left.max);
            AddBox(sections.Right.min, sections.Right.max);
            if (sections.Header.size.y > 0.001f)
                AddBox(sections.Header.min, sections.Header.max);
            if (sections.Sill.size.y > 0.001f)
                AddBox(sections.Sill.min, sections.Sill.max);

            Finish(mesh);
            _cache[key] = mesh;
            return mesh;
        }

        /// <summary>Top surface at y = 0, so a floor placed at the room's origin IS the floor.</summary>
        public static Mesh Floor(float width, float depth, float thickness)
        {
            var key = new Key(Kind.Floor, width, depth, thickness, 0f, 0f, 0f);
            if (_cache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            var mesh = Begin("CIYC_Floor_" + Fmt(width) + "x" + Fmt(depth));
            AddBox(new Vector3(-width * 0.5f, -thickness, -depth * 0.5f),
                   new Vector3(width * 0.5f, 0f, depth * 0.5f));
            Finish(mesh);

            _cache[key] = mesh;
            return mesh;
        }

        /// <summary>Underside at y = 0, so a ceiling placed at the room height IS the ceiling.</summary>
        public static Mesh Ceiling(float width, float depth, float thickness)
        {
            var key = new Key(Kind.Ceiling, width, depth, thickness, 0f, 0f, 0f);
            if (_cache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            var mesh = Begin("CIYC_Ceiling_" + Fmt(width) + "x" + Fmt(depth));
            AddBox(new Vector3(-width * 0.5f, 0f, -depth * 0.5f),
                   new Vector3(width * 0.5f, thickness, depth * 0.5f));
            Finish(mesh);

            _cache[key] = mesh;
            return mesh;
        }

        // ------------------------------------------------------------------ sections

        /// <summary>
        /// The solid parts of a wall around its opening, in the wall's own space.
        ///
        /// The builder needs these twice: once as geometry and once as colliders, and they must
        /// be the same rectangles both times. Computing them in two places is how a doorway
        /// ends up visually open and physically shut.
        /// </summary>
        public struct WallSections
        {
            public bool HasOpening;
            public Bounds Left;
            public Bounds Right;
            public Bounds Header;   // above the opening
            public Bounds Sill;     // below it - zero-height for a door, real for a window
        }

        public static WallSections Sections(float width, float height, float thickness,
            float openingWidth, float openingHeight, float openingBottom)
        {
            var s = new WallSections();

            float half = width * 0.5f;
            float halfT = thickness * 0.5f;
            float halfOpening = openingWidth * 0.5f;
            float openingTop = openingBottom + openingHeight;

            // An opening that reaches an edge is not an opening, it is a shorter wall; one
            // taller than the wall would leave a header of negative height. Either way the
            // caller gets a solid wall rather than geometry that folds through itself.
            bool valid = openingWidth > 0.01f && openingHeight > 0.01f
                         && halfOpening < half - 0.01f
                         && openingBottom >= -0.001f
                         && openingTop <= height + 0.001f;

            if (!valid)
            {
                s.HasOpening = false;
                return s;
            }

            s.HasOpening = true;
            s.Left = FromMinMax(new Vector3(-half, 0f, -halfT),
                                new Vector3(-halfOpening, height, halfT));
            s.Right = FromMinMax(new Vector3(halfOpening, 0f, -halfT),
                                 new Vector3(half, height, halfT));
            s.Header = FromMinMax(new Vector3(-halfOpening, openingTop, -halfT),
                                  new Vector3(halfOpening, height, halfT));
            s.Sill = FromMinMax(new Vector3(-halfOpening, 0f, -halfT),
                                new Vector3(halfOpening, openingBottom, halfT));
            return s;
        }

        private static Bounds FromMinMax(Vector3 min, Vector3 max)
        {
            var b = new Bounds();
            b.SetMinMax(min, max);
            return b;
        }

        /// <summary>Frees the cached meshes. The house is rebuilt, not accumulated.</summary>
        public static void ClearCache()
        {
            foreach (var pair in _cache)
            {
                if (pair.Value != null)
                    Object.Destroy(pair.Value);
            }

            _cache.Clear();
        }

        // -------------------------------------------------------------------- building

        private enum Kind { SolidWall, OpeningWall, Floor, Ceiling }

        private struct Key
        {
            private readonly Kind _kind;
            private readonly int _a, _b, _c, _d, _e, _f;

            public Key(Kind kind, float a, float b, float c, float d, float e, float f)
            {
                _kind = kind;
                // Quantised to a millimetre. Two walls whose widths differ by a float rounding
                // error are the same wall, and giving each its own mesh is how a cache stops
                // being one.
                _a = Mm(a); _b = Mm(b); _c = Mm(c); _d = Mm(d); _e = Mm(e); _f = Mm(f);
            }

            private static int Mm(float v) => Mathf.RoundToInt(v * 1000f);

            public override int GetHashCode()
            {
                unchecked
                {
                    int h = (int)_kind;
                    h = h * 397 ^ _a; h = h * 397 ^ _b; h = h * 397 ^ _c;
                    h = h * 397 ^ _d; h = h * 397 ^ _e; h = h * 397 ^ _f;
                    return h;
                }
            }

            public override bool Equals(object obj)
            {
                if (!(obj is Key other))
                    return false;

                return _kind == other._kind && _a == other._a && _b == other._b &&
                       _c == other._c && _d == other._d && _e == other._e && _f == other._f;
            }
        }

        private static readonly Dictionary<Key, Mesh> _cache = new Dictionary<Key, Mesh>();

        private static readonly List<Vector3> _vertices = new List<Vector3>(256);
        private static readonly List<Vector3> _normals = new List<Vector3>(256);
        private static readonly List<Vector2> _uv = new List<Vector2>(256);
        private static readonly List<int> _triangles = new List<int>(384);

        private static Mesh Begin(string name)
        {
            _vertices.Clear();
            _normals.Clear();
            _uv.Clear();
            _triangles.Clear();
            return new Mesh { name = name };
        }

        private static void Finish(Mesh mesh)
        {
            mesh.vertices = _vertices.ToArray();
            mesh.normals = _normals.ToArray();
            mesh.uv = _uv.ToArray();
            mesh.triangles = _triangles.ToArray();
            mesh.RecalculateBounds();
            // URP Lit samples a normal map along the tangent, and a mesh without tangents
            // lights as if every surface faced the same way.
            mesh.RecalculateTangents();
        }

        /// <summary>
        /// One axis-aligned box, six faces, four vertices each.
        ///
        /// Not eight shared vertices: a shared corner would average three face normals and
        /// round off every edge in the house. UVs run in metres along the face's own two axes,
        /// so a 6 m wall and a 2 m wall show the same texture size.
        /// </summary>
        private static void AddBox(Vector3 min, Vector3 max)
        {
            // +X and -X: UV across Z (width of the face) and Y (height)
            Quad(new Vector3(max.x, min.y, max.z), new Vector3(max.x, min.y, min.z),
                 new Vector3(max.x, max.y, min.z), new Vector3(max.x, max.y, max.z),
                 Vector3.right, Vector3.back, Vector3.up);
            Quad(new Vector3(min.x, min.y, min.z), new Vector3(min.x, min.y, max.z),
                 new Vector3(min.x, max.y, max.z), new Vector3(min.x, max.y, min.z),
                 Vector3.left, Vector3.forward, Vector3.up);

            // +Y and -Y: UV across X and Z
            Quad(new Vector3(min.x, max.y, max.z), new Vector3(max.x, max.y, max.z),
                 new Vector3(max.x, max.y, min.z), new Vector3(min.x, max.y, min.z),
                 Vector3.up, Vector3.right, Vector3.back);
            Quad(new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z),
                 new Vector3(max.x, min.y, max.z), new Vector3(min.x, min.y, max.z),
                 Vector3.down, Vector3.right, Vector3.forward);

            // +Z and -Z: UV across X and Y
            Quad(new Vector3(min.x, min.y, max.z), new Vector3(max.x, min.y, max.z),
                 new Vector3(max.x, max.y, max.z), new Vector3(min.x, max.y, max.z),
                 Vector3.forward, Vector3.right, Vector3.up);
            Quad(new Vector3(max.x, min.y, min.z), new Vector3(min.x, min.y, min.z),
                 new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y, min.z),
                 Vector3.back, Vector3.left, Vector3.up);
        }

        /// <summary>
        /// One face, wound counter-clockwise seen from the direction the normal points, which
        /// is what Unity draws front-facing. Wound the other way the room would be inside out:
        /// visible from outside and invisible from within, which is exactly where the player is.
        /// </summary>
        private static void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d,
            Vector3 normal, Vector3 uAxis, Vector3 vAxis)
        {
            int start = _vertices.Count;

            _vertices.Add(a); _vertices.Add(b); _vertices.Add(c); _vertices.Add(d);
            _normals.Add(normal); _normals.Add(normal); _normals.Add(normal); _normals.Add(normal);

            // PROJECTED from each vertex's own position, not counted from a corner.
            //
            // Every face used to start its UV at (0,0) and run to (span, span). One box, one
            // wall, no problem - but a wall with an opening is FOUR boxes: left, right, header
            // and sill. Each of them restarted the pattern at zero, so the wallpaper jumped at
            // every doorway and the header showed a slice of pattern that lined up with nothing
            // beside it. Projecting instead means the whole wall shares one continuous
            // coordinate system and the sections cannot disagree, because none of them has an
            // origin of its own.
            //
            // The axes carry the same direction the corner-counted version had, so nothing
            // mirrors or rotates relative to what was there before.
            AddProjectedUv(a, uAxis, vAxis);
            AddProjectedUv(b, uAxis, vAxis);
            AddProjectedUv(c, uAxis, vAxis);
            AddProjectedUv(d, uAxis, vAxis);

            // Nachgerechnet, nicht geraten. Fuer die +X-Flaeche liegt der Bildschirm so:
            // rechts = Cross(+Y, -X) = +Z, oben = +Y. Die vier Ecken landen dann auf
            // (1,0) (0,0) (0,1) (1,1), und die Reihenfolge 0-1-2 hat die Flaeche -0,5, laeuft
            // also im Uhrzeigersinn - was Unity vorderseitig zeichnet. Die Reihenfolge
            // 0-2-1 haette +0,5 und damit jede Wand von INNEN unsichtbar gemacht, was genau
            // die Seite ist, auf der der Spieler steht.
            _triangles.Add(start); _triangles.Add(start + 1); _triangles.Add(start + 2);
            _triangles.Add(start); _triangles.Add(start + 2); _triangles.Add(start + 3);
        }

        private static void AddProjectedUv(Vector3 vertex, Vector3 uAxis, Vector3 vAxis)
        {
            _uv.Add(new Vector2(Vector3.Dot(vertex, uAxis) * UvUnitsPerMetre,
                                Vector3.Dot(vertex, vAxis) * UvUnitsPerMetre));
        }

        private static string Fmt(float v) => v.ToString("0.00");
    }
}
