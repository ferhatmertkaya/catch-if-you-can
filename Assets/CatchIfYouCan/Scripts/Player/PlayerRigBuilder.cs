using CatchIfYouCan.Interaction;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CatchIfYouCan.Player
{
    /// <summary>
    /// Builds the character-independent half of the player: the transforms, the camera and
    /// every component that does not depend on which character was chosen.
    ///
    /// <para>
    /// This is the single description of that hierarchy, and it is used twice: at runtime
    /// when no player prefab is available, and by the editor tool that bakes PF_Player. A
    /// prefab produced from the same code that would otherwise run cannot drift from it,
    /// which is the failure this shape is chosen to avoid - a prefab and a factory that
    /// slowly disagree about where the hand anchor is.
    /// </para>
    ///
    /// <para>
    /// The private-field wiring below is reflection here because a code-built hierarchy has
    /// nowhere else to put it. Once the prefab exists those same assignments are serialized
    /// in the asset, so the prefab path performs none of it.
    /// </para>
    /// </summary>
    public static class PlayerRigBuilder
    {
        /// <summary>Builds the rig at the origin. The caller places it.</summary>
        public static PlayerRig Build()
        {
            var player = new GameObject("Player");
            player.tag = "Player";

            var controller = player.AddComponent<CharacterController>();
            // PlayerController owns these at Awake; matched here so the capsule is never
            // briefly the wrong size on the frame it is built.
            controller.height = PlayerFactory.CapsuleHeight;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, PlayerFactory.CapsuleHeight * 0.5f, 0f);

            var cameraRoot = new GameObject("CameraRoot");
            cameraRoot.transform.SetParent(player.transform, false);
            // Where the eyes are, in both axes that matter. The height was already right;
            // the forward offset was missing, and that is what put the camera in the neck.
            cameraRoot.transform.localPosition =
                new Vector3(0f, PlayerFactory.EyeHeight, PlayerFactory.EyeForward);

            var visualRoot = new GameObject("VisualRoot");
            visualRoot.transform.SetParent(player.transform, false);
            // Scaled here rather than on the player, so collision, movement and the camera
            // keep their own numbers. The character's feet are at its local origin and this
            // sits at the player's, so scaling about it leaves the feet on the floor.
            visualRoot.transform.localScale = Vector3.one * PlayerFactory.VisualScale;

            var handAnchor = new GameObject("HandAnchor");
            handAnchor.transform.SetParent(cameraRoot.transform, false);
            // Right, matching the hand target in PlayerBodyMotion. This is only the fallback
            // for an item whose hand bone cannot be found; the two must agree on a side or a
            // fallback item appears on the opposite side from the arm holding it.
            handAnchor.transform.localPosition = new Vector3(0.12f, -0.08f, 0.35f);

            // A transform of its own between the pitch pivot and the camera, purely so the
            // idle breathing has somewhere to live. Three systems already write to the two
            // transforms around it - PlayerLook the pivot's rotation, PlayerController the
            // pivot's height while crouching, FearSystem the camera's own local position -
            // and a fourth writer on either would mean one silently cancelling another.
            var cameraBreath = new GameObject("CameraBreath");
            cameraBreath.transform.SetParent(cameraRoot.transform, false);
            var cameraIdle = cameraBreath.AddComponent<CameraIdleMotion>();

            var cameraGo = new GameObject("MainCamera");
            cameraGo.tag = "MainCamera";
            cameraGo.transform.SetParent(cameraBreath.transform, false);

            var viewCamera = cameraGo.AddComponent<Camera>();
            // 5 cm, re-checked against the moved camera and left alone. From the eye
            // position the nearest body geometry is the shoulder line, about 25 cm below and
            // behind, and the collapsed head sits about 18 cm away; 5 cm clears both without
            // squeezing the depth buffer the way a millimetre near plane would.
            viewCamera.nearClipPlane = 0.05f;

            // The room's Volume carries the grading and vignette that hold its mood, and
            // none of it reached the player: URP leaves renderPostProcessing off by default,
            // and GraphicsManager only walks the cameras that exist when it runs, which is
            // before this one is built.
            var cameraData = cameraGo.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData == null)
                cameraData = cameraGo.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = true;

            var listener = cameraGo.AddComponent<AudioListener>();

            var playerLook = cameraRoot.AddComponent<PlayerLook>();
            PlayerFactory.SetPrivateField(playerLook, "playerBody", player.transform);

            var playerController = player.AddComponent<PlayerController>();
            PlayerFactory.SetPrivateField(playerController, "cameraRoot", cameraRoot.transform);
            PlayerFactory.SetPrivateField(playerController, "playerLook", playerLook);

            // Wired here rather than found in Awake: the camera hierarchy is built before
            // the controller exists, so the component's own GetComponentInParent runs too
            // early and comes back empty. Without it the breathing works and the "is the
            // player doing anything" test only ever sees the look, never the movement stick.
            PlayerFactory.SetPrivateField(cameraIdle, "playerController", playerController);

            var inventory = player.AddComponent<PlayerInventory>();
            player.AddComponent<PlayerNoiseEmitter>();
            var fear = player.AddComponent<FearSystem>();

            var interaction = player.AddComponent<InteractionController>();
            PlayerFactory.SetPrivateField(interaction, "viewCamera", viewCamera);
            PlayerFactory.SetPrivateField(fear, "targetCamera", viewCamera);

            inventory.SetHandAnchor(handAnchor.transform);

            var rig = player.AddComponent<PlayerRig>();
            rig.Bind(cameraRoot.transform, cameraBreath.transform, visualRoot.transform,
                     handAnchor.transform, viewCamera, listener);

            return rig;
        }

    }
}
