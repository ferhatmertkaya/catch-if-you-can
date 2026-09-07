using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace CatchIfYouCan.Procedural
{
    /// <summary>
    /// Says what is actually standing in the generated house, once, after it is built.
    ///
    /// <para>
    /// <b>Why a report rather than a fix.</b> "There is a very large dark rectangular block in
    /// that room" is a description of a picture, and at least four different things produce that
    /// picture: a furniture fallback drawn as a cube because its definition has no prefab, a
    /// fixture placed before its scale was set, a room shell whose primitive stand-in ran, and a
    /// renderer switched off leaving only its collider. Guessing which one from the outside is
    /// how a project ends up scaling a placeholder until it looks acceptable and never finding
    /// out what made it. So this measures every renderer in the house and names the ones that
    /// cannot be what they claim to be, with enough detail to go straight to the call site.
    /// </para>
    ///
    /// <para>
    /// Structure is exempt by name, not by size: a floor IS six metres across and a wall IS
    /// three metres tall, so flagging them would bury the one line that matters under forty that
    /// do not.
    /// </para>
    /// </summary>
    public static class HouseContentReport
    {
        /// <summary>
        /// The largest a piece of loose content may be in any axis before it is suspicious.
        /// A wardrobe is about 2.2 m tall and 1.2 m wide; nothing in a house is 3 m in every
        /// direction except the room itself.
        /// </summary>
        public const float SuspiciousSizeMetres = 2.60f;

        /// <summary>Names that ARE the room, and are supposed to be room-sized.</summary>
        private static readonly string[] StructuralPrefixes =
        {
            "Floor", "Ceiling", "Wall", "Door", "Window", "Trim", "Room_", "Baseboard", "Stairs",
            "Socket_", "HideSpot", "Hinge", "Leaf",
        };

        private static readonly List<Renderer> _buffer = new List<Renderer>(256);

        public static void Report(GeneratedHouse house)
        {
            if (house?.Root == null)
                return;

            int total = 0;
            int structural = 0;
            var suspicious = new StringBuilder();
            int suspiciousCount = 0;

            for (int i = 0; i < house.Rooms.Count; i++)
            {
                GeneratedRoomInstance room = house.Rooms[i];
                if (room?.Root == null)
                    continue;

                _buffer.Clear();
                room.Root.GetComponentsInChildren(true, _buffer);

                for (int r = 0; r < _buffer.Count; r++)
                {
                    Renderer renderer = _buffer[r];
                    if (renderer == null)
                        continue;

                    total++;

                    if (IsStructural(renderer.gameObject.name))
                    {
                        structural++;
                        continue;
                    }

                    // Measured in the object's OWN space and then scaled, never as a world AABB
                    // divided by something (CLAUDE.md mistake 12). A world AABB of a rotated
                    // object is bigger than the object, and reporting that as its size sends the
                    // reader after a piece of furniture that is the size it was meant to be.
                    Vector3 size = LocalSize(renderer);
                    float largest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));

                    // Zwei Gruende, gemeldet zu werden, und der zweite ist der wichtigere.
                    //
                    // GROESSE allein reicht nicht: ein Kleiderschrank ist 2.2 m hoch und voellig
                    // richtig, und ihn zu melden wuerde die eine Zeile begraben, auf die es
                    // ankommt. Der Wuerfel, der dieses Projekt beschaeftigt hat, war 1 x 1 x 1 m
                    // und waere unter jeder vernuenftigen Groessenschwelle durchgerutscht.
                    //
                    // Was ihn verraet, ist seine HERKUNFT: er trug ein Unity-Primitivmesh. Ein
                    // eingerichteter Raum enthaelt keine Wuerfel, Kapseln oder Kugeln - alles
                    // darin ist ein Modell mit einem Namen. Also wird beides geprueft.
                    bool oversized = largest > SuspiciousSizeMetres;
                    bool primitive = IsUnityPrimitive(MeshNameOf(renderer));

                    if (!oversized && !primitive)
                        continue;

                    suspiciousCount++;
                    if (suspicious.Length < 900)
                    {
                        suspicious.Append("\n  - '").Append(renderer.gameObject.name)
                                  .Append("' in ").Append(room.Root.name)
                                  .Append(" size=").Append(size.ToString("F2"))
                                  .Append(" at=").Append(renderer.transform.position.ToString("F1"))
                                  .Append(" mesh=").Append(MeshNameOf(renderer))
                                  .Append(" material=").Append(renderer.sharedMaterial != null
                                      ? renderer.sharedMaterial.name : "<none>")
                                  .Append(" shader=").Append(renderer.sharedMaterial != null &&
                                                             renderer.sharedMaterial.shader != null
                                      ? renderer.sharedMaterial.shader.name : "<none>")
                                  .Append(" enabled=").Append(renderer.enabled)
                                  .Append(" flagged=").Append(oversized
                                      ? (primitive ? "gross+primitiv" : "gross")
                                      : "primitiv")
                                  .Append(" path=").Append(PathOf(renderer.transform, room.Root.transform));
                    }
                }
            }

            string headline = "[CIYC][House][Content] rooms=" + house.Rooms.Count +
                              " renderers=" + total + " structural=" + structural +
                              " loose=" + (total - structural) +
                              " flagged=" + suspiciousCount +
                              " (groesser als " + SuspiciousSizeMetres.ToString("F2") +
                              " m oder auf einem Unity-Primitivmesh)";

            if (suspiciousCount == 0)
            {
                Core.CIYCLog.Info(headline + " - kein Platzhalter und nichts Ueberdimensioniertes.");
                return;
            }

            Core.CIYCLog.Error(headline + ". Jedes davon ist entweder ein Platzhalter fuer " +
                               "Inhalt, den es noch nicht gibt, oder ein Stueck, das nach dem " +
                               "Erzeugen nie skaliert wurde. Der Hierarchie-Pfad sagt, welches:" +
                               suspicious + (suspiciousCount > 6 ? "\n  ... und weitere" : ""));
        }

        /// <summary>
        /// Ob dieses Mesh eines von Unitys eingebauten Primitiven ist.
        ///
        /// <para>
        /// <c>GameObject.CreatePrimitive</c> gibt seinem Mesh immer denselben Namen: "Cube",
        /// "Capsule", "Sphere", "Cylinder", "Plane", "Quad". Ein Objekt in einem eingerichteten
        /// Raum, das so ein Mesh traegt, ist ein Platzhalter - unabhaengig davon, wie gross es
        /// ist und wie ordentlich es texturiert wurde.
        /// </para>
        /// </summary>
        private static bool IsUnityPrimitive(string meshName)
        {
            switch (meshName)
            {
                case "Cube":
                case "Capsule":
                case "Sphere":
                case "Cylinder":
                case "Plane":
                case "Quad":
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsStructural(string name)
        {
            for (int i = 0; i < StructuralPrefixes.Length; i++)
            {
                if (name.StartsWith(StructuralPrefixes[i], System.StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// The size the object actually occupies: its mesh's own bounds times the scale that has
        /// been applied to it. Not <c>Renderer.bounds</c>, which is a WORLD axis-aligned box and
        /// therefore larger than the object whenever it is turned.
        /// </summary>
        private static Vector3 LocalSize(Renderer renderer)
        {
            Vector3 meshSize = Vector3.one;

            var filter = renderer.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null)
                meshSize = filter.sharedMesh.bounds.size;
            else if (renderer is SkinnedMeshRenderer skinned && skinned.sharedMesh != null)
                meshSize = skinned.sharedMesh.bounds.size;

            Vector3 scale = renderer.transform.lossyScale;
            return new Vector3(Mathf.Abs(meshSize.x * scale.x),
                               Mathf.Abs(meshSize.y * scale.y),
                               Mathf.Abs(meshSize.z * scale.z));
        }

        private static string MeshNameOf(Renderer renderer)
        {
            var filter = renderer.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null)
                return filter.sharedMesh.name;

            return renderer is SkinnedMeshRenderer skinned && skinned.sharedMesh != null
                ? skinned.sharedMesh.name
                : "<none>";
        }

        private static string PathOf(Transform child, Transform root)
        {
            var path = new StringBuilder(child.name);
            Transform walk = child.parent;

            while (walk != null && walk != root)
            {
                path.Insert(0, walk.name + "/");
                walk = walk.parent;
            }

            return path.ToString();
        }
    }
}
