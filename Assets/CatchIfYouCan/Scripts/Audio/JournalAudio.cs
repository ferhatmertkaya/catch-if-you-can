using CatchIfYouCan.UI;
using UnityEngine;

namespace CatchIfYouCan.Audio
{
    public class JournalAudio : MonoBehaviour
    {
        [SerializeField] private JournalController journal;
        [SerializeField] private string openId = "UI.Journal.Open";
        [SerializeField] private string closeId = "UI.Journal.Close";
        [SerializeField] private string evidenceClickId = "UI.Journal.EvidenceClick";

        private void Awake()
        {
            if (journal == null)
                journal = GetComponent<JournalController>();
        }

        public void PlayEvidenceClick()
        {
            AudioManager.Instance?.PlayEvent(evidenceClickId, null, 0.45f);
        }

        public void PlayOpen()
        {
            AudioManager.Instance?.PlayEvent(openId, null, 0.55f);
        }

        public void PlayClose()
        {
            AudioManager.Instance?.PlayEvent(closeId, null, 0.5f);
        }
    }
}
