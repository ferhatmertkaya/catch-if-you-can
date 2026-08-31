using System;
using System.Collections.Generic;
using CatchIfYouCan.Procedural.Deterministic;

namespace CatchIfYouCan.Procedural
{
    [Serializable]
    public class HouseLayoutNode
    {
        public int Id;
        public RoomCategory Category;
        public GridCell Cell;
        public List<SocketDirection> OpenDirections = new List<SocketDirection>();
    }

    [Serializable]
    public class HouseLayoutEdge
    {
        public int NodeAId;
        public int NodeBId;
        public SocketDirection DirectionFromA;
    }

    /// <summary>
    /// A read-only graph view over an authoritative <see cref="HouseLayout"/>.
    ///
    /// This type used to BE the generator: it built the room graph itself with
    /// System.Random and shuffled nodes with OrderBy(_ => rng.Next()). Generation now lives
    /// in HouseLayoutBuilder (engine-free, Stage A) and this is a projection of the result,
    /// kept so existing consumers - GeneratedHouse, HouseValidator, the editor test tool -
    /// keep working unchanged.
    ///
    /// It also no longer references UnityEngine.Vector2Int, which it previously used
    /// without importing UnityEngine at all; the file did not compile.
    /// </summary>
    public class HouseLayoutGraph
    {
        public const int MinRooms = 6;
        public const int MaxRooms = 14;

        public List<HouseLayoutNode> Nodes { get; } = new List<HouseLayoutNode>();
        public List<HouseLayoutEdge> Edges { get; } = new List<HouseLayoutEdge>();

        /// <summary>Projects an authoritative layout into graph form.</summary>
        public static HouseLayoutGraph FromLayout(HouseLayout layout)
        {
            var graph = new HouseLayoutGraph();
            if (layout == null)
                return graph;

            for (int i = 0; i < layout.Rooms.Count; i++)
            {
                var room = layout.Rooms[i];
                var node = new HouseLayoutNode
                {
                    Id = room.RoomId,
                    Category = room.Category,
                    Cell = room.Cell
                };

                for (int d = 0; d < Directions.Cardinal.Length; d++)
                {
                    var dir = Directions.Cardinal[d];
                    if (room.IsOpen(dir))
                        node.OpenDirections.Add(dir);
                }

                graph.Nodes.Add(node);
            }

            for (int i = 0; i < layout.Connections.Count; i++)
            {
                var c = layout.Connections[i];
                graph.Edges.Add(new HouseLayoutEdge
                {
                    NodeAId = c.RoomAId,
                    NodeBId = c.RoomBId,
                    DirectionFromA = c.DirectionFromA
                });
            }

            return graph;
        }

        /// <summary>
        /// Convenience for tooling that only wants a graph for a seed. Uses the default map
        /// and the fallback content set; gameplay generation goes through
        /// ProceduralHouseGenerator so it uses the project's real authored content.
        /// </summary>
        public static HouseLayoutGraph Build(int seed)
        {
            var layout = HouseLayoutBuilder.Generate(
                seed, MapDefinition.HouseDefault, ContentSnapshot.CreateFallback(), out _);
            return FromLayout(layout);
        }

        public HouseLayoutNode GetNode(int id)
        {
            for (int i = 0; i < Nodes.Count; i++)
            {
                if (Nodes[i].Id == id)
                    return Nodes[i];
            }

            return null;
        }

        public HouseLayoutNode GetNodeAt(GridCell cell)
        {
            for (int i = 0; i < Nodes.Count; i++)
            {
                if (Nodes[i].Cell.Equals(cell))
                    return Nodes[i];
            }

            return null;
        }

        public IEnumerable<HouseLayoutNode> GetNeighbors(int nodeId)
        {
            for (int i = 0; i < Edges.Count; i++)
            {
                var edge = Edges[i];
                if (edge.NodeAId == nodeId)
                    yield return GetNode(edge.NodeBId);
                else if (edge.NodeBId == nodeId)
                    yield return GetNode(edge.NodeAId);
            }
        }

        public bool HasEdge(int nodeA, int nodeB)
        {
            for (int i = 0; i < Edges.Count; i++)
            {
                var edge = Edges[i];
                if ((edge.NodeAId == nodeA && edge.NodeBId == nodeB) ||
                    (edge.NodeAId == nodeB && edge.NodeBId == nodeA))
                    return true;
            }

            return false;
        }
    }
}
