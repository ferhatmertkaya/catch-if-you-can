#if UNITY_EDITOR
using CatchIfYouCan.Content;
using UnityEditor;
using UnityEngine;

namespace CatchIfYouCan.EditorTools
{
    public class ExternalAssetModelPostprocessor : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith("Assets/External/"))
                return;

            var importer = (ModelImporter)assetImporter;
            bool humanoid = assetPath.StartsWith(ExternalAssetPaths.QuaterniusMonsters) ||
                            assetPath.Contains("character-");

            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.meshCompression = ModelImporterMeshCompression.Medium;
            importer.isReadable = false;
            importer.importAnimation = humanoid;
            importer.animationType = humanoid ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.None;
            importer.importBlendShapes = false;
            importer.optimizeMeshPolygons = true;
            importer.optimizeMeshVertices = true;
        }
    }
}
#endif
