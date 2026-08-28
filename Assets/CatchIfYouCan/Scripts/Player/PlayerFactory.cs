using CatchIfYouCan.Input;
using CatchIfYouCan.Interaction;
using UnityEngine;

namespace CatchIfYouCan.Player
{
    public sealed class PlayerBuildResult
    {
        public GameObject Root;
        public Transform HandAnchor;
        public Transform CameraRoot;
        public Camera ViewCamera;
    }

    public static class PlayerFactory
    {
        public static PlayerBuildResult Create(Vector3 position, Quaternion rotation)
        {
            EnsureMobileInput();

            var player = new GameObject("Player");
            player.transform.SetPositionAndRotation(position, rotation);
            player.tag = "Player";

            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);

            var cameraRoot = new GameObject("CameraRoot");
            cameraRoot.transform.SetParent(player.transform, false);
            cameraRoot.transform.localPosition = new Vector3(0f, 1.6f, 0f);

            var handAnchor = new GameObject("HandAnchor");
            handAnchor.transform.SetParent(cameraRoot.transform, false);
            handAnchor.transform.localPosition = new Vector3(0.12f, -0.08f, 0.35f);

            var cameraGo = new GameObject("MainCamera");
            cameraGo.tag = "MainCamera";
            cameraGo.transform.SetParent(cameraRoot.transform, false);
            var viewCamera = cameraGo.AddComponent<Camera>();
            viewCamera.nearClipPlane = 0.05f;
            cameraGo.AddComponent<AudioListener>();

            var playerLook = cameraRoot.AddComponent<PlayerLook>();
            SetPrivateField(playerLook, "playerBody", player.transform);

            var playerController = player.AddComponent<PlayerController>();
            SetPrivateField(playerController, "cameraRoot", cameraRoot.transform);
            SetPrivateField(playerController, "playerLook", playerLook);

            player.AddComponent<PlayerInventory>();
            player.AddComponent<PlayerNoiseEmitter>();
            player.AddComponent<FearSystem>();

            var interaction = player.AddComponent<InteractionController>();
            SetPrivateField(interaction, "viewCamera", viewCamera);

            var inventory = player.GetComponent<PlayerInventory>();
            inventory.SetHandAnchor(handAnchor.transform);

            var fear = player.GetComponent<FearSystem>();
            SetPrivateField(fear, "targetCamera", viewCamera);

            return new PlayerBuildResult
            {
                Root = player,
                HandAnchor = handAnchor.transform,
                CameraRoot = cameraRoot.transform,
                ViewCamera = viewCamera
            };
        }

        public static MobileInputController EnsureMobileInput()
        {
            if (MobileInputController.Instance != null)
                return MobileInputController.Instance;

            var inputGo = new GameObject("MobileInputController");
            var input = inputGo.AddComponent<MobileInputController>();

            var joystick = Object.FindFirstObjectByType<VirtualJoystick>();
            if (joystick != null)
                input.BindJoystick(joystick);

            return input;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            if (target == null)
                return;

            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(target, value);
        }
    }
}
