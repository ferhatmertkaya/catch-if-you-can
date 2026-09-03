using CatchIfYouCan.UI;
using UnityEngine;

namespace CatchIfYouCan.Core.SceneSetup
{
    /// <summary>A scene's own list of what it needs before it can run.</summary>
    public interface ISceneInstaller
    {
        void Install();
    }

    /// <summary>
    /// What every scene needs regardless of what it is: something to render through,
    /// something to hear through, an event system, and a light so it is not black.
    ///
    /// <para>
    /// These four used to live in a switch on the scene's name. That meant a scene the
    /// switch had never heard of - a renamed one, a split one, a new one - received none of
    /// them and failed silently, because "no camera" and "no event system" do not throw,
    /// they just produce a black screen that ignores input.
    /// </para>
    ///
    /// <para>
    /// The helpers below still find things by name where the old code did, because the four
    /// existing scenes are authored around those names and changing them is scene surgery,
    /// not code. Every one of them prefers an explicit serialized reference first, so a
    /// scene that has been authored with one never falls back to a search.
    /// </para>
    /// </summary>
    public abstract class SceneInstallerBase : MonoBehaviour, ISceneInstaller
    {
        [Header("Scene basics")]
        [Tooltip("The camera this scene renders through before a player exists. Registered " +
                 "as the fallback view camera, so nothing has to ask Camera.main.")]
        [SerializeField] protected Camera bootstrapCamera;

        [Tooltip("Ambient level for the directional light created when the scene has none.")]
        [SerializeField] protected float fallbackLightIntensity = 0.2f;

        public abstract void Install();

        /// <summary>Runs the part that is identical for every scene.</summary>
        protected void InstallSceneBasics()
        {
            EventSystemUtil.EnsureEventSystem();

            var camera = EnsureBootstrapCamera();
            if (camera != null)
                LocalPlayerService.SetFallbackCamera(camera);

            EnsureDirectionalLight(fallbackLightIntensity);
        }

        /// <summary>
        /// The scene's own camera. An authored reference wins; otherwise an existing tagged
        /// camera is adopted, and only if there is none at all is one built - which is the
        /// case that used to leave a directly opened scene rendering nothing.
        /// </summary>
        protected Camera EnsureBootstrapCamera()
        {
            if (bootstrapCamera != null)
                return bootstrapCamera;

            // Deliberate: this is the one place that is allowed to ask what the scene tagged,
            // because the question here is "does this scene already carry a camera", not
            // "which camera does the local player see through".
            var existing = Camera.main;
            if (existing != null)
            {
                bootstrapCamera = existing;
                return bootstrapCamera;
            }

            var go = new GameObject("Bootstrap Camera");
            go.tag = "MainCamera";

            var camera = go.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.02f, 0.05f, 0.04f, 1f);

            go.AddComponent<AudioListener>();
            go.transform.position = new Vector3(0f, 1.6f, -6f);
            go.transform.rotation = Quaternion.Euler(10f, 0f, 0f);

            CIYCLog.Warn("Scene '" + gameObject.scene.name + "' had no camera, so a bootstrap " +
                         "camera was created. Assign one on " + GetType().Name + " to control " +
                         "what it sees before the player spawns.");

            bootstrapCamera = camera;
            return bootstrapCamera;
        }

        protected static void EnsureDirectionalLight(float intensity)
        {
            if (Object.FindAnyObjectByType<Light>() != null)
                return;

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = intensity;
            light.color = new Color(0.55f, 0.65f, 0.58f);
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        protected static GameObject FindOrCreate(string name)
        {
            var existing = GameObject.Find(name);
            return existing != null ? existing : new GameObject(name);
        }

        protected static GameObject FindOrCreateChild(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null)
                return child.gameObject;

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        /// <summary>
        /// Raises a screen only when this call is what created the runtime UI. Arriving from
        /// the boot flow the canvas already exists and something else owns what is on it, so
        /// raising a screen here would take the display away from it.
        /// </summary>
        protected static void ShowScreenIfWeBuiltTheUi(UIScreen screen, bool hideOthers)
        {
            bool existedBefore = CiycServices.RuntimeUiRoot != null;
            CiycServices.EnsureCore();

            if (existedBefore || UIManager.Instance == null)
                return;

            UIManager.Instance.Show(screen, hideOthers);
        }
    }
}
