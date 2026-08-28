using CatchIfYouCan.Interaction;
using UnityEngine;

namespace CatchIfYouCan.Audio
{
    /// <summary>
    /// Physically motivated hide acoustics: low-pass outside world, louder breath,
    /// floor-transmitted steps emphasized when under bed.
    /// </summary>
    [RequireComponent(typeof(HideSpot))]
    public class HideSpotAudio : MonoBehaviour
    {
        [SerializeField] private HideAcousticsMode mode = HideAcousticsMode.Wardrobe;
        [SerializeField] private float exteriorLowPassHz = 900f;
        [SerializeField] private float underBedLowPassHz = 1400f;

        private HideSpot _hide;
        private AudioLowPassFilter _listenerFilter;
        private float _defaultCutoff = 22000f;
        private bool _wasHidden;

        private void Awake()
        {
            _hide = GetComponent<HideSpot>();
        }

        private void Update()
        {
            bool hidden = _hide != null && _hide.PlayerHidden;
            if (hidden == _wasHidden)
                return;

            _wasHidden = hidden;
            EnsureListenerFilter();

            if (_listenerFilter == null)
                return;

            if (hidden)
            {
                _defaultCutoff = _listenerFilter.cutoffFrequency;
                _listenerFilter.cutoffFrequency = mode == HideAcousticsMode.UnderBed
                    ? underBedLowPassHz
                    : exteriorLowPassHz;
                AudioManager.Instance?.PlayEvent("Player.Hide.Enter", transform.position, 0.35f);
            }
            else
            {
                _listenerFilter.cutoffFrequency = _defaultCutoff;
                AudioManager.Instance?.PlayEvent("Player.Hide.Exit", transform.position, 0.3f);
            }
        }

        private void EnsureListenerFilter()
        {
            if (_listenerFilter != null)
                return;

            var listener = FindFirstObjectByType<AudioListener>();
            if (listener == null)
                return;

            _listenerFilter = listener.GetComponent<AudioLowPassFilter>();
            if (_listenerFilter == null)
                _listenerFilter = listener.gameObject.AddComponent<AudioLowPassFilter>();
            _defaultCutoff = _listenerFilter.cutoffFrequency;
        }
    }

    public enum HideAcousticsMode
    {
        Wardrobe,
        UnderBed,
        Curtain,
        Other
    }
}
