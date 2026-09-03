using CatchIfYouCan.Procedural;
using CatchIfYouCan.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CatchIfYouCan.Core
{
    public static class SceneAutoSetup
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnAfterSceneLoad()
        {
            var scene = SceneManager.GetActiveScene();
            switch (scene.name)
            {
                case CiycScenes.Boot:
                    EnsureBoot();
                    break;
                case CiycScenes.MainMenu:
                    EnsureMainMenu();
                    break;
                case CiycScenes.Training:
                    EnsureTraining();
                    break;
                case CiycScenes.Investigation:
                    EnsureInvestigation();
                    break;
            }
        }

        private static void EnsureBoot()
        {
            EnsureEventSystem();
            EnsureMainCamera();

            var bootstrapGo = FindOrCreate("BOOTSTRAP");
            if (bootstrapGo.GetComponent<Bootstrap>() == null)
                bootstrapGo.AddComponent<Bootstrap>();

            // The old text splash ("SplashCanvas" + a "CATCH IF YOU CAN" title) used to be built
            // here. It is replaced by StartupIntroVideo, which Bootstrap raises itself so the
            // screen is black before any of this runs. The menu's own logo is a separate thing
            // and is still built by RuntimeUIFactory.WireMainMenu.
        }

        private static void EnsureMainMenu()
        {
            EnsureEventSystem();
            EnsureMainCamera();
            EnsureDirectionalLight(0.15f);

            // Entered from boot this is a no-op; opened directly in the editor it is what
            // makes the scene runnable at all. Either way it is the same implementation,
            // so the two paths cannot drift apart.
            CiycServices.EnsureCore();

            if (UIManager.Instance != null)
                UIManager.Instance.Show(UIScreen.MainMenu, true);
        }

        private static void EnsureTraining()
        {
            EnsureEventSystem();
            EnsureMainCamera();
            EnsureDirectionalLight(0.25f);

            CiycServices.EnsureCore();

            var world = FindOrCreate("WORLD");
            FindOrCreateChild(world.transform, "VanAnchor").transform.localPosition = new Vector3(0f, 0f, -14f);
            FindOrCreateChild(world.transform, "HouseAnchor").transform.localPosition = Vector3.zero;

            var bootstrapGo = FindOrCreate("TRAINING_BOOTSTRAP");
            if (bootstrapGo.GetComponent<TrainingBootstrap>() == null)
                bootstrapGo.AddComponent<TrainingBootstrap>();
        }

        private static void EnsureInvestigation()
        {
            EnsureEventSystem();
            EnsureMainCamera();
            EnsureDirectionalLight(0.2f);

            // Same rule as InvestigationBootstrap: the HUD is only raised when this call
            // is what created the canvas.
            bool uiExistedBefore = CiycServices.RuntimeUiRoot != null;
            CiycServices.EnsureCore();
            if (!uiExistedBefore && UIManager.Instance != null)
                UIManager.Instance.Show(UIScreen.HUD, false);

            var world = FindOrCreate("WORLD");
            FindOrCreateChild(world.transform, "VanAnchor").transform.localPosition = new Vector3(0f, 0f, -14f);
            FindOrCreateChild(world.transform, "HouseAnchor").transform.localPosition = Vector3.zero;

            var managers = FindOrCreate("MANAGERS");
            FindOrCreateChild(managers.transform, "ProceduralHouseGenerator");
            FindOrCreateChild(managers.transform, "GhostSpawnManager");
            FindOrCreateChild(managers.transform, "MissionManager");
            FindOrCreateChild(managers.transform, "ObjectiveManager");
            FindOrCreateChild(managers.transform, "EvidenceManager");

            var bootstrapGo = FindOrCreate("INVESTIGATION_BOOTSTRAP");
            if (bootstrapGo.GetComponent<InvestigationBootstrap>() == null)
                bootstrapGo.AddComponent<InvestigationBootstrap>();
            if (bootstrapGo.GetComponent<CatchIfYouCan.Audio.InvestigationAudioBootstrap>() == null)
                bootstrapGo.AddComponent<CatchIfYouCan.Audio.InvestigationAudioBootstrap>();
        }

        private static void EnsureEventSystem()
        {
            CatchIfYouCan.UI.EventSystemUtil.EnsureEventSystem();
        }

        private static void EnsureMainCamera()
        {
            var cam = Camera.main;
            if (cam != null)
                return;

            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var camera = go.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.02f, 0.05f, 0.04f, 1f);
            go.AddComponent<AudioListener>();
            go.transform.position = new Vector3(0f, 1.6f, -6f);
            go.transform.rotation = Quaternion.Euler(10f, 0f, 0f);
        }

        private static void EnsureDirectionalLight(float intensity)
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

        private static GameObject FindOrCreate(string name)
        {
            var existing = GameObject.Find(name);
            return existing != null ? existing : new GameObject(name);
        }

        private static GameObject FindOrCreateChild(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null)
                return child.gameObject;

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        private static readonly Vector2 VectorOne = Vector2.one;
    }
}
