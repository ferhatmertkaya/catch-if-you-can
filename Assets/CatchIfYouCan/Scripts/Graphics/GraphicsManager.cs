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

        protected override void Awake()
        {
            persist = true;
            base.Awake();
            CurrentProfile = defaultProfile;
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

            var urp = GetUrpAsset(profile) ?? GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
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
                QualitySettings.shadows = shadows ? ShadowQuality.All : ShadowQuality.Disable;
                QualitySettings.pixelLightCount = profile switch
                {
                    GraphicsProfile.Low => 1,
                    GraphicsProfile.Medium => 2,
                    _ => 4
                };
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
            scale = Mathf.Clamp(scale, 0.5f, 1.5f);
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp != null)
                urp.renderScale = scale;
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
