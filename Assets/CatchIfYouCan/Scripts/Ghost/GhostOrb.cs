using System.Collections.Generic;
using UnityEngine;
using CatchIfYouCan.Evidence;

namespace CatchIfYouCan.Ghost
{
    /// <summary>
    /// A drifting mote near the ghost, visible only down a video camera feed.
    ///
    /// <para>
    /// <b>"Camera only" used to mean the opposite of what it says.</b> The camera it resolved
    /// was <c>LocalPlayerService.ResolveViewCamera</c> - the player's own eyes - so an orb
    /// configured as camera-only switched its renderers on precisely when the player was
    /// looking at it with the naked eye, and a video camera had nothing to do with it. It also
    /// tested that once, in Start, and never again, so an orb the player turned away from
    /// stayed lit.
    /// </para>
    ///
    /// <para>
    /// It is now what the flag says: the renderers are off, and a video camera switches them on
    /// for the single frame it renders its feed and off again immediately. Nothing else ever
    /// sees them, which is what makes finding one worth carrying a camera for.
    /// </para>
    /// </summary>
    public class GhostOrb : MonoBehaviour
    {
        [SerializeField] private ParticleSystem orbParticles;
        [SerializeField] private bool cameraOnlyVisibility;
        [SerializeField] private float driftSpeed = 0.4f;
        [SerializeField] private float lifetime = 12f;

        private float _spawnTime;
        private Renderer[] _renderers;

        /// <summary>
        /// Every orb currently drifting. A camera about to render its feed needs all of them
        /// and there is normally none; the alternative is a scene sweep per rendered frame,
        /// which is the cost phases W and Y removed everywhere else.
        /// </summary>
        private static readonly List<GhostOrb> Alive = new List<GhostOrb>();

        /// <summary>Read-only view. Do not hold onto it across frames.</summary>
        public static IReadOnlyList<GhostOrb> All => Alive;

        /// <summary>Whether this one is only visible through a feed.</summary>
        public bool CameraOnly => cameraOnlyVisibility;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => Alive.Clear();

        private void OnEnable()
        {
            if (!Alive.Contains(this))
                Alive.Add(this);
        }

        private void OnDisable()
        {
            Alive.Remove(this);
        }

        private void Awake()
        {
            if (orbParticles == null)
                orbParticles = GetComponentInChildren<ParticleSystem>();

            _renderers = GetComponentsInChildren<Renderer>(true);
            _spawnTime = Time.time;
        }

        private void Start()
        {
            ApplyVisibilityMode();
        }

        private void Update()
        {
            transform.position += Vector3.up * Mathf.Sin(Time.time * 2f) * driftSpeed * Time.deltaTime;

            if (lifetime > 0f && Time.time - _spawnTime >= lifetime)
                Destroy(gameObject);
        }

        public void Configure(EvidenceType evidenceType, float scale, bool camOnly)
        {
            cameraOnlyVisibility = camOnly;
            transform.localScale = Vector3.one * scale;

            if (orbParticles != null)
            {
                var main = orbParticles.main;
                main.startColor = GetColorForEvidence(evidenceType);
                orbParticles.Play();
            }

            ApplyVisibilityMode();
        }

        /// <summary>
        /// Shows the orb to one camera for the duration of one render, and turns it to face
        /// that camera so a flat mote reads as a mote.
        ///
        /// <para>
        /// Called immediately before and after a single <c>Camera.Render</c>, so no other
        /// camera - the player's included - is ever drawing while this is on.
        /// </para>
        /// </summary>
        public static void SetVisibleTo(Camera viewer, bool visible)
        {
            for (int i = 0; i < Alive.Count; i++)
            {
                var orb = Alive[i];
                if (orb == null || !orb.cameraOnlyVisibility)
                    continue;

                if (visible && viewer != null)
                    orb.transform.LookAt(viewer.transform.position);

                orb.SetRenderersEnabled(visible);
            }
        }

        private void ApplyVisibilityMode()
        {
            // Camera-only starts hidden and stays hidden until a feed asks for it. An orb that
            // is not camera-only is just a mote in the room and is simply visible.
            SetRenderersEnabled(!cameraOnlyVisibility);
        }

        private void SetRenderersEnabled(bool enabled)
        {
            if (_renderers == null) return;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                    _renderers[i].enabled = enabled;
            }
        }

        private static Color GetColorForEvidence(EvidenceType type)
        {
            switch (type)
            {
                case EvidenceType.GhostOrb: return new Color(0.4f, 0.85f, 1f, 0.8f);
                case EvidenceType.EMFSurge: return new Color(0.2f, 1f, 0.3f, 0.7f);
                case EvidenceType.SpectralGrid: return new Color(1f, 0.3f, 0.9f, 0.75f);
                default: return new Color(0.7f, 0.9f, 1f, 0.6f);
            }
        }
    }
}
