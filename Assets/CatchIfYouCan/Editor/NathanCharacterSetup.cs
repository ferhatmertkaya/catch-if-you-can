#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using CatchIfYouCan.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CatchIfYouCan.EditorTools
{
    /// <summary>
    /// Turns the raw Renderpeople "Nathan" delivery into the player's character visual.
    ///
    /// <para>
    /// Almost everything about this character is described in files that are checked in: the rig
    /// type, the mobile texture budgets and the URP material are all authored as assets. Three
    /// things are not, because they cannot be: an Animator Controller, a prefab and a generated
    /// idle clip all have to point at objects that live *inside* the imported FBX, and the file
    /// IDs of those objects are minted by Unity's model importer at import time. Writing them by
    /// hand would mean guessing numbers that only Unity can produce, and a wrong guess is a
    /// reference that silently resolves to nothing. So this tool builds that last mile through
    /// Unity's own API, where the references are correct by construction.
    /// </para>
    ///
    /// <para>
    /// It is idempotent. Running it again re-imports with the same settings and overwrites the
    /// three generated assets, which is what makes it safe to run after pulling a new version of
    /// the model.
    /// </para>
    ///
    /// <para>
    /// Every number this tool depends on was measured from the FBX rather than assumed, and it
    /// re-measures the important ones after import and prints them. If the model ever changes —
    /// a different take, a rescale, a re-export facing the other way — the report says so instead
    /// of the character quietly ending up sideways or half a metre tall.
    /// </para>
    /// </summary>
    public static class NathanCharacterSetup
    {
        private const string Root = "Assets/CatchIfYouCan/Art/Characters/Nathan/";
        private const string FbxPath = Root + "Models/rp_nathan_animated_003_walking.fbx";
        private const string MaterialPath = Root + "Materials/Nathan_Body.mat";
        private const string IdleClipPath = Root + "Animations/Nathan_Idle.anim";
        private const string ControllerPath = Root + "Animations/Nathan_PlayerVisual.controller";
        private const string VisualPrefabPath = Root + "Prefabs/Nathan_PlayerVisual.prefab";

        private const string ResourcePrefabPath =
            "Assets/CatchIfYouCan/Resources/Characters/Player_CharacterVisual.prefab";

        /// <summary>The material slot name inside the FBX, read out of the file itself.</summary>
        private const string FbxMaterialName = "rp_nathan_animated_003_mat";

        /// <summary>The FBX's only animation take.</summary>
        private const string TakeName = "Take 001";

        /// <summary>The bone the walk carries its travel on; the rig's Root Node.</summary>
        private const string RootBoneName = "rp_nathan_animated_003_walking_root";

        // The take runs frames 0..68 at 30 fps. Frame 0 is the bind pose — every bone rotation is
        // exactly zero there and then jumps by tens of degrees into the first walk pose — so the
        // usable cycle starts at frame 1. Frames 1..68 hold two full strides and are each other's
        // best loop partner out of the whole take.
        private const float WalkFirstFrame = 1f;
        private const float WalkLastFrame = 68f;
        private const float SourceFrameRate = 30f;

        // Frame 50 is where the two thighs line up, which is as close to standing as a walk cycle
        // ever gets. The legs are put back to the bind pose afterwards; what this frame supplies
        // is the upper body — arms hanging rather than held out in the T-pose.
        private const float IdleSourceFrame = 50f;

        private const string MenuRoot = "Catch If You Can/";

        /// <summary>Creates an asset folder and any missing parents. No-op if it already exists.</summary>
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            int slash = path.LastIndexOf('/');
            if (slash <= 0)
                return;

            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }

        [MenuItem(MenuRoot + "4. SPIELINHALT/Characters/Nathan Player Visual [SCHREIBT ASSET]", false, 401)]
        public static void Build()
        {
            var log = new StringBuilder();
            log.AppendLine("[CIYC] Nathan character setup");

            // Git does not track empty directories, so on a fresh clone the Animations and
            // Prefabs folders arrive as orphaned .meta files that Unity deletes on import.
            // Creating them here is what stops the first run failing on a missing folder.
            EnsureFolder("Assets/CatchIfYouCan/Art/Characters/Nathan/Animations");
            EnsureFolder("Assets/CatchIfYouCan/Art/Characters/Nathan/Prefabs");
            EnsureFolder("Assets/CatchIfYouCan/Resources/Characters");

            // The character textures are the only ones in the project with a platform override,
            // and an override left on Automatic is what asks iOS for the retired PVRTC format.
            NathanTextureImportSettings.Apply(log);

            var model = ConfigureImporter(log);
            if (model == null)
            {
                Debug.LogError(log.ToString());
                return;
            }

            var walkClip = FindWalkClip(log);
            if (walkClip == null)
            {
                Debug.LogError(log.ToString());
                return;
            }

            var idleClip = BuildIdleClip(model, walkClip, log);
            var controller = BuildController(walkClip, idleClip, log);
            var visualPrefab = BuildVisualPrefab(model, controller, log);
            if (visualPrefab == null)
            {
                Debug.LogError(log.ToString());
                return;
            }

            BuildResourcePrefab(visualPrefab, log);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Measure(visualPrefab, walkClip, log);
            Debug.Log(log.ToString());
        }

        [MenuItem(MenuRoot + "4. SPIELINHALT/Characters/Nathan pruefen [NUR LESEN]", false, 402)]
        public static void Validate()
        {
            var log = new StringBuilder();
            log.AppendLine("[CIYC] Nathan character validation");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VisualPrefabPath);
            if (prefab == null)
            {
                log.AppendLine("  MISSING: " + VisualPrefabPath + " — run Build first.");
                Debug.LogWarning(log.ToString());
                return;
            }

            Measure(prefab, FindWalkClip(log), log);
            Debug.Log(log.ToString());
        }

        // ---- import ------------------------------------------------------------------------

        /// <summary>
        /// Applies the parts of the import that only the API can express, then reimports.
        ///
        /// <para>
        /// The clip split and the material remap live here rather than in the .meta because the
        /// exact serialised shape of both differs between Unity versions; asking the importer to
        /// write them guarantees they match whatever version is opening the project.
        /// </para>
        /// </summary>
        private static GameObject ConfigureImporter(StringBuilder log)
        {
            var importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
            if (importer == null)
            {
                log.AppendLine("  ERROR: no model importer at " + FbxPath);
                return null;
            }

            string take = TakeName;
            var takes = importer.importedTakeInfos;
            if (takes != null && takes.Length > 0)
            {
                bool found = false;
                for (int i = 0; i < takes.Length; i++)
                    if (takes[i].name == TakeName) found = true;

                if (!found)
                {
                    // Never invent a clip name: if the delivery ever changes, use whatever the
                    // file actually contains and say so, rather than splitting a take that is not
                    // there and producing an empty clip.
                    take = takes[0].name;
                    log.AppendLine("  NOTE: take '" + TakeName + "' not found; using '" + take + "'");
                }
            }

            var walk = new ModelImporterClipAnimation
            {
                name = "Nathan_Walk",
                takeName = take,
                firstFrame = WalkFirstFrame,
                lastFrame = WalkLastFrame,
                loopTime = true,
                // The two ends of the trimmed range are within about 1.5 degrees RMS of each
                // other, which is the noise floor of this take. Loop Pose closes that gap instead
                // of letting it read as a hitch once a second.
                loopPose = true,
                cycleOffset = 0f,
                keepOriginalOrientation = true,
                keepOriginalPositionY = true,
                keepOriginalPositionXZ = false,
                lockRootRotation = false,
                lockRootHeightY = false,
                lockRootPositionXZ = false,
                heightFromFeet = false,
                maskType = ClipAnimationMaskType.None
            };

            importer.clipAnimations = new[] { walk };

            // The Root Node, and it was the whole of the walking-away bug. This rig is Generic,
            // and a Generic rig lifts root motion out of a clip only when motionNodeName names the
            // bone to lift it from. That field was empty. The bone name was sitting in
            // rootMotionBoneName instead, which is part of the human description and which only a
            // Humanoid rig ever reads — so nothing was extracted, the clip's 2.866 m of travel
            // stayed as plain animation on the root bone, and applyRootMotion had no say over it.
            importer.motionNodeName = RootBoneName;

            // Material creation is off, so the model brings no materials of its own into the
            // project; this points its one slot at the URP material that is checked in.
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null)
            {
                importer.AddRemap(
                    new AssetImporter.SourceAssetIdentifier(typeof(Material), FbxMaterialName),
                    material);
            }
            else
            {
                log.AppendLine("  WARNING: material missing at " + MaterialPath);
            }

            importer.SaveAndReimport();

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (model == null)
                log.AppendLine("  ERROR: model failed to import");
            else
                log.AppendLine("  imported " + FbxPath);

            return model;
        }

        private static AnimationClip FindWalkClip(StringBuilder log)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(FbxPath);
            for (int i = 0; i < assets.Length; i++)
            {
                var clip = assets[i] as AnimationClip;
                // The importer also emits a hidden __preview__ clip; only the named one is ours.
                if (clip != null && clip.name == "Nathan_Walk")
                    return clip;
            }

            log.AppendLine("  ERROR: Nathan_Walk clip not found inside " + FbxPath);
            return null;
        }

        // ---- generated idle ----------------------------------------------------------------

        /// <summary>
        /// Builds a standing pose, because the delivery contains a walk and nothing else.
        ///
        /// <para>
        /// A walk cycle has no frame where the character is simply stood up — one knee is always
        /// bent — so neither freezing a frame nor falling back to the bind pose gives an idle
        /// worth having; the first leaves a foot in the air and the second is a T-pose. What this
        /// does instead is take the upper body from the frame where the thighs line up and put the
        /// legs back to the bind pose, which is where they are straight and shoulder width apart.
        /// The result is a person standing still, assembled entirely out of the model's own data.
        /// </para>
        /// </summary>
        private static AnimationClip BuildIdleClip(GameObject model, AnimationClip walk, StringBuilder log)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            try
            {
                var bind = new Dictionary<Transform, Pose>();
                var scales = new Dictionary<Transform, Vector3>();
                var all = instance.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    bind[all[i]] = new Pose(all[i].localPosition, all[i].localRotation);
                    scales[all[i]] = all[i].localScale;
                }

                // The clip starts at the take's frame 1, so the frame we want sits this far in.
                float sampleTime = (IdleSourceFrame - WalkFirstFrame) / SourceFrameRate;
                walk.SampleAnimation(instance, sampleTime);

                // Root motion should have been lifted out of the clip into the root motion curves.
                // If it was not, the root will have walked away from the origin by now, and that is
                // worth saying out loud rather than discovering as a character that slides.
                var rootBone = FindBone(instance.transform, "_root");
                if (rootBone != null)
                {
                    float drift = rootBone.localPosition.magnitude;
                    log.AppendLine("  root drift when sampling the walk at " +
                                   sampleTime.ToString("0.000") + "s: " + drift.ToString("0.000") + " m" +
                                   (drift > 0.05f ? "  <-- NOT EXTRACTED" : "  (extracted correctly)"));

                    // This went unnoticed for weeks because it was one line in a long log and the
                    // README asserted the opposite. A warning is harder to read past.
                    if (drift > 0.05f)
                        Debug.LogWarning("[CIYC] Nathan's walk still carries " +
                                         drift.ToString("0.00") + " m of root travel after import, " +
                                         "so the body will walk away from the player. The rig's " +
                                         "Root Node (motionNodeName) is not resolving to '" +
                                         RootBoneName + "'. PlayerVisualAnimator pins the bone at " +
                                         "runtime so this is survivable, but the import is wrong.");
                }

                RestoreBind(instance.transform, bind, scales);
                RestoreBind(rootBone, bind, scales);
                RestoreBind(FindBone(instance.transform, "_hip"), bind, scales);
                RestoreSubtree(FindBone(instance.transform, "_upperleg_l"), bind, scales);
                RestoreSubtree(FindBone(instance.transform, "_upperleg_r"), bind, scales);

                var clip = SnapshotPose(instance);
                clip.frameRate = SourceFrameRate;

                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings);

                var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(IdleClipPath);
                if (existing != null)
                {
                    EditorUtility.CopySerialized(clip, existing);
                    EditorUtility.SetDirty(existing);
                    log.AppendLine("  updated " + IdleClipPath);
                    return existing;
                }

                AssetDatabase.CreateAsset(clip, IdleClipPath);
                log.AppendLine("  created " + IdleClipPath);
                return clip;
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        /// <summary>Writes every transform's current local pose into a new constant clip.</summary>
        private static AnimationClip SnapshotPose(GameObject root)
        {
            var clip = new AnimationClip { name = "Nathan_Idle", legacy = false };
            const float length = 1f;

            var all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (t == root.transform)
                    continue;   // the clip must not move the object it is played on

                string path = AnimationUtility.CalculateTransformPath(t, root.transform);

                SetConstant(clip, path, "m_LocalPosition.x", t.localPosition.x, length);
                SetConstant(clip, path, "m_LocalPosition.y", t.localPosition.y, length);
                SetConstant(clip, path, "m_LocalPosition.z", t.localPosition.z, length);

                SetConstant(clip, path, "m_LocalRotation.x", t.localRotation.x, length);
                SetConstant(clip, path, "m_LocalRotation.y", t.localRotation.y, length);
                SetConstant(clip, path, "m_LocalRotation.z", t.localRotation.z, length);
                SetConstant(clip, path, "m_LocalRotation.w", t.localRotation.w, length);
            }

            clip.EnsureQuaternionContinuity();
            return clip;
        }

        private static void SetConstant(AnimationClip clip, string path, string property, float value, float length)
        {
            var curve = new AnimationCurve(new Keyframe(0f, value), new Keyframe(length, value));
            AnimationUtility.SetEditorCurve(
                clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), property), curve);
        }

        private static Transform FindBone(Transform root, string suffix)
        {
            var all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
                if (all[i].name.EndsWith(suffix))
                    return all[i];
            return null;
        }

        private static void RestoreBind(Transform t, Dictionary<Transform, Pose> bind, Dictionary<Transform, Vector3> scales)
        {
            if (t == null || !bind.ContainsKey(t))
                return;

            t.localPosition = bind[t].position;
            t.localRotation = bind[t].rotation;
            t.localScale = scales[t];
        }

        private static void RestoreSubtree(Transform t, Dictionary<Transform, Pose> bind, Dictionary<Transform, Vector3> scales)
        {
            if (t == null)
                return;

            var all = t.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
                RestoreBind(all[i], bind, scales);
        }

        // ---- controller --------------------------------------------------------------------

        private static AnimatorController BuildController(AnimationClip walk, AnimationClip idle, StringBuilder log)
        {
            // Rebuild from scratch rather than editing in place: a re-run would otherwise add a
            // second Speed parameter and a second pair of states to the controller that is
            // already there.
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
                AssetDatabase.DeleteAsset(ControllerPath);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            // Speed is the metres per second the player is actually covering; IsWalking is the
            // same signal thresholded. PlayerVisualAnimator writes both and only writes the ones
            // it finds here, so the names have to match it exactly.
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("IsWalking", AnimatorControllerParameterType.Bool);

            var machine = controller.layers[0].stateMachine;

            var idleState = machine.AddState("Idle");
            idleState.motion = idle;
            machine.defaultState = idleState;

            var walkState = machine.AddState("Walk");
            walkState.motion = walk;

            // No exit time: the character has to start and stop with the player, not at the end of
            // whatever stride it happened to be in.
            var toWalk = idleState.AddTransition(walkState);
            toWalk.hasExitTime = false;
            toWalk.duration = 0.12f;
            toWalk.AddCondition(AnimatorConditionMode.If, 0f, "IsWalking");

            var toIdle = walkState.AddTransition(idleState);
            toIdle.hasExitTime = false;
            toIdle.duration = 0.15f;
            toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsWalking");

            EditorUtility.SetDirty(controller);
            log.AppendLine("  created " + ControllerPath + " (Idle default, Walk on IsWalking)");
            return controller;
        }

        // ---- prefabs -----------------------------------------------------------------------

        private static GameObject BuildVisualPrefab(GameObject model, AnimatorController controller, StringBuilder log)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            try
            {
                instance.name = "Nathan_PlayerVisual";

                // The Animator belongs on the model root because the imported clip's curve paths
                // are relative to it. Moving it anywhere else silently unbinds every bone.
                var animator = instance.GetComponent<Animator>();
                if (animator == null)
                {
                    animator = instance.AddComponent<Animator>();

                    // An Animator added by hand has no Avatar, and a Generic rig without one
                    // plays nothing. The importer already built one; take that.
                    var modelAnimator = model.GetComponent<Animator>();
                    if (modelAnimator != null)
                        animator.avatar = modelAnimator.avatar;
                }

                if (animator.avatar == null)
                    log.AppendLine("  WARNING: the Animator has no Avatar; check the model's Rig tab");

                animator.runtimeAnimatorController = controller;

                // The CharacterController owns where the player is. Root motion would fight it,
                // desync the collider from the mesh and walk the character through walls the
                // controller had already stopped it at.
                animator.applyRootMotion = false;

                // The local player's body is drawn shadows-only, so its renderer spends most of
                // its life invisible. Culling the animator with it would freeze the shadow into a
                // pose and leave it there.
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);

                // Loudly, because the quiet version of this is a character with no texture.
                // The model imports with materialImportMode None and carries no material of its
                // own, so this assignment is the only thing that ever puts one on the mesh. If it
                // is skipped the renderer keeps an empty slot and Unity draws the default grey,
                // which is exactly what "the character lost its texture" looks like.
                if (material == null)
                    Debug.LogError("[CIYC] No material at " + MaterialPath + ". The character " +
                                   "will be built with an empty material slot and render " +
                                   "untextured.");

                var renderers = instance.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (material != null)
                    {
                        // Material import is off on the model, so a renderer can arrive with an
                        // empty slot array rather than one null slot. Falling back to a single
                        // slot is what stops the body rendering magenta in that case.
                        int slotCount = Mathf.Max(1, renderers[i].sharedMaterials.Length);
                        var slots = new Material[slotCount];
                        for (int s = 0; s < slotCount; s++) slots[s] = material;
                        renderers[i].sharedMaterials = slots;
                    }

                    // Starts visible. LocalPlayerBodyVisibility switches this to ShadowsOnly at
                    // runtime for the local player, and leaves a remote copy alone.
                    renderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                }

                // Read it back rather than trusting the write. The prefab this produced last time
                // carries overrides for the Animator and none at all for the materials, so the
                // assignment above did not reach the asset and nothing said so.
                for (int i = 0; i < renderers.Length; i++)
                {
                    var mats = renderers[i].sharedMaterials;
                    bool missing = mats == null || mats.Length == 0;
                    for (int m = 0; !missing && m < mats.Length; m++)
                        if (mats[m] == null) missing = true;

                    if (missing)
                        Debug.LogError("[CIYC] " + renderers[i].name + " still has no material " +
                                       "after the build. It will render untextured.");
                    else
                        log.AppendLine("  material on " + renderers[i].name + ": " +
                                       mats[0].name);
                }

                // A character is moved by its CharacterController; a collider on the body would
                // add a second, animated shape that collides with the world and with itself.
                var colliders = instance.GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < colliders.Length; i++)
                {
                    log.AppendLine("  removed stray " + colliders[i].GetType().Name + " on " +
                                   colliders[i].name);
                    Object.DestroyImmediate(colliders[i], true);
                }

                FaceForward(instance, log);

                var prefab = PrefabUtility.SaveAsPrefabAsset(instance, VisualPrefabPath);
                log.AppendLine("  created " + VisualPrefabPath);
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        /// <summary>
        /// Makes sure the character faces +Z, and says which way it was facing to begin with.
        ///
        /// <para>
        /// Forward is measured from the skeleton rather than assumed: the vector from the ankle to
        /// the toe of a standing character points where the character is looking, and it survives
        /// whatever axis conversion the importer applied on the way in. Guessing this is how a
        /// character ends up moonwalking around the room.
        /// </para>
        /// </summary>
        private static void FaceForward(GameObject instance, StringBuilder log)
        {
            var ankle = FindBone(instance.transform, "_foot_l");
            var toe = FindBone(instance.transform, "_foot_end_l");
            if (ankle == null || toe == null)
            {
                log.AppendLine("  NOTE: foot bones not found; facing left as imported");
                return;
            }

            Vector3 forward = instance.transform.InverseTransformPoint(toe.position) -
                              instance.transform.InverseTransformPoint(ankle.position);
            forward.y = 0f;

            log.AppendLine("  measured facing (ankle to toe, model space): " + forward.normalized);

            if (forward.z < 0f)
            {
                instance.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) *
                                                   instance.transform.localRotation;
                log.AppendLine("  the model faced -Z, so it was turned 180 degrees to face +Z");
            }
            else
            {
                log.AppendLine("  the model already faces +Z; no correction applied");
            }
        }

        /// <summary>
        /// Wraps the art prefab in the object <see cref="PlayerFactory"/> loads by name.
        ///
        /// <para>
        /// The wrapper is deliberately not the same asset as the character. It is the gameplay
        /// side's stable handle — swap which character hangs under it and nothing outside this
        /// folder changes — and it is where the local-player-only components live, which a remote
        /// player's copy of the same body must not have.
        /// </para>
        /// </summary>
        private static void BuildResourcePrefab(GameObject visualPrefab, StringBuilder log)
        {
            var root = new GameObject("Player_CharacterVisual");
            try
            {
                var body = (GameObject)PrefabUtility.InstantiatePrefab(visualPrefab);
                body.transform.SetParent(root.transform, false);
                body.transform.localPosition = Vector3.zero;

                // The body's own rotation is left exactly as the prefab authored it. Forcing it to
                // identity here would quietly undo the facing correction measured during the
                // build, which is the one thing about this transform that is not arbitrary.

                // PlayerFactory only adds this if the prefab root does not already carry one, so
                // putting it here keeps the settings visible and editable in the Inspector rather
                // than materialising with defaults at runtime.
                root.AddComponent<LocalPlayerBodyVisibility>();

                PrefabUtility.SaveAsPrefabAsset(root, ResourcePrefabPath);
                log.AppendLine("  created " + ResourcePrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ---- verification ------------------------------------------------------------------

        /// <summary>
        /// Prints what the character actually turned out to be. These are measurements, not
        /// assertions of success: nothing here proves the character looks right in Play Mode.
        /// </summary>
        private static void Measure(GameObject prefab, AnimationClip walk, StringBuilder log)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                var renderers = instance.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0)
                {
                    log.AppendLine("  ERROR: the prefab has no renderers");
                    return;
                }

                var bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    bounds.Encapsulate(renderers[i].bounds);

                log.AppendLine("  height        : " + bounds.size.y.ToString("0.000") + " m" +
                               (bounds.size.y > 1.6f && bounds.size.y < 2.0f
                                   ? "  (human scale)"
                                   : "  <-- NOT human scale, check Convert Units on the model"));
                log.AppendLine("  feet at y     : " + bounds.min.y.ToString("0.000") + " m" +
                               (Mathf.Abs(bounds.min.y) < 0.05f
                                   ? "  (stands on the floor)"
                                   : "  <-- the model does not sit on y = 0"));
                log.AppendLine("  width / depth : " + bounds.size.x.ToString("0.000") + " m / " +
                               bounds.size.z.ToString("0.000") + " m");

                int missing = 0;
                int skinnedBones = 0;
                for (int i = 0; i < renderers.Length; i++)
                {
                    var mats = renderers[i].sharedMaterials;
                    for (int m = 0; m < mats.Length; m++)
                        if (mats[m] == null) missing++;

                    var skinned = renderers[i] as SkinnedMeshRenderer;
                    if (skinned != null && skinned.bones != null)
                        skinnedBones += skinned.bones.Length;
                }

                log.AppendLine("  renderers     : " + renderers.Length +
                               ", empty material slots: " + missing +
                               (missing == 0 ? "  (nothing will render magenta)" : "  <-- WILL RENDER MAGENTA"));
                log.AppendLine("  skinned bones : " + skinnedBones);
                log.AppendLine("  colliders     : " + instance.GetComponentsInChildren<Collider>(true).Length +
                               "  (a character must have none; the CharacterController is the collider)");

                var animator = instance.GetComponentInChildren<Animator>();
                if (animator == null)
                {
                    log.AppendLine("  ERROR: no Animator");
                }
                else
                {
                    log.AppendLine("  animator      : rootMotion=" + animator.applyRootMotion +
                                   ", controller=" +
                                   (animator.runtimeAnimatorController != null
                                       ? animator.runtimeAnimatorController.name
                                       : "NONE"));
                }

                if (walk != null)
                {
                    var settings = AnimationUtility.GetAnimationClipSettings(walk);
                    log.AppendLine("  walk clip     : " + walk.name +
                                   ", " + walk.length.ToString("0.000") + " s @ " +
                                   walk.frameRate.ToString("0") + " fps, loopTime=" + settings.loopTime);
                }

                log.AppendLine("  NOT VERIFIED  : Play Mode was not run. Shading, foot contact and " +
                               "loop quality still need eyes on them.");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}
#endif
