using UnityEngine;

/// <summary>
/// Plays a single ring of the rotary phone, with a little pitch and volume variation so
/// repeated rings never sound like one clip looping.
///
/// <para>
/// This used to schedule its own rings and, when it rang, offer the red lighting a chance to
/// escalate. Both jobs have moved out: <see cref="CatchIfYouCan.Art.MainMenuHorrorEventDirector"/>
/// decides when anything happens, and <see cref="CatchIfYouCan.Art.MainMenuPhoneHorrorEvent"/>
/// owns the three-ring sequence. The phone no longer has any connection to the red event — that
/// is now a separate beat the director can choose instead.
/// </para>
///
/// <para>
/// What is left is only the audio: which source, which clip, and how much each ring varies.
/// </para>
/// </summary>
[DisallowMultipleComponent]
public class RotaryPhoneRandomRing : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private AudioSource audioSource;

    [Header("Variation")]
    [Tooltip("Each ring is pitched slightly differently so three in a row sound like a real " +
             "bell rather than the same sample three times.")]
    [SerializeField] private float minPitch = 0.96f;
    [SerializeField] private float maxPitch = 1.03f;
    [SerializeField] private float minVolume = 0.65f;
    [SerializeField] private float maxVolume = 0.85f;

    /// <summary>Length of the assigned ring clip in seconds, or 0 when there is none.</summary>
    public float ClipLength => audioSource != null && audioSource.clip != null
        ? audioSource.clip.length
        : 0f;

    /// <summary>True while a ring is sounding.</summary>
    public bool IsRinging => audioSource != null && audioSource.isPlaying;

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

    private void OnDisable() => StopRinging();

    /// <summary>
    /// Sounds one ring. Caller decides how many and how far apart; nothing here loops.
    /// </summary>
    public void PlayRing()
    {
        if (audioSource == null || audioSource.clip == null)
            return;

        audioSource.pitch = Random.Range(minPitch, maxPitch);
        audioSource.volume = Random.Range(minVolume, maxVolume);
        audioSource.Play();
    }

    /// <summary>Cuts the current ring short. Used when an event is cancelled.</summary>
    public void StopRinging()
    {
        if (audioSource != null)
            audioSource.Stop();
    }
}
