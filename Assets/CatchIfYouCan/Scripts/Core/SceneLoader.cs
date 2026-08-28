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

        public void LoadScene(string sceneName) => StartCoroutine(LoadRoutine(sceneName));

        public void LoadBoot() => LoadScene("00_Boot");
        public void LoadMainMenu() => LoadScene("01_MainMenu");
        public void LoadTraining() => LoadScene("02_Training");
        public void LoadInvestigation() => LoadScene("03_Investigation");

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
