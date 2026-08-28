using UnityEngine;

namespace CatchIfYouCan.Interaction
{
    public interface IInteractable
    {
        string Prompt { get; }
        float HoldDuration { get; }
        InteractionType InteractionType { get; }
        float Distance { get; }
        bool CanInteract(GameObject interactor);
        void Interact(GameObject interactor);
    }
}
