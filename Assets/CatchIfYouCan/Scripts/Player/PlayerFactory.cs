using CatchIfYouCan.Core;
using CatchIfYouCan.Input;
using CatchIfYouCan.Interaction;
using UnityEngine;
using UnityEngine.Rendering.Universal;

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
        /// <summary>
        /// Builds the local player as whatever character this machine chose.
        ///
        /// <para>
        /// The choice is read here, once, rather than by each part that needs it. Two reads
        /// of a mutable static during one build can disagree, and a body built from one
        /// character with the rig profile of another is a rig that binds to bones that are
        /// not there.
        /// </para>
        /// </summary>
        public static PlayerBuildResult Create(Vector3 position, Quaternion rotation)
        {
            return Create(position, rotation, Character.CharacterService.Resolve());
        }

        /// <summary>
        /// Builds a player as a named character.
        ///
        /// <para>
        /// The character is a parameter rather than something this asks
        /// <see cref="Character.CharacterService"/> for, because that service holds exactly
        /// one character: the one this machine chose. Correct for the local player and
        /// silently wrong for anybody else - every remote player would wear the local
        /// player's face, body metrics and rig profile. This is the same mistake as asking
        /// <see cref="LocalPlayerService"/> where the player is, and it is prevented the same
        /// way: whoever knows which player is being built says which character it is.
        /// </para>
        ///
        /// <para>
        /// Null means the built-in fallback - the Resources visual and Nathan's rig naming -
        /// which is what kept working before any catalog existed.
        /// </para>
        /// </summary>
        public static PlayerBuildResult Create(Vector3 position, Quaternion rotation,
                                               Character.CharacterDefinition character)
        {
            EnsureMobileInput();

            var rig = InstantiateRig(position, rotation);
            if (rig == null)
                return null;

            var player = rig.gameObject;

            // Everything below depends on which character was chosen, so none of it can live
            // in the prefab. The rig above is the half that is the same for everybody.
            var characterVisual = AttachCharacterVisual(player, rig.VisualRoot, character);
            AttachBodyMotion(player, rig.VisualRoot, rig.CameraRoot, characterVisual, character);
            AttachFlashlight(player, characterVisual);
            AttachFootsteps(player);

            // Built here because this is the only moment a player exists to drive; the caller
            // switches it on once the screen has faded back in.
            var touchHud = UI.TouchHudFactory.Create();
            touchHud.SetActive(false);

            // Announced at the end, when every part a consumer might ask for exists. The
            // mirror, the room tone and the ambience emitters can all outlive a player and
            // are built before one, so they follow this rather than resolving once.
            LocalPlayerService.Register(player, rig.ViewCamera, rig.ViewListener);

            return new PlayerBuildResult
            {
                Root = player,
                HandAnchor = rig.HandAnchor,
                CameraRoot = rig.CameraRoot,
                ViewCamera = rig.ViewCamera,
                VisualRoot = rig.VisualRoot,
                CharacterVisual = characterVisual,
                TouchHud = touchHud
            };
        }

        /// <summary>
        /// The rig, from the authored prefab when there is one and from code when there is
        /// not.
        ///
        /// <para>
        /// Both paths run the same description of the hierarchy - the prefab is baked from
        /// <see cref="PlayerRigBuilder"/> by an editor tool - so they cannot drift apart.
        /// The prefab is preferred because its wiring is serialized rather than assigned by
        /// reflection, and because a prefab is something a person can open and look at.
        /// </para>
        /// </summary>
        private static PlayerRig InstantiateRig(Vector3 position, Quaternion rotation)
        {
            var registry = Content.CiycContentRegistry.Load();
            var prefab = registry != null ? registry.PlayerPrefab : null;

            PlayerRig rig = null;

            if (prefab != null)
            {
                var instance = Object.Instantiate(prefab, position, rotation);
                instance.name = "Player";
                rig = instance.GetComponent<PlayerRig>();

                if (rig == null)
                {
                    // A prefab without the component is a prefab the factory cannot read, and
                    // guessing at its children by name is what this component exists to stop.
                    CIYCLog.Error("The player prefab '" + prefab.name + "' has no PlayerRig " +
                                  "component, so its parts cannot be located. Falling back to " +
                                  "building the player in code. Rebuild the prefab with " +
                                  "Catch If You Can > Player > Build Player Prefab.");
                    Object.Destroy(instance);
                }
                else if (!rig.IsComplete)
                {
                    CIYCLog.Error("The player prefab is missing: " + rig.DescribeMissing() +
                                  ". Falling back to building the player in code. Rebuild it " +
                                  "with Catch If You Can > Player > Build Player Prefab.");
                    Object.Destroy(rig.gameObject);
                    rig = null;
                }
            }

            if (rig == null)
                rig = PlayerRigBuilder.Build();

            rig.transform.SetPositionAndRotation(position, rotation);
            return rig;
        }

        /// <summary>
        /// Resources path of the character visual prefab. Loaded if it is there and skipped
        /// silently if it is not, so the player is fully playable as a camera-only capsule until
        /// a character is imported and nothing has to change in code when one is.
        /// </summary>
        public const string CharacterVisualResourcePath = "Characters/Player_CharacterVisual";

        /// <summary>
        /// Camera height above the player root, in metres.
        ///
        /// <para>
        /// Tuned by hand in Play Mode rather than derived. The anatomical eye line is 1.788 m
        /// (Nathan's eye bones at 1.719 m scaled by <see cref="VisualScale"/>), and a camera
        /// exactly there sat level with the top of the neck, so looking down went straight into
        /// the collar. 1.68 m drops the view about 10 cm, to roughly mouth height, which puts the
        /// open top of the neck behind and level with the camera instead of below it.
        /// </para>
        /// </summary>
        public const float EyeHeight = 1.68f;

        /// <summary>
        /// How far forward of the spine the camera sits.
        ///
        /// <para>
        /// This was 0, and that is the whole of the "camera is inside the neck" problem. Eyes are
        /// not on the axis of the neck; they are at the front of the face. Nathan's eye bones are
        /// measured at z = +8.8 cm in model space, which at <see cref="VisualScale"/> is 9.2 cm,
        /// so a camera at x = z = 0 sat 9 cm behind his own eyes — inside the head, looking out
        /// through the throat and the collar. Looking down from there shows the underside of the
        /// jaw and the top of the chest at point-blank range, which is exactly what was reported.
        /// </para>
        ///
        /// <para>
        /// It goes on the CameraRoot rather than on the camera under it. CameraRoot is the pitch
        /// pivot, so an offset placed below it would swing on an arc as the player looks down and
        /// end up back over the neck at the very moment the view needs to clear it. On the pivot
        /// itself the offset stays fixed in body space: the player always looks down the front of
        /// their own chest rather than through it. It is well inside the 0.35 m capsule radius, so
        /// the camera cannot be pushed through a wall the controller has already stopped at.
        /// </para>
        ///
        /// <para>
        /// 0.21 m is a hand-tuned value, not the 0.092 m the eye bones measure. Sitting further
        /// forward than the anatomical eye puts the whole neck and collar behind the camera, which
        /// is what stops a downward look ending inside them, and it opens up the chest and legs
        /// below. Do not raise it much further without checking the capsule: at 0.35 m the camera
        /// would be on the collision surface rather than inside it.
        /// </para>
        /// </summary>
        public const float EyeForward = 0.21f;

        /// <summary>
        /// Character scale. 1.04 takes the measured 1.86 m model to about 1.93 m — the small
        /// lift that stops the viewpoint feeling short, well short of anything that reads as a
        /// giant.
        /// </summary>
        public const float VisualScale = 1.04f;

        /// <summary>
        /// Collision capsule height. Deliberately a little under the character's full height,
        /// the same way it was before: the top of the head does not need to be in the capsule.
        /// </summary>
        public const float CapsuleHeight = 1.86f;

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

        private static GameObject AttachCharacterVisual(GameObject player, Transform visualRoot,
                                                        Character.CharacterDefinition character)
        {
            // The given character wins when there is one; the Resources path is the fallback
            // that kept working while the catalog did not exist.
            var prefab = character != null && character.VisualPrefab != null
                ? character.VisualPrefab
                : Resources.Load<GameObject>(CharacterVisualResourcePath);

            if (prefab == null)
            {
                // This used to return quietly, and quietly is how the player ended up with no
                // body at all: the prefab is generated by an editor step, the step had not been
                // run, and nothing anywhere said so. A missing character is not a normal state.
                Debug.LogError("[CIYC] No character visual at Resources/" +
                               CharacterVisualResourcePath + ", so the player has no body. " +
                               "Run Catch If You Can > Characters > Build Nathan Player Visual.");
                return null;
            }

            var visual = Object.Instantiate(prefab, visualRoot);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;

            // Animation is driven from the controller's real velocity, so this is added to the
            // player root where the CharacterController lives rather than to the model.
            var visualAnimator = player.GetComponent<PlayerVisualAnimator>();
            if (visualAnimator == null)
                visualAnimator = player.AddComponent<PlayerVisualAnimator>();
            visualAnimator.BindAnimator(visual.GetComponentInChildren<Animator>());

            // The model imports with no material of its own, so the character build is the only
            // thing that ever puts one on the mesh. When that step fails the renderer keeps an
            // empty slot and Unity draws the default grey, which reads as the character having
            // lost its texture and gives no clue why. Say why.
            var visualRenderers = visual.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < visualRenderers.Length; i++)
            {
                var mats = visualRenderers[i].sharedMaterials;
                bool missing = mats == null || mats.Length == 0;
                for (int m = 0; !missing && m < mats.Length; m++)
                    if (mats[m] == null) missing = true;

                if (missing)
                {
                    Debug.LogError("[CIYC] " + visualRenderers[i].name + " has no material, so " +
                                   "the character renders untextured. Rebuild it with " +
                                   "Catch If You Can > Characters > Build Nathan Player Visual; " +
                                   "that step is what assigns Nathan_Body.mat.",
                                   visualRenderers[i]);
                    break;
                }
            }

            // The local player must not see their own head from the inside; the model itself is
            // left whole so a remote copy can still be drawn in full.
            var bodyVisibility = visual.GetComponent<LocalPlayerBodyVisibility>();
            if (bodyVisibility == null)
                visual.AddComponent<LocalPlayerBodyVisibility>();

            return visual;
        }

        /// <summary>
        /// Adds the layer that crouches, sidesteps, breathes and blinks.
        ///
        /// <para>
        /// On the player root rather than the model, like the rest of the animation: the model is
        /// loaded on demand and can be missing entirely, and a component that lives on it would
        /// take the whole feature with it. This one binds to whatever character turned up, or to
        /// nothing.
        /// </para>
        /// </summary>
        private static void AttachBodyMotion(GameObject player, Transform visualRoot,
                                             Transform cameraRoot, GameObject characterVisual,
                                             Character.CharacterDefinition character)
        {
            var motion = player.GetComponent<PlayerBodyMotion>();
            if (motion == null)
                motion = player.AddComponent<PlayerBodyMotion>();

            // Set before BindAnimator, which is when the bones are actually looked up.
            // Null is fine and means the built-in naming, which is Nathan's.
            motion.SetRigProfile(character != null ? character.RigProfile : null);

            SetPrivateField(motion, "visualRoot", visualRoot);
            SetPrivateField(motion, "playerBody", player.transform);
            SetPrivateField(motion, "cameraRoot", cameraRoot);
            SetPrivateField(motion, "playerController", player.GetComponent<PlayerController>());

            if (characterVisual != null)
                motion.BindAnimator(characterVisual.GetComponentInChildren<Animator>());
        }

        /// <summary>
        /// Builds the placeholder torch and puts it in the player's hands.
        ///
        /// <para>
        /// It is made as an ordinary item and handed to <see cref="PlayerInventory"/> rather than
        /// bolted to the player, so everything about carrying it - equipping, dropping, picking
        /// it back up, the HUD button that does those - is the code that already existed. The
        /// only thing the torch itself adds is that its beam survives being put down.
        /// </para>
        /// </summary>
        private static void AttachFlashlight(GameObject player, GameObject characterVisual)
        {
            var inventory = player.GetComponent<PlayerInventory>();
            if (inventory == null)
                return;

            var go = new GameObject("Flashlight_Placeholder");
            go.transform.SetParent(player.transform, false);

            // A trigger, not a solid: the interaction ray is cast with
            // QueryTriggerInteraction.Collide, so this is enough to be picked up, and a solid
            // collider on something held inside the player's own capsule is nothing but a source
            // of contacts to resolve.
            var collider = go.AddComponent<SphereCollider>();
            collider.radius = 0.14f;
            collider.isTrigger = true;

            var torch = go.AddComponent<Equipment.HeldFlashlight>();

            // AddComponent runs Awake synchronously, so the torch has already built its visual
            // by this line - blind, with no definition, which is the placeholder capsule.
            // BindDefinition is what rebuilds it from the real model, and it is also what makes
            // PlayerInventory.IsTorch true so the torch reaches its own slot and the player's
            // hand rather than an investigation slot. Both of those depend on this lookup, so a
            // miss is not something to step over quietly: it produces exactly the symptom
            // "there is no flashlight in my hand" while every line here still runs.
            var definition = Equipment.EquipmentDefinitionFactory.GetById(
                Equipment.EquipmentIds.Flashlight);
            if (definition != null)
            {
                torch.BindDefinition(definition);
            }
            else
            {
                Core.CIYCLog.Error(
                    "[CIYC][Flashlight] No definition for '" + Equipment.EquipmentIds.Flashlight +
                    "'. The torch keeps the placeholder it built before this line and, with no " +
                    "definition, will not be recognised as the torch - so it takes an " +
                    "investigation slot instead of the hand.");
            }

            torch.BindCharacter(characterVisual != null ? characterVisual.transform : null,
                                player.transform);

            var pickup = go.AddComponent<Interaction.InteractivePickup>();
            pickup.Configure(torch, "Pick Up Flashlight", destroyWhenTaken: false);

            inventory.AddItem(torch);

            // The fear system asks whether the player is standing in their own light, and the
            // only light that can answer is the one built two lines up. It is created at
            // runtime, so nothing can have serialized a reference to it.
            var fear = player.GetComponent<FearSystem>();
            if (fear != null && torch.Beam != null)
                fear.SetFlashlight(torch.Beam);
        }

        public static MobileInputController EnsureMobileInput()
        {
            if (MobileInputController.Instance != null)
                return MobileInputController.Instance;

            var inputGo = new GameObject("MobileInputController");
            var input = inputGo.AddComponent<MobileInputController>();

            var joystick = Object.FindAnyObjectByType<VirtualJoystick>();
            if (joystick != null)
                input.BindJoystick(joystick);

            return input;
        }

        /// <summary>
        /// Assigns a serialized private field on a component built in code.
        ///
        /// Reflection because a code-built hierarchy has nowhere else to put the wiring.
        /// The prefab path does none of this: the same assignments are serialized in the
        /// asset by the tool that bakes it.
        /// </summary>
        internal static void SetPrivateField(object target, string fieldName, object value)
        {
            if (target == null)
                return;

            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(target, value);
        }
    }
}
