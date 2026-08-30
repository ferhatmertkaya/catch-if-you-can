using CatchIfYouCan.Save;
using CatchIfYouCan.Utilities;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CatchIfYouCan.Graphics
{
    public enum GraphicsProfile
    {
        Low,
        Medium,
        High
    }

    public class GraphicsManager : SingletonBehaviour<GraphicsManager>
    {
        [SerializeField] private GraphicsProfile defaultProfile = GraphicsProfile.Medium;
        [SerializeField] private UniversalRenderPipelineAsset urpLow;
        [SerializeField] private UniversalRenderPipelineAsset urpMedium;
        [SerializeField] private UniversalRenderPipelineAsset urpHigh;

        public GraphicsProfile CurrentProfile { get; private set; }

        private UniversalRenderPipelineAsset _runtimePipeline;

        protected override void Awake()
        {
            persist = true;
            base.Awake();
            CurrentProfile = defaultProfile;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (_runtimePipeline != null)
            {
                QualitySettings.renderPipeline = null;
                Destroy(_runtimePipeline);
                _runtimePipeline = null;
            }
        }

        /// <summary>
        /// Returns the pipeline asset this manager is allowed to write to.
        /// Never the project asset from GraphicsSettings: that is authored content, and
        /// writing to it from Play Mode edits the .asset on disk in the Editor and makes the
        /// authored look drift away from what a device renders. A per-profile asset assigned
        /// in the inspector is owned by this component; otherwise we tune a runtime clone of
        /// the project asset, which is discarded when the manager goes away.
        /// </summary>
        private UniversalRenderPipelineAsset GetWritablePipeline(GraphicsProfile profile)
        {
            var dedicated = GetUrpAsset(profile);
            if (dedicated != null)
                return dedicated;

            if (_runtimePipeline == null)
            {
                var source = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                if (source == null)
                    return null;

                _runtimePipeline = Instantiate(source);
                _runtimePipeline.name = source.name + " (Runtime)";
            }

            return _runtimePipeline;
        }

        public void ApplyFromSettings(SettingsManager settings)
        {
            if (settings == null) return;
            var profile = QualityIndexToProfile(settings.QualityLevel);
            ApplyProfile(profile, settings.TargetFps, settings.ResolutionScale, settings.Shadows, settings.PostProcessing);
        }

        public void ApplyProfile(GraphicsProfile profile, int targetFps = 60, float renderScale = 1f,
            bool shadows = true, bool postProcessing = true)
        {
            CurrentProfile = profile;
            int qualityIndex = ProfileToQualityIndex(profile);
            QualitySettings.SetQualityLevel(qualityIndex, true);

            var urp = GetWritablePipeline(profile);
            if (urp != null)
            {
                QualitySettings.renderPipeline = urp;
                urp.renderScale = Mathf.Clamp(renderScale, 0.5f, 1.5f);
                urp.shadowDistance = shadows
                    ? profile switch
                    {
                        GraphicsProfile.Low => 25f,
                        GraphicsProfile.Medium => 45f,
                        _ => 70f
                    }
                    : 0f;
                urp.supportsHDR = profile != GraphicsProfile.Low;
            }
            else
            {
                QualitySettings.shadows = shadows ? UnityEngine.ShadowQuality.All : UnityEngine.ShadowQuality.Disable;
                QualitySettings.particleRaycastBudget = profile switch
                {
                    GraphicsProfile.Low => 64,
                    GraphicsProfile.Medium => 256,
                    _ => 1024
                };
            }

            ApplyCameraSettings(postProcessing, renderScale);
            Application.targetFrameRate = targetFps > 0 ? targetFps : 60;
        }

        public void SetTargetFps(int fps)
        {
            Application.targetFrameRate = fps > 0 ? fps : 60;
        }

        public void SetRenderScale(float scale)
        {
            var urp = GetWritablePipeline(CurrentProfile);
            if (urp == null)
                return;

            QualitySettings.renderPipeline = urp;
            urp.renderScale = Mathf.Clamp(scale, 0.5f, 1.5f);
        }

        private void ApplyCameraSettings(bool postProcessing, float renderScale)
        {
            var cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (var cam in cameras)
            {
                if (cam == null) continue;
                cam.allowHDR = CurrentProfile != GraphicsProfile.Low;
                cam.allowMSAA = CurrentProfile != GraphicsProfile.Low;

                var data = cam.GetUniversalAdditionalCameraData();
                if (data != null)
                {
                    data.renderPostProcessing = postProcessing && CurrentProfile != GraphicsProfile.Low;
                    data.antialiasing = CurrentProfile == GraphicsProfile.High
                        ? AntialiasingMode.SubpixelMorphologicalAntiAliasing
                        : AntialiasingMode.None;
                }
            }

            SetRenderScale(renderScale);
        }

        private UniversalRenderPipelineAsset GetUrpAsset(GraphicsProfile profile)
        {
            return profile switch
            {
                GraphicsProfile.Low => urpLow != null ? urpLow : urpMedium,
                GraphicsProfile.High => urpHigh != null ? urpHigh : urpMedium,
                _ => urpMedium != null ? urpMedium : urpHigh
            };
        }

        private static GraphicsProfile QualityIndexToProfile(int index) => index switch
        {
            <= 0 => GraphicsProfile.Low,
            1 => GraphicsProfile.Medium,
            _ => GraphicsProfile.High
        };

        private static int ProfileToQualityIndex(GraphicsProfile profile) => profile switch
        {
            GraphicsProfile.Low => 0,
            GraphicsProfile.Medium => 1,
            _ => 2
        };

        public void ApplyLow() => ApplyProfile(GraphicsProfile.Low, 30, 0.75f, false, false);
        public void ApplyMedium() => ApplyProfile(GraphicsProfile.Medium, 60, 0.85f, true, true);
        public void ApplyHigh() => ApplyProfile(GraphicsProfile.High, 60, 1f, true, true);
    }
}
