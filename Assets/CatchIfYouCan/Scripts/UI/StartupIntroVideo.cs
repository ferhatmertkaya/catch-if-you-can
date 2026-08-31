using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace CatchIfYouCan.UI
{
    /// <summary>
    /// The startup intro: a black screen, one full-screen video, then the main menu fading up.
    ///
    /// <para>
    /// This replaces the old "CATCH IF YOU CAN" splash title that <c>SceneAutoSetup</c> used to
    /// build in <c>00_Boot</c>. It is <b>not</b> the main menu logo — that one lives in
    /// <c>RuntimeUIFactory.WireMainMenu</c> and in the scene's own branding canvas, and is
    /// untouched by this file.
    /// </para>
    ///
    /// <para>
    /// The whole presentation is one <see cref="Image"/> called the cover. It starts opaque
    /// black on the very first frame, before the video is even asked to load, which is what
    /// stops any flash of camera clear colour or half-built UI. From there the same cover does
    /// every transition: it fades out to reveal the video, back in to end it, and out one last
    /// time to reveal the menu. A separate opaque backdrop sits behind the video so the
    /// letterbox bars are black rather than whatever the boot camera happens to clear to.
    /// </para>
    ///
    /// <para>
    /// The intro deliberately lives entirely in <c>00_Boot</c> and the menu scene is loaded only
    /// once the video has finished. That is what keeps the phone ring, the horror event and the
    /// candle flicker quiet during the intro: those components are in <c>01_MainMenu</c>, so
    /// while the video plays they do not exist yet and there is no second scheduler to gate or
    /// duplicate.
    /// </para>
    ///
    /// <para>
    /// Every failure path lands on the menu. A missing clip, a decoder that never prepares, a
    /// playback error or a clip that never reports its end all fall through to the same reveal,
    /// so the game can never sit on a permanent black screen.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StartupIntroVideo : MonoBehaviour
    {
        /// <summary>Resources-relative path, no extension. See <c>Assets/CatchIfYouCan/Resources/Video/Intro</c>.</summary>
        public const string VideoResourcePath = "Video/Intro/CIYC_StartupIntro";

        /// <summary>
        /// True from the moment the black cover goes up until the menu has finished fading in.
        /// </summary>
        /// <remarks>
        /// The menu's random beats — the phone ring and, through it, the horror event — must not
        /// fire while the intro owns the screen. They are polled against this rather than being
        /// stopped and restarted, so the phone keeps its own single scheduler and there is no
        /// second one to go out of sync. Static because the phone lives in a scene that does not
        /// exist yet when the intro starts, so there is nothing to hold a reference to.
        ///
        /// <para>
        /// It is cleared only after the reveal fade completes, which closes the race where the
        /// menu scene has loaded behind the cover and a ring lands on the last frame of the fade.
        /// </para>
        /// </remarks>
        public static bool IsIntroPlaying { get; private set; }

        [Header("Fades (seconds, unscaled)")]
        [SerializeField, Range(0f, 3f)] private float fadeToVideo = 0.35f;
        [SerializeField, Range(0f, 3f)] private float fadeToBlack = 0.6f;
        [SerializeField, Range(0f, 3f)] private float fadeToMenu = 0.8f;

        [Header("Safety")]
        [Tooltip("Give up preparing the decoder after this long and go straight to the menu.")]
        [SerializeField, Range(1f, 30f)] private float prepareTimeout = 8f;

        [Tooltip("Extra grace on top of the clip duration before playback is assumed stuck.")]
        [SerializeField, Range(0.5f, 10f)] private float playbackGrace = 2f;

        private Canvas _canvas;
        private Image _cover;
        private Image _backdrop;
        private RawImage _videoImage;
        private VideoPlayer _player;
        private AudioSource _audio;
        private RenderTexture _target;
        private VideoClip _clip;

        private bool _reachedEnd;
        private bool _errored;

        /// <summary>
        /// Builds the overlay and puts the screen to black immediately. Call this as early as
        /// possible — the cover is opaque from the frame this returns, so anything created
        /// afterwards is hidden behind it.
        /// </summary>
        public static StartupIntroVideo Create()
        {
            IsIntroPlaying = true;
            var go = new GameObject("StartupIntro");
            DontDestroyOnLoad(go);
            var intro = go.AddComponent<StartupIntroVideo>();
            intro.BuildOverlay();
            return intro;
        }

        private void BuildOverlay()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Above the splash canvas (200) and the runtime UI canvas (100).
            _canvas.sortingOrder = 300;
            gameObject.AddComponent<GraphicRaycaster>();

            // Opaque, so the letterbox bars around a non-matching aspect stay black.
            _backdrop = CreateFullScreenImage("Backdrop", 1f);

            var imageGo = new GameObject("Video", typeof(RectTransform));
            imageGo.transform.SetParent(transform, false);
            var imageRect = imageGo.GetComponent<RectTransform>();
            Stretch(imageRect);
            _videoImage = imageGo.AddComponent<RawImage>();
            _videoImage.color = Color.white;
            _videoImage.raycastTarget = false;
            _videoImage.enabled = false;

            // Letterboxes or pillarboxes the clip inside the screen instead of stretching it.
            var fitter = imageGo.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 16f / 9f;

            // Last child, so it draws over the video.
            _cover = CreateFullScreenImage("Cover", 1f);
        }

        private Image CreateFullScreenImage(string name, float alpha)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            Stretch(go.GetComponent<RectTransform>());
            var image = go.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, alpha);
            // Swallow taps while the intro owns the screen.
            image.raycastTarget = true;
            return image;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        /// <summary>
        /// Prepares and plays the clip, ending with the screen fully black either way. Never
        /// throws and never blocks forever; on any failure it simply returns early and the
        /// caller carries on to the menu.
        /// </summary>
        public IEnumerator Run()
        {
            _clip = Resources.Load<VideoClip>(VideoResourcePath);
            if (_clip == null)
            {
                Debug.LogWarning($"[CIYC] Startup intro clip '{VideoResourcePath}' not found; skipping to the menu.", this);
                yield break;
            }

            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            // Routed through Unity's audio rather than the decoder's direct output so the
            // player's master volume setting still applies to the intro.
            _audio.volume = CatchIfYouCan.Audio.AudioManager.Instance != null
                ? CatchIfYouCan.Audio.AudioManager.Instance.GetMasterVolume()
                : 1f;

            _player = gameObject.AddComponent<VideoPlayer>();
            _player.playOnAwake = false;
            _player.isLooping = false;
            _player.source = VideoSource.VideoClip;
            _player.clip = _clip;
            _player.renderMode = VideoRenderMode.RenderTexture;
            _player.audioOutputMode = VideoAudioOutputMode.AudioSource;
            _player.SetTargetAudioSource(0, _audio);
            _player.skipOnDrop = true;
            _player.waitForFirstFrame = true;
            _player.errorReceived += OnVideoError;
            _player.loopPointReached += OnVideoEnded;

            _player.Prepare();

            float waited = 0f;
            while (!_player.isPrepared && !_errored && waited < prepareTimeout)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            if (_errored || !_player.isPrepared)
            {
                Debug.LogWarning("[CIYC] Startup intro could not be prepared in time; skipping to the menu.", this);
                yield break;
            }

            // Only now that a decoded frame is guaranteed do we size the target and reveal.
            int width = (int)_player.width;
            int height = (int)_player.height;
            if (width <= 0 || height <= 0)
            {
                width = 1280;
                height = 720;
            }

            _target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
            {
                name = "StartupIntroTarget",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            _target.Create();

            _player.targetTexture = _target;
            _videoImage.texture = _target;
            _videoImage.enabled = true;
            _videoImage.GetComponent<AspectRatioFitter>().aspectRatio = (float)width / height;

            _player.Play();

            yield return Fade(1f, 0f, fadeToVideo);

            float limit = (float)_clip.length + playbackGrace;
            float elapsed = 0f;
            while (!_reachedEnd && !_errored && elapsed < limit)
            {
#if UNITY_EDITOR
                // Fully qualified: a bare "Input" here binds to the project's own
                // CatchIfYouCan.Input namespace, not UnityEngine.Input.
                if (UnityEngine.Input.GetKeyDown(KeyCode.Space) ||
                    UnityEngine.Input.GetKeyDown(KeyCode.Escape))
                    break;
#endif
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return Fade(_cover.color.a, 1f, fadeToBlack);

            _player.Stop();
            if (_audio != null)
                _audio.Stop();
        }

        /// <summary>
        /// Runs the whole startup presentation and loads <paramref name="nextSceneName"/> behind
        /// the black cover, calling <paramref name="onMenuVisible"/> once the menu is on screen.
        ///
        /// <para>
        /// This is driven from the intro's own object on purpose. <c>Bootstrap</c> lives in
        /// <c>00_Boot</c> and is destroyed the moment the menu scene loads, so a coroutine owned
        /// by it would stop half way and leave the cover up forever. This object survives the
        /// load, so the fade always completes.
        /// </para>
        /// </summary>
        public IEnumerator Sequence(string nextSceneName, System.Action onMenuVisible)
        {
            yield return Run();

            if (Core.SceneLoader.Instance != null)
                Core.SceneLoader.Instance.LoadScene(nextSceneName);
            else
                UnityEngine.SceneManagement.SceneManager.LoadScene(nextSceneName);

            // Wait for the scene swap rather than a fixed delay, so a slow device simply stays
            // on black instead of revealing a half-loaded menu. The cap is a backstop: if the
            // load never completes we uncover anyway rather than sit on black forever.
            float waited = 0f;
            while (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != nextSceneName
                   && waited < 20f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            // One more frame so SceneAutoSetup's AfterSceneLoad pass has built the menu UI.
            yield return null;

            yield return Reveal();

            // Only now is the menu actually on screen, so this is the moment the phone and the
            // horror event become eligible. Released before the callback so anything it starts
            // sees an open gate.
            IsIntroPlaying = false;

            onMenuVisible?.Invoke();

            Dispose();
        }

        /// <summary>
        /// Fades the black cover away to show whatever loaded behind it. The overlay is still
        /// present afterwards; call <see cref="Dispose"/> to remove it.
        /// </summary>
        public IEnumerator Reveal()
        {
            // Hidden behind a fully opaque cover at this point, so tearing the video down is
            // invisible — and it must happen before the fade, or the backdrop would hide the menu.
            TearDownVideo();

            yield return Fade(_cover.color.a, 0f, fadeToMenu);
        }

        private void TearDownVideo()
        {
            if (_player != null)
            {
                _player.errorReceived -= OnVideoError;
                _player.loopPointReached -= OnVideoEnded;
                _player.Stop();
                _player.targetTexture = null;
                _player.clip = null;
                Destroy(_player);
                _player = null;
            }

            if (_audio != null)
            {
                _audio.Stop();
                Destroy(_audio);
                _audio = null;
            }

            if (_videoImage != null)
            {
                _videoImage.texture = null;
                _videoImage.enabled = false;
            }

            if (_backdrop != null)
                _backdrop.enabled = false;

            if (_target != null)
            {
                _target.Release();
                Destroy(_target);
                _target = null;
            }
        }

        /// <summary>Removes the overlay. Safe to call more than once.</summary>
        public void Dispose()
        {
            // Belt and braces: whatever path got us here, the menu must never be left with its
            // phone permanently gated because the intro ended unexpectedly.
            IsIntroPlaying = false;

            TearDownVideo();

            bool hadClip = _clip != null;
            _clip = null;

            if (this != null && gameObject != null)
                Destroy(gameObject);

            // The clip is a few megabytes; let it go now rather than at the next level load.
            if (hadClip)
                Resources.UnloadUnusedAssets();
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            if (_cover == null)
                yield break;

            if (duration <= 0f)
            {
                SetCoverAlpha(to);
                yield break;
            }

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                SetCoverAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(t / duration)));
                yield return null;
            }
            SetCoverAlpha(to);
        }

        private void SetCoverAlpha(float alpha)
        {
            var c = _cover.color;
            c.a = Mathf.Clamp01(alpha);
            _cover.color = c;
            // Stop swallowing input once the cover is effectively gone.
            _cover.raycastTarget = c.a > 0.01f;
        }

        private void OnVideoEnded(VideoPlayer source) => _reachedEnd = true;

        private void OnVideoError(VideoPlayer source, string message)
        {
            _errored = true;
            Debug.LogWarning($"[CIYC] Startup intro playback error: {message}", this);
        }

        private void OnDestroy()
        {
            // Covers a scene reload or an editor stop landing mid-intro.
            IsIntroPlaying = false;

            if (_player != null)
            {
                _player.errorReceived -= OnVideoError;
                _player.loopPointReached -= OnVideoEnded;
            }
            if (_target != null)
            {
                _target.Release();
                _target = null;
            }
        }
    }
}
