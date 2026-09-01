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

        /// <summary>
        /// Where the character model hangs. Always present and always empty of gameplay logic:
        /// the root owns position, collision and movement, and this owns nothing but appearance.
        /// Keeping them apart is what lets a remote player later use the same visual under a
        /// networked root without dragging the local input and camera along with it.
        /// </summary>
        public Transform VisualRoot;

        /// <summary>The instantiated character, or null when no visual prefab is available.</summary>
        public GameObject CharacterVisual;

        /// <summary>
        /// The on-screen controls. Handed back rather than shown, so the caller decides when the
        /// player is allowed to see them — they must not appear over the transition fade.
        /// </summary>
        public GameObject TouchHud;
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
            // Eye height, not head height. Nathan's eye bones sit at 1.719 m in the bind pose, so
            // the camera goes where his eyes are; that is what makes looking down at his own
            // chest and legs read as a body rather than a prop hanging below a floating camera.
            cameraRoot.transform.localPosition = new Vector3(0f, EyeHeight, 0f);

            var visualRoot = new GameObject("VisualRoot");
            visualRoot.transform.SetParent(player.transform, false);

            var handAnchor = new GameObject("HandAnchor");
            handAnchor.transform.SetParent(cameraRoot.transform, false);
            handAnchor.transform.localPosition = new Vector3(0.12f, -0.08f, 0.35f);

            var cameraGo = new GameObject("MainCamera");
            cameraGo.tag = "MainCamera";
            cameraGo.transform.SetParent(cameraRoot.transform, false);
            var viewCamera = cameraGo.AddComponent<Camera>();
            // 5 cm. The nearest thing the camera can see of its own body is the shoulder, about
            // 25 cm away when looking straight down, so this clears it comfortably without
            // squeezing the depth buffer the way a millimetre near plane would.
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

            var characterVisual = AttachCharacterVisual(player, visualRoot.transform);
            AttachFootsteps(player);

            // Built here because this is the only moment a player exists to drive; the caller
            // switches it on once the screen has faded back in.
            var touchHud = UI.TouchHudFactory.Create();
            touchHud.SetActive(false);

            return new PlayerBuildResult
            {
                Root = player,
                HandAnchor = handAnchor.transform,
                CameraRoot = cameraRoot.transform,
                ViewCamera = viewCamera,
                VisualRoot = visualRoot.transform,
                CharacterVisual = characterVisual,
                TouchHud = touchHud
            };
        }

        /// <summary>
        /// Resources path of the character visual prefab. Loaded if it is there and skipped
        /// silently if it is not, so the player is fully playable as a camera-only capsule until
        /// a character is imported and nothing has to change in code when one is.
        /// </summary>
        public const string CharacterVisualResourcePath = "Characters/Player_CharacterVisual";

        /// <summary>Where the camera sits, matching the character's eye bones rather than the
        /// top of the capsule.</summary>
        public const float EyeHeight = 1.7f;

        /// <summary>Placeholder wood footsteps, replaced by dropping real recordings in.</summary>
        public const string FootstepClipResourcePath = "Audio/SFX/Footsteps";

        /// <summary>
        /// Gives the player one AudioSource for footsteps and a controller that decides when to
        /// use it. One source, reused for every step; nothing is created while walking.
        /// </summary>
        private static void AttachFootsteps(GameObject player)
        {
            var source = player.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            // Close and dry. These are the player's own boots, not a sound happening across the
            // room, so they should not thin out with distance or pan away from centre.
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.priority = 160;

            var footsteps = player.GetComponent<CatchIfYouCan.Audio.FootstepController>();
            if (footsteps == null)
                footsteps = player.AddComponent<CatchIfYouCan.Audio.FootstepController>();

            footsteps.BindSource(source);
            footsteps.SetWoodClips(Resources.LoadAll<AudioClip>(FootstepClipResourcePath));
        }

        private static GameObject AttachCharacterVisual(GameObject player, Transform visualRoot)
        {
            var prefab = Resources.Load<GameObject>(CharacterVisualResourcePath);
            if (prefab == null)
                return null;

            var visual = Object.Instantiate(prefab, visualRoot);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;

            // Animation is driven from the controller's real velocity, so this is added to the
            // player root where the CharacterController lives rather than to the model.
            var visualAnimator = player.GetComponent<PlayerVisualAnimator>();
            if (visualAnimator == null)
                visualAnimator = player.AddComponent<PlayerVisualAnimator>();
            visualAnimator.BindAnimator(visual.GetComponentInChildren<Animator>());

            // The local player must not see their own head from the inside; the model itself is
            // left whole so a remote copy can still be drawn in full.
            var bodyVisibility = visual.GetComponent<LocalPlayerBodyVisibility>();
            if (bodyVisibility == null)
                visual.AddComponent<LocalPlayerBodyVisibility>();

            return visual;
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
