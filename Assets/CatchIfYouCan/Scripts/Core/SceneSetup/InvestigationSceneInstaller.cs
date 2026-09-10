using CatchIfYouCan.Audio;
using CatchIfYouCan.Procedural;
using CatchIfYouCan.UI;
using UnityEngine;

namespace CatchIfYouCan.Core.SceneSetup
{
    /// <summary>
    /// The mission scene. Everything in it is generated, so this installs the systems that
    /// do the generating and the anchors they generate around.
    /// </summary>
    [AddComponentMenu("Catch If You Can/Scene Setup/Investigation Scene Installer")]
    public sealed class InvestigationSceneInstaller : SceneInstallerBase
    {
        [Header("World anchors")]
        [SerializeField] private Transform worldRoot;
        [SerializeField] private Transform vanAnchor;
        [SerializeField] private Transform houseAnchor;

        [Header("Bootstrap")]
        [SerializeField] private InvestigationBootstrap investigationBootstrap;

        public override void Install()
        {
            fallbackLightIntensity = 0.2f;
            InstallSceneBasics();

            ShowScreenIfWeBuiltTheUi(UIScreen.HUD, false);

            SceneAnchors.EnsureWorldAnchors(ref worldRoot, ref vanAnchor, ref houseAnchor);

            // The five empty children this scene carries under MANAGERS - named
            // ProceduralHouseGenerator, GhostSpawnManager, MissionManager, ObjectiveManager
            // and EvidenceManager - are not created here any more. They never had components
            // on them; InvestigationBootstrap builds the real ones on separate objects, so
            // the scene simply held five decoys with the same names as the live systems.
            if (investigationBootstrap == null)
                investigationBootstrap = Object.FindAnyObjectByType<InvestigationBootstrap>();

            if (investigationBootstrap == null)
            {
                var go = FindOrCreate("INVESTIGATION_BOOTSTRAP");
                investigationBootstrap = go.GetComponent<InvestigationBootstrap>()
                                         ?? go.AddComponent<InvestigationBootstrap>();
            }

            var bootstrapGo = investigationBootstrap.gameObject;
            if (bootstrapGo.GetComponent<InvestigationAudioBootstrap>() == null)
                bootstrapGo.AddComponent<InvestigationAudioBootstrap>();
        }
    }
}
