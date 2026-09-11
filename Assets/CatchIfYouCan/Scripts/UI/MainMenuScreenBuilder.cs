using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CatchIfYouCan.UI
{
    /// <summary>
    /// Builds the main menu - PLAY, SETTINGS, CREDITS, their brush strokes and the grunge
    /// backing - into the branding canvas.
    ///
    /// <para>
    /// <b>There is ONE of these, and two callers.</b> `MainMenuAuthoringTool` calls it from the
    /// editor so the objects are written into `01_MainMenu.unity` and saved, and
    /// <see cref="MainMenuModeController"/> calls it on Start so the menu is on screen even
    /// where nobody has clicked that command. Two builders would be two opinions about where
    /// PLAY sits, and the one that runs less often is the one that drifts (mistake 1).
    /// </para>
    ///
    /// <para>
    /// <b>It ADOPTS before it creates, every single time.</b> Every object is looked up by name
    /// before it is built, and an object that is already there keeps its RectTransform, its
    /// text, its font, its size and its colour - untouched. That is what makes the two callers
    /// safe together: once the tool has written the menu and the scene is saved, the runtime
    /// finds it and changes nothing. Before that, the runtime builds it so the screen is not
    /// simply the old one with no explanation (mistake 47).
    /// </para>
    ///
    /// <para>
    /// <b>No TMPro type is named here.</b> Text goes through <see cref="RuntimeUIFactory"/>,
    /// which is the one place this project decides TextMeshPro-or-legacy and guards it behind
    /// `TMP_PRESENT`. An unguarded `using TMPro;` does not break this screen - it fails
    /// Assembly-CSharp and with it every gameplay script in the project (mistake 48).
    /// </para>
    /// </summary>
    public static class MainMenuScreenBuilder
    {
        public const string CanvasName = "MainMenuBrandingCanvas";
        public const string RootName = "MainMenuRoot";
        public const string GrungeName = "MenuGrungeBackground";
        public const string NavName = "Navigation";
        public const string BrushChildName = "SelectionBrush";
        public const string TapLabelName = "TapToStartText";
        public const string SettingsPanelName = "MenuSettingsPanel";
        public const string CreditsPanelName = "MenuCreditsPanel";

        // Under Resources, so the SAME path works in the editor and in a build (mistake 3).
        public const string BrushResource = "UI/Menu/T_MenuBrushStroke";
        public const string GrungeResource = "UI/Menu/T_MenuGrungePanel";

        /// <summary>The three rows and the caption each STARTS with. The scene wins after that.</summary>
        public static readonly (string Id, string Caption)[] Rows =
        {
            ("Play", "PLAY"),
            ("Settings", "SETTINGS"),
            ("Credits", "CREDITS"),
        };

        // Starting points, all fractions of the canvas so they land the same on a phone in
        // landscape, on 16:9 and on an ultrawide. Every one of them is meant to be dragged.
        private const float NavLeft = 0.055f;
        private const float NavRight = 0.400f;
        // BELOW the logo, not across it. The logo is drawn with preserveAspect inside a rect
        // that runs from 16% to 88% of the screen and is scaled 1.23 in the scene, so its ink
        // fills most of the left column - the first placement put PLAY straight through the
        // word CAN. Where exactly its bottom edge lands depends on the aspect the CanvasScaler
        // resolves, which is why this is a starting point and not a measurement: the column is
        // an authored object now, so the last word on where it sits belongs to whoever drags it.
        private const float NavBottom = 0.050f;
        private const float NavTop = 0.280f;
        private const float RowHeightPx = 74f;
        private const float RowStepPx = 86f;
        private const int CaptionSize = 38;

        /// <summary>
        /// Builds or adopts the menu for this controller's scene. Returns false only when there
        /// is no canvas to build into.
        /// </summary>
        public static bool Build(MainMenuModeController controller, out string report)
        {
            if (controller == null)
            {
                report = "no MainMenuModeController - nothing owns the menu";
                return false;
            }

            Scene scene = controller.gameObject.scene;
            GameObject canvasGo = FindInScene(scene, CanvasName);

            if (canvasGo == null)
            {
                report = CanvasName + " is not in this scene. The menu goes inside the existing " +
                         "branding canvas; a second one is never built.";
                return false;
            }

            var notes = new List<string>();

            if (canvasGo.GetComponent<GraphicRaycaster>() == null)
            {
                canvasGo.AddComponent<GraphicRaycaster>();
                notes.Add("GraphicRaycaster ergaenzt");
            }

            notes.Add(EnsureEventSystem(scene));

            GameObject root = EnsureChild(canvasGo, RootName, out bool rootNew);
            if (rootNew)
                Stretch(root, Vector2.zero, Vector2.one);

            // ---- the dark torn backing -------------------------------------------------------
            GameObject grunge = EnsureChild(root, GrungeName, out bool grungeNew);
            var grungeImage = Ensure<Image>(grunge);
            if (grungeNew)
            {
                Stretch(grunge, new Vector2(-0.01f, 0.075f), new Vector2(0.50f, 0.545f));
                grungeImage.sprite = Resources.Load<Sprite>(GrungeResource);
                grungeImage.type = Image.Type.Simple;
                grungeImage.preserveAspect = false;
                grungeImage.color = new Color(1f, 1f, 1f, 0.85f);
                grungeImage.raycastTarget = false;
                grunge.transform.SetSiblingIndex(0);
            }

            // ---- the column ------------------------------------------------------------------
            GameObject nav = EnsureChild(root, NavName, out bool navNew);
            if (navNew)
                Stretch(nav, new Vector2(NavLeft, NavBottom), new Vector2(NavRight, NavTop));

            var brushSprite = Resources.Load<Sprite>(BrushResource);
            if (brushSprite == null)
                notes.Add("WARNUNG: Resources/" + BrushResource + " laedt nicht als Sprite");

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

                Ensure<MainMenuNavHover>(buttonGo);

                // ---- SelectionBrush, one per row, a child of the row ------------------------
                GameObject brushGo = EnsureChild(buttonGo, BrushChildName, out bool brushNew);
                var brushImage = Ensure<Image>(brushGo);
                if (brushNew)
                {
                    var br = Rect(brushGo);
                    br.anchorMin = Vector2.zero;
                    br.anchorMax = Vector2.one;
                    // Wider than the row on the left, so the stroke overhangs the caption the way
                    // a painted mark would rather than stopping square at the text.
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

                brushGo.SetActive(i == 0);

                // ---- Label_* -----------------------------------------------------------------
                Component label = EnsureLabel(buttonGo, "Label_" + Rows[i].Id, Rows[i].Caption,
                                              CaptionSize, TextAnchor.MiddleLeft,
                                              UITheme.FontRole.Button, out bool lblNew);
                if (label == null)
                {
                    notes.Add("WARNUNG: Beschriftung '" + Rows[i].Caption + "' nicht gebaut");
                    continue;
                }

                if (lblNew)
                {
                    // The ONLY place a caption's text is written. After this the scene is
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
            }

            GameObject settings = EnsurePanel(root, SettingsPanelName, "SETTINGS",
                "Die Einstellungen kommen hierher.");
            GameObject credits = EnsurePanel(root, CreditsPanelName, "CREDITS",
                "Catch If You Can");

            notes.Add(RetireTapToStart(scene));

            // Bound LAST, and Bind re-runs the wiring rather than only writing fields: the
            // component's Awake has already been and gone, inside the AddComponent above, with
            // no rows to wire (mistakes 25, 27).
            var navigation = Ensure<MainMenuNavigation>(nav);
            navigation.Bind(controller, built.ToArray(), settings, credits);

            notes.Add(built.Count + " Zeilen");
            report = string.Join(", ", notes);
            return true;
        }

        /// <summary>True when the scene already carries an authored menu with rows bound.</summary>
        public static bool AlreadyAuthored(MainMenuModeController controller)
        {
            if (controller == null)
                return false;

            Scene scene = controller.gameObject.scene;
            if (!scene.IsValid())
                return false;

            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                // Inactive included: a menu switched off in the Hierarchy is a different
                // problem from one that was never written, and must not read as the same.
                var found = roots[i].GetComponentsInChildren<MainMenuNavigation>(true);
                if (found.Length > 0 && found[0].RowCount > 0)
                    return true;
            }
            return false;
        }

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

                // The back button is NOT wired here. MainMenuNavigation wires every button
                // inside the panel when it is bound - one path, and one that certainly runs.
            }

            panel.SetActive(false);
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
        /// A caption, through the project's OWN text factory - the single place that decides
        /// TextMeshPro-or-legacy. An EXISTING label is returned untouched.
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
            return RuntimeUIFactory.CreateText(parent.transform, name, caption, size, align,
                                               true, role);
        }

        /// <summary>
        /// The text component on an object, whatever the factory built it as. By
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
        /// Takes TAP ANYWHERE TO START off the screen AND out of the input path. Hiding the
        /// label alone leaves the worse half behind: <see cref="MainMenuTapToStart"/> reads raw
        /// Input from anywhere, so one click on SETTINGS would open the panel AND hand the
        /// player to the lobby - one press through two paths (mistake 32).
        /// </summary>
        private static string RetireTapToStart(Scene scene)
        {
            var report = new List<string>();

            GameObject labelGo = FindInScene(scene, TapLabelName);
            if (labelGo != null && labelGo.activeSelf)
            {
                labelGo.SetActive(false);
                report.Add("TAP-Schild aus");
            }

            var tap = FindComponentInScene<MainMenuTapToStart>(scene);
            if (tap != null && tap.enabled)
            {
                tap.enabled = false;
                report.Add("TAP-Eingabe aus");
            }

            return report.Count > 0 ? string.Join(", ", report) : "TAP war schon weg";
        }

        private static string EnsureEventSystem(Scene scene)
        {
            var existing = Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
            if (existing != null && existing.gameObject.scene == scene)
                return "EventSystem war da";

            var es = EventSystemUtil.EnsureEventSystem();
            if (es == null)
                return "WARNUNG: kein EventSystem - Maus und Touch erreichen das Menue nicht";

            // new GameObject lands in the ACTIVE scene, not necessarily this one (mistake 17).
            if (es.gameObject.scene != scene && scene.IsValid())
                SceneManager.MoveGameObjectToScene(es.gameObject, scene);

            return "EventSystem gebaut";
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

        /// <summary>Looked up before it is created, so a second run adopts the first one's work.</summary>
        private static GameObject EnsureChild(GameObject parent, string name, out bool created)
        {
            GameObject go = FindUnder(parent.transform, name);
            created = go == null;

            if (created)
            {
                go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(parent.transform, false);
            }

            return go;
        }

        /// <summary>GetComponent before AddComponent, on every run (mistake 27).</summary>
        private static T Ensure<T>(GameObject go) where T : Component
        {
            T have = go.GetComponent<T>();
            return have != null ? have : go.AddComponent<T>();
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
            if (!scene.IsValid())
                return null;

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
            if (!scene.IsValid())
                return null;

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
