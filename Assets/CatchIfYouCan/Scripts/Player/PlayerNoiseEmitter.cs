using UnityEngine;
using CatchIfYouCan.Core;

namespace CatchIfYouCan.Player
{
    public enum NoiseLevel
    {
        Silent,
        Crouch,
        Walk,
        Run,
        Action
    }

    public class PlayerNoiseEmitter : MonoBehaviour
    {
        [SerializeField] private float crouchNoise = 0.05f;
        [SerializeField] private float walkNoise = 0.25f;
        [SerializeField] private float runNoise = 0.65f;
        [SerializeField] private float actionNoise = 0.85f;
        [SerializeField] private float emitInterval = 0.35f;
        [SerializeField] private float minMoveSpeed = 0.05f;

        private float _lastEmitTime;
        private NoiseLevel _currentLevel = NoiseLevel.Silent;

        public NoiseLevel CurrentLevel => _currentLevel;

        public void UpdateFromMovement(float speed, bool isSprinting, bool isCrouching, bool isGrounded)
        {
            if (!isGrounded || speed < minMoveSpeed)
            {
                _currentLevel = NoiseLevel.Silent;
                return;
            }

            if (isCrouching)
                _currentLevel = NoiseLevel.Crouch;
            else if (isSprinting)
                _currentLevel = NoiseLevel.Run;
            else
                _currentLevel = NoiseLevel.Walk;

            if (Time.time - _lastEmitTime < emitInterval)
                return;

            float intensity = GetIntensityForLevel(_currentLevel);
            if (intensity <= 0f)
                return;

            _lastEmitTime = Time.time;
            GameEvents.NoiseGenerated(intensity, transform.position);
        }

        public void EmitActionNoise()
        {
            _currentLevel = NoiseLevel.Action;
            _lastEmitTime = Time.time;
            GameEvents.NoiseGenerated(actionNoise, transform.position);
        }

        public void EmitCustomNoise(float intensity)
        {
            _lastEmitTime = Time.time;
            GameEvents.NoiseGenerated(Mathf.Clamp01(intensity), transform.position);
        }

        private float GetIntensityForLevel(NoiseLevel level)
        {
            switch (level)
            {
                case NoiseLevel.Crouch: return crouchNoise;
                case NoiseLevel.Walk: return walkNoise;
                case NoiseLevel.Run: return runNoise;
                case NoiseLevel.Action: return actionNoise;
                default: return 0f;
            }
        }
    }
}
