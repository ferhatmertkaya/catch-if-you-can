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

        private const string BrushPath =
            "Assets/CatchIfYouCan/Resources/UI/Menu/T_MenuBrushStroke.png";
        private const string GrungePath =
            "Assets/CatchIfYouCan/Resources/UI/Menu/T_MenuGrungePanel.png";

        private const string GrungeName = "MenuGrungeBackdrop";
        private const string MenuRootName = "MainMenuNav";
        private const string BrushName = "SelectionBrush";
        private const string SettingsPanelName = "MenuSettingsPanel";
        private const string CreditsPanelName = "MenuCreditsPanel";

        private static readonly string[] RowLabels = { "PLAY", "SETTINGS", "CREDITS" };

        [MenuItem("Catch If You Can/1. LOBBY/Logo und Hauptmenue backen [AENDERT SZENE]", false, 105)]
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
            string menuReport = BuildMenu(scene, canvasGo);
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
                "[CIYC] Branding-Canvas in 01_MainMenu gebaut: Logo, Grunge-Hintergrund und das " +
                "Hauptmenue PLAY / SETTINGS / CREDITS.\n" +
                labelReport + "\n" + menuReport + "\n" + wiring + "\n" +
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
        /// The menu itself: the grunge backdrop, the three rows, the brush stroke, and the two
        /// panel shells.
        ///
        /// <para>
        /// <b>It also adds a GraphicRaycaster and an EventSystem, and that is not a detail.</b>
        /// This scene has neither today - which is exactly why the old menu read raw Input from
        /// <c>MainMenuTapToStart</c> instead of using a Button. Adding Buttons without both would
        /// have produced a menu that draws perfectly and answers nothing, which is the shape this
        /// project has paid for more than any other.
        /// </para>
        ///
        /// <para>
        /// <b>Where it sits was measured, not guessed.</b> The logo's rect runs from 16% to 88% of
        /// the screen, so "under the logo" looked impossible - 173 px. But a rect is not a
        /// drawing: the logo PNG is 1024x1536 and its ink, read out of the file, is empty below
        /// about a third of the screen height and spans x 3% to 27%. The menu takes that measured
        /// gap, left-aligned with the ink above it.
        /// </para>
        /// </summary>
        private static string BuildMenu(Scene scene, GameObject canvasGo)
        {
            var notes = new List<string>();

            var brushSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BrushPath);
            var grungeSprite = AssetDatabase.LoadAssetAtPath<Sprite>(GrungePath);

            if (brushSprite == null || grungeSprite == null)
            {
                // Named apart, because "no sprite" has two very different causes: the file is
                // missing, or it imported as a plain Texture rather than as a Sprite.
                return "WARNUNG: Menue-Sprites fehlen - brush=" +
                       (brushSprite != null ? "ok" : BrushPath) + " grunge=" +
                       (grungeSprite != null ? "ok" : GrungePath) +
                       ". Texture Type muss Sprite (2D and UI) sein.";
            }

            // --- the canvas must be able to receive a click at all -------------------------
            if (canvasGo.GetComponent<GraphicRaycaster>() == null)
            {
                Undo.AddComponent<GraphicRaycaster>(canvasGo);
                notes.Add("GraphicRaycaster ergaenzt (die Canvas hatte keinen - ohne ihn ist " +
                          "jeder Button sichtbar und tot).");
            }

            notes.Add(EnsureEventSystem(scene));

            // --- the dark irregular backdrop ----------------------------------------------
            GameObject grungeGo = EnsureChild(scene, canvasGo, GrungeName, out bool grungeNew);
            var grungeImage = Ensure<Image>(grungeGo);
            grungeImage.sprite = grungeSprite;
            grungeImage.type = Image.Type.Simple;
            grungeImage.preserveAspect = false;
            // Tinted rather than baked dark: the sprite carries its SHAPE in alpha and a flat
            // black in RGB, so the palette lives in UITheme and one texture serves any colour.
            grungeImage.color = new Color(0f, 0f, 0f, 0.82f);
            grungeImage.raycastTarget = false;
            SetRect(grungeGo, new Vector2(-0.02f, -0.015f), new Vector2(0.40f, 0.40f));
            grungeGo.transform.SetSiblingIndex(0);

            // --- the menu column ------------------------------------------------------------
            GameObject menuGo = EnsureChild(scene, canvasGo, MenuRootName, out _);
            SetRect(menuGo, new Vector2(0.035f, 0.045f), new Vector2(0.315f, 0.325f));

            var nav = Ensure<MainMenuNavigation>(menuGo);

            // --- the white stroke, ONE of it -------------------------------------------------
            GameObject brushGo = EnsureChild(scene, menuGo, BrushName, out _);
            var brushImage = Ensure<Image>(brushGo);
            brushImage.sprite = brushSprite;
            brushImage.type = Image.Type.Simple;
            brushImage.preserveAspect = false;
            brushImage.color = Color.white;
            brushImage.raycastTarget = false;
            RowRect(brushGo, 0);
            brushGo.transform.SetSiblingIndex(0);

            // --- the three rows --------------------------------------------------------------
            var built = new List<MainMenuNavigation.Row>();
            for (int i = 0; i < RowLabels.Length; i++)
            {
                GameObject rowGo = EnsureChild(scene, menuGo, "MenuRow_" + RowLabels[i], out _);
                RectTransform rowRect = RowRect(rowGo, i);

                // A transparent Image purely so the row is a raycast target. uGUI ignores alpha
                // when hit-testing unless a threshold is set, so this is clickable at alpha 0 -
                // and an invisible target the size of the row is a comfortable touch box.
                var hit = Ensure<Image>(rowGo);
                hit.color = new Color(0f, 0f, 0f, 0f);
                hit.raycastTarget = true;

                var button = Ensure<Button>(rowGo);
                // The transition is left at its default on purpose rather than set to None. uGUI's
                // colour tint multiplies the target graphic's colour, and this target is a fully
                // transparent Image - zero alpha times any tint is still zero. So Unity's own
                // highlight cannot draw anything, the brush and the caption colour remain the only
                // opinion about which row is selected, and no API this project cannot verify
                // offline is touched to achieve it.
                button.targetGraphic = hit;

                Component label = FindLabelComponent(rowGo);
                if (label == null)
                {
                    label = RuntimeUIFactory.CreateText(
                        rowGo.transform, "Caption", RowLabels[i], 44,
                        TextAnchor.MiddleLeft, true, UITheme.FontRole.Button);

                    if (label == null)
                    {
                        notes.Add("WARNUNG: Beschriftung '" + RowLabels[i] + "' konnte nicht " +
                                  "gebaut werden.");
                        continue;
                    }

                    Undo.RegisterCreatedObjectUndo(label.gameObject, "Bake menu");
                }

                UITheme.SetText(label, RowLabels[i]);
                UITheme.SetTextColor(label, i == 0 ? UITheme.BackgroundDark : UITheme.TextMuted);

                var labelRect = label.transform as RectTransform;
                if (labelRect != null)
                {
                    labelRect.anchorMin = new Vector2(0f, 0f);
                    labelRect.anchorMax = new Vector2(1f, 1f);
                    labelRect.offsetMin = new Vector2(34f, 0f);
                    labelRect.offsetMax = new Vector2(-12f, 0f);
                    labelRect.localScale = Vector3.one;
                }

                var hover = Ensure<MainMenuNavHover>(rowGo);
                hover.EditorBind(nav, i);
                EditorUtility.SetDirty(hover);

                built.Add(new MainMenuNavigation.Row
                {
                    button = button,
                    label = label,
                    rect = rowRect
                });

                EditorUtility.SetDirty(rowGo);
            }

            // --- the two panel shells ---------------------------------------------------------
            GameObject settings = BuildPanelShell(scene, canvasGo, SettingsPanelName, "SETTINGS",
                "Die Einstellungen kommen hierher. Diese Huelle ist absichtlich leer: der Knopf " +
                "ist verdrahtet, das System dahinter ist eine eigene Aufgabe.");
            GameObject credits = BuildPanelShell(scene, canvasGo, CreditsPanelName, "CREDITS",
                "Catch If You Can");

            // --- TAP ANYWHERE TO START goes away, and so does the input path behind it --------
            notes.Add(RetireTapToStart(scene));

            nav.EditorBind(
                FindComponentInScene<MainMenuModeController>(scene),
                built.ToArray(),
                brushGo.GetComponent<RectTransform>(),
                settings,
                credits);
            EditorUtility.SetDirty(nav);
            EditorUtility.SetDirty(menuGo);
            EditorUtility.SetDirty(grungeGo);

            notes.Add("Menue: " + built.Count + " von " + RowLabels.Length + " Zeilen, " +
                      "Pinselstrich " + (brushGo != null ? "gesetzt" : "FEHLT") +
                      ", Grunge " + (grungeNew ? "neu" : "aktualisiert") + ".");

            return string.Join("\n", notes);
        }

        /// <summary>
        /// Takes TAP ANYWHERE TO START off the screen AND out of the input path.
        ///
        /// <para>
        /// Hiding the label alone would have left the worse half behind.
        /// <see cref="MainMenuTapToStart"/> reads raw Input from anywhere on the screen, so with
        /// it still enabled one click on SETTINGS would have opened the settings panel AND handed
        /// the player to the lobby - one press through two paths, which is mistake 32 exactly.
        /// </para>
        /// <para>
        /// Disabled rather than destroyed: the component is still on the object, visible in the
        /// Inspector, and a tick puts the old behaviour back. A deleted one is a decision nobody
        /// can see was made.
        /// </para>
        /// </summary>
        private static string RetireTapToStart(Scene scene)
        {
            var report = new List<string>();

            GameObject labelGo = FindInScene(scene, LabelName);
            if (labelGo != null && labelGo.activeSelf)
            {
                Undo.RecordObject(labelGo, "Retire tap to start");
                labelGo.SetActive(false);
                EditorUtility.SetDirty(labelGo);
                report.Add("'" + LabelMessage + "' ausgeblendet");
            }
            else
            {
                report.Add("'" + LabelMessage + "' war schon weg");
            }

            var tap = FindComponentInScene<MainMenuTapToStart>(scene);
            if (tap != null && tap.enabled)
            {
                Undo.RecordObject(tap, "Retire tap to start");
                tap.enabled = false;
                EditorUtility.SetDirty(tap);
                report.Add("MainMenuTapToStart abgeschaltet (sonst startet ein Klick auf " +
                           "SETTINGS zusaetzlich die Lobby)");
            }
            else
            {
                report.Add("MainMenuTapToStart war schon aus" + (tap == null ? " (nicht da)" : ""));
            }

            return string.Join(", ", report);
        }

        /// <summary>A panel shell: a dim sheet, a title, a line, and a way back out.</summary>
        private static GameObject BuildPanelShell(Scene scene, GameObject canvasGo, string name,
                                                  string title, string body)
        {
            GameObject panel = EnsureChild(scene, canvasGo, name, out bool created);

            var sheet = Ensure<Image>(panel);
            sheet.sprite = null;
            sheet.color = UITheme.Overlay;
            sheet.raycastTarget = true;
            SetRect(panel, Vector2.zero, Vector2.one);

            if (created)
            {
                Component head = RuntimeUIFactory.CreateText(panel.transform, "Title", title, 64,
                    TextAnchor.MiddleCenter, true, UITheme.FontRole.Title);
                if (head != null)
                {
                    Undo.RegisterCreatedObjectUndo(head.gameObject, "Bake menu");
                    UITheme.SetTextColor(head, UITheme.TextPrimary);
                    SetRect(head.gameObject, new Vector2(0.2f, 0.60f), new Vector2(0.8f, 0.74f));
                }

                Component line = RuntimeUIFactory.CreateText(panel.transform, "Body", body, 28,
                    TextAnchor.MiddleCenter, false, UITheme.FontRole.Body);
                if (line != null)
                {
                    Undo.RegisterCreatedObjectUndo(line.gameObject, "Bake menu");
                    UITheme.SetTextColor(line, UITheme.TextMuted);
                    SetRect(line.gameObject, new Vector2(0.2f, 0.44f), new Vector2(0.8f, 0.58f));
                }

                Button back = RuntimeUIFactory.CreateButton(panel.transform, "ZURUECK", null);
                if (back != null)
                {
                    Undo.RegisterCreatedObjectUndo(back.gameObject, "Bake menu");
                    SetRect(back.gameObject, new Vector2(0.40f, 0.26f), new Vector2(0.60f, 0.36f));
                }

                // The back button is NOT wired here. A listener added at bake time is either a
                // lambda, which is not serialised and comes back empty in Play, or a persistent
                // listener through an editor API this project cannot verify offline. The panel is
                // handed to MainMenuNavigation instead, which wires every button inside it in
                // Awake - one path, and one that certainly runs.
            }

            panel.SetActive(false);
            EditorUtility.SetDirty(panel);
            return panel;
        }

        /// <summary>
        /// An EventSystem in THIS scene.
        ///
        /// <para>
        /// Through <see cref="EventSystemUtil"/>, which already decides between the old and the
        /// new input backend - writing that branch here would be a second answer to a question
        /// the project has answered once. What it cannot decide is which SCENE the object lands
        /// in: <c>new GameObject</c> goes to the ACTIVE scene, and that is mistake 17. So the
        /// result is checked and moved if it went somewhere else.
        /// </para>
        /// </summary>
        private static string EnsureEventSystem(Scene scene)
        {
            var existing = Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
            if (existing != null && existing.gameObject.scene == scene)
                return "EventSystem war schon in dieser Szene.";

            var es = EventSystemUtil.EnsureEventSystem();
            if (es == null)
                return "WARNUNG: kein EventSystem - Maus und Touch erreichen das Menue nicht.";

            if (es.gameObject.scene != scene)
            {
                SceneManager.MoveGameObjectToScene(es.gameObject, scene);
                Undo.RegisterCreatedObjectUndo(es.gameObject, "Bake menu");
                return "EventSystem gebaut und in 01_MainMenu verschoben.";
            }

            Undo.RegisterCreatedObjectUndo(es.gameObject, "Bake menu");
            return "EventSystem gebaut.";
        }

        /// <summary>One row's rectangle: full width of the column, stacked from the top.</summary>
        private static RectTransform RowRect(GameObject go, int index)
        {
            var rect = go.GetComponent<RectTransform>();
            if (rect == null)
                rect = go.AddComponent<RectTransform>();

            const float RowHeight = 84f;
            const float RowStep = 100f;

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(0f, 0f);
            rect.offsetMax = new Vector2(0f, 0f);
            rect.sizeDelta = new Vector2(0f, RowHeight);
            rect.anchoredPosition = new Vector2(0f, -index * RowStep);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            return rect;
        }

        private static void SetRect(GameObject go, Vector2 min, Vector2 max)
        {
            var rect = go.GetComponent<RectTransform>();
            if (rect == null)
                rect = go.AddComponent<RectTransform>();

            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private static GameObject EnsureChild(Scene scene, GameObject parent, string name,
                                              out bool created)
        {
            GameObject go = FindInScene(scene, name);
            created = go == null;

            if (created)
            {
                go = new GameObject(name, typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(go, "Bake menu");
            }

            go.transform.SetParent(parent.transform, false);
            go.SetActive(true);
            return go;
        }

        /// <summary>GetComponent before AddComponent, on every run (CLAUDE.md mistake 27).</summary>
        private static T Ensure<T>(GameObject go) where T : Component
        {
            T have = go.GetComponent<T>();
            return have != null ? have : Undo.AddComponent<T>(go);
        }

        /// <summary>
        /// The caption a row already carries, whatever type the text factory built it as.
        ///
        /// <para>
        /// Found by NAME on a child rather than by component type: this file cannot see TMPro at
        /// all - the project guards it behind TMP_PRESENT because it is optional - so there is no
        /// type here to ask for.
        /// </para>
        /// </summary>
        private static Component FindLabelComponent(GameObject rowGo)
        {
            Transform child = rowGo.transform.Find("Caption");
            if (child == null)
                return null;

            var graphics = child.GetComponents<Component>();
            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i] is Graphic && !(graphics[i] is Image))
                    return graphics[i];
            }

            return null;
        }

        private static T FindComponentInScene<T>(Scene scene) where T : Component
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var found = roots[i].GetComponentsInChildren<T>(true);
                if (found.Length > 0)
                    return found[0];
            }
            return null;
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
