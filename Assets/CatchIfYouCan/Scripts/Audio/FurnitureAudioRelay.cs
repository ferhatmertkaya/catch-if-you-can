using CatchIfYouCan.Interaction;
using UnityEngine;

namespace CatchIfYouCan.Audio
{
    public class FurnitureAudioRelay : MonoBehaviour
    {
        [SerializeField] private string openId = "Env.Furniture.Open";
        [SerializeField] private string closeId = "Env.Furniture.Close";
        [SerializeField] private string wardrobeOpenId = "Env.Wardrobe.Open";
        [SerializeField] private string wardrobeCloseId = "Env.Wardrobe.Close";

        private InteractiveDrawer _drawer;
        private bool _wasOpen;

        private void Awake()
        {
            _drawer = GetComponent<InteractiveDrawer>();
        }

        private void Update()
        {
            if (_drawer == null) return;
            bool open = IsOpen(_drawer);
            if (open == _wasOpen) return;
            PlayForState(open, IsWardrobe());
            _wasOpen = open;
        }

        public void PlayOpen(bool wardrobe = false)
        {
            AudioManager.Instance?.PlayEvent(wardrobe ? wardrobeOpenId : openId, transform.position, 0.55f);
        }

        public void PlayClose(bool wardrobe = false)
        {
            AudioManager.Instance?.PlayEvent(wardrobe ? wardrobeCloseId : closeId, transform.position, 0.5f);
        }

        private void PlayForState(bool open, bool wardrobe)
        {
            if (open) PlayOpen(wardrobe);
            else PlayClose(wardrobe);
        }

        private static bool IsOpen(InteractiveDrawer drawer)
        {
            return drawer.InteractionType == InteractionType.Close;
        }

        private bool IsWardrobe()
        {
            return name.ToLowerInvariant().Contains("wardrobe") || name.ToLowerInvariant().Contains("closet");
        }
    }
}
