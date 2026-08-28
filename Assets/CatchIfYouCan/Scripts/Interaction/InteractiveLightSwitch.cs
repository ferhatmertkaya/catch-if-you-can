using UnityEngine;
using CatchIfYouCan.Core;
using CatchIfYouCan.Player;

namespace CatchIfYouCan.Interaction
{
    public class InteractiveLightSwitch : MonoBehaviour, IInteractable
    {
        [SerializeField] private LightController lightController;
        [SerializeField] private string onPrompt = "Turn Off";
        [SerializeField] private string offPrompt = "Turn On";
        [SerializeField] private float distance = 2f;

        public string Prompt => lightController != null && lightController.IsOn ? onPrompt : offPrompt;
        public float HoldDuration => 0f;
        public InteractionType InteractionType =>
            lightController != null && lightController.IsOn ? InteractionType.TurnOff : InteractionType.TurnOn;
        public float Distance => distance;

        public bool CanInteract(GameObject interactor) => lightController != null;

        public void Interact(GameObject interactor)
        {
            if (lightController == null)
                return;

            lightController.Toggle();

            PlayerNoiseEmitter noise = interactor.GetComponent<PlayerNoiseEmitter>();
            noise?.EmitActionNoise();
        }
    }
}
