using System;
using System.Collections.Generic;
using System.Linq;
using Random = System.Random;

namespace CatchIfYouCan.Procedural
{
    [Serializable]
    public class HouseLayoutNode
    {
        public int Id;
        public RoomCategory Category;
        public Vector2Int GridCell;
        public List<SocketDirection> OpenDirections = new List<SocketDirection>();
    }

    [Serializable]
    public class HouseLayoutEdge
    {
        public int NodeAId;
        public int NodeBId;
        public SocketDirection DirectionFromA;
    }

    public class HouseLayoutGraph
    {
        public const int MinRooms = 6;
        public const int MaxRooms = 14;

        private static readonly RoomCategory[] OptionalSpecialRooms =
        {
            RoomCategory.Basement,
            RoomCategory.Garage,
            RoomCategory.Attic
        };

        private static readonly RoomCategory[] FillerCategories =
        {
            RoomCategory.Hallway,
            RoomCategory.Kitchen,
            RoomCategory.DiningRoom,
            RoomCategory.Storage,
            RoomCategory.Laundry,
            RoomCategory.Office,
            RoomCategory.KidsRoom,
            RoomCategory.UtilityRoom
        };

        public List<HouseLayoutNode> Nodes { get; } = new List<HouseLayoutNode>();
        public List<HouseLayoutEdge> Edges { get; } = new List<HouseLayoutEdge>();

        public static HouseLayoutGraph Build(int seed, int minRooms = MinRooms, int maxRooms = MaxRooms)
        {
            var rng = SeedManager.CreateRandom(seed);
            var graph = new HouseLayoutGraph();
            minRooms = Math.Max(MinRooms, minRooms);
            maxRooms = Math.Min(MaxRooms, maxRooms);
            int targetRooms = rng.Next(minRooms, maxRooms + 1);

            var entrance = graph.AddNode(RoomCategory.Entrance, Vector2Int.zero);
            var required = new Queue<RoomCategory>(new[]
            {
                RoomCategory.LivingRoom,
                RoomCategory.Bedroom,
                RoomCategory.Bathroom
            });

            int safety = 0;
            while (graph.Nodes.Count < targetRooms && safety++ < 64)
            {
                if (!graph.TryExpand(rng, required.Count > 0 ? required.Dequeue() : PickFiller(rng)))
                    break;
            }

            while (required.Count > 0)
            {
                var requiredCategory = required.Dequeue();
                if (!graph.TryExpand(rng, requiredCategory))
                    graph.ForceRequiredRoom(requiredCategory);
            }

            int specialCount = rng.Next(0, 3);
            for (int i = 0; i < specialCount && graph.Nodes.Count < maxRooms; i++)
            {
                var special = OptionalSpecialRooms[rng.Next(0, OptionalSpecialRooms.Length)];
                if (graph.Nodes.Any(n => n.Category == special))
                    continue;

                graph.TryExpand(rng, special);
            }

            while (graph.Nodes.Count < minRooms)
            {
                if (!graph.TryExpand(rng, PickFiller(rng)))
                    graph.ForceRequiredRoom(RoomCategory.Hallway);
            }

            graph.RefreshOpenDirections();
            return graph;
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

        public HouseLayoutNode GetNodeAt(Vector2Int cell)
        {
            for (int i = 0; i < Nodes.Count; i++)
            {
                if (Nodes[i].GridCell == cell)
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

        private HouseLayoutNode AddNode(RoomCategory category, Vector2Int cell)
        {
            var node = new HouseLayoutNode
            {
                Id = Nodes.Count,
                Category = category,
                GridCell = cell
            };
            Nodes.Add(node);
            return node;
        }

        private bool TryExpand(Random rng, RoomCategory category)
        {
            var candidates = BuildExpansionCandidates(rng);
            if (candidates.Count == 0)
                return false;

            var pick = candidates[rng.Next(0, candidates.Count)];
            var parent = pick.Parent;
            var direction = pick.Direction;
            var offset = RoomSocket.DirectionToGridOffset(direction);
            var newCell = parent.GridCell + offset;

            if (GetNodeAt(newCell) != null)
                return false;

            var child = AddNode(category, newCell);
            Edges.Add(new HouseLayoutEdge
            {
                NodeAId = parent.Id,
                NodeBId = child.Id,
                DirectionFromA = direction
            });
            return true;
        }

        private void ForceRequiredRoom(RoomCategory category)
        {
            if (Nodes.Any(n => n.Category == category))
                return;

            var parent = Nodes[0];
            for (int i = 0; i < Nodes.Count; i++)
            {
                if (Nodes[i].Category == RoomCategory.Hallway || Nodes[i].Category == RoomCategory.LivingRoom)
                {
                    parent = Nodes[i];
                    break;
                }
            }

            for (int step = 1; step <= 8; step++)
            {
                var directions = new[]
                {
                    SocketDirection.North,
                    SocketDirection.East,
                    SocketDirection.South,
                    SocketDirection.West
                };

                for (int d = 0; d < directions.Length; d++)
                {
                    var cell = parent.GridCell + RoomSocket.DirectionToGridOffset(directions[d]) * step;
                    if (GetNodeAt(cell) != null)
                        continue;

                    var child = AddNode(category, cell);
                    var bridge = parent;
                    var previousCell = parent.GridCell;
                    for (int s = step - 1; s >= 1; s--)
                    {
                        var hallwayCell = parent.GridCell + RoomSocket.DirectionToGridOffset(directions[d]) * s;
                        if (GetNodeAt(hallwayCell) == null)
                        {
                            var hallway = AddNode(RoomCategory.Hallway, hallwayCell);
                            Edges.Add(new HouseLayoutEdge
                            {
                                NodeAId = bridge.Id,
                                NodeBId = hallway.Id,
                                DirectionFromA = DirectionBetween(bridge.GridCell, hallwayCell)
                            });
                            bridge = hallway;
                        }
                        else
                        {
                            bridge = GetNodeAt(hallwayCell);
                        }

                        previousCell = hallwayCell;
                    }

                    Edges.Add(new HouseLayoutEdge
                    {
                        NodeAId = bridge.Id,
                        NodeBId = child.Id,
                        DirectionFromA = DirectionBetween(bridge.GridCell, child.GridCell)
                    });
                    return;
                }
            }
        }

        private List<ExpansionCandidate> BuildExpansionCandidates(Random rng)
        {
            var list = new List<ExpansionCandidate>();
            var shuffled = Nodes.OrderBy(_ => rng.Next()).ToList();

            for (int n = 0; n < shuffled.Count; n++)
            {
                var node = shuffled[n];
                var directions = new[]
                {
                    SocketDirection.North,
                    SocketDirection.East,
                    SocketDirection.South,
                    SocketDirection.West
                };

                for (int i = 0; i < directions.Length; i++)
                {
                    var dir = directions[i];
                    if (IsDirectionUsed(node.Id, dir))
                        continue;

                    var cell = node.GridCell + RoomSocket.DirectionToGridOffset(dir);
                    if (GetNodeAt(cell) != null)
                        continue;

                    list.Add(new ExpansionCandidate { Parent = node, Direction = dir });
                }
            }

            return list;
        }

        private bool IsDirectionUsed(int nodeId, SocketDirection direction)
        {
            for (int i = 0; i < Edges.Count; i++)
            {
                var edge = Edges[i];
                if (edge.NodeAId == nodeId && edge.DirectionFromA == direction)
                    return true;

                if (edge.NodeBId == nodeId && RoomSocket.Opposite(edge.DirectionFromA) == direction)
                    return true;
            }

            return false;
        }

        private void RefreshOpenDirections()
        {
            for (int i = 0; i < Nodes.Count; i++)
                Nodes[i].OpenDirections.Clear();

            for (int i = 0; i < Nodes.Count; i++)
            {
                var node = Nodes[i];
                var dirs = new[]
                {
                    SocketDirection.North,
                    SocketDirection.East,
                    SocketDirection.South,
                    SocketDirection.West
                };

                for (int d = 0; d < dirs.Length; d++)
                {
                    if (!IsDirectionUsed(node.Id, dirs[d]))
                        node.OpenDirections.Add(dirs[d]);
                }
            }
        }

        private static SocketDirection DirectionBetween(Vector2Int from, Vector2Int to)
        {
            var delta = to - from;
            if (delta.y > 0) return SocketDirection.North;
            if (delta.y < 0) return SocketDirection.South;
            if (delta.x > 0) return SocketDirection.East;
            return SocketDirection.West;
        }

        private static RoomCategory PickFiller(Random rng)
        {
            return FillerCategories[rng.Next(0, FillerCategories.Length)];
        }

        private struct ExpansionCandidate
        {
            public HouseLayoutNode Parent;
            public SocketDirection Direction;
        }
    }
}
