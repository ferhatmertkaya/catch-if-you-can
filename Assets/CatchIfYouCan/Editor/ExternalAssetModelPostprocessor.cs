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

        /// <summary>
        /// Makes the character's walk cycle loop, which is the whole of the T-pose bug.
        ///
        /// <para>
        /// <b>Nathan's importer carries no clip definitions at all.</b> Its meta reads
        /// <c>clipAnimations: []</c>, so Unity imports the take with defaults, and the default
        /// for <c>loopTime</c> is FALSE. The clip is 2.267 seconds long ("Take 001"); stretched
        /// by the animator's speed match that is the three to four seconds after which the walk
        /// reaches its end, stops, shows the bind pose for a frame and is restarted from zero by
        /// <c>PlayerVisualAnimator.KeepWalkCycleRunning</c> - which says in its own log that it
        /// is a stopgap and that the real fix is Loop Time on the clip.
        /// </para>
        ///
        /// <para>
        /// This is that fix, applied where import settings belong rather than by hand-writing a
        /// clipAnimations block into the meta - no other model in this project has one, so there
        /// is no known-good shape to copy and a malformed one would break the import outright.
        /// <c>defaultClipAnimations</c> is what Unity read from the file; assigning it back
        /// through <c>clipAnimations</c> makes those takes explicit, with looping on.
        /// </para>
        ///
        /// <para>
        /// <c>loopPose</c> stays off deliberately. It shifts the pose to make the ends meet,
        /// which on a walk that already loops cleanly is a change to the gait rather than a fix
        /// for it.
        /// </para>
        /// </summary>
        private void OnPreprocessAnimation()
        {
            if (assetPath.IndexOf("/Characters/", System.StringComparison.OrdinalIgnoreCase) < 0)
                return;

            var importer = (ModelImporter)assetImporter;

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
                clips = importer.defaultClipAnimations;

            if (clips == null || clips.Length == 0)
                return;

            bool changed = false;
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i].loopTime)
                    continue;

                clips[i].loopTime = true;
                changed = true;
            }

            if (!changed)
                return;

            importer.clipAnimations = clips;
            Debug.Log("[CIYC] " + assetPath + ": set Loop Time on " + clips.Length +
                      " clip(s). Without it the walk stops at the end of its cycle and the " +
                      "character drops into the bind pose for a frame before it restarts.");
        }
    }
}
#endif
