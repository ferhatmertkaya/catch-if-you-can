using System;
using System.Collections.Generic;
using System.IO;
using CatchIfYouCan.Content;
using CatchIfYouCan.Procedural.Deterministic;
using UnityEngine;

namespace CatchIfYouCan.Procedural
{
    public static class PropDefinitionFactory
    {
        public struct PropBlueprint
        {
            public string PropName;
            public string ModelFileName;
            public string[] RoomTags;
            public Vector3 BoundsSize;
            public float Weight;
            public bool IsArchitecture;
        }

        public static PropBlueprint[] CreateAllBlueprints()
        {
            var list = new List<PropBlueprint>();
            AppendFromFolder(list, ExternalAssetPaths.KenneyFurnitureModels, "Kenney");
            AppendFromFolder(list, ExternalAssetPaths.KenneyDungeonModels, "Dungeon");
            return list.ToArray();
        }

        [Obsolete("Use CreateAllBlueprints")]
        public static PropBlueprint[] CreateDefaultBlueprints() => CreateAllBlueprints();

        private static void AppendFromFolder(List<PropBlueprint> list, string folder, string sourcePrefix)
        {
            if (!Directory.Exists(folder))
                return;

            var files = Directory.GetFiles(folder, "*.fbx", SearchOption.TopDirectoryOnly);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < files.Length; i++)
            {
                string fileName = Path.GetFileNameWithoutExtension(files[i]);
                if (ShouldSkipModel(fileName))
                    continue;

                list.Add(new PropBlueprint
                {
                    PropName = BuildDisplayName(fileName, sourcePrefix),
                    ModelFileName = fileName,
                    RoomTags = InferRoomTags(fileName),
                    BoundsSize = InferBounds(fileName),
                    Weight = InferWeight(fileName),
                    IsArchitecture = IsArchitecturePiece(fileName)
                });
            }
        }

        public static PropDefinition CreateDefinition(PropBlueprint blueprint, GameObject prefab)
        {
            var def = ScriptableObject.CreateInstance<PropDefinition>();
            def.StableId = BuildStableId(blueprint);
            def.PropName = blueprint.PropName;
            def.Kind = ClassifyKind(blueprint.BoundsSize);
            def.Prefab = prefab;
            def.CategoryTags = blueprint.RoomTags;
            def.BoundsSize = blueprint.BoundsSize;
            def.Weight = blueprint.Weight;
            return def;
        }

        /// <summary>
        /// Content identity, derived from the source MODEL file rather than the display
        /// name. Display names are regenerated from filenames and are not guaranteed unique;
        /// a stable id has to survive a rename without silently changing which prop a stored
        /// seed produces.
        /// </summary>
        private static string BuildStableId(PropBlueprint blueprint)
        {
            string source = !string.IsNullOrEmpty(blueprint.ModelFileName)
                ? blueprint.ModelFileName
                : blueprint.PropName;
            return "PROP_" + source;
        }

        /// <summary>
        /// Splits authored props into furniture and small props by footprint.
        ///
        /// Generation places the two from separate RNG streams into separate sockets, so
        /// without this every authored prop would land in the small-prop pass and no
        /// furniture would ever spawn. The threshold is a footprint a person would have to
        /// walk around rather than step over.
        /// </summary>
        private static PropKind ClassifyKind(Vector3 boundsSize)
        {
            const float FurnitureFootprintMetres = 0.9f;
            return boundsSize.x >= FurnitureFootprintMetres || boundsSize.z >= FurnitureFootprintMetres
                ? PropKind.Furniture
                : PropKind.Prop;
        }

        private static bool ShouldSkipModel(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return true;

            string lower = fileName.ToLowerInvariant();
            if (lower.StartsWith("character-"))
                return true;
            if (lower == "door")
                return true;

            return IsArchitecturePiece(fileName) && !lower.Contains("doorway");
        }

        private static bool IsArchitecturePiece(string fileName)
        {
            string lower = fileName.ToLowerInvariant();
            return lower.StartsWith("wall") ||
                   lower.StartsWith("floor") ||
                   lower == "paneling" ||
                   lower == "doorway" ||
                   lower == "doorwayfront" ||
                   lower == "doorwayopen" ||
                   lower == "dirt";
        }

        private static string BuildDisplayName(string fileName, string sourcePrefix)
        {
            if (fileName.StartsWith("kitchen", StringComparison.OrdinalIgnoreCase))
                return "Kitchen" + ToTitle(fileName.Substring(7));
            if (fileName.StartsWith("bathroom", StringComparison.OrdinalIgnoreCase))
                return "Bathroom" + ToTitle(fileName.Substring(8));
            if (fileName.StartsWith("lounge", StringComparison.OrdinalIgnoreCase))
                return "Lounge" + ToTitle(fileName.Substring(6));
            if (fileName.StartsWith("table", StringComparison.OrdinalIgnoreCase))
                return "Table" + ToTitle(fileName.Substring(5));
            if (fileName.StartsWith("bed", StringComparison.OrdinalIgnoreCase))
                return "Bed" + ToTitle(fileName.Substring(3));
            if (fileName.StartsWith("bookcase", StringComparison.OrdinalIgnoreCase))
                return "Bookcase" + ToTitle(fileName.Substring(8));
            if (fileName.StartsWith("lamp", StringComparison.OrdinalIgnoreCase))
                return "Lamp" + ToTitle(fileName.Substring(4));

            return ToTitle(fileName);
        }

        private static string ToTitle(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return char.ToUpperInvariant(value[0]) + value.Substring(1);
        }

        private static string[] InferRoomTags(string fileName)
        {
            string lower = fileName.ToLowerInvariant();

            if (lower.Contains("kitchen")) return Tags("Kitchen", "DiningRoom");
            if (lower.Contains("bath") || lower == "shower" || lower == "toilet") return Tags("Bathroom");
            if (lower.Contains("bed") || lower.Contains("cabinetbed")) return Tags("Bedroom", "KidsRoom");
            if (lower.Contains("lounge") || lower.Contains("sofa") || lower == "televisionmodern" || lower == "televisionvintage" || lower == "televisionantenna")
                return Tags("LivingRoom");
            if (lower.Contains("desk") || lower.Contains("bookcase") || lower.Contains("laptop") || lower.Contains("computer"))
                return Tags("Office", "KidsRoom");
            if (lower.Contains("washer") || lower.Contains("dryer")) return Tags("Laundry", "UtilityRoom");
            if (lower.Contains("rug") || lower.Contains("plant") || lower.Contains("bear")) return Tags("LivingRoom", "Bedroom", "Entrance");
            if (lower.Contains("chair") || lower.Contains("stool") || lower.Contains("bench")) return Tags("DiningRoom", "Kitchen", "Office", "LivingRoom");
            if (lower.Contains("table")) return Tags("DiningRoom", "Kitchen", "LivingRoom", "Office");
            if (lower.Contains("barrel") || lower.Contains("chest") || lower.Contains("trap") || lower.Contains("banner"))
                return Tags("Basement", "Garage", "Attic", "Storage");
            if (lower.Contains("column") || lower.Contains("rocks") || lower.Contains("stones") || lower.Contains("wood"))
                return Tags("Basement", "Garage", "Attic", "UtilityRoom");
            if (lower.Contains("coin")) return Tags("KidsRoom", "Office", "Storage");
            if (lower.Contains("cardboard") || lower.Contains("trash")) return Tags("Storage", "Garage", "Attic", "UtilityRoom");
            if (lower.Contains("coat")) return Tags("Entrance", "Hallway", "Bedroom");
            if (lower.Contains("speaker") || lower.Contains("radio")) return Tags("LivingRoom", "Bedroom", "Office");

            return Tags("Any");
        }

        private static Vector3 InferBounds(string fileName)
        {
            string lower = fileName.ToLowerInvariant();

            if (lower.Contains("beddouble") || lower.Contains("bedbunk")) return new Vector3(2f, 0.55f, 2.8f);
            if (lower.Contains("bedsingle")) return new Vector3(1.2f, 0.5f, 2.2f);
            if (lower.Contains("loungesofalong") || lower.Contains("sofa")) return new Vector3(2f, 0.85f, 0.9f);
            if (lower.Contains("loungesofacorner")) return new Vector3(1.6f, 0.85f, 1.6f);
            if (lower.Contains("bookcase")) return new Vector3(1f, 1.8f, 0.45f);
            if (lower.Contains("desk")) return new Vector3(1.2f, 0.75f, 0.7f);
            if (lower.Contains("table")) return new Vector3(1.1f, 0.75f, 0.8f);
            if (lower.Contains("chair") || lower.Contains("stool")) return new Vector3(0.5f, 0.9f, 0.5f);
            if (lower.Contains("fridge")) return new Vector3(0.8f, 1.8f, 0.7f);
            if (lower.Contains("bathtub")) return new Vector3(1.7f, 0.6f, 0.8f);
            if (lower.Contains("toilet")) return new Vector3(0.45f, 0.8f, 0.65f);
            if (lower.Contains("washer") || lower.Contains("dryer")) return new Vector3(0.7f, 0.9f, 0.7f);
            if (lower.Contains("television")) return new Vector3(1.1f, 0.7f, 0.15f);
            if (lower.Contains("plant") || lower.Contains("bear")) return new Vector3(0.45f, 0.9f, 0.45f);
            if (lower.Contains("rug")) return new Vector3(1.8f, 0.05f, 1.2f);
            if (lower.Contains("barrel")) return new Vector3(0.6f, 0.8f, 0.6f);
            if (lower.Contains("chest")) return new Vector3(0.8f, 0.55f, 0.55f);
            if (lower.Contains("column")) return new Vector3(0.5f, 2.4f, 0.5f);
            if (lower.Contains("banner")) return new Vector3(0.2f, 2f, 0.2f);
            if (lower.Contains("coin")) return new Vector3(0.15f, 0.05f, 0.15f);

            return new Vector3(0.8f, 0.8f, 0.8f);
        }

        private static float InferWeight(string fileName)
        {
            string lower = fileName.ToLowerInvariant();
            if (lower.Contains("bed") || lower.Contains("sofa") || lower.Contains("fridge") || lower.Contains("desk"))
                return 1.1f;
            if (lower.Contains("rug") || lower.Contains("coin") || lower.Contains("pillow"))
                return 0.35f;
            if (lower.Contains("cardboard") || lower.Contains("books"))
                return 0.45f;
            return 0.75f;
        }

        private static string[] Tags(params string[] values) => values;
    }
}
