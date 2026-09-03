using UnityEngine;

namespace CatchIfYouCan.Player
{
    /// <summary>
    /// Typed access to the parts of the player that are the same for every character.
    ///
    /// <para>
    /// This exists so the factory can find the camera root, the hand anchor and the visual
    /// root without searching for them by name. Name lookups inside a prefab are the kind
    /// of dependency that survives review and then breaks silently the first time somebody
    /// renames a child in the Hierarchy.
    /// </para>
    ///
    /// <para>
    /// VisualRoot is deliberately empty here. Which character hangs off it is a runtime
    /// decision, so baking one in would make the prefab a Nathan prefab rather than a
    /// player prefab.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Catch If You Can/Player/Player Rig")]
    public sealed class PlayerRig : MonoBehaviour
    {
        [SerializeField] private Transform cameraRoot;
        [SerializeField] private Transform cameraBreath;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform handAnchor;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private AudioListener viewListener;

        public Transform CameraRoot => cameraRoot;
        public Transform CameraBreath => cameraBreath;
        public Transform VisualRoot => visualRoot;
        public Transform HandAnchor => handAnchor;
        public Camera ViewCamera => viewCamera;
        public AudioListener ViewListener => viewListener;

        /// <summary>Called by the builder, and by the editor tool that bakes the prefab.</summary>
        public void Bind(Transform cameraRootTransform, Transform cameraBreathTransform,
                         Transform visualRootTransform, Transform handAnchorTransform,
                         Camera camera, AudioListener listener)
        {
            cameraRoot = cameraRootTransform;
            cameraBreath = cameraBreathTransform;
            visualRoot = visualRootTransform;
            handAnchor = handAnchorTransform;
            viewCamera = camera;
            viewListener = listener;
        }

        /// <summary>
        /// Whether every part the factory needs is present. A prefab that has lost a
        /// reference should say so once, not produce a player with no camera.
        /// </summary>
        public bool IsComplete =>
            cameraRoot != null && cameraBreath != null && visualRoot != null &&
            handAnchor != null && viewCamera != null && viewListener != null;

        public string DescribeMissing()
        {
            var missing = new System.Text.StringBuilder();
            if (cameraRoot == null) missing.Append("cameraRoot ");
            if (cameraBreath == null) missing.Append("cameraBreath ");
            if (visualRoot == null) missing.Append("visualRoot ");
            if (handAnchor == null) missing.Append("handAnchor ");
            if (viewCamera == null) missing.Append("viewCamera ");
            if (viewListener == null) missing.Append("viewListener ");
            return missing.ToString().TrimEnd();
        }
    }
}
