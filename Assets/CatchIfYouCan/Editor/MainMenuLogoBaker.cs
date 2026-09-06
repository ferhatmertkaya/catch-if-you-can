#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using CatchIfYouCan.UI;

namespace CatchIfYouCan.EditorTools
{
    /// <summary>
    /// Owns the cinematic menu's branding canvas: the canvas itself, the baked logo, the
    /// TAP ANYWHERE TO START label, and the reference the mode controller hides them by.
    ///
    /// <para>
    /// All four together, in one tool, because they are one thing. The canvas was deleted while
    /// the lobby was rebuilt by hand, and rebuilding only the logo would leave two of the other
    /// three broken: no label at all, and a null slot in
    /// <c>MainMenuModeController.cinematicUiRoots</c>. That null does not throw - the controller
    /// skips nulls - it just means nothing is hidden at the handover, so the logo and the label
    /// would stay on screen over the lobby. Restoring three quarters of a thing is how the next
    /// bug gets built.
    /// </para>
    /// <para>
    /// The label's geometry is restored from what the scene had: centred, 650 x 80 at 0.75
    /// scale, 220 px below the middle, 26 pt. Its TYPE is not hand-built here - it comes from
    /// <see cref="RuntimeUIFactory.CreateText"/>, the project's one text builder, which already
    /// knows whether TextMeshPro is present and falls back to a legacy Text when it is not.
    /// Writing that branch a second time would be the two-flashlights mistake, and this file
    /// cannot even see TMPro: the project guards it behind TMP_PRESENT because it is optional.
    /// </para>
    /// <para>
    /// One deliberate difference from the deleted original, said out loud rather than slipped
    /// in: the old label was set in TextMeshPro's default sans, and this one is set in the
    /// project's Header face, because that is what <c>FontRole</c> exists for - leaving it out
    /// is how every screen ended up in the built-in sans.
    /// </para>
    /// </summary>
    public static class MainMenuLogoBaker
    {
        private static readonly string ScenePath =
            Core.CiycScenes.PathOf(Core.CiycScene.MainMenu);

        private const string LogoPath =
            "Assets/CatchIfYouCan/Resources/UI/Branding/CatchIfYouCan_Logo.png";

        private const string CanvasName = "MainMenuBrandingCanvas";
        private const string LogoName = "GameLogo_Baked";
        private const string LabelName = "TapToStartText";
        private const string LabelMessage = "TAP ANYWHERE TO START";

        [MenuItem("Catch If You Can/Scene Authoring/Logo und TAP-Text backen [AENDERT SZENE]", false, 304)]
        public static void BakeLogoIntoScene()
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(LogoPath);

            if (sprite == null)
            {
                Debug.LogError(
                    "[CIYC] Logo could not be loaded as Sprite: " +
                    LogoPath +
                    ". Texture Type must be Sprite (2D and UI).");
                return;
            }

            var scene = SceneManager.GetActiveScene();

            if (scene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return;

                scene = EditorSceneManager.OpenScene(
                    ScenePath,
                    OpenSceneMode.Single);
            }

            GameObject canvasGo = FindInScene(scene, CanvasName);

            if (canvasGo == null)
            {
                canvasGo = new GameObject(
                    CanvasName,
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(CanvasScaler));
                Undo.RegisterCreatedObjectUndo(canvasGo, "Bake branding");
            }

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 101;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            GameObject logoGo = FindInScene(scene, LogoName);

            if (logoGo == null)
            {
                logoGo = new GameObject(
                    LogoName,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                Undo.RegisterCreatedObjectUndo(logoGo, "Bake branding");
            }

            logoGo.transform.SetParent(canvasGo.transform, false);

            var rect = logoGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(-0.15f, 0.16f);
            rect.anchorMax = new Vector2(0.45f, 0.88f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;

            var image = logoGo.GetComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.enabled = true;

            GameObject labelGo = BuildLabel(scene, canvasGo, out string labelReport);
            string wiring = WireIntoModeController(scene, canvasGo);

            EditorUtility.SetDirty(canvasGo);
            EditorUtility.SetDirty(logoGo);
            if (labelGo != null)
                EditorUtility.SetDirty(labelGo);

            // Marked dirty, NOT saved. This used to call SaveScene, which meant one click here
            // also wrote out every unrelated edit anyone had open - the only command in the
            // project that could do that, and nothing said so. The result is now looked at
            // first and saved by hand.
            EditorSceneManager.MarkSceneDirty(scene);

            Selection.activeGameObject = canvasGo;

            Debug.Log(
                "[CIYC] Branding-Canvas in 01_MainMenu gebaut: Logo mit direkter Sprite-Referenz " +
                "und die Beschriftung '" + LabelMessage + "'.\n" +
                labelReport + "\n" + wiring + "\n" +
                "Die Szene ist GEAENDERT, aber NICHT gespeichert. Erst ansehen, dann speichern.");
        }

        /// <summary>
        /// The TAP ANYWHERE TO START label, built by the project's own text factory.
        ///
        /// <para>
        /// It is a plain label with no button behind it. The tap is read by
        /// <see cref="MainMenuTapToStart"/> straight from Input, deliberately, so a full-screen
        /// invisible button does not sit over the menu swallowing everything else. Which also
        /// means the label going missing was silent: tapping still worked, there was simply
        /// nothing on screen saying so.
        /// </para>
        /// <para>
        /// An existing label keeps its own component and only has its rectangle corrected. Only
        /// a missing one is built, and then through <see cref="RuntimeUIFactory.CreateText"/> -
        /// so the TextMeshPro-or-legacy decision is made in the one place that already makes it.
        /// </para>
        /// </summary>
        private static GameObject BuildLabel(Scene scene, GameObject canvasGo, out string how)
        {
            GameObject labelGo = FindInScene(scene, LabelName);

            if (labelGo == null)
            {
                Component text = RuntimeUIFactory.CreateText(
                    canvasGo.transform, LabelName, LabelMessage, 26,
                    TextAnchor.MiddleCenter, false, UITheme.FontRole.Header);

                if (text == null)
                {
                    how = "WARNUNG: der Text konnte nicht gebaut werden. Kein TAP-Hinweis.";
                    return null;
                }

                labelGo = text.gameObject;
                Undo.RegisterCreatedObjectUndo(labelGo, "Bake branding");
                how = "Label neu gebaut (" + text.GetType().Name + ").";
            }
            else
            {
                how = "Label war schon da; nur das Rechteck gesetzt.";
            }

            labelGo.transform.SetParent(canvasGo.transform, false);

            var rect = labelGo.GetComponent<RectTransform>();
            if (rect == null)
            {
                how += " WARNUNG: kein RectTransform - das Rechteck bleibt ungesetzt.";
                return labelGo;
            }

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, -220f);
            rect.sizeDelta = new Vector2(650f, 80f);
            rect.localScale = Vector3.one * 0.75f;
            rect.localRotation = Quaternion.identity;

            return labelGo;
        }

        /// <summary>
        /// Puts the canvas back into the controller's cinematic UI roots, so the handover hides
        /// it again.
        ///
        /// <para>
        /// This is the half nobody notices is missing. A null slot in that array does not throw
        /// and does not log: the controller's loops skip nulls. The symptom is only visible one
        /// screen later, as branding sitting over the lobby.
        /// </para>
        /// <para>
        /// Through a public method on the controller rather than reflection or a
        /// SerializedProperty looked up by string. Both of those keep compiling after the field
        /// is renamed and quietly stop doing anything (CLAUDE.md mistake 4).
        /// </para>
        /// </summary>
        private static string WireIntoModeController(Scene scene, GameObject canvasGo)
        {
            // Walked rather than asked for with an includeInactive overload: the offline
            // typecheck harness does not carry that one, and a stub agreeing with me is not
            // verification (CLAUDE.md mistake 9). Inactive objects must be included - the
            // controller sits on an active root today, but nothing guarantees that tomorrow.
            MainMenuModeController controller = null;
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length && controller == null; i++)
            {
                var all = roots[i].GetComponentsInChildren<Transform>(true);
                for (int c = 0; c < all.Length && controller == null; c++)
                    controller = all[c].GetComponent<MainMenuModeController>();
            }

            if (controller == null)
                return "WARNUNG: kein MainMenuModeController in der Szene. Die Canvas wird beim " +
                       "Uebergang in die Lobby NICHT ausgeblendet und steht dann ueber dem Raum.";

            GameObject[] have = controller.EditorCinematicUiRoots;
            var wanted = new List<GameObject>();
            bool already = false;

            for (int i = 0; have != null && i < have.Length; i++)
            {
                if (have[i] == null)
                    continue;
                wanted.Add(have[i]);
                if (have[i] == canvasGo)
                    already = true;
            }

            if (!already)
                wanted.Add(canvasGo);

            Undo.RecordObject(controller, "Bake branding");
            controller.EditorSetCinematicUiRoots(wanted.ToArray());
            EditorUtility.SetDirty(controller);

            // Read back, because "I set it" and "it is set" are different claims.
            GameObject[] now = controller.EditorCinematicUiRoots;
            int nulls = 0, found = 0;
            for (int i = 0; now != null && i < now.Length; i++)
            {
                if (now[i] == null)
                    nulls++;
                else if (now[i] == canvasGo)
                    found++;
            }

            if (found != 1)
                return "WARNUNG: die Canvas steht " + found + " mal in cinematicUiRoots. " +
                       "Erwartet war genau einmal.";

            return "cinematicUiRoots: " + (now != null ? now.Length : 0) + " Eintrag(e), " +
                   "davon leer " + nulls + ". Die Canvas ist eingetragen und wird beim " +
                   "Uebergang ausgeblendet." + (already ? " (war schon drin)" : "");
        }

        /// <summary>
        /// Finds by name across the scene's roots INCLUDING inactive objects.
        ///
        /// <para>
        /// <c>GameObject.Find</c> skips inactive ones, and a branding canvas that was switched
        /// off at a handover and then saved is exactly that. Found with Find, it would be
        /// rebuilt beside the one already there.
        /// </para>
        /// </summary>
        private static GameObject FindInScene(Scene scene, string name)
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == name)
                    return roots[i];

                var all = roots[i].GetComponentsInChildren<Transform>(true);
                for (int c = 0; c < all.Length; c++)
                    if (all[c].name == name)
                        return all[c].gameObject;
            }
            return null;
        }
    }
}
#endif
