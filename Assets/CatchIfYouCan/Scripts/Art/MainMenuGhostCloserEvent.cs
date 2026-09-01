using System.Collections;
using UnityEngine;

namespace CatchIfYouCan.Art
{
    /// <summary>
    /// The ghost gets closer, but is never seen moving.
    ///
    /// <para>
    /// The corridor lights stutter and drop to black; while the frame is dark the ghost is
    /// simply somewhere else; the lights come back and it is nearer than it was. No walk cycle,
    /// no slide, no interpolation — the whole effect is that the viewer's own eyes missed it.
    /// After the last step the same trick puts it back exactly where it started.
    /// </para>
    ///
    /// <para>
    /// The ghost is not moved by writing its transform. <see cref="MenuGhostFloat"/> is the one
    /// writer of that transform and this asks it for an offset instead, the same way the events
    /// ask <see cref="CandleFlicker"/> for a flicker rather than writing the candle light. The
    /// float keeps breathing on top of the offset, so the ghost is still alive while it stands
    /// closer, and because the offset is assigned rather than accumulated the ghost lands on the
    /// identical base position after one run or a hundred.
    /// </para>
    ///
    /// <para>
    /// Nothing here touches the fog: it stays exactly at its normal menu state for the whole
    /// event, and the ghost arrives through it. Nothing here touches the red lights either —
    /// this is a green-lit event, and the darkness is the only thing that changes.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Catch If You Can/Main Menu Ghost Closer Event")]
    public sealed class MainMenuGhostCloserEvent : MonoBehaviour, IMainMenuHorrorEvent
    {
        [Header("References")]
        [Tooltip("The ghost's float component. The offset goes through it so there is still " +
                 "only one writer of the ghost transform.")]
        [SerializeField] private MenuGhostFloat ghostFloat;

        [Tooltip("The menu camera the ghost approaches. Falls back to Camera.main.")]
        [SerializeField] private Camera menuCamera;

        [Tooltip("The normal environmental lights. Driven to near black for the teleport frames " +
                 "and restored exactly. The doorway lights below are deliberately NOT in here.")]
        [SerializeField] private Light[] dimmedLights = new Light[0];

        [Tooltip("The doorway lights. These are never taken all the way down, so the doorway " +
                 "still reads and the ghost keeps a silhouette to be seen against.")]
        [SerializeField] private Light[] doorwayLights = new Light[0];

        [Tooltip("The candle light's flicker component. The existing strong-flicker API.")]
        [SerializeField] private CandleFlicker candleFlicker;

        [Header("Ghost Closer Event")]
        [SerializeField] private bool enableGhostCloserEvent = true;

        [Tooltip("How much nearer the ghost gets on each step, in metres.")]
        [SerializeField, Min(0f)] private float ghostCloserStepDistance = 0.30f;

        [Tooltip("How many times it steps closer before returning.")]
        [SerializeField, Min(1)] private int ghostCloserSteps = 2;

        [Tooltip("Quiet beat before the first flicker, so the event does not start on top of " +
                 "whatever the player was just looking at.")]
        [SerializeField, Min(0f)] private float ghostCloserInitialDelay = 0.5f;

        [Tooltip("How long the ghost stands at the first closer position.")]
        [SerializeField, Min(0f)] private float ghostCloserFirstHold = 1.0f;

        [Tooltip("How long it stands at every position after the first.")]
        [SerializeField, Min(0f)] private float ghostCloserSecondHold = 1.2f;

        [Tooltip("How long the frame stays black after the ghost has been moved.")]
        [SerializeField, Min(0.02f)] private float ghostCloserDarkDuration = 0.15f;

        [Tooltip("How long the lights stutter before each blackout.")]
        [SerializeField, Min(0f)] private float ghostCloserFlickerDuration = 0.5f;

        [Tooltip("Pause after the ghost is back where it started, before the event ends.")]
        [SerializeField, Min(0f)] private float ghostCloserFinalHold = 0.4f;

        [Tooltip("The ghost is never allowed nearer than this to the camera, however large the " +
                 "step distance is set. Steps are clamped, not skipped.")]
        [SerializeField, Min(0.1f)] private float ghostCloserMinimumCameraDistance = 0.65f;

        [Tooltip("Shortest gap between two Ghost Closer events. The director may pick it sooner; " +
                 "it declines until this has passed, which is what keeps it the rarer beat.")]
        [SerializeField, Min(0f)] private float ghostCloserCooldown = 60f;

        [Header("Darkness and reveal")]
        [Tooltip("Light level during the blackout, as a fraction of authored. Near zero.")]
        [SerializeField, Range(0f, 0.2f)] private float ghostCloserDarkLevel = 0.02f;

        [Tooltip("Floor the doorway lights hold during the VISIBLE part of the event, as a " +
                 "fraction of authored. This is what keeps a doorway to see and a silhouette " +
                 "to see against it while the lights stutter.")]
        [SerializeField, Range(0f, 0.6f)] private float doorwayDarkLevel = 0.12f;

        [Tooltip("Doorway level during the teleport frame itself. This one has to be near zero: " +
                 "the doorway is directly behind the ghost, so any light left here is exactly " +
                 "the light that would show the ghost jumping.")]
        [SerializeField, Range(0f, 0.1f)] private float teleportDoorwayLevel = 0.02f;

        [Tooltip("Light level the reveal comes back to. 1 is the normal menu. A touch above 1 " +
                 "makes the closer face readable without lighting the corridor up.")]
        [SerializeField, Range(0.5f, 2f)] private float ghostCloserRevealLevel = 1f;

        [Tooltip("How long the lights take to come back after a blackout.")]
        [SerializeField, Min(0f)] private float ghostCloserRevealDuration = 0.12f;

        [Header("Candle")]
        [Tooltip("Candle intensity scale while the event runs.")]
        [SerializeField] private Vector2 candleEventIntensity = new Vector2(0.25f, 0.55f);

        [Tooltip("How much wider the candle's flicker swings during the event.")]
        [SerializeField, Range(1f, 6f)] private float candleTurbulence = 4f;

        [Tooltip("Candle level during the teleport frame only. The candle is the last thing " +
                 "still lit when everything else is out, so it drops too for that instant and " +
                 "comes straight back. CandleFlicker still owns the intensity; this is asked " +
                 "for through its event API, not written directly.")]
        [SerializeField, Range(0f, 0.1f)] private float candleBlackoutIntensity = 0.03f;

        [Header("Debug")]
        [SerializeField] private bool logEvents;

        // ---- captured once, never written back ------------------------------------------
        private float[] _dimBaseline;
        private float[] _doorwayBaseline;
        private Coroutine _routine;
        private bool _ready;
        private float _lastFinishedTime = -99999f;
        private float _candleFloor = 1f;

        // Cached so the per-blackout wait does not allocate.
        private readonly WaitForEndOfFrame _endOfFrame = new WaitForEndOfFrame();

        public bool IsPlaying => _routine != null;

        /// <summary>Name shown in the director's log line.</summary>
        public string EventName => "GhostCloser";

        /// <summary>
        /// Adds the cooldown to the usual checks, so the director can leave this one out of the
        /// draw entirely while it is counting down instead of picking it and being refused.
        /// </summary>
        public bool IsAvailable =>
            _ready && enableGhostCloserEvent && isActiveAndEnabled && !IsPlaying
            && ghostFloat != null && menuCamera != null
            && Time.time - _lastFinishedTime >= ghostCloserCooldown;

        private void Awake()
        {
            _dimBaseline = new float[dimmedLights.Length];
            for (int i = 0; i < dimmedLights.Length; i++)
                if (dimmedLights[i] != null)
                    _dimBaseline[i] = dimmedLights[i].intensity;

            _doorwayBaseline = new float[doorwayLights.Length];
            for (int i = 0; i < doorwayLights.Length; i++)
                if (doorwayLights[i] != null)
                    _doorwayBaseline[i] = doorwayLights[i].intensity;

            if (menuCamera == null)
                menuCamera = Camera.main;

            _ready = true;
        }

        private void OnDisable() => CancelAndRestore();

        /// <summary>
        /// Starts the event unless one is already running, it is switched off, or it ran too
        /// recently. Declining on cooldown is normal: the director simply waits and rolls again,
        /// which is what makes this the rarer of the three beats without needing event weights.
        /// </summary>
        public bool TryBegin()
        {
            if (!_ready || !enableGhostCloserEvent || !isActiveAndEnabled || IsPlaying)
                return false;

            if (ghostFloat == null || menuCamera == null)
                return false;

            if (Time.time - _lastFinishedTime < ghostCloserCooldown)
                return false;

            _routine = StartCoroutine(RunEvent());
            return true;
        }

        /// <summary>Starts the event regardless of the cooldown. For testing.</summary>
        [ContextMenu("Force Ghost Closer Event")]
        public void ForceBegin()
        {
            if (!_ready || IsPlaying || ghostFloat == null)
                return;

            _routine = StartCoroutine(RunEvent());
        }

        public void CancelAndRestore()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            RestoreBaselines();
        }

        private void RestoreBaselines()
        {
            if (!_ready)
                return;

            // The ghost goes home first: an interrupted event must never leave it standing
            // closer to the camera than it was authored.
            if (ghostFloat != null)
                ghostFloat.ClearEventOffset();

            for (int i = 0; i < dimmedLights.Length; i++)
                if (dimmedLights[i] != null)
                    dimmedLights[i].intensity = _dimBaseline[i];

            for (int i = 0; i < doorwayLights.Length; i++)
                if (doorwayLights[i] != null)
                    doorwayLights[i].intensity = _doorwayBaseline[i];

            if (candleFlicker != null)
                candleFlicker.ClearEventModulation();
        }

        // ---- the event ------------------------------------------------------------------

        private IEnumerator RunEvent()
        {
            if (logEvents)
                Debug.Log("[CIYC] Ghost closer event: begin", this);

            _candleFloor = Random.Range(candleEventIntensity.x, candleEventIntensity.y);

            // Direction is measured from where the ghost *rests*, not from the live transform,
            // which is a centimetre or two off at any moment because of the float.
            Vector3 basePosition = ghostFloat.BaseWorldPosition;
            Vector3 toCamera = menuCamera.transform.position - basePosition;

            // Flattened: the ghost closes the distance across the floor, it does not rise
            // toward a camera that happens to sit higher than it.
            toCamera.y = 0f;

            float distance = toCamera.magnitude;
            if (distance < 0.0001f)
            {
                // Degenerate setup — nowhere to move to. Bail out rather than divide by zero.
                RestoreBaselines();
                _routine = null;
                yield break;
            }

            Vector3 direction = toCamera / distance;

            // However the step distance is set later, the ghost may never come nearer than this.
            float maxTravel = Mathf.Max(0f, distance - ghostCloserMinimumCameraDistance);

            yield return Wait(ghostCloserInitialDelay);

            // The candles pick up their strong flicker for the whole event.
            ApplyCandle(_candleFloor);

            for (int step = 1; step <= ghostCloserSteps; step++)
            {
                float travel = Mathf.Min(ghostCloserStepDistance * step, maxTravel);
                yield return FlickerThenMove(direction * travel, ghostCloserFlickerDuration);
                yield return Wait(step == 1 ? ghostCloserFirstHold : ghostCloserSecondHold);
            }

            // The last flicker runs a little longer, then the ghost is simply back.
            yield return FlickerThenMove(Vector3.zero, ghostCloserFlickerDuration * 1.35f);
            yield return Wait(ghostCloserFinalHold);

            RestoreBaselines();
            _lastFinishedTime = Time.time;
            _routine = null;

            if (logEvents)
                Debug.Log("[CIYC] Ghost closer event: restored", this);
        }

        /// <summary>
        /// One beat of the illusion: stutter, go black, move the ghost only once the black
        /// frame is genuinely on screen, then bring the light back.
        /// </summary>
        private IEnumerator FlickerThenMove(Vector3 worldOffset, float flickerDuration)
        {
            yield return Flicker(flickerDuration);

            // Everything goes, doorway and candle included. The doorway keeps a floor during
            // the visible stutter so there is a silhouette to read, but that floor is exactly
            // what would betray the jump, so for this one frame it goes with the rest.
            SetLightFactor(ghostCloserDarkLevel, teleportDoorwayLevel);
            ApplyCandle(candleBlackoutIntensity);

            // This is the part that has to be right. Waiting for the end of the frame means the
            // dark frame has actually been rendered and presented before anything moves; moving
            // first and darkening after would let the viewer catch the ghost snapping.
            yield return _endOfFrame;

            ghostFloat.SetEventWorldOffset(worldOffset);

            // Still black while the new position is taken up, so the change is never on screen.
            yield return Wait(ghostCloserDarkDuration);

            yield return Reveal();

            // Back to the event's normal candle level for the visible hold.
            ApplyCandle(_candleFloor);
        }

        /// <summary>
        /// Irregular stutter. Each on and off segment is rolled separately so it never reads as
        /// a metronome; the light also never returns to quite the same brightness twice.
        /// </summary>
        private IEnumerator Flicker(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                // The bright step is never quite the same twice, and the dark step goes properly
                // dark rather than merely dim — a stutter between 100% and 20% reads as a fade,
                // not as failing wiring.
                float onTime = Random.Range(0.04f, 0.14f);
                SetLightFactor(Random.Range(0.45f, 1f));
                yield return Wait(onTime);

                float offTime = Random.Range(0.03f, 0.12f);
                SetLightFactor(Random.Range(0.01f, 0.08f));
                yield return Wait(offTime);

                elapsed += onTime + offTime;
            }
        }

        private IEnumerator Reveal()
        {
            float d = Mathf.Max(0.01f, ghostCloserRevealDuration);
            for (float e = 0f; e < d; e += Time.deltaTime)
            {
                float k = e / d;
                SetLightFactor(Mathf.Lerp(ghostCloserDarkLevel, ghostCloserRevealLevel, k * k));
                yield return null;
            }
            SetLightFactor(ghostCloserRevealLevel);
        }

        // ---- helpers --------------------------------------------------------------------

        /// <summary>Allocation-free wait; WaitForSeconds would allocate on every flicker segment.</summary>
        private IEnumerator Wait(float seconds)
        {
            for (float e = 0f; e < seconds; e += Time.deltaTime)
                yield return null;
        }

        /// <summary>
        /// Always the captured baseline times the factor, never the running value, so repeated
        /// flicker segments cannot walk the corridor's brightness down.
        /// </summary>
        private void SetLightFactor(float factor) => SetLightFactor(factor, doorwayDarkLevel);

        /// <summary>
        /// Drives every controlled light from its captured baseline.
        /// <paramref name="doorwayFloor"/> is what the doorway lights are not allowed to fall
        /// below — the visible-phase floor normally, and a near-zero one for the teleport frame.
        /// </summary>
        private void SetLightFactor(float factor, float doorwayFloor)
        {
            float f = Mathf.Max(0f, factor);
            for (int i = 0; i < dimmedLights.Length; i++)
                if (dimmedLights[i] != null)
                    dimmedLights[i].intensity = _dimBaseline[i] * f;

            float doorway = Mathf.Max(f, doorwayFloor);
            for (int i = 0; i < doorwayLights.Length; i++)
                if (doorwayLights[i] != null)
                    doorwayLights[i].intensity = _doorwayBaseline[i] * doorway;
        }

        private void ApplyCandle(float intensityScale)
        {
            if (candleFlicker != null)
                candleFlicker.ApplyEventModulation(intensityScale, candleTurbulence);
        }
    }
}
