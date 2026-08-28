using System;
using System.Collections.Generic;
using System.Reflection;
using CatchIfYouCan.Core;
using UnityEngine;
using UnityEngine.AI;

namespace CatchIfYouCan.Procedural
{
    public class NavMeshRuntimeBuilder : MonoBehaviour
    {
        [SerializeField] private Transform geometryRoot;
        [SerializeField] private int defaultArea = 0;
        [SerializeField] private LayerMask includedLayers = ~0;
        [SerializeField] private float agentRadius = 0.35f;
        [SerializeField] private float agentHeight = 1.8f;
        [SerializeField] private float maxSlope = 45f;
        [SerializeField] private float stepHeight = 0.4f;

        private NavMeshData _navMeshData;
        private NavMeshDataInstance _instance;

        public bool Build(Transform root)
        {
            if (root != null)
                geometryRoot = root;

            if (geometryRoot == null)
            {
                CIYCLog.Warn("NavMeshRuntimeBuilder: no geometry root.");
                return false;
            }

            RemoveExisting();

            if (TryBuildWithNavMeshSurface())
            {
                CIYCLog.Info("NavMesh built via NavMeshSurface.");
                return true;
            }

            return BuildWithNavMeshBuilder();
        }

        private bool TryBuildWithNavMeshSurface()
        {
            Type surfaceType = Type.GetType("Unity.AI.Navigation.NavMeshSurface, Unity.AI.Navigation");
            if (surfaceType == null)
                return false;

            try
            {
                var surfaceComponent = gameObject.GetComponent(surfaceType);
                if (surfaceComponent == null)
                    surfaceComponent = gameObject.AddComponent(surfaceType);

                var collectObjectsField = surfaceType.GetField("collectObjects", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (collectObjectsField != null)
                {
                    var collectEnum = Type.GetType("Unity.AI.Navigation.CollectObjects, Unity.AI.Navigation");
                    if (collectEnum != null)
                    {
                        object childrenValue = Enum.Parse(collectEnum, "Children");
                        collectObjectsField.SetValue(surfaceComponent, childrenValue);
                    }
                }

                var layerMaskField = surfaceType.GetField("layerMask", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                layerMaskField?.SetValue(surfaceComponent, includedLayers);

                var useGeometryField = surfaceType.GetField("useGeometry", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (useGeometryField != null)
                {
                    var renderMeshValue = Enum.ToObject(useGeometryField.FieldType, 0);
                    useGeometryField.SetValue(surfaceComponent, renderMeshValue);
                }

                var buildMethod = surfaceType.GetMethod("BuildNavMesh", BindingFlags.Instance | BindingFlags.Public);
                buildMethod?.Invoke(surfaceComponent, null);
                return true;
            }
            catch (Exception ex)
            {
                CIYCLog.Warn($"NavMeshSurface build failed: {ex.Message}");
                return false;
            }
        }

        private bool BuildWithNavMeshBuilder()
        {
            var sources = new List<NavMeshBuildSource>();
            var bounds = new Bounds(geometryRoot.position, Vector3.one * 4f);
            bool hasGeometry = false;

            var meshFilters = geometryRoot.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < meshFilters.Length; i++)
            {
                var filter = meshFilters[i];
                if (filter == null || filter.sharedMesh == null)
                    continue;

                if (!ShouldInclude(filter.gameObject))
                    continue;

                hasGeometry = true;
                bounds.Encapsulate(filter.GetComponent<Renderer>() != null
                    ? filter.GetComponent<Renderer>().bounds
                    : new Bounds(filter.transform.position, Vector3.one));

                sources.Add(new NavMeshBuildSource
                {
                    shape = NavMeshBuildSourceShape.Mesh,
                    sourceObject = filter.sharedMesh,
                    transform = filter.transform.localToWorldMatrix,
                    area = defaultArea
                });
            }

            if (!hasGeometry)
            {
                CreateFallbackWalkablePlanes(sources, ref bounds);
                hasGeometry = sources.Count > 0;
            }

            if (!hasGeometry)
            {
                CIYCLog.Warn("NavMeshRuntimeBuilder: no geometry found.");
                return false;
            }

            var buildSettings = NavMesh.GetSettingsByID(0);
            buildSettings.agentRadius = agentRadius;
            buildSettings.agentHeight = agentHeight;
            buildSettings.agentSlope = maxSlope;
            buildSettings.agentClimb = stepHeight;

            bounds.Expand(2f);
            _navMeshData = NavMeshBuilder.BuildNavMeshData(
                buildSettings,
                sources,
                bounds,
                geometryRoot.position,
                geometryRoot.rotation);

            if (_navMeshData == null)
            {
                CIYCLog.Warn("NavMeshRuntimeBuilder: BuildNavMeshData returned null.");
                return false;
            }

            _instance = NavMesh.AddNavMeshData(_navMeshData);
            CIYCLog.Info($"NavMesh built with {sources.Count} sources.");
            return true;
        }

        private void CreateFallbackWalkablePlanes(List<NavMeshBuildSource> sources, ref Bounds bounds)
        {
            var planes = geometryRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < planes.Length; i++)
            {
                var t = planes[i];
                if (t == geometryRoot || !t.name.Contains("Floor"))
                    continue;

                var mesh = CreatePlaneMesh(6f, 6f);
                sources.Add(new NavMeshBuildSource
                {
                    shape = NavMeshBuildSourceShape.Mesh,
                    sourceObject = mesh,
                    transform = t.localToWorldMatrix,
                    area = defaultArea
                });
                bounds.Encapsulate(new Bounds(t.position, new Vector3(6f, 0.1f, 6f)));
            }
        }

        private static Mesh CreatePlaneMesh(float width, float depth)
        {
            var mesh = new Mesh { name = "NavFallbackPlane" };
            float hw = width * 0.5f;
            float hd = depth * 0.5f;
            mesh.vertices = new[]
            {
                new Vector3(-hw, 0f, -hd),
                new Vector3(hw, 0f, -hd),
                new Vector3(hw, 0f, hd),
                new Vector3(-hw, 0f, hd)
            };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateNormals();
            return mesh;
        }

        private bool ShouldInclude(GameObject go)
        {
            if (((1 << go.layer) & includedLayers.value) == 0)
                return false;

            return go.CompareTag("Environment") || go.name.Contains("Floor") || go.name.Contains("Wall");
        }

        private void RemoveExisting()
        {
            if (_instance.valid)
                _instance.Remove();

            _navMeshData = null;
        }

        private void OnDestroy()
        {
            RemoveExisting();
        }
    }
}
