using System.Collections.Generic;
using System.Text;
using CatchIfYouCan.Procedural;
using UnityEditor;
using UnityEngine;

namespace CatchIfYouCan.EditorTools
{
    public static class HouseGeneratorTestTool
    {
        [MenuItem("Catch If You Can/Debug and Legacy/100 Haeuser generieren (Test) [AENDERT SZENE]", false, 1204)]
        public static void Generate100Houses()
        {
            var report = new StringBuilder();
            report.AppendLine("=== House Generator Test (seeds 0-99) ===");

            int pass = 0;
            int fail = 0;
            var generatorGo = new GameObject("__CIYC_HouseTestGenerator");
            var generator = generatorGo.AddComponent<ProceduralHouseGenerator>();

            try
            {
                for (int seed = 0; seed < 100; seed++)
                {
                    var graphErrors = ValidateGraph(seed);
                    GeneratedHouse house = null;
                    HouseValidationResult validation = null;

                    try
                    {
                        house = generator.Generate(seed);
                        validation = HouseValidator.Validate(house);
                    }
                    catch (System.Exception ex)
                    {
                        graphErrors.Add($"Exception: {ex.Message}");
                    }

                    int roomCount = house?.Rooms?.Count ?? 0;
                    bool graphOk = graphErrors.Count == 0;
                    bool houseOk = validation != null && validation.IsValid;
                    bool ok = graphOk && houseOk;

                    if (ok)
                        pass++;
                    else
                        fail++;

                    report.Append($"Seed {seed,3} | Rooms {roomCount,2} | ");
                    report.AppendLine(ok ? "PASS" : "FAIL");

                    if (!graphOk)
                    {
                        for (int i = 0; i < graphErrors.Count; i++)
                            report.AppendLine("  Graph: " + graphErrors[i]);
                    }

                    if (validation != null && !validation.IsValid)
                    {
                        for (int i = 0; i < validation.Errors.Count; i++)
                            report.AppendLine("  House: " + validation.Errors[i]);
                    }

                    if (house?.Root != null)
                    {
                        for (int c = house.Root.childCount - 1; c >= 0; c--)
                            Object.DestroyImmediate(house.Root.GetChild(c).gameObject);
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(generatorGo);
            }

            report.AppendLine($"Summary: {pass} passed, {fail} failed out of 100.");
            Debug.Log(report.ToString());
            EditorUtility.DisplayDialog("House Generator Test",
                $"Complete.\nPassed: {pass}\nFailed: {fail}\nSee Console for details.", "OK");
        }

        private static List<string> ValidateGraph(int seed)
        {
            var errors = new List<string>();
            var graph = HouseLayoutGraph.Build(seed);

            if (graph.Nodes.Count < HouseLayoutGraph.MinRooms)
                errors.Add($"Too few rooms ({graph.Nodes.Count}).");
            if (graph.Nodes.Count > HouseLayoutGraph.MaxRooms)
                errors.Add($"Too many rooms ({graph.Nodes.Count}).");

            var cells = new HashSet<CatchIfYouCan.Procedural.Deterministic.GridCell>();
            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                var cell = graph.Nodes[i].Cell;
                if (!cells.Add(cell))
                    errors.Add($"Grid overlap at {cell} (node {graph.Nodes[i].Id}).");
            }

            for (int i = 0; i < graph.Edges.Count; i++)
            {
                var edge = graph.Edges[i];
                if (graph.GetNode(edge.NodeAId) == null || graph.GetNode(edge.NodeBId) == null)
                    errors.Add($"Edge {i} references missing node.");
            }

            return errors;
        }
    }
}
