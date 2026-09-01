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
    /// instead: the phone event and the red room event are peers, either can be picked, and the
    /// wait before each is rolled fresh so the menu never falls into an audible rhythm.
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
        private IMainMenuHorrorEvent _previous;
        private Coroutine _loop;

        /// <summary>True while any event this director owns is mid-flight.</summary>
        public bool IsEventRunning { get; private set; }

        private void Awake()
        {
            // Concrete fields rather than an interface array: Unity does not serialize
            // interfaces, and two explicit slots are clearer in the Inspector than a list of
            // MonoBehaviours that may or may not implement the right thing.
            if (phoneEvent != null) _events.Add(phoneEvent);
            if (redEvent != null) _events.Add(redEvent);
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
                yield return new WaitForSeconds(Random.Range(low, high));

                // A second guard: the intro can only run once, but the menu scene may be
                // reloaded, and an event must never start while anything is covering the view.
                while (CatchIfYouCan.UI.StartupIntroVideo.IsIntroPlaying)
                    yield return null;

                if (IsEventRunning || AnyEventPlaying())
                    continue;

                var picked = Pick();
                if (picked == null)
                    continue;

                IsEventRunning = true;
                if (!picked.TryBegin())
                {
                    // Declined — it may have been disabled since. Try again after the next wait
                    // rather than hammering it.
                    IsEventRunning = false;
                    continue;
                }

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
        /// Picks an event at random, skipping the one that just played when there is a choice.
        /// With a single event configured it simply returns that one, so the menu still has a
        /// beat rather than going silent.
        /// </summary>
        private IMainMenuHorrorEvent Pick()
        {
            if (_events.Count == 0)
                return null;

            if (_events.Count == 1 || !preventImmediateRepeat || _previous == null)
                return _events[Random.Range(0, _events.Count)];

            // Choose among everything except the previous pick. Indexing past it rather than
            // re-rolling keeps this allocation free and always terminates.
            int index = Random.Range(0, _events.Count - 1);
            for (int i = 0; i < _events.Count; i++)
            {
                if (ReferenceEquals(_events[i], _previous))
                    continue;
                if (index == 0)
                    return _events[i];
                index--;
            }

            return _events[Random.Range(0, _events.Count)];
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

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (maxEventInterval < minEventInterval)
                maxEventInterval = minEventInterval;
        }
#endif
    }
}
