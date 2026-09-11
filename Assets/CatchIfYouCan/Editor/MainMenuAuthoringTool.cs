using System.Collections.Generic;
using CatchIfYouCan.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CatchIfYouCan.EditorTools
{
    /// <summary>
    /// Writes the main menu into <c>01_MainMenu.unity</c> as REAL, SAVED GameObjects.
    ///
    /// <para>
    /// <b>This is authoring, not building.</b> Everything it writes is a normal Unity object you
    /// can select in the Hierarchy, drag in the Scene view, and edit in the Inspector. Once the
    /// scene is saved, no code creates any of it again: <see cref="MainMenuNavigation"/> only
    /// decides which row is selected and what a press does. The scene is the design; C# is the
    /// behaviour.
    /// </para>
    ///
    /// <para>
    /// <b>It never overwrites your work.</b> Every object is looked up before it is created, and
    /// an object that already exists keeps its RectTransform, its text, its font, its size and
    /// its colour. Run it twice and the second run changes nothing. That is what makes it safe to
    /// re-run after you have moved things by hand - it fills in what is missing and leaves the
    /// rest alone.
    /// </para>
    ///
    /// <para>
    /// <b>It reuses the Canvas and the EventSystem.</b> The scene already has
    /// <c>MainMenuBrandingCanvas</c> carrying the logo, and the menu goes inside it rather than
    /// beside it in a canvas of its own - two canvases would be two draw orders arguing about
    /// which one is on top, and a second EventSystem makes Unity disable one of them at random.
    /// </para>
    ///
    /// <para>
    /// The scene is marked dirty and NOT saved. Look at the result first, then save.
    /// </para>
    /// </summary>
    public static class MainMenuAuthoringTool
    {
        private static readonly string ScenePath =
            "Assets/CatchIfYouCan/Scenes/01_MainMenu.unity";

        private const string CanvasName = "MainMenuBrandingCanvas";
        private const string RootName = "MainMenuRoot";
        private const string GrungeName = "MenuGrungeBackground";
        private const string NavName = "Navigation";
        private const string BrushChildName = "SelectionBrush";
        private const string TapLabelName = "TapToStartText";
        private const string SettingsPanelName = "MenuSettingsPanel";
        private const string CreditsPanelName = "MenuCreditsPanel";

        private const string BrushPath =
            "Assets/CatchIfYouCan/Resources/UI/Menu/T_MenuBrushStroke.png";
        private const string GrungePath =
            "Assets/CatchIfYouCan/Resources/UI/Menu/T_MenuGrungePanel.png";

        // No font constant here on purpose. UITheme.ApplyFont already assigns the project's
        // branded font for a role, and it is the ONE place that decides TextMeshPro-or-legacy -
        // a decision this file must not make a second time. Reaching past it for a TMP type is
        // precisely what took the build down: TMP_PRESENT is not defined in this project, so an
        // unguarded `using TMPro;` fails the whole assembly.

        /// <summary>The three rows, and the text each one STARTS with. The scene wins after that.</summary>
        private static readonly (string Id, string Caption)[] Rows =
        {
            ("Play", "PLAY"),
            ("Settings", "SETTINGS"),
            ("Credits", "CREDITS"),
        };

        // Defaults only - every one of these is a starting point you are meant to drag. They are
        // fractions of the canvas rather than pixels, so the menu lands in the same place on a
        // phone in landscape, on 16:9 and on an ultrawide.
        private const float NavLeft = 0.055f;
        private const float NavRight = 0.400f;
        private const float NavBottom = 0.150f;
        private const float NavTop = 0.455f;
        private const float RowHeightPx = 74f;
        private const float RowStepPx = 86f;
        private const int CaptionSize = 38;

        [MenuItem("Catch If You Can/1. LOBBY/Hauptmenue in die Szene schreiben [AENDERT SZENE]",
                  false, 106)]
        public static void AuthorMainMenu()
        {
            Scene scene = SceneManager.GetActiveScene();

            if (scene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return;

                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            GameObject canvasGo = FindInScene(scene, CanvasName);
            if (canvasGo == null)
            {
                Debug.LogError("[CIYC] " + CanvasName + " is not in " + scene.name +
                               ". The menu goes inside the existing branding canvas; this tool " +
                               "does not build a second one.");
                return;
            }

            var notes = new List<string>();

            if (canvasGo.GetComponent<GraphicRaycaster>() == null)
            {
                Undo.AddComponent<GraphicRaycaster>(canvasGo);
                notes.Add("GraphicRaycaster ergaenzt (ohne ihn ist jeder Knopf sichtbar und tot).");
            }

            notes.Add(EnsureEventSystem(scene));

            // ---- MainMenuRoot: a full-screen holder so everything below anchors inside it ----
            GameObject root = EnsureChild(canvasGo, RootName, out bool rootNew);
            if (rootNew)
                Stretch(root, Vector2.zero, Vector2.one);

            // ---- MenuGrungeBackground -------------------------------------------------------
            GameObject grunge = EnsureChild(root, GrungeName, out bool grungeNew);
            var grungeImage = Ensure<Image>(grunge);
            if (grungeNew)
            {
                Stretch(grunge, new Vector2(-0.01f, 0.075f), new Vector2(0.50f, 0.545f));
                grungeImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(GrungePath);
                grungeImage.type = Image.Type.Simple;
                grungeImage.preserveAspect = false;
                grungeImage.color = new Color(1f, 1f, 1f, 0.85f);
                grungeImage.raycastTarget = false;
                grunge.transform.SetSiblingIndex(0);

                notes.Add(grungeImage.sprite != null
                    ? "MenuGrungeBackground gebaut."
                    : "MenuGrungeBackground gebaut, ABER " + GrungePath + " laedt nicht als " +
                      "Sprite - Texture Type muss Sprite (2D and UI) sein. Weise das Sprite " +
                      "von Hand zu.");
            }

            // ---- Navigation ------------------------------------------------------------------
            GameObject nav = EnsureChild(root, NavName, out bool navNew);
            if (navNew)
                Stretch(nav, new Vector2(NavLeft, NavBottom), new Vector2(NavRight, NavTop));

            var brushSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BrushPath);
            if (brushSprite == null)
                notes.Add("WARNUNG: " + BrushPath + " laedt nicht als Sprite. Die Pinselstriche " +
                          "entstehen ohne Bild; weise es von Hand zu.");

            var built = new List<MainMenuNavigation.Row>();

            for (int i = 0; i < Rows.Length; i++)
            {
                GameObject buttonGo = EnsureChild(nav, "Button_" + Rows[i].Id, out bool btnNew);

                if (btnNew)
                {
                    var rect = Rect(buttonGo);
                    rect.anchorMin = new Vector2(0f, 1f);
                    rect.anchorMax = new Vector2(1f, 1f);
                    rect.pivot = new Vector2(0.5f, 1f);
                    rect.offsetMin = Vector2.zero;
                    rect.offsetMax = Vector2.zero;
                    rect.sizeDelta = new Vector2(0f, RowHeightPx);
                    rect.anchoredPosition = new Vector2(0f, -i * RowStepPx);
                    rect.localScale = Vector3.one;
                }

                // A transparent Image so the whole row is a comfortable touch target. uGUI
                // ignores alpha when hit-testing unless a threshold is set, so this is clickable
                // at alpha 0 while drawing nothing.
                var hit = Ensure<Image>(buttonGo);
                if (btnNew)
                {
                    hit.color = new Color(0f, 0f, 0f, 0f);
                    hit.raycastTarget = true;
                }

                var button = Ensure<Button>(buttonGo);
                button.targetGraphic = hit;
                // Left at the default transition rather than set to None: uGUI's colour tint
                // MULTIPLIES the target graphic's colour, and this target is fully transparent -
                // zero alpha times any tint is still zero. So Unity's own highlight cannot draw
                // anything and the brush stays the only statement about which row is selected.

                Ensure<MainMenuNavHover>(buttonGo);

                // ---- SelectionBrush, one per row, a child of the row ------------------------
                GameObject brushGo = EnsureChild(buttonGo, BrushChildName, out bool brushNew);
                var brushImage = Ensure<Image>(brushGo);
                if (brushNew)
                {
                    var br = Rect(brushGo);
                    br.anchorMin = Vector2.zero;
                    br.anchorMax = Vector2.one;
                    // Wider than the row on the left, so the stroke overhangs the text the way a
                    // painted mark would rather than stopping square at the caption.
                    br.offsetMin = new Vector2(-22f, 2f);
                    br.offsetMax = new Vector2(14f, -2f);
                    br.localScale = Vector3.one;

                    brushImage.sprite = brushSprite;
                    brushImage.type = Image.Type.Simple;
                    brushImage.preserveAspect = false;
                    brushImage.color = Color.white;
                    brushImage.raycastTarget = false;
                    brushGo.transform.SetSiblingIndex(0);
                }

                // Only PLAY starts selected. Switched here rather than left to the first frame so
                // the Scene view shows what Play Mode will show.
                brushGo.SetActive(i == 0);

                // ---- Label_* -----------------------------------------------------------------
                Component label = EnsureLabel(buttonGo, "Label_" + Rows[i].Id, Rows[i].Caption,
                                              CaptionSize, TextAnchor.MiddleLeft,
                                              UITheme.FontRole.Button, out bool lblNew);
                if (label == null)
                {
                    notes.Add("WARNUNG: Beschriftung '" + Rows[i].Caption + "' nicht gebaut.");
                    continue;
                }

                if (lblNew)
                {
                    // The ONLY place a caption's text is ever written. After this the scene is
                    // authoritative and nothing in the project touches it again.
                    UITheme.SetText(label, Rows[i].Caption);
                    UITheme.SetTextColor(label, i == 0
                        ? new Color(0.055f, 0.055f, 0.067f, 1f)
                        : new Color(0.78f, 0.78f, 0.80f, 1f));

                    var lr = Rect(label.gameObject);
                    lr.anchorMin = Vector2.zero;
                    lr.anchorMax = Vector2.one;
                    lr.offsetMin = new Vector2(54f, 0f);
                    lr.offsetMax = new Vector2(-12f, 0f);
                    lr.localScale = Vector3.one;
                }

                built.Add(new MainMenuNavigation.Row
                {
                    button = button,
                    label = label,
                    selectionBrush = brushGo,
                });

                EditorUtility.SetDirty(buttonGo);
            }

            // ---- the two panels ---------------------------------------------------------------
            GameObject settings = EnsurePanel(root, SettingsPanelName, "SETTINGS",
                "Die Einstellungen kommen hierher. Diese Huelle ist absichtlich leer: der Knopf " +
                "ist verdrahtet, das System dahinter ist eine eigene Aufgabe.");
            GameObject credits = EnsurePanel(root, CreditsPanelName, "CREDITS",
                "Catch If You Can");

            notes.Add(RetireTapToStart(scene));

            // ---- the behaviour component, bound to what was just authored ---------------------
            var navigation = Ensure<MainMenuNavigation>(nav);
            navigation.Bind(FindComponentInScene<MainMenuModeController>(scene),
                            built.ToArray(), settings, credits);
            EditorUtility.SetDirty(navigation);
            EditorUtility.SetDirty(nav);
            EditorUtility.SetDirty(root);

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = nav;

            Debug.Log(
                "[CIYC] Hauptmenue in " + scene.name + " geschrieben: " + built.Count + " Zeilen.\n" +
                string.Join("\n", notes) + "\n" +
                "Die Beschriftungen stehen jetzt in der Szene: " + RootName + "/" + NavName +
                "/Button_Play/Label_Play und so weiter. Der Text gehoert ab hier DIR - kein Code " +
                "schreibt ihn noch.\n" +
                "Die Szene ist GEAENDERT, aber NICHT gespeichert. Erst ansehen, dann speichern.");
        }

        /// <summary>A panel shell: a dim sheet, a title, a line of body text, and a way out.</summary>
        private static GameObject EnsurePanel(GameObject parent, string name, string title,
                                              string body)
        {
            GameObject panel = EnsureChild(parent, name, out bool created);

            var sheet = Ensure<Image>(panel);
            if (created)
            {
                Stretch(panel, Vector2.zero, Vector2.one);
                sheet.sprite = null;
                sheet.color = new Color(0.02f, 0.02f, 0.03f, 0.92f);
                sheet.raycastTarget = true;

                PanelLabel(panel, "Title", title, 64, UITheme.FontRole.Title,
                           new Vector2(0.2f, 0.60f), new Vector2(0.8f, 0.74f), Color.white);
                PanelLabel(panel, "Body", body, 26, UITheme.FontRole.Body,
                           new Vector2(0.2f, 0.40f), new Vector2(0.8f, 0.58f),
                           new Color(0.72f, 0.72f, 0.74f, 1f));

                GameObject backGo = EnsureChild(panel, "Button_Back", out _);
                Stretch(backGo, new Vector2(0.40f, 0.26f), new Vector2(0.60f, 0.34f));
                var backImage = Ensure<Image>(backGo);
                backImage.color = new Color(1f, 1f, 1f, 0.10f);
                backImage.raycastTarget = true;
                Ensure<Button>(backGo).targetGraphic = backImage;
                PanelLabel(backGo, "Label_Back", "ZURUECK", 24, UITheme.FontRole.Button,
                           Vector2.zero, Vector2.one, Color.white);

                // The back button is NOT wired here. A listener added at author time is either a
                // lambda, which Unity does not serialise, or a persistent listener through an API
                // this project cannot verify offline. MainMenuNavigation wires every button
                // inside the panel instead - one path, and one that certainly runs.
            }

            panel.SetActive(false);
            EditorUtility.SetDirty(panel);
            return panel;
        }

        private static void PanelLabel(GameObject parent, string name, string text, int size,
                                       UITheme.FontRole role, Vector2 min, Vector2 max,
                                       Color colour)
        {
            Component label = EnsureLabel(parent, name, text, size, TextAnchor.MiddleCenter,
                                          role, out bool created);
            if (label == null || !created)
                return;

            UITheme.SetText(label, text);
            UITheme.SetTextColor(label, colour);
            Stretch(label.gameObject, min, max);
        }

        /// <summary>
        /// A caption, built through the project's OWN text factory.
        ///
        /// <para>
        /// <see cref="RuntimeUIFactory.CreateText"/> is the single place this project decides
        /// between a TextMeshPro label and a legacy one, and it is guarded behind TMP_PRESENT
        /// because that define is not set here. Naming a TMPro type directly - which an earlier
        /// version of this file did - fails the entire assembly when the namespace does not
        /// resolve, and a broken Assembly-CSharp is not a broken menu, it is a broken GAME.
        /// </para>
        /// <para>
        /// An EXISTING label is returned untouched, so its text, font, size and rectangle are
        /// whatever the last person set them to.
        /// </para>
        /// </summary>
        private static Component EnsureLabel(GameObject parent, string name, string caption,
                                             int size, TextAnchor align, UITheme.FontRole role,
                                             out bool created)
        {
            GameObject existing = FindUnder(parent.transform, name);
            if (existing != null)
            {
                created = false;
                return FindTextComponent(existing);
            }

            created = true;
            Component made = RuntimeUIFactory.CreateText(parent.transform, name, caption, size,
                                                         align, true, role);
            if (made != null)
                Undo.RegisterCreatedObjectUndo(made.gameObject, "Author main menu");

            return made;
        }

        /// <summary>
        /// The text component on an object, whatever type the factory built it as. By
        /// Graphic-minus-Image rather than by type, because this file must not name a TMPro type.
        /// </summary>
        private static Component FindTextComponent(GameObject go)
        {
            var parts = go.GetComponents<Component>();
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] is Graphic && !(parts[i] is Image))
                    return parts[i];
            }
            return null;
        }

        /// <summary>
        /// Takes TAP ANYWHERE TO START off the screen AND out of the input path.
        ///
        /// <para>
        /// Hiding the label alone leaves the worse half behind.
        /// <see cref="MainMenuTapToStart"/> reads raw Input from anywhere on the screen, so with
        /// it still enabled one click on SETTINGS would open the settings panel AND hand the
        /// player to the lobby - one press through two paths, which is mistake 32 exactly.
        /// </para>
        /// <para>
        /// Disabled rather than destroyed: the component stays on the object, visible in the
        /// Inspector, and a tick puts the old behaviour back. A deleted one is a decision nobody
        /// can see was made.
        /// </para>
        /// </summary>
        private static string RetireTapToStart(Scene scene)
        {
            var report = new List<string>();

            GameObject labelGo = FindInScene(scene, TapLabelName);
            if (labelGo != null && labelGo.activeSelf)
            {
                Undo.RecordObject(labelGo, "Retire tap to start");
                labelGo.SetActive(false);
                EditorUtility.SetDirty(labelGo);
                report.Add("'TAP ANYWHERE TO START' ausgeblendet");
            }
            else
            {
                report.Add("'TAP ANYWHERE TO START' war schon weg");
            }

            var tap = FindComponentInScene<MainMenuTapToStart>(scene);
            if (tap != null && tap.enabled)
            {
                Undo.RecordObject(tap, "Retire tap to start");
                tap.enabled = false;
                EditorUtility.SetDirty(tap);
                report.Add("MainMenuTapToStart abgeschaltet");
            }
            else
            {
                report.Add("MainMenuTapToStart war schon aus" + (tap == null ? " (nicht da)" : ""));
            }

            return string.Join(", ", report);
        }

        private static string EnsureEventSystem(Scene scene)
        {
            var existing = Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
            if (existing != null && existing.gameObject.scene == scene)
                return "EventSystem war schon in dieser Szene.";

            var es = EventSystemUtil.EnsureEventSystem();
            if (es == null)
                return "WARNUNG: kein EventSystem - Maus und Touch erreichen das Menue nicht.";

            // new GameObject lands in the ACTIVE scene, which is not necessarily this one
            // (CLAUDE.md mistake 17). Moved rather than assumed.
            if (es.gameObject.scene != scene)
            {
                SceneManager.MoveGameObjectToScene(es.gameObject, scene);
                Undo.RegisterCreatedObjectUndo(es.gameObject, "Author main menu");
                return "EventSystem gebaut und in diese Szene verschoben.";
            }

            Undo.RegisterCreatedObjectUndo(es.gameObject, "Author main menu");
            return "EventSystem gebaut.";
        }

        private static RectTransform Rect(GameObject go)
        {
            var rect = go.GetComponent<RectTransform>();
            return rect != null ? rect : go.AddComponent<RectTransform>();
        }

        private static void Stretch(GameObject go, Vector2 min, Vector2 max)
        {
            var rect = Rect(go);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        /// <summary>
        /// Looked up before it is created, so a second run adopts the first one's work - and so
        /// anything you have moved, renamed a value on, or retyped is left exactly as you left it.
        /// </summary>
        private static GameObject EnsureChild(GameObject parent, string name, out bool created)
        {
            GameObject go = FindUnder(parent.transform, name);
            created = go == null;

            if (created)
            {
                go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(parent.transform, false);
                Undo.RegisterCreatedObjectUndo(go, "Author main menu");
            }

            return go;
        }

        /// <summary>GetComponent before AddComponent, on every run (CLAUDE.md mistake 27).</summary>
        private static T Ensure<T>(GameObject go) where T : Component
        {
            T have = go.GetComponent<T>();
            return have != null ? have : Undo.AddComponent<T>(go);
        }

        /// <summary>
        /// A descendant by name, INCLUDING inactive ones. The panels and the retired tap label
        /// are exactly the kind this looks for, and an active-only search would miss them and
        /// build a second one beside each.
        /// </summary>
        private static GameObject FindUnder(Transform root, string name)
        {
            var all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != root && all[i].name == name)
                    return all[i].gameObject;
            }
            return null;
        }

        private static GameObject FindInScene(Scene scene, string name)
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == name)
                    return roots[i];

                GameObject found = FindUnder(roots[i].transform, name);
                if (found != null)
                    return found;
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
    }
}
