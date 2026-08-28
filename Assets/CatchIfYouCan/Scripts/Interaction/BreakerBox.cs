using UnityEngine;
using CatchIfYouCan.Core;
using CatchIfYouCan.Player;

namespace CatchIfYouCan.Interaction
{
    public class BreakerBox : MonoBehaviour, IInteractable
    {
        [SerializeField] private LightController[] houseLights;
        [SerializeField] private string onPrompt = "Flip Breaker On";
        [SerializeField] private string offPrompt = "Flip Breaker Off";
        [SerializeField] private float distance = 2f;
        [SerializeField] private float actionNoise = 0.5f;

        public bool BreakerOn { get; private set; }

        public string Prompt => BreakerOn ? offPrompt : onPrompt;
        public float HoldDuration => 0.35f;
        public InteractionType InteractionType => BreakerOn ? InteractionType.TurnOff : InteractionType.TurnOn;
        public float Distance => distance;

        public bool CanInteract(GameObject interactor) => true;

        public void Interact(GameObject interactor)
        {
            BreakerOn = !BreakerOn;
            ApplyHousePower(BreakerOn);

            PlayerNoiseEmitter noise = interactor.GetComponent<PlayerNoiseEmitter>();
            if (noise != null)
                noise.EmitCustomNoise(actionNoise);
            else
                GameEvents.NoiseGenerated(actionNoise, transform.position);

            GameEvents.BreakerChanged();
        }

        private void ApplyHousePower(bool powered)
        {
            if (houseLights == null)
                return;

            for (int i = 0; i < houseLights.Length; i++)
            {
                if (houseLights[i] == null)
                    continue;

                houseLights[i].SetOn(powered);
            }
        }
    }
}
