using UnityEngine;

namespace CatchIfYouCan.Equipment
{
    public class EMFSpot : MonoBehaviour
    {
        /// <summary>
        /// Every spot currently alive. Kept so a detector does not have to sweep the scene.
        ///
        /// <para>
        /// The EMF detector called FindObjectsByType every frame it was switched on, which
        /// walks every object in the scene to find at most a handful of these. A spot knows
        /// when it exists; nothing else should have to go looking.
        /// </para>
        /// </summary>
        private static readonly System.Collections.Generic.List<EMFSpot> Alive =
            new System.Collections.Generic.List<EMFSpot>();

        /// <summary>The strongest reading at a point, and how far away it came from.</summary>
        public static float StrongestAt(Vector3 point)
        {
            float max = 0f;
            for (int i = 0; i < Alive.Count; i++)
            {
                var spot = Alive[i];
                if (spot == null)
                    continue;

                float strength = spot.GetStrengthAtPoint(point);
                if (strength > max)
                    max = strength;
            }

            return max;
        }

        /// <summary>How many are alive, for a lab readout.</summary>
        public static int ActiveCount => Alive.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => Alive.Clear();

        [SerializeField] private float strength = 1f;
        [SerializeField] private float duration = 8f;
        [SerializeField] private float decayPerSecond = 0.15f;
        [SerializeField] private float radius = 4f;

        private float _remainingDuration;
        private float _currentStrength;

        public float Strength => _currentStrength;
        public float Radius => radius;
        public bool IsActive => _currentStrength > 0.01f && _remainingDuration > 0f;

        private void OnEnable()
        {
            _remainingDuration = duration;
            _currentStrength = strength;

            if (!Alive.Contains(this))
                Alive.Add(this);
        }

        private void OnDisable()
        {
            Alive.Remove(this);
        }

        private void Update()
        {
            if (_remainingDuration <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            _remainingDuration -= Time.deltaTime;
            _currentStrength = Mathf.Max(0f, _currentStrength - decayPerSecond * Time.deltaTime);
            if (_currentStrength <= 0.01f)
                Destroy(gameObject);
        }

        public void Initialize(float spotStrength, float spotDuration, float spotDecay, float spotRadius)
        {
            strength = spotStrength;
            duration = spotDuration;
            decayPerSecond = spotDecay;
            radius = spotRadius;
            _remainingDuration = duration;
            _currentStrength = strength;
        }

        public float GetStrengthAtPoint(Vector3 worldPoint)
        {
            if (!IsActive)
                return 0f;

            float distance = Vector3.Distance(transform.position, worldPoint);
            if (distance > radius)
                return 0f;

            float falloff = 1f - (distance / radius);
            return _currentStrength * falloff;
        }
    }
}
