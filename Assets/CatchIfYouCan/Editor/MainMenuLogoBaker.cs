#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CatchIfYouCan.EditorTools
{
    public static class MainMenuLogoBaker
    {
        private const string ScenePath =
            "Assets/CatchIfYouCan/Scenes/01_MainMenu.unity";

        private const string LogoPath =
            "Assets/CatchIfYouCan/Resources/UI/Branding/CatchIfYouCan_Logo.png";

        private const string CanvasName =
            "MainMenuBrandingCanvas";

        private const string LogoName =
            "GameLogo_Baked";

        [MenuItem("Catch If You Can/Main Menu/Bake Logo Into Scene")]
        public static void BakeLogoIntoScene()
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(LogoPath);

            if (sprite == null)
            {
                Debug.LogError(
                    "[CIYC] Logo could not be loaded as Sprite: " +
                    LogoPath +
                    ". Texture Type must be Sprite (2D and UI).");
                return;
            }

            var scene = SceneManager.GetActiveScene();

            if (scene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return;

                scene = EditorSceneManager.OpenScene(
                    ScenePath,
                    OpenSceneMode.Single);
            }

            var canvasGo = GameObject.Find(CanvasName);

            if (canvasGo == null)
            {
                canvasGo = new GameObject(
                    CanvasName,
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(CanvasScaler));
            }

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 101;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var logoGo = GameObject.Find(LogoName);

            if (logoGo == null)
            {
                logoGo = new GameObject(
                    LogoName,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
            }

            logoGo.transform.SetParent(canvasGo.transform, false);

            var rect = logoGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(-0.15f, 0.16f);
            rect.anchorMax = new Vector2(0.45f, 0.88f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;

            var image = logoGo.GetComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.enabled = true;

            EditorUtility.SetDirty(canvasGo);
            EditorUtility.SetDirty(logoGo);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Selection.activeGameObject = logoGo;

            Debug.Log(
                "[CIYC] GameLogo_Baked saved into 01_MainMenu with direct Sprite reference.");
        }
    }
}
#endif
