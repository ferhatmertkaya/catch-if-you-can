using System.Collections;
using UnityEngine;

public class RotaryPhoneRandomRing : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private AudioSource audioSource;

    [Header("Random delay")]
    [SerializeField] private float minDelay = 15f;
    [SerializeField] private float maxDelay = 40f;

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
            float delay = Random.Range(minDelay, maxDelay);
            yield return new WaitForSeconds(delay);

            if (audioSource == null || audioSource.clip == null)
                continue;

            int ringCount = Random.Range(minRings, maxRings + 1);

            for (int i = 0; i < ringCount; i++)
            {
                audioSource.pitch = Random.Range(minPitch, maxPitch);
                audioSource.volume = Random.Range(minVolume, maxVolume);

                audioSource.Play();

                yield return new WaitForSeconds(audioSource.clip.length + pauseBetweenRings);
            }
        }
    }
}
