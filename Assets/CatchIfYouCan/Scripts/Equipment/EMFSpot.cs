using UnityEngine;

namespace CatchIfYouCan.Equipment
{
    public class EMFSpot : MonoBehaviour
    {
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
