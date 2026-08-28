using UnityEngine;
using CatchIfYouCan.Core;
using CatchIfYouCan.Equipment;
using CatchIfYouCan.Player;

namespace CatchIfYouCan.Interaction
{
    public class InteractivePickup : MonoBehaviour, IInteractable
    {
        [SerializeField] private EquipmentBase itemComponent;
        [SerializeField] private string prompt = "Pick Up";
        [SerializeField] private float distance = 2f;
        [SerializeField] private bool destroyOnPickup = true;
        [SerializeField] private float pickupNoise = 0.2f;

        public string Prompt => prompt;
        public float HoldDuration => 0f;
        public InteractionType InteractionType => InteractionType.Pickup;
        public float Distance => distance;

        private void Awake()
        {
            if (itemComponent == null)
                itemComponent = GetComponent<EquipmentBase>();
        }

        public bool CanInteract(GameObject interactor)
        {
            if (itemComponent == null)
                return false;

            PlayerInventory inventory = interactor.GetComponent<PlayerInventory>();
            return inventory != null;
        }

        public void Interact(GameObject interactor)
        {
            if (itemComponent == null)
                return;

            PlayerInventory inventory = interactor.GetComponent<PlayerInventory>();
            if (inventory == null || !inventory.AddItem(itemComponent))
                return;

            PlayerNoiseEmitter noise = interactor.GetComponent<PlayerNoiseEmitter>();
            if (noise != null)
                noise.EmitCustomNoise(pickupNoise);
            else
                GameEvents.NoiseGenerated(pickupNoise, transform.position);

            if (destroyOnPickup)
            {
                itemComponent = null;
                Destroy(gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }
}
