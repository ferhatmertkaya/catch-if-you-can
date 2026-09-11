using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CatchIfYouCan.UI
{
    /// <summary>
    /// Builds the cinematic main menu - the four rows, the brush stroke, the footer and the two
    /// panel shells - onto the branding canvas.
    ///
    /// <para>
    /// <b>Why this is runtime code and not an editor baker.</b> It WAS a baker, and that is
    /// exactly how it failed. The rows only existed once somebody opened Unity and clicked a
    /// menu item; the scene file on disk still carried nothing but the logo and TAP ANYWHERE TO
    /// START. So the code shipped, the scene did not change, and the screen was identical to
    /// the one before the work - which is indistinguishable from the menu having been deleted,
    /// and was read as exactly that. Most work on this project happens where Unity cannot run,
    /// so a screen that only a click can create is a screen that does not exist.
    /// </para>
    ///
    /// <para>
    /// <b>It is IDEMPOTENT, and that is load-bearing rather than tidy.</b> The editor tool still
    /// calls this same method, so a scene may already carry a baked menu when the runtime builds
    /// one. Every object is looked up before it is created and every component is asked for
    /// before it is added, so the second run adopts what the first one left instead of standing a
    /// duplicate beside it - CLAUDE.md mistakes 27, 30 and 46, which is the same lesson three
    /// times: an <c>Awake</c> or a builder that assumes it has never run is wrong for anything
    /// that is ever built twice.
    /// </para>
    ///
    /// <para>
    /// <b>There is ONE of it.</b> The editor tool does not carry its own copy of these rectangles
    /// - it calls here. Two builders would be two opinions about where PLAY sits, and the one
    /// that runs less often is the one that drifts (mistake 1).
    /// </para>
    ///
    /// <para>
    /// The geometry is MEASURED off the design reference in fractions of its width and height,
    /// not eyeballed. The caption inset is computed from the two measured edges rather than
    /// written down as a round number: the first attempt guessed 34 px where the reference says
    /// 92, and a wrong inset does not read as a wrong constant - it reads as a brush stroke that
    /// has slipped off its row.
    /// </para>
    /// </summary>
    public static class MainMenuScreenBuilder
    {
        public const string CanvasName = "MainMenuBrandingCanvas";
        public const string MenuRootName = "MainMenuNav";
        public const string FooterName = "MainMenuFooter";
        public const string BrushName = "SelectionBrush";
        public const string SettingsPanelName = "MenuSettingsPanel";
        public const string CreditsPanelName = "MenuCreditsPanel";
        public const string TapLabelName = "TapToStartText";
        public const string StaleBackdropName = "MenuGrungeBackdrop";

        /// <summary>
        /// The brush sprite, under Resources so it SHIPS. The editor path used to reach it by
        /// asset path, which resolves in the editor and nowhere else (CLAUDE.md mistake 3).
        /// </summary>
        public const string BrushResource = "UI/Menu/T_MenuBrushStroke";

        /// <summary>PLAY, SETTINGS, CREDITS, QUIT. The order is not cosmetic: the navigator
        /// switches by INDEX, so a swapped list is a PLAY that opens the credits.</summary>
        public static readonly string[] RowLabels = { "PLAY", "SETTINGS", "CREDITS", "QUIT" };

        // --- measured off the reference (1672 x 941, 16:9) -------------------------------------
        public const float TextLeft = 0.100f;    // where every caption starts
        public const float BrushLeft = 0.052f;   // the stroke overhangs the text to the left
        public const float MenuRight = 0.345f;
        public const float MenuTop = 0.520f;     // top of the PLAY row, from the BOTTOM
        public const float RowStepPx = 70f;      // 0.065 of height at the 1080 reference
        public const float RowHeightPx = 74f;    // 0.069 of height - the stroke's own height
        public const int CaptionSize = 38;

        /// <summary>
        /// Builds (or adopts) the whole menu for this controller's scene. Returns a report.
        /// </summary>
        public static string Build(MainMenuModeController controller)
        {
            if (controller == null)
                return "WARNUNG: kein MainMenuModeController - das Menue hat keine Szene.";

            Scene scene = controller.gameObject.scene;
            GameObject canvasGo = EnsureCanvas(scene);

            if (canvasGo == null)
                return "WARNUNG: keine Branding-Canvas - das Menue kann nirgends gezeichnet werden.";

            return Build(canvasGo, controller, scene);
        }

        /// <summary>
        /// The one implementation. The editor tool calls this too, with the canvas it just made.
        /// </summary>
        public static string Build(GameObject canvasGo, MainMenuModeController controller,
                                   Scene scene)
        {
            var notes = new List<string>();

            var brushSprite = Resources.Load<Sprite>(BrushResource);
            if (brushSprite == null)
            {
                // Two very different causes, and the message has to separate them: the file is
                // missing, or it imported as a plain Texture rather than as a Sprite. Either way
                // the rows are still built - a menu with no highlight is usable, a missing menu
                // is not - so this is a note rather than a return.
                notes.Add("WARNUNG: Resources/" + BrushResource + " laedt nicht als Sprite " +
                          "(Texture Type muss Sprite (2D and UI) sein). Menue ohne Pinselstrich.");
            }

            // A leftover grunge backdrop from an older pass is REMOVED rather than left switched
            // off: a disabled object in the hierarchy reads to the next person as something
            // somebody meant to keep (mistake 14).
            GameObject stale = FindInScene(scene, StaleBackdropName);
            if (stale != null)
            {
                // Destroy is DEFERRED to the end of the frame and does nothing at all in edit
                // mode, where it warns instead. The editor tool calls this same method, so the
                // one case that has to work is the one Destroy cannot do.
                if (Application.isPlaying)
                    Object.Destroy(stale);
                else
                    Object.DestroyImmediate(stale);

                notes.Add("alter Grunge-Hintergrund entfernt (die Referenz hat keinen).");
            }

            // --- the canvas must be able to receive a click at all ---------------------------
            if (canvasGo.GetComponent<GraphicRaycaster>() == null)
            {
                canvasGo.AddComponent<GraphicRaycaster>();
                notes.Add("GraphicRaycaster ergaenzt (ohne ihn ist jeder Button sichtbar und tot).");
            }

            notes.Add(EnsureEventSystem(scene));

            // --- the menu column --------------------------------------------------------------
            GameObject menuGo = EnsureChild(canvasGo, MenuRootName, out _);
            float menuHeight = (RowLabels.Length - 1) * RowStepPx + RowHeightPx;
            SetRect(menuGo,
                new Vector2(BrushLeft, MenuTop - menuHeight / 1080f),
                new Vector2(MenuRight, MenuTop));

            var nav = Ensure<MainMenuNavigation>(menuGo);

            // --- the white stroke, ONE of it ---------------------------------------------------
            GameObject brushGo = EnsureChild(menuGo, BrushName, out _);
            var brushImage = Ensure<Image>(brushGo);
            brushImage.sprite = brushSprite;
            brushImage.type = Image.Type.Simple;
            brushImage.preserveAspect = false;
            brushImage.color = Color.white;
            brushImage.raycastTarget = false;
            brushImage.enabled = brushSprite != null;
            RowRect(brushGo, 0);
            brushGo.transform.SetSiblingIndex(0);

            // --- the four rows ------------------------------------------------------------------
            var built = new List<MainMenuNavigation.Row>();
            for (int i = 0; i < RowLabels.Length; i++)
            {
                GameObject rowGo = EnsureChild(menuGo, "MenuRow_" + RowLabels[i], out _);
                RectTransform rowRect = RowRect(rowGo, i);

                // A transparent Image purely so the row is a raycast target. uGUI ignores alpha
                // when hit-testing unless a threshold is set, so this is clickable at alpha 0 -
                // and an invisible target the size of the row is a comfortable touch box.
                var hit = Ensure<Image>(rowGo);
                hit.color = new Color(0f, 0f, 0f, 0f);
                hit.raycastTarget = true;

                var button = Ensure<Button>(rowGo);
                // The transition is left at its default rather than set to None. uGUI's colour
                // tint MULTIPLIES the target graphic's colour, and this target is fully
                // transparent - zero alpha times any tint is still zero. So Unity's own highlight
                // cannot draw anything, the brush and the caption colour stay the only opinion
                // about which row is selected, and no API this project cannot verify offline is
                // touched to achieve it.
                button.targetGraphic = hit;

                Component label = FindLabelComponent(rowGo);
                if (label == null)
                {
                    label = RuntimeUIFactory.CreateText(
                        rowGo.transform, "Caption", RowLabels[i], CaptionSize,
                        TextAnchor.MiddleLeft, true, UITheme.FontRole.Button);

                    if (label == null)
                    {
                        notes.Add("WARNUNG: Beschriftung '" + RowLabels[i] + "' nicht gebaut.");
                        continue;
                    }
                }

                UITheme.SetText(label, RowLabels[i]);
                UITheme.SetTextColor(label, i == 0 ? UITheme.BackgroundDark : UITheme.TextMuted);

                var labelRect = label.transform as RectTransform;
                if (labelRect != null)
                {
                    labelRect.anchorMin = new Vector2(0f, 0f);
                    labelRect.anchorMax = new Vector2(1f, 1f);
                    // The captions line up at TextLeft while the stroke starts further out, so
                    // the inset is the measured gap between the two rather than a round number.
                    labelRect.offsetMin = new Vector2((TextLeft - BrushLeft) * 1920f, 0f);
                    labelRect.offsetMax = new Vector2(-12f, 0f);
                    labelRect.localScale = Vector3.one;
                }

                Ensure<MainMenuNavHover>(rowGo).Bind(nav, i);

                built.Add(new MainMenuNavigation.Row
                {
                    button = button,
                    label = label,
                    rect = rowRect
                });
            }

            notes.Add(BuildFooter(canvasGo, scene));

            // --- the two panel shells -----------------------------------------------------------
            GameObject settings = BuildPanelShell(canvasGo, SettingsPanelName, "SETTINGS",
                "Die Einstellungen kommen hierher. Diese Huelle ist absichtlich leer: der Knopf " +
                "ist verdrahtet, das System dahinter ist eine eigene Aufgabe.");
            GameObject credits = BuildPanelShell(canvasGo, CreditsPanelName, "CREDITS",
                "Catch If You Can");

            // --- TAP ANYWHERE TO START goes away, and so does the input path behind it ----------
            notes.Add(RetireTapToStart(scene));

            // Bind LAST, and note that Bind re-runs the wiring rather than only writing fields.
            // MainMenuNavigation's Awake ran INSIDE the AddComponent above, one call before this
            // line, with rows empty - CLAUDE.md mistakes 25 and 27. Binding without re-wiring
            // would leave a menu that draws perfectly and answers nothing.
            nav.Bind(controller, built.ToArray(),
                     brushGo.GetComponent<RectTransform>(), settings, credits);

            notes.Add("Menue: " + built.Count + " von " + RowLabels.Length + " Zeilen, " +
                      "Pinselstrich " + (brushSprite != null ? "gesetzt" : "FEHLT") + ".");

            return string.Join("\n", notes);
        }

        /// <summary>
        /// The branding canvas. Found by name, and only built if the scene genuinely has none.
        /// </summary>
        private static GameObject EnsureCanvas(Scene scene)
        {
            GameObject found = FindInScene(scene, CanvasName);
            if (found != null)
                return found;

            var made = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas),
                                      typeof(CanvasScaler));

            // new GameObject lands in the ACTIVE scene, which is not necessarily this one
            // (CLAUDE.md mistake 17). Moved rather than assumed.
            if (made.scene != scene && scene.IsValid())
                SceneManager.MoveGameObjectToScene(made, scene);

            var canvas = made.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 101;

            var scaler = made.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            return made;
        }

        /// <summary>
        /// The footer: version, rights, engine credit and the tagline.
        ///
        /// <para>
        /// Anchored to the BOTTOM-LEFT corner rather than laid out proportionally. On an
        /// ultrawide a proportional footer drifts towards the middle and ends up under the ghost;
        /// pinned to the corner it stays a footer at every aspect, which is the only thing a
        /// footer has to do.
        /// </para>
        /// <para>
        /// The engine credit is TEXT. The reference shows Unity's cube mark and this project has
        /// no such file - drawing a company's trademark from memory into a generated PNG is not a
        /// thing to do quietly in a builder. "Unity | URP" says the same and is honest about
        /// where it came from; drop the real mark in later and point an Image at it.
        /// </para>
        /// </summary>
        private static string BuildFooter(GameObject canvasGo, Scene scene)
        {
            GameObject footer = EnsureChild(canvasGo, FooterName, out _);
            SetRect(footer, Vector2.zero, Vector2.one);

            var rect = footer.GetComponent<RectTransform>();
            if (rect != null)
                rect.SetSiblingIndex(0);

            int built = 0;

            // Measured off the reference: the version sits at 0.833 from the top, the rights
            // block at 0.896..0.938, both starting at x 0.026.
            built += Line(footer, scene, "Version", "v0.1.0", 20, UITheme.TextDisabled,
                          new Vector2(0.026f, 0.147f), new Vector2(0.20f, 0.173f),
                          TextAnchor.LowerLeft) ? 1 : 0;

            built += Line(footer, scene, "Rights", "A CATCH IF YOU CAN GAME\nALL RIGHTS RESERVED.",
                          17, UITheme.TextMuted,
                          new Vector2(0.026f, 0.055f), new Vector2(0.175f, 0.105f),
                          TextAnchor.LowerLeft) ? 1 : 0;

            // The rule between the rights block and the engine credit, as in the reference.
            GameObject divider = EnsureChild(footer, "FooterDivider", out _);
            SetRect(divider, new Vector2(0.188f, 0.058f), new Vector2(0.1895f, 0.102f));
            var rule = Ensure<Image>(divider);
            rule.sprite = null;
            rule.color = UITheme.Border;
            rule.raycastTarget = false;

            built += Line(footer, scene, "Engine", "Unity  |  URP", 19, UITheme.TextMuted,
                          new Vector2(0.203f, 0.055f), new Vector2(0.34f, 0.105f),
                          TextAnchor.LowerLeft) ? 1 : 0;

            built += Line(footer, scene, "Tagline", "Some places never let go.", 22,
                          UITheme.TextMuted,
                          new Vector2(0.70f, 0.048f), new Vector2(0.972f, 0.098f),
                          TextAnchor.LowerRight) ? 1 : 0;

            return "Fusszeile: " + built + " von 4 Zeilen plus Trennstrich.";
        }

        /// <summary>One footer line, through the project's own text builder.</summary>
        private static bool Line(GameObject parent, Scene scene, string name, string value,
                                 int size, Color colour, Vector2 min, Vector2 max,
                                 TextAnchor align)
        {
            GameObject go = FindUnder(parent.transform, "Footer_" + name);
            Component text;

            if (go == null)
            {
                text = RuntimeUIFactory.CreateText(parent.transform, "Footer_" + name, value, size,
                                                   align, false, UITheme.FontRole.Body);
                if (text == null)
                    return false;

                go = text.gameObject;
            }
            else
            {
                text = FindTextComponent(go);
                if (text == null)
                    return false;
            }

            go.transform.SetParent(parent.transform, false);
            UITheme.SetText(text, value);
            UITheme.SetTextColor(text, colour);
            SetRect(go, min, max);
            return true;
        }

        /// <summary>
        /// The text component on an object, whatever type the factory built it as.
        ///
        /// <para>
        /// By Graphic-minus-Image rather than by type: TextMeshPro is optional in this project
        /// and guarded behind TMP_PRESENT, so there is not always a type here to ask for.
        /// </para>
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
        /// Hiding the label alone would leave the worse half behind.
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
                labelGo.SetActive(false);
                report.Add("'TAP ANYWHERE TO START' ausgeblendet");
            }
            else
            {
                report.Add("'TAP ANYWHERE TO START' war schon weg");
            }

            var tap = FindComponentInScene<MainMenuTapToStart>(scene);
            if (tap != null && tap.enabled)
            {
                tap.enabled = false;
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
        private static GameObject BuildPanelShell(GameObject canvasGo, string name, string title,
                                                  string body)
        {
            GameObject panel = EnsureChild(canvasGo, name, out bool created);

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
                    UITheme.SetTextColor(head, UITheme.TextPrimary);
                    SetRect(head.gameObject, new Vector2(0.2f, 0.60f), new Vector2(0.8f, 0.74f));
                }

                Component line = RuntimeUIFactory.CreateText(panel.transform, "Body", body, 28,
                    TextAnchor.MiddleCenter, false, UITheme.FontRole.Body);
                if (line != null)
                {
                    UITheme.SetTextColor(line, UITheme.TextMuted);
                    SetRect(line.gameObject, new Vector2(0.2f, 0.44f), new Vector2(0.8f, 0.58f));
                }

                Button back = RuntimeUIFactory.CreateButton(panel.transform, "ZURUECK", null);
                if (back != null)
                    SetRect(back.gameObject, new Vector2(0.40f, 0.26f), new Vector2(0.60f, 0.36f));

                // The back button is NOT wired here. MainMenuNavigation wires every button inside
                // the panel when it is bound - one path, and one that certainly runs.
            }

            panel.SetActive(false);
            return panel;
        }

        /// <summary>
        /// An EventSystem in THIS scene.
        ///
        /// <para>
        /// Through <see cref="EventSystemUtil"/>, which already decides between the old and the
        /// new input backend - writing that branch here would be a second answer to a question
        /// the project has answered once. What it cannot decide is which SCENE the object lands
        /// in: <c>new GameObject</c> goes to the ACTIVE scene, which is mistake 17. So the result
        /// is checked and moved if it went elsewhere.
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

            if (es.gameObject.scene != scene && scene.IsValid())
            {
                SceneManager.MoveGameObjectToScene(es.gameObject, scene);
                return "EventSystem gebaut und in diese Szene verschoben.";
            }

            return "EventSystem gebaut.";
        }

        /// <summary>One row's rectangle: full width of the column, stacked from the top.</summary>
        private static RectTransform RowRect(GameObject go, int index)
        {
            var rect = go.GetComponent<RectTransform>();
            if (rect == null)
                rect = go.AddComponent<RectTransform>();

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(0f, 0f);
            rect.offsetMax = new Vector2(0f, 0f);
            rect.sizeDelta = new Vector2(0f, RowHeightPx);
            rect.anchoredPosition = new Vector2(0f, -index * RowStepPx);
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
            else
            {
                go.transform.SetParent(parent.transform, false);
            }

            go.SetActive(true);
            return go;
        }

        /// <summary>GetComponent before AddComponent, on every run (CLAUDE.md mistake 27).</summary>
        private static T Ensure<T>(GameObject go) where T : Component
        {
            T have = go.GetComponent<T>();
            return have != null ? have : go.AddComponent<T>();
        }

        /// <summary>
        /// The caption a row already carries, whatever type the text factory built it as.
        /// Found by NAME on a child rather than by component type, because TextMeshPro is
        /// optional here and there is not always a type to ask for.
        /// </summary>
        private static Component FindLabelComponent(GameObject rowGo)
        {
            Transform child = rowGo.transform.Find("Caption");
            if (child == null)
                return null;

            return FindTextComponent(child.gameObject);
        }

        /// <summary>
        /// A descendant by name, INCLUDING inactive ones - the panels and the retired tap label
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
