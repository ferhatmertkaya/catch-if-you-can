using UnityEngine;
using CatchIfYouCan.UI;

namespace CatchIfYouCan.Core
{
    public class Bootstrap : MonoBehaviour
    {
        private void Start()
        {
            // Created first and before anything else so the screen is black from the very first
            // frame: EnsureManagers builds the runtime UI canvas, and none of that may be
            // visible even for a frame before the intro.
            var intro = StartupIntroVideo.Create();

            EnsureManagers();

            // Started on the intro object, not on this one. Bootstrap is destroyed with 00_Boot
            // when the menu loads, which would strand the coroutine and leave the screen black.
            intro.StartCoroutine(intro.Sequence(CiycScenes.MainMenu, ShowHeadphonesTip));
        }

        private static void ShowHeadphonesTip()
        {
            if (PlayerPrefs.GetInt("ciyc_headphones_tip", 0) == 1)
                return;

            PlayerPrefs.SetInt("ciyc_headphones_tip", 1);
            GameEvents.TipRequested("BEST EXPERIENCED WITH HEADPHONES");
        }

        private void EnsureManagers()
        {
            // One implementation, in CiycServices. This method used to be one of two
            // competing lists; the other lived in InvestigationBootstrap and disagreed
            // with it about nine of eleven entries.
            CiycServices.EnsureCore();
        }
    }
}
