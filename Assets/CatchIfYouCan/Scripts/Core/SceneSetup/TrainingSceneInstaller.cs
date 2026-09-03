using CatchIfYouCan.Procedural;
using UnityEngine;

namespace CatchIfYouCan.Core.SceneSetup
{
    /// <summary>Training: the investigation loop with the training flag set.</summary>
    [AddComponentMenu("Catch If You Can/Scene Setup/Training Scene Installer")]
    public sealed class TrainingSceneInstaller : SceneInstallerBase
    {
        [Header("World anchors")]
        [SerializeField] private Transform worldRoot;
        [SerializeField] private Transform vanAnchor;
        [SerializeField] private Transform houseAnchor;

        [Header("Bootstrap")]
        [SerializeField] private TrainingBootstrap trainingBootstrap;

        public override void Install()
        {
            fallbackLightIntensity = 0.25f;
            InstallSceneBasics();

            SceneAnchors.EnsureWorldAnchors(ref worldRoot, ref vanAnchor, ref houseAnchor);

            if (trainingBootstrap == null)
                trainingBootstrap = Object.FindAnyObjectByType<TrainingBootstrap>();

            if (trainingBootstrap == null)
                trainingBootstrap = FindOrCreate("TRAINING_BOOTSTRAP").AddComponent<TrainingBootstrap>();
        }
    }
}
