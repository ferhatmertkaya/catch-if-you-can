using CatchIfYouCan.Core;
using UnityEngine;

namespace CatchIfYouCan.Procedural
{
    public class TrainingBootstrap : MonoBehaviour
    {
        [SerializeField] private bool trainingMode = true;
        [SerializeField] private int trainingSeed = 42;
        [SerializeField] private InvestigationBootstrap investigationBootstrap;

        public bool TrainingMode => trainingMode;

        private void Awake()
        {
            PlayerPrefs.SetInt("ciyc_training", trainingMode ? 1 : 0);
            PlayerPrefs.SetInt("ciyc_training_seed", trainingSeed);

            if (investigationBootstrap == null)
                investigationBootstrap = GetComponent<InvestigationBootstrap>();
            if (investigationBootstrap == null)
                investigationBootstrap = gameObject.AddComponent<InvestigationBootstrap>();
        }

        private void Start()
        {
            if (trainingMode)
                GameEvents.TipRequested("TRAINING MODE — Learn the basics safely.");
        }
    }
}
