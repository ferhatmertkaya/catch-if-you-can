using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CatchIfYouCan.Art
{
    /// <summary>
    /// Decides which supernatural beat the main menu plays next, and when.
    ///
    /// <para>
    /// The phone used to schedule itself and drag the red lighting along with it, so the menu
    /// only ever had one thing to show and it always arrived the same way. Scheduling lives here
    /// instead: the phone, red room and ghost closer events are peers, any can be picked, and
    /// the wait before each is rolled fresh so the menu never falls into an audible rhythm.
    /// </para>
    ///
    /// <para>
    /// One beat at a time. An event is asked to begin only when nothing else is running, and the
    /// director then waits for it to finish restoring the scene before it starts counting toward
    /// the next one. That is what keeps two events from writing the same lights at once.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Catch If You Can/Main Menu Horror Event Director")]
    public sealed class MainMenuHorrorEventDirector : MonoBehaviour
    {
        [Header("Events (any may be left empty)")]
        [SerializeField] private MainMenuPhoneHorrorEvent phoneEvent;
        [SerializeField] private MainMenuRedRoomEvent redEvent;
        [SerializeField] private MainMenuGhostCloserEvent ghostCloserEvent;

        [Header("Random Horror Events")]
        [Tooltip("Shortest wait between one event finishing and the next beginning.")]
        [SerializeField, Min(0f)] private float minEventInterval = 18f;

        [Tooltip("Longest wait between one event finishing and the next beginning.")]
        [SerializeField, Min(0f)] private float maxEventInterval = 45f;

        [Tooltip("Avoid picking the event that just played, when there is another to pick.")]
        [SerializeField] private bool preventImmediateRepeat = true;

        [Tooltip("Extra wait after the menu first appears, before the first event.")]
        [SerializeField, Min(0f)] private float firstEventDelay = 12f;

        [Header("Debug")]
        [Tooltip("One line per event chosen. No per-frame logging.")]
        [SerializeField] private bool logEvents;

        private readonly List<IMainMenuHorrorEvent> _events = new List<IMainMenuHorrorEvent>();
        private readonly List<IMainMenuHorrorEvent> _eligible = new List<IMainMenuHorrorEvent>();
        private IMainMenuHorrorEvent _previous;
        private Coroutine _loop;

        // The menu draws from its own stream rather than the process-wide UnityEngine.Random,
        // which every cosmetic system in the project also draws from. Whose turn it is in the
        // menu should not depend on how many times a flicker or a footstep happened to draw
        // first, and the project already treats that shared stream as something cosmetic code
        // must not lean on for meaningful choices.
        private System.Random _rng;

        /// <summary>True while any event this director owns is mid-flight.</summary>
        public bool IsEventRunning { get; private set; }

        private void Awake()
        {
            // Concrete fields rather than an interface array: Unity does not serialize
            // interfaces, and two explicit slots are clearer in the Inspector than a list of
            // MonoBehaviours that may or may not implement the right thing.
            if (phoneEvent != null) _events.Add(phoneEvent);
            if (redEvent != null) _events.Add(redEvent);
            // Ghost Closer carries its own cooldown and simply declines when it is too soon,
            // which makes it the rarer beat without needing per-event weights here.
            if (ghostCloserEvent != null) _events.Add(ghostCloserEvent);

            _rng = new System.Random(
                unchecked((int)System.DateTime.UtcNow.Ticks) ^ (GetInstanceID() * 397));
        }

        private void OnEnable()
        {
            if (_events.Count > 0)
                _loop = StartCoroutine(EventLoop());
        }

        private void OnDisable()
        {
            if (_loop != null)
            {
                StopCoroutine(_loop);
                _loop = null;
            }

            // Whatever was running gets put back; the events restore from their own captured
            // baselines, so this is safe even part way through a fade.
            for (int i = 0; i < _events.Count; i++)
                _events[i].CancelAndRestore();

            IsEventRunning = false;
        }

        private IEnumerator EventLoop()
        {
            // The startup intro owns the screen first. Nothing may fire behind it.
            while (CatchIfYouCan.UI.StartupIntroVideo.IsIntroPlaying)
                yield return null;

            yield return new WaitForSeconds(firstEventDelay);

            while (true)
            {
                float low = Mathf.Min(minEventInterval, maxEventInterval);
                float high = Mathf.Max(minEventInterval, maxEventInterval);
                yield return new WaitForSeconds(low + (float)_rng.NextDouble() * (high - low));

                // A second guard: the intro can only run once, but the menu scene may be
                // reloaded, and an event must never start while anything is covering the view.
                while (CatchIfYouCan.UI.StartupIntroVideo.IsIntroPlaying)
                    yield return null;

                if (IsEventRunning || AnyEventPlaying())
                    continue;

                BuildEligible();

                // Try the eligible events in a random order until one actually starts. Giving up
                // on the first refusal is what used to cost a whole interval and quietly drop an
                // event out of the rotation.
                IMainMenuHorrorEvent picked = null;
                while (_eligible.Count > 0)
                {
                    int index = _rng.Next(_eligible.Count);
                    var candidate = _eligible[index];
                    _eligible.RemoveAt(index);

                    if (candidate.TryBegin())
                    {
                        picked = candidate;
                        break;
                    }
                }

                if (picked == null)
                    continue;

                IsEventRunning = true;
                _previous = picked;
                if (logEvents)
                    Debug.Log($"[CIYC] Horror event: {picked.EventName}", this);

                // Hold the lock until the event has finished putting the scene back.
                while (picked.IsPlaying)
                    yield return null;

                IsEventRunning = false;
            }
        }

        private bool AnyEventPlaying()
        {
            for (int i = 0; i < _events.Count; i++)
                if (_events[i].IsPlaying)
                    return true;
            return false;
        }

        /// <summary>
        /// Fills <see cref="_eligible"/> with the events that could actually run right now, in
        /// registration order; the caller draws from it at random.
        ///
        /// <para>
        /// Asking availability first is the important part. Picking blind and being refused meant
        /// that whenever one event sat on a cooldown the other two were the only ones that ever
        /// started, and anti-repeat then forced them to alternate — a fixed A, B, A, B order that
        /// looked hard-coded but was really just two survivors with no third choice.
        /// </para>
        ///
        /// <para>
        /// Anti-repeat is applied only while it still leaves something to pick, so the last
        /// available event is never filtered away into silence.
        /// </para>
        /// </summary>
        private void BuildEligible()
        {
            _eligible.Clear();
            for (int i = 0; i < _events.Count; i++)
                if (_events[i].IsAvailable)
                    _eligible.Add(_events[i]);

            if (preventImmediateRepeat && _previous != null && _eligible.Count > 1)
                _eligible.Remove(_previous);
        }

        /// <summary>Runs the phone beat now, ignoring the schedule. For testing.</summary>
        [ContextMenu("Trigger Phone Event")]
        public void TriggerPhoneEvent()
        {
            if (phoneEvent != null && !AnyEventPlaying())
                phoneEvent.TryBegin();
        }

        /// <summary>Runs the red beat now, ignoring the schedule. For testing.</summary>
        [ContextMenu("Trigger Red Event")]
        public void TriggerRedEvent()
        {
            if (redEvent != null && !AnyEventPlaying())
                redEvent.TryBegin();
        }

        /// <summary>Runs the ghost closer beat now, ignoring schedule and cooldown. For testing.</summary>
        [ContextMenu("Trigger Ghost Closer Event")]
        public void TriggerGhostCloserEvent()
        {
            if (ghostCloserEvent != null && !AnyEventPlaying())
                ghostCloserEvent.ForceBegin();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (maxEventInterval < minEventInterval)
                maxEventInterval = minEventInterval;
        }
#endif
    }
}
