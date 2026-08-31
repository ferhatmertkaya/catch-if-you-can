using System.Collections;
using CatchIfYouCan.Art;
using UnityEngine;

public class RotaryPhoneRandomRing : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("Optional. When set, each ring sequence offers this event a chance to escalate " +
             "into the red horror sequence. Leave empty and the phone just rings.")]
    [SerializeField] private MainMenuPhoneHorrorEvent horrorEvent;

    [Header("Random delay")]
    [SerializeField] private float minDelay = 15f;
    [SerializeField] private float maxDelay = 40f;

    [Header("Testing")]
    [Tooltip("Shortens the wait between rings so the event can be seen without waiting. " +
             "Editor convenience only; leave off for the real cadence.")]
    [SerializeField] private bool debugFastEvents;
    [SerializeField] private float debugMinDelay = 5f;
    [SerializeField] private float debugMaxDelay = 10f;

    [Header("Ring sequence")]
    [SerializeField] private int minRings = 2;
    [SerializeField] private int maxRings = 4;
    [SerializeField] private float pauseBetweenRings = 1.1f;

    [Header("Variation")]
    [SerializeField] private float minPitch = 0.96f;
    [SerializeField] private float maxPitch = 1.03f;
    [SerializeField] private float minVolume = 0.65f;
    [SerializeField] private float maxVolume = 0.85f;

    private Coroutine ringRoutine;

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            audioSource.loop = false;
        }
    }

    private void OnEnable()
    {
        ringRoutine = StartCoroutine(RandomRingLoop());
    }

    private void OnDisable()
    {
        if (ringRoutine != null)
        {
            StopCoroutine(ringRoutine);
            ringRoutine = null;
        }

        if (audioSource != null)
            audioSource.Stop();
    }

    private IEnumerator RandomRingLoop()
    {
        while (true)
        {
            // The startup intro owns the screen, and a ring behind it would be heard over the
            // video with nothing to see. Waiting here rather than stopping and restarting the
            // loop keeps this the one and only scheduler. Because the wait comes before the
            // delay roll, the full delay is counted from the moment the menu is actually
            // visible, so a ring cannot land on the last frame of the reveal.
            while (CatchIfYouCan.UI.StartupIntroVideo.IsIntroPlaying)
                yield return null;

            float delay = debugFastEvents
                ? Random.Range(debugMinDelay, debugMaxDelay)
                : Random.Range(minDelay, maxDelay);
            yield return new WaitForSeconds(delay);

            if (audioSource == null || audioSource.clip == null)
                continue;

            int ringCount = Random.Range(minRings, maxRings + 1);

            // Offer the visual event a chance to escalate this ring. It answers false most of
            // the time, and the phone rings exactly as it always has. When it answers true it
            // takes over the ending: it cuts the audio at its climax, and the loop below stops
            // issuing rings at that moment rather than ringing through the blackout.
            bool escalated = horrorEvent != null && horrorEvent.TryBegin();

            for (int i = 0; i < ringCount; i++)
            {
                if (escalated && !horrorEvent.PhoneShouldKeepRinging)
                    break;

                audioSource.pitch = Random.Range(minPitch, maxPitch);
                audioSource.volume = Random.Range(minVolume, maxVolume);

                audioSource.Play();

                yield return new WaitForSeconds(audioSource.clip.length + pauseBetweenRings);
            }

            // One scheduler owns the cadence: don't queue the next ring until the visual
            // event has finished restoring the scene.
            while (escalated && horrorEvent.IsPlaying)
                yield return null;
        }
    }
}
