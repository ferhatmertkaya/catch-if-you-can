using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CatchIfYouCan.UI
{
    /// <summary>
    /// The boot readout the player watches after pressing PLAY.
    ///
    /// <para>
    /// <b>Every line names something that has already happened.</b> The steps are not on a timer
    /// pretending to measure work - the handover calls <see cref="Advance"/> at the points where
    /// the work is genuinely done: the screen is black and the cinematic director has stood down,
    /// the lobby root is active, the player and their kit exist, the ambience is running. A
    /// progress readout that runs ahead of the thing it reports is a control that manufactures
    /// evidence, and this project has paid for that shape three times already (mistakes 23, 44
    /// and the guard of 43).
    /// </para>
    ///
    /// <para>
    /// <b>What IS presentation, said out loud:</b> the minimum dwell. Some of these phases finish
    /// in a few milliseconds, and a caption that flashes for two frames is not a caption. So a
    /// step stays up for at least <see cref="minimumStepTime"/> - but it is never marked DONE
    /// before its work is done, only held visible a little longer after it. The readout can lag
    /// reality; it cannot lead it.
    /// </para>
    ///
    /// <para>
    /// <b>It draws no black sheet of its own.</b> <see cref="TransitionFade"/> owns the transition
    /// overlay and is already opaque by the time this appears - a second full-screen sheet would
    /// be a second owner of the same thing, which a guard forbids. This canvas carries text and
    /// one thin progress rule, and sits one sorting order above the fade so it reads on top of it.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Catch If You Can/Mission Boot Sequence")]
    public sealed class MissionBootSequence : MonoBehaviour
    {
        /// <summary>One above <see cref="TransitionFade.SortingOrder"/>, so it reads on the black.</summary>
        public const int SortingOrder = TransitionFade.SortingOrder + 1;

        /// <summary>
        /// The steps, in the order the handover reaches them.
        ///
        /// <para>
        /// Named rather than numbered at the call site: an index would let a caller report a step
        /// the sequence never reached, and the two would then disagree about where the handover
        /// had got to.
        /// </para>
        /// </summary>
        public enum Step
        {
            Initialising = 0,
            LoadingEnvironment = 1,
            CalibratingEquipment = 2,
            ReleasingTheSpirits = 3
        }

        private static readonly string[] Captions =
        {
            "INITIALISING SYSTEMS",
            "LOADING ENVIRONMENT",
            "CALIBRATING EQUIPMENT",
            "RELEASING THE SPIRITS"
        };

        [Tooltip("How long a step stays on screen at the very least, in seconds. Some phases " +
                 "finish in milliseconds and a caption nobody can read is not a caption. This " +
                 "only ever HOLDS a step longer; it never marks one done early.")]
        [SerializeField, Range(0.05f, 1.5f)] private float minimumStepTime = 0.34f;

        [Tooltip("How long the finished readout lingers before it is dismissed.")]
        [SerializeField, Range(0f, 1.5f)] private float finishedHold = 0.25f;

        private static MissionBootSequence _instance;

        private Component[] _rows;
        private RectTransform _progressFill;
        private Component _statusLine;
        private CanvasGroup _group;
        private int _current;
        private float _stepShownAt;

        /// <summary>The live sequence, or null when none has been raised.</summary>
        public static MissionBootSequence Instance => _instance;

        /// <summary>
        /// Raises the readout, building it on first use.
        ///
        /// <para>
        /// GetComponent-before-build and a static instance, so a second press of PLAY - or a
        /// second route into the handover - cannot stack two readouts on top of each other
        /// (mistakes 27 and 30, in a menu).
        /// </para>
        /// </summary>
        public static MissionBootSequence Ensure()
        {
            if (_instance != null)
                return _instance;

            var go = new GameObject("CIYC_MissionBootSequence");
            DontDestroyOnLoad(go);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _instance = go.AddComponent<MissionBootSequence>();
            _instance.Build(go.transform);
            return _instance;
        }

        private void Build(Transform root)
        {
            _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            // Nothing here is clickable, and a readout that eats a touch would be a readout that
            // can trap the player on a black screen.
            _group.blocksRaycasts = false;
            _group.interactable = false;

            var column = new GameObject("Steps", typeof(RectTransform));
            column.transform.SetParent(root, false);
            var columnRect = column.GetComponent<RectTransform>();
            columnRect.anchorMin = new Vector2(0.08f, 0.16f);
            columnRect.anchorMax = new Vector2(0.62f, 0.46f);
            columnRect.offsetMin = Vector2.zero;
            columnRect.offsetMax = Vector2.zero;

            _rows = new Component[Captions.Length];
            for (int i = 0; i < Captions.Length; i++)
            {
                Component text = RuntimeUIFactory.CreateText(
                    column.transform, "Step_" + i, Captions[i], 30,
                    TextAnchor.MiddleLeft, false, UITheme.FontRole.Body);

                if (text == null)
                    continue;

                _rows[i] = text;
                UITheme.SetTextColor(text, UITheme.TextDisabled);

                var rect = text.transform as RectTransform;
                if (rect == null)
                    continue;

                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.sizeDelta = new Vector2(0f, 46f);
                rect.anchoredPosition = new Vector2(0f, -i * 54f);
            }

            // A rule rather than a bar: it is drawn from the number of steps that are actually
            // finished, so it cannot show a fraction the captions disagree with.
            var track = new GameObject("ProgressTrack", typeof(RectTransform));
            track.transform.SetParent(root, false);
            var trackRect = track.GetComponent<RectTransform>();
            trackRect.anchorMin = new Vector2(0.08f, 0.125f);
            trackRect.anchorMax = new Vector2(0.62f, 0.125f);
            trackRect.pivot = new Vector2(0.5f, 0.5f);
            trackRect.sizeDelta = new Vector2(0f, 2f);
            trackRect.anchoredPosition = Vector2.zero;
            var trackImage = track.AddComponent<Image>();
            trackImage.color = UITheme.Border;
            trackImage.raycastTarget = false;

            var fill = new GameObject("ProgressFill", typeof(RectTransform));
            fill.transform.SetParent(track.transform, false);
            _progressFill = fill.GetComponent<RectTransform>();
            _progressFill.anchorMin = new Vector2(0f, 0f);
            _progressFill.anchorMax = new Vector2(0f, 1f);
            _progressFill.pivot = new Vector2(0f, 0.5f);
            _progressFill.offsetMin = Vector2.zero;
            _progressFill.offsetMax = Vector2.zero;
            var fillImage = fill.AddComponent<Image>();
            fillImage.color = UITheme.Primary;
            fillImage.raycastTarget = false;

            Component status = RuntimeUIFactory.CreateText(
                root, "Status", string.Empty, 22,
                TextAnchor.MiddleLeft, false, UITheme.FontRole.Body);

            if (status != null)
            {
                _statusLine = status;
                UITheme.SetTextColor(status, UITheme.TextMuted);
                var rect = status.transform as RectTransform;
                if (rect != null)
                {
                    rect.anchorMin = new Vector2(0.08f, 0.075f);
                    rect.anchorMax = new Vector2(0.62f, 0.11f);
                    rect.offsetMin = Vector2.zero;
                    rect.offsetMax = Vector2.zero;
                }
            }
        }

        /// <summary>Raises the readout with the first step already running.</summary>
        public void Begin()
        {
            _current = 0;
            _stepShownAt = Time.unscaledTime;

            for (int i = 0; i < _rows.Length; i++)
                Paint(i);

            SetProgress(0);
            SetStatus(Captions.Length + " steps");

            if (_group != null)
                _group.alpha = 1f;
        }

        /// <summary>
        /// Marks <paramref name="step"/> finished and moves on, holding it visible long enough to
        /// be read.
        ///
        /// <para>
        /// The step argument is checked rather than trusted. A caller that reports a step out of
        /// order is reporting about a handover this readout is not watching, and quietly accepting
        /// it would make the two disagree in exactly the way a progress readout must not.
        /// </para>
        /// </summary>
        public IEnumerator Advance(Step step)
        {
            int index = (int)step;

            if (index != _current)
            {
                Core.CIYCLog.Warn("[CIYC][BOOT] step " + step + " reported while step " +
                                     (_current < Captions.Length ? Captions[_current] : "(none)") +
                                     " was running. The readout follows the handover; it does " +
                                     "not lead it.");
                _current = Mathf.Clamp(index, 0, Captions.Length - 1);
            }

            float held = Time.unscaledTime - _stepShownAt;
            if (held < minimumStepTime)
                yield return new WaitForSecondsRealtime(minimumStepTime - held);

            _current = Mathf.Min(_current + 1, Captions.Length);
            _stepShownAt = Time.unscaledTime;

            for (int i = 0; i < _rows.Length; i++)
                Paint(i);

            SetProgress(_current);
            SetStatus(_current >= Captions.Length
                ? "READY"
                : Captions.Length - _current + " remaining");
        }

        /// <summary>Finishes the readout and takes it off the screen.</summary>
        public IEnumerator Complete()
        {
            _current = Captions.Length;
            for (int i = 0; i < _rows.Length; i++)
                Paint(i);

            SetProgress(Captions.Length);
            SetStatus("READY");

            if (finishedHold > 0f)
                yield return new WaitForSecondsRealtime(finishedHold);

            Hide();
        }

        /// <summary>
        /// Takes the readout off the screen immediately.
        ///
        /// <para>
        /// Public and separate from <see cref="Complete"/> on purpose: whoever raises this must be
        /// able to clear it from a failure path too. A readout left on a black screen because the
        /// handover took a branch nobody thought about is worse than no readout at all.
        /// </para>
        /// </summary>
        public void Hide()
        {
            if (_group != null)
                _group.alpha = 0f;
        }

        private void Paint(int index)
        {
            if (_rows == null || index >= _rows.Length || _rows[index] == null)
                return;

            Color colour;
            string prefix;

            if (index < _current)
            {
                colour = UITheme.Primary;
                prefix = "[ OK ]  ";
            }
            else if (index == _current)
            {
                colour = UITheme.TextPrimary;
                prefix = "[ >> ]  ";
            }
            else
            {
                colour = UITheme.TextDisabled;
                prefix = "[    ]  ";
            }

            UITheme.SetTextColor(_rows[index], colour);
            UITheme.SetText(_rows[index], prefix + Captions[index]);
        }

        private void SetProgress(int done)
        {
            if (_progressFill == null)
                return;

            float t = Captions.Length == 0 ? 1f : Mathf.Clamp01(done / (float)Captions.Length);
            _progressFill.anchorMax = new Vector2(t, 1f);
            _progressFill.offsetMin = Vector2.zero;
            _progressFill.offsetMax = Vector2.zero;
        }

        private void SetStatus(string value)
        {
            if (_statusLine != null)
                UITheme.SetText(_statusLine, value);
        }
    }
}
