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

            // Never offer something the player is already holding. Without this a carried item
            // whose collider sits in front of the camera - anything held at the viewmodel anchor
            // - is picked up by the interaction ray every frame and permanently reads as "Pick
            // Up", covering whatever is actually in front of the player.
            if (itemComponent.IsEquipped)
                return false;

            PlayerInventory inventory = interactor.GetComponent<PlayerInventory>();
            return inventory != null && inventory.HasFreeSlot;
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

            // When the item lives on this same object - which is what Awake's fallback sets up -
            // the inventory now owns the thing being destroyed. Destroying or deactivating it
            // here would take the item straight back out of the bag it was just put in. Only the
            // separate-marker case has anything left to clean up.
            if (itemComponent.gameObject == gameObject)
                return;

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
