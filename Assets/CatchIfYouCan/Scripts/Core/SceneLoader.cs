using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CatchIfYouCan.Core
{
    public class SceneLoader : MonoBehaviour
    {
        public static SceneLoader Instance { get; private set; }

        [SerializeField] private CanvasGroup loadingCanvas;
        [SerializeField] private Slider progressBar;
        [SerializeField] private Text tipText;
        [SerializeField] private Text logoText;

        private static readonly string[] Tips =
        {
            "EMF 5 is strong evidence — but not every spike means a hunt.",
            "Salt footprints glow under UV light.",
            "Stay silent during a hunt. Noise draws the entity.",
            "The Warding Relic can interrupt a hunt nearby.",
            "Cold rooms often mark the entity's favored space.",
            "Headphones reveal distant whispers more clearly.",
            "Hide spots are not always safe. Listen first.",
            "Spectral Grid silhouettes last only a moment."
        };

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            if (loadingCanvas != null) loadingCanvas.alpha = 0f;
        }

        /// <summary>
        /// Loads a production scene by identity. Prefer this over the string overload:
        /// a typo in an identity is a compile error, a typo in a name is a black screen.
        /// </summary>
        public void Load(CiycScene scene) => LoadScene(CiycScenes.NameOf(scene));

        public void LoadScene(string sceneName)
        {
            // A scene missing from the build list loads perfectly in the editor and not at
            // all on device, where LoadSceneAsync returns null and the coroutine then
            // dereferences it. That NullReferenceException names this file, not the scene
            // that was never registered. Refuse first and say which scene and why.
            if (!CiycScenes.IsRegisteredInBuild(sceneName))
            {
                CIYCLog.Error("Cannot load scene '" + sceneName + "': it is not in the build " +
                              "settings scene list. Add " + CiycScenes.PathOf(sceneName) +
                              " under File > Build Settings, or run " +
                              "Catch If You Can > Setup Project.");
                return;
            }

            StartCoroutine(LoadRoutine(sceneName));
        }

        public void LoadBoot() => Load(CiycScene.Boot);
        public void LoadMainMenu() => Load(CiycScene.MainMenu);
        public void LoadLobby() => Load(CiycScene.Lobby);
        public void LoadTraining() => Load(CiycScene.Training);
        public void LoadInvestigation() => Load(CiycScene.Investigation);

        private IEnumerator LoadRoutine(string sceneName)
        {
            if (GameManager.Instance != null)
                GameManager.Instance.SetState(GameState.Loading);

            if (loadingCanvas != null)
            {
                loadingCanvas.blocksRaycasts = true;
                loadingCanvas.alpha = 1f;
            }
            if (logoText != null) logoText.text = "CATCH IF YOU CAN";
            if (tipText != null) tipText.text = Tips[Random.Range(0, Tips.Length)];
            if (progressBar != null) progressBar.value = 0f;

            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
            op.allowSceneActivation = false;
            while (op.progress < 0.9f)
            {
                if (progressBar != null) progressBar.value = op.progress;
                yield return null;
            }
            if (progressBar != null) progressBar.value = 1f;
            yield return new WaitForSecondsRealtime(0.35f);
            op.allowSceneActivation = true;
            yield return null;
            if (loadingCanvas != null)
            {
                float t = 1f;
                while (t > 0f)
                {
                    t -= Time.unscaledDeltaTime * 2f;
                    loadingCanvas.alpha = Mathf.Clamp01(t);
                    yield return null;
                }
                loadingCanvas.blocksRaycasts = false;
            }
        }
    }
}
