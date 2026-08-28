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
                case "00_Boot":
                    EnsureBoot();
                    break;
                case "01_MainMenu":
                    EnsureMainMenu();
                    break;
                case "02_Training":
                    EnsureTraining();
                    break;
                case "03_Investigation":
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

            var splash = FindOrCreate("SplashCanvas");
            if (splash.GetComponent<Canvas>() == null)
            {
                var canvas = splash.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 200;
                splash.AddComponent<CanvasScaler>();
                splash.AddComponent<GraphicRaycaster>();
            }

            if (splash.GetComponent<CanvasGroup>() == null)
                splash.AddComponent<CanvasGroup>();

            var title = GameObject.Find("SplashTitle");
            if (title == null)
            {
                title = new GameObject("SplashTitle");
                title.transform.SetParent(splash.transform, false);
                var rect = title.AddComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = VectorOne;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                RuntimeUIFactory.CreateText(title.transform, "Title", "CATCH IF YOU CAN", 48);
            }

            var bootstrap = bootstrapGo.GetComponent<Bootstrap>();
            var splashGroup = splash.GetComponent<CanvasGroup>();
            var field = typeof(Bootstrap).GetField("splashGroup",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null && field.GetValue(bootstrap) == null)
                field.SetValue(bootstrap, splashGroup);
        }

        private static void EnsureMainMenu()
        {
            EnsureEventSystem();
            EnsureMainCamera();
            EnsureDirectionalLight(0.15f);

            if (GameObject.Find("RuntimeUI") == null)
                RuntimeUIFactory.BuildCompleteUI();

            var menuRoot = FindOrCreate("MAIN_MENU_ROOT");
            if (menuRoot.GetComponent<MainMenuController>() == null)
                menuRoot.AddComponent<MainMenuController>();

            EnsureHallwayPlaceholders();
        }

        private static void EnsureTraining()
        {
            EnsureEventSystem();
            EnsureMainCamera();
            EnsureDirectionalLight(0.25f);

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

            if (GameObject.Find("RuntimeUI") == null)
            {
                RuntimeUIFactory.BuildCompleteUI();
                if (UIManager.Instance != null)
                    UIManager.Instance.Show(UIScreen.HUD, false);
            }

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
            if (Object.FindFirstObjectByType<Light>() != null)
                return;

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = intensity;
            light.color = new Color(0.55f, 0.65f, 0.58f);
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static void EnsureHallwayPlaceholders()
        {
            var root = FindOrCreate("HallwayPlaceholders");
            if (root.transform.childCount > 0)
                return;

            for (int i = 0; i < 6; i++)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = $"Wall_{i}";
                cube.transform.SetParent(root.transform, false);
                cube.transform.localScale = new Vector3(0.2f, 2.5f, Random.Range(1.5f, 3f));
                cube.transform.localPosition = new Vector3((i - 2.5f) * 1.2f, 1.25f, -4f - i * 0.3f);
                var renderer = cube.GetComponent<Renderer>();
                if (renderer != null)
                    renderer.sharedMaterial = Art.RuntimeMaterialFactory.GetDarkWall();
            }
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
