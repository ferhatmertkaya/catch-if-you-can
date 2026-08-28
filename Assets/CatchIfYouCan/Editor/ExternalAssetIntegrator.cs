using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CatchIfYouCan.Content;
using CatchIfYouCan.Ghost;
using CatchIfYouCan.Interaction;
using CatchIfYouCan.Procedural;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

namespace CatchIfYouCan.EditorTools
{
    public static class ExternalAssetIntegrator
    {
        private const string GhostMaterialPath = "Assets/CatchIfYouCan/Materials/Ghost_RiggedDissolve.mat";
        private const string MonsterPrefabsRoot = "Assets/CatchIfYouCan/Prefabs/Ghost/AllMonsters";

        [MenuItem("Catch If You Can/Integrate External Assets")]
        public static void IntegrateExternalAssets()
        {
            string report = RunIntegration();
            EditorUtility.DisplayDialog("Catch If You Can — Asset Integration", report, "OK");
        }

        public static string RunIntegration()
        {
            var report = new StringBuilder();
            report.AppendLine("=== External Asset Integration (FULL) ===");

            ExternalAssetDownloader.EnsureBundledAssetsPresent();

            if (!Directory.Exists(ExternalAssetPaths.KenneyFurnitureModels))
            {
                report.AppendLine("ERROR: Kenney furniture models missing.");
                return report.ToString();
            }

            EnsureFolders();
            ConfigureImportSettings(report);
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            var propPrefabs = BuildAllPropPrefabs(report);
            var propDefinitions = BuildPropDefinitions(propPrefabs, report);
            var roomDefinitions = KenneyRoomPrefabBuilder.BuildAllRoomDefinitions(report);
            var doorPrefab = BuildDoorPrefab(report);
            var ghostPrefabs = BuildGhostPrefabs(report);
            BuildAllMonsterShowcasePrefabs(report);
            WireGhostDefinitions(ghostPrefabs, report);
            BuildContentCatalog(propDefinitions, roomDefinitions, doorPrefab, report);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            report.AppendLine("Full integration complete.");
            Debug.Log(report.ToString());
            return report.ToString();
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/CatchIfYouCan/Resources/CatchIfYouCan/Ghosts");
            EnsureFolder(ExternalAssetPaths.PropPrefabsRoot);
            EnsureFolder(ExternalAssetPaths.GhostPrefabsRoot);
            EnsureFolder(MonsterPrefabsRoot);
            EnsureFolder(ExternalAssetPaths.PropDefinitionsRoot);
            EnsureFolder("Assets/CatchIfYouCan/ScriptableObjects/Content");
            EnsureFolder("Assets/CatchIfYouCan/ScriptableObjects/Rooms");
            EnsureFolder("Assets/CatchIfYouCan/Prefabs/Rooms/Kenney");
            EnsureFolder("Assets/CatchIfYouCan/Materials");
        }

        private static void ConfigureImportSettings(StringBuilder report)
        {
            int configured = 0;
            configured += ConfigureModelsInFolder(ExternalAssetPaths.KenneyFurnitureModels, false);
            configured += ConfigureModelsInFolder(ExternalAssetPaths.KenneyDungeonModels, IsAnimatedModel);
            configured += ConfigureModelsInFolder(ExternalAssetPaths.QuaterniusMonsters, true);
            report.AppendLine($"Import settings configured: {configured} models.");
        }

        private static bool IsAnimatedModel(string assetPath)
        {
            string name = Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();
            return name.StartsWith("character-");
        }

        private static int ConfigureModelsInFolder(string folder, System.Func<string, bool> animatedPredicate)
        {
            if (!AssetDatabase.IsValidFolder(folder))
                return 0;

            int count = 0;
            var guids = AssetDatabase.FindAssets("t:Model", new[] { folder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null)
                    continue;

                bool humanoid = animatedPredicate(path);
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
                importer.meshCompression = ModelImporterMeshCompression.Medium;
                importer.isReadable = false;
                importer.importAnimation = humanoid;
                importer.animationType = humanoid ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.None;
                importer.importBlendShapes = false;
                importer.optimizeMeshPolygons = true;
                importer.optimizeMeshVertices = true;
                importer.SaveAndReimport();
                count++;
            }

            return count;
        }

        private static int ConfigureModelsInFolder(string folder, bool humanoid)
        {
            return ConfigureModelsInFolder(folder, _ => humanoid);
        }

        private static Dictionary<string, GameObject> BuildAllPropPrefabs(StringBuilder report)
        {
            var map = new Dictionary<string, GameObject>();
            var blueprints = PropDefinitionFactory.CreateAllBlueprints();

            for (int i = 0; i < blueprints.Length; i++)
            {
                var bp = blueprints[i];
                if (bp.IsArchitecture)
                    continue;

                string modelPath = ResolveModelPath(bp.ModelFileName);
                if (modelPath == null)
                    continue;

                string prefabPath = $"{ExternalAssetPaths.PropPrefabsRoot}/{SanitizeFileName(bp.PropName)}_{bp.ModelFileName}.prefab";
                var prefab = BuildStaticPropPrefab(modelPath, prefabPath, bp.PropName, bp.BoundsSize);
                if (prefab != null)
                    map[bp.ModelFileName] = prefab;
            }

            report.AppendLine($"Prop prefabs built: {map.Count} / {blueprints.Length} blueprints.");
            return map;
        }

        private static string ResolveModelPath(string modelFileName)
        {
            string furniture = $"{ExternalAssetPaths.KenneyFurnitureModels}/{modelFileName}.fbx";
            if (File.Exists(furniture))
                return furniture;

            string dungeon = $"{ExternalAssetPaths.KenneyDungeonModels}/{modelFileName}.fbx";
            if (File.Exists(dungeon))
                return dungeon;

            return null;
        }

        private static GameObject BuildStaticPropPrefab(string modelPath, string prefabPath, string objectName, Vector3 targetBounds)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (source == null)
                return null;

            var root = new GameObject(objectName);
            var visual = Object.Instantiate(source, root.transform);
            visual.name = "Visual";
            NormalizeScale(visual.transform, targetBounds);

            if (root.GetComponentInChildren<Collider>() == null)
            {
                var box = root.AddComponent<BoxCollider>();
                box.size = targetBounds;
                box.center = new Vector3(0f, targetBounds.y * 0.5f, 0f);
            }

            var prefab = SavePrefab(root, prefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static PropDefinition[] BuildPropDefinitions(Dictionary<string, GameObject> prefabs, StringBuilder report)
        {
            var list = new List<PropDefinition>();
            var blueprints = PropDefinitionFactory.CreateAllBlueprints();

            for (int i = 0; i < blueprints.Length; i++)
            {
                var bp = blueprints[i];
                if (bp.IsArchitecture)
                    continue;

                if (!prefabs.TryGetValue(bp.ModelFileName, out var prefab) || prefab == null)
                    continue;

                string assetPath = $"{ExternalAssetPaths.PropDefinitionsRoot}/prop_{bp.ModelFileName}.asset";
                var existing = AssetDatabase.LoadAssetAtPath<PropDefinition>(assetPath);
                if (existing != null)
                {
                    existing.PropName = bp.PropName;
                    existing.Prefab = prefab;
                    existing.CategoryTags = bp.RoomTags;
                    existing.BoundsSize = bp.BoundsSize;
                    existing.Weight = bp.Weight;
                    EditorUtility.SetDirty(existing);
                    list.Add(existing);
                    continue;
                }

                var def = PropDefinitionFactory.CreateDefinition(bp, prefab);
                AssetDatabase.CreateAsset(def, assetPath);
                list.Add(def);
            }

            report.AppendLine($"Prop definitions: {list.Count}.");
            return list.ToArray();
        }

        private static GameObject BuildDoorPrefab(StringBuilder report)
        {
            string modelPath = $"{ExternalAssetPaths.KenneyDungeonModels}/door.fbx";
            string prefabPath = "Assets/CatchIfYouCan/Prefabs/Interactables/Door_Kenney.prefab";

            if (!File.Exists(modelPath))
            {
                report.AppendLine("Door model missing — keeping existing door prefab if any.");
                return AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CatchIfYouCan/Prefabs/Door.prefab");
            }

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var root = new GameObject("Door_Kenney");
            var visual = Object.Instantiate(source, root.transform);
            visual.name = "Visual";
            NormalizeScale(visual.transform, new Vector3(1.2f, 2.2f, 0.2f));

            var hinge = new GameObject("Hinge").transform;
            hinge.SetParent(root.transform, false);
            hinge.localPosition = new Vector3(-0.55f, 0f, 0f);
            visual.transform.SetParent(hinge, true);

            var door = root.AddComponent<InteractiveDoor>();
            SetPrivateField(door, "hinge", hinge);

            var prefab = SavePrefab(root, prefabPath);
            Object.DestroyImmediate(root);
            report.AppendLine($"Door prefab: {prefabPath}");
            return prefab;
        }

        private static Dictionary<string, GameObject> BuildGhostPrefabs(StringBuilder report)
        {
            var map = new Dictionary<string, GameObject>();
            var ghosts = GhostDefinitionFactory.CreateAllDefaultGhosts();
            var ghostMaterial = EnsureGhostMaterial();

            for (int i = 0; i < ghosts.Length; i++)
            {
                var ghost = ghosts[i];
                string modelPath = ResolveGhostModelPath(ghost.Id, ghost.VisualProfile, report);
                if (modelPath == null)
                    continue;

                string prefabPath = $"{ExternalAssetPaths.GhostPrefabsRoot}/Ghost_{ghost.Id}.prefab";
                var prefab = BuildGhostPrefab(ghost, modelPath, prefabPath, ghostMaterial);
                if (prefab == null)
                    continue;

                map[ghost.Id] = prefab;
                SavePrefabCopy(prefab, $"Assets/CatchIfYouCan/Resources/CatchIfYouCan/Ghosts/{ghost.Id}.prefab");
            }

            BuildProfileGhostPrefabs(ghostMaterial, report);
            report.AppendLine($"Gameplay ghost prefabs: {map.Count}.");
            return map;
        }

        private static void BuildAllMonsterShowcasePrefabs(StringBuilder report)
        {
            int built = 0;
            var ghostMaterial = EnsureGhostMaterial();
            var modelPaths = CollectAllMonsterModelPaths();

            for (int i = 0; i < modelPaths.Count; i++)
            {
                string path = modelPaths[i];
                string id = Path.GetFileNameWithoutExtension(path);
                var tempDef = ScriptableObject.CreateInstance<GhostDefinition>();
                tempDef.Id = $"monster_{id}";
                tempDef.DisplayName = id;
                tempDef.VisualProfile = InferProfileFromName(id);

                string prefabPath = $"{MonsterPrefabsRoot}/Monster_{SanitizeFileName(id)}.prefab";
                if (BuildGhostPrefab(tempDef, path, prefabPath, ghostMaterial) != null)
                    built++;
            }

            report.AppendLine($"Monster showcase prefabs: {built}.");
        }

        private static List<string> CollectAllMonsterModelPaths()
        {
            var paths = new List<string>();
            AppendModels(paths, ExternalAssetPaths.QuaterniusMonsters, "*.gltf", "*.glb");
            AppendModels(paths, ExternalAssetPaths.KenneyDungeonModels, "character-*.fbx");
            return paths.Distinct().ToList();
        }

        private static void AppendModels(List<string> paths, string folder, params string[] patterns)
        {
            if (!Directory.Exists(folder))
                return;

            for (int p = 0; p < patterns.Length; p++)
            {
                var files = Directory.GetFiles(folder, patterns[p], SearchOption.TopDirectoryOnly);
                for (int i = 0; i < files.Length; i++)
                    paths.Add(files[i].Replace('\\', '/'));
            }
        }

        private static GhostVisualProfile InferProfileFromName(string id)
        {
            string lower = id.ToLowerInvariant();
            if (lower.Contains("creep") || lower.Contains("blob"))
                return GhostVisualProfile.CrawlingEntity;
            if (lower.Contains("demon") || lower.Contains("shade"))
                return GhostVisualProfile.TallShadow;
            if (lower.Contains("human"))
                return GhostVisualProfile.DistortedWoman;
            if (lower.Contains("orc"))
                return GhostVisualProfile.FacelessFigure;
            return GhostVisualProfile.HumanSilhouette;
        }

        private static string ResolveGhostModelPath(string ghostId, GhostVisualProfile profile, StringBuilder report)
        {
            string modelPath = GhostVisualCatalog.GetModelAssetPath(ghostId);
            if (!File.Exists(modelPath))
            {
                report.AppendLine($"Fallback model for {ghostId}: {modelPath} missing.");
                modelPath = GhostVisualCatalog.GetModelAssetPathForProfile(profile);
            }

            return File.Exists(modelPath) ? modelPath : null;
        }

        private static void BuildProfileGhostPrefabs(Material ghostMaterial, StringBuilder report)
        {
            var profiles = System.Enum.GetValues(typeof(GhostVisualProfile)).Cast<GhostVisualProfile>().Distinct();
            int created = 0;

            foreach (var profile in profiles)
            {
                string modelPath = GhostVisualCatalog.GetModelAssetPathForProfile(profile);
                if (!File.Exists(modelPath))
                    continue;

                string prefabPath = $"{ExternalAssetPaths.GhostPrefabsRoot}/GhostProfile_{profile}.prefab";
                var tempDef = ScriptableObject.CreateInstance<GhostDefinition>();
                tempDef.Id = $"profile_{profile}";
                tempDef.DisplayName = profile.ToString();
                tempDef.VisualProfile = profile;

                var prefab = BuildGhostPrefab(tempDef, modelPath, prefabPath, ghostMaterial, 1f, 0f);
                if (prefab == null)
                    continue;

                SavePrefabCopy(prefab, $"Assets/CatchIfYouCan/Resources/CatchIfYouCan/Ghosts/profile_{profile}.prefab");
                created++;
            }

            report.AppendLine($"Ghost profile prefabs: {created}.");
        }

        private static GameObject BuildGhostPrefab(
            GhostDefinition ghost,
            string modelPath,
            string prefabPath,
            Material ghostMaterial,
            float scaleOverride = -1f,
            float yOffsetOverride = float.MinValue)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (source == null)
                return null;

            float scale = scaleOverride > 0f ? scaleOverride : GhostVisualCatalog.GetScaleMultiplier(ghost.Id);
            float yOffset = yOffsetOverride != float.MinValue ? yOffsetOverride : GhostVisualCatalog.GetVerticalOffset(ghost.Id);

            var root = new GameObject($"Ghost_{ghost.DisplayName}");
            root.tag = "Ghost";

            var agent = root.AddComponent<NavMeshAgent>();
            agent.height = ghost.Id == "the_crawler" || ghost.Id.Contains("creep") ? 1.1f : 1.8f;
            agent.radius = ghost.Id == "the_crawler" || ghost.Id.Contains("creep") ? 0.45f : 0.35f;
            agent.baseOffset = yOffset;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;

            var eye = new GameObject("EyePoint").transform;
            eye.SetParent(root.transform, false);
            eye.localPosition = new Vector3(0f, agent.height * 0.85f, 0f);

            var visualRoot = new GameObject("VisualRoot").transform;
            visualRoot.SetParent(root.transform, false);
            visualRoot.localPosition = new Vector3(0f, yOffset, 0f);
            visualRoot.localScale = Vector3.one * scale;

            var visual = Object.Instantiate(source, visualRoot);
            visual.name = "Model";
            ApplyGhostMaterial(visual, ghostMaterial);

            var animator = visual.GetComponentInChildren<Animator>();
            if (animator == null)
                animator = visual.AddComponent<Animator>();

            var controller = BuildGhostAnimatorController(modelPath, ghost.Id);
            if (controller != null)
                animator.runtimeAnimatorController = controller;

            var renderers = visual.GetComponentsInChildren<Renderer>(true);
            var controllerComponent = root.AddComponent<GhostController>();
            controllerComponent.SetManifestationRenderers(renderers);

            var rig = root.AddComponent<GhostRigController>();
            rig.BindAnimator(animator);

            var perception = root.AddComponent<GhostPerception>();
            SetPrivateField(perception, "eyePoint", eye);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void SavePrefabCopy(GameObject prefab, string path)
        {
            if (prefab == null)
                return;

            EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
                AssetDatabase.DeleteAsset(path);

            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(prefab), path);
        }

        private static AnimatorController BuildGhostAnimatorController(string modelPath, string ghostId)
        {
            string controllerDir = "Assets/CatchIfYouCan/Art/Animators/Ghosts";
            EnsureFolder(controllerDir);
            string safeId = SanitizeFileName(ghostId);
            string controllerPath = $"{controllerDir}/AC_{safeId}.controller";

            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (existing != null)
                return existing;

            var clips = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__")).ToList();
            if (clips.Count == 0)
                return null;

            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var rootStateMachine = controller.layers[0].stateMachine;

            AnimationClip PickClip(params string[] names)
            {
                for (int i = 0; i < names.Length; i++)
                {
                    var clip = clips.FirstOrDefault(c => c.name == names[i] || c.name.Contains(names[i]));
                    if (clip != null)
                        return clip;
                }

                return clips[0];
            }

            AddState(rootStateMachine, "Idle", PickClip("Idle", "Idle1_Action", "Idle2_Action", "Sleep_loop_Action"));
            AddState(rootStateMachine, "Walk", PickClip("Walk", "Walk1_Action", "Walk2_Action"));
            AddState(rootStateMachine, "Run", PickClip("Run", "Walk2_Action", "Walk1_Action"));
            AddState(rootStateMachine, "Manifest", PickClip("Roar", "Roar_Action", "Wave", "Punch", "Punch_Action"));
            AddState(rootStateMachine, "Attack", PickClip("Punch", "Punch_Action", "Weapon", "Bite_Action"));

            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            return controller;
        }

        private static void AddState(AnimatorStateMachine machine, string name, AnimationClip clip)
        {
            if (clip == null)
                return;

            var state = machine.AddState(name);
            state.motion = clip;
            if (machine.defaultState == null && name == "Idle")
                machine.defaultState = state;
        }

        private static Material EnsureGhostMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(GhostMaterialPath);
            if (existing != null)
                return existing;

            var shader = Shader.Find("CatchIfYouCan/GhostDissolve")
                         ?? Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard");

            var mat = new Material(shader);
            mat.name = "Ghost_RiggedDissolve";
            mat.SetColor("_BaseColor", new Color(0.1f, 0.9f, 0.3f, 0.85f));
            mat.SetColor("_EmissionColor", new Color(0.2f, 1f, 0.4f) * 3f);
            mat.EnableKeyword("_EMISSION");

            AssetDatabase.CreateAsset(mat, GhostMaterialPath);
            return mat;
        }

        private static void ApplyGhostMaterial(GameObject visualRoot, Material ghostMaterial)
        {
            var renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var mats = renderers[i].sharedMaterials;
                for (int m = 0; m < mats.Length; m++)
                    mats[m] = ghostMaterial;
                renderers[i].sharedMaterials = mats;
                renderers[i].enabled = false;
            }
        }

        private static void WireGhostDefinitions(Dictionary<string, GameObject> ghostPrefabs, StringBuilder report)
        {
            int wired = 0;
            foreach (var pair in ghostPrefabs)
            {
                string assetPath = $"Assets/CatchIfYouCan/ScriptableObjects/{pair.Key}.asset";
                var def = AssetDatabase.LoadAssetAtPath<GhostDefinition>(assetPath);
                if (def == null)
                {
                    var ghosts = GhostDefinitionFactory.CreateAllDefaultGhosts();
                    def = ghosts.FirstOrDefault(g => g.Id == pair.Key);
                    if (def == null)
                        continue;

                    AssetDatabase.CreateAsset(def, assetPath);
                }

                def.Prefab = pair.Value;
                EditorUtility.SetDirty(def);
                wired++;
            }

            report.AppendLine($"Ghost definitions wired: {wired}.");
        }

        private static void BuildContentCatalog(
            PropDefinition[] propDefinitions,
            RoomDefinition[] roomDefinitions,
            GameObject doorPrefab,
            StringBuilder report)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<InvestigationContentCatalog>(ExternalAssetPaths.ContentCatalogAsset);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<InvestigationContentCatalog>();
                AssetDatabase.CreateAsset(catalog, ExternalAssetPaths.ContentCatalogAsset);
            }

            catalog.PropDefinitions = propDefinitions;
            catalog.RoomDefinitions = roomDefinitions;
            catalog.DoorPrefab = doorPrefab;
            EditorUtility.SetDirty(catalog);

            EnsureFolder("Assets/CatchIfYouCan/Resources/CatchIfYouCan");
            if (AssetDatabase.LoadAssetAtPath<InvestigationContentCatalog>(ExternalAssetPaths.ContentCatalogResources) != null)
                AssetDatabase.DeleteAsset(ExternalAssetPaths.ContentCatalogResources);

            AssetDatabase.CopyAsset(ExternalAssetPaths.ContentCatalogAsset, ExternalAssetPaths.ContentCatalogResources);
            report.AppendLine($"Content catalog: {propDefinitions?.Length ?? 0} props, {roomDefinitions?.Length ?? 0} rooms.");
        }

        private static string SanitizeFileName(string value)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                value = value.Replace(c, '_');
            return value.Replace(' ', '_');
        }

        private static void NormalizeScale(Transform visual, Vector3 targetBounds)
        {
            var renderers = visual.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return;

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            var size = bounds.size;
            if (size.x <= 0.001f || size.y <= 0.001f || size.z <= 0.001f)
                return;

            float sx = targetBounds.x / size.x;
            float sy = targetBounds.y / size.y;
            float sz = targetBounds.z / size.z;
            float uniform = Mathf.Min(sx, sy, sz);
            visual.localScale = Vector3.one * uniform;

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            visual.position -= new Vector3(0f, bounds.min.y, 0f);
        }

        private static GameObject SavePrefab(GameObject root, string path)
        {
            EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
            return PrefabUtility.SaveAsPrefabAsset(root, path);
        }

        private static void EnsureFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || AssetDatabase.IsValidFolder(path))
                return;

            var parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target?.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(target, value);
        }
    }
}
