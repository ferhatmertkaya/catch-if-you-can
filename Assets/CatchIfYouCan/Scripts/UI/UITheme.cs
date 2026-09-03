using UnityEngine;
using UnityEngine.UI;

#if TMP_PRESENT || UNITY_TEXTMESHPRO
using TMPro;
#endif

namespace CatchIfYouCan.UI
{
    /// <summary>
    /// The one place the interface's colours, weights and fonts are decided.
    ///
    /// <para>
    /// <b>The palette is black, white and grey. The green is an accent and nothing else.</b>
    /// It used to be the other way round: <see cref="Border"/> was the brand green and
    /// <see cref="ApplyPanel"/> put a border on every panel it made, so every surface in the
    /// game was outlined in it; primary buttons were filled with it; and every hover repainted
    /// a whole button bright green. Three separate decisions, all in this file, which together
    /// made the menus read as a developer dashboard rather than as a horror game. Green now
    /// appears on a pressed control, a selected row and a small indicator - and nowhere that
    /// covers more than a hairline or a few square centimetres.
    /// </para>
    ///
    /// <para>
    /// <b><see cref="ApplyButtonColors"/> writes <c>fadeDuration</c> deliberately.</b> Unity's
    /// default <see cref="ColorBlock"/> fades tint changes over 0.1 s, and this method used to
    /// assign every other field and leave that one at its default - so every button in the game
    /// took a tenth of a second to acknowledge a press. That is not a slow script or a slow
    /// event system; it is this one unset field, and it is why the menus felt unresponsive. The
    /// press now lands on the same frame as the touch.
    /// </para>
    /// </summary>
    public static class UITheme
    {
        // ---- surfaces: black, and shades of black -----------------------------------------

        /// <summary>Fullscreen menu ground. Near-black, not dark green.</summary>
        public static readonly Color BackgroundDark = Hex("#000000");

        /// <summary>A panel or a control at rest. Neutral, so nothing is tinted by default.</summary>
        public static readonly Color BackgroundPanel = Hex("#0B0B0B");

        /// <summary>A control raised off the ground - the resting fill of a primary button.</summary>
        public static readonly Color Surface = Hex("#141414");

        /// <summary>Pointer over, or keyboard focus. A step lighter, still neutral.</summary>
        public static readonly Color SurfaceHover = Hex("#1E1E1E");

        /// <summary>
        /// Held down. Barely green - enough to read as the brand, far from a green fill. The
        /// visible part of a press is the accent bar and the scale, not this.
        /// </summary>
        public static readonly Color SurfacePressed = Hex("#16211A");

        /// <summary>Behind a fullscreen menu. Neutral black, so the menu is not a green wash.</summary>
        public static readonly Color Overlay = new Color(0f, 0f, 0f, 0.94f);

        // ---- text --------------------------------------------------------------------------

        public static readonly Color TextPrimary = Hex("#FFFFFF");
        public static readonly Color TextMuted = Hex("#9A9A9A");
        public static readonly Color TextDisabled = Hex("#565656");

        // ---- lines -------------------------------------------------------------------------

        /// <summary>The default hairline. Neutral grey: a border is structure, not decoration.</summary>
        public static readonly Color Border = Hex("#2B2B2B");

        /// <summary>A border that needs to be seen - the focused row, a framed preview.</summary>
        public static readonly Color BorderStrong = Hex("#3F3F3F");

        // ---- the accent, used sparingly ------------------------------------------------------

        /// <summary>The brand green. A selected state, a small indicator, a thin rule.</summary>
        public static readonly Color Primary = Hex("#57FF68");

        /// <summary>The brand green, subdued. Held controls and accent bars.</summary>
        public static readonly Color Secondary = Hex("#19D77B");

        /// <summary>A green quiet enough to outline a selected row without filling it.</summary>
        public static readonly Color AccentBorder = Hex("#2E7A4B");

        public static readonly Color Warning = Hex("#FF5252");

        // ---- metrics -------------------------------------------------------------------------

        /// <summary>A hairline. Two pixels read as a frame around everything; one reads as an edge.</summary>
        public const float PanelBorderWidth = 1f;

        public const float CornerRadiusFeel = 12f;
        public const float ButtonHeight = 56f;
        public const float LargeButtonHeight = 72f;

        /// <summary>The width of the accent bar that marks a pressed or selected control.</summary>
        public const float AccentBarWidth = 4f;

        public static Color Hex(string hex)
        {
            if (ColorUtility.TryParseHtmlString(hex, out Color c))
                return c;
            return Color.white;
        }

        // ---- typography ----------------------------------------------------------------------
        //
        // Three real faces, all of which are already in this repository. Nothing is downloaded
        // and no font name is invented: Anton and Oswald Bold ship with the TextMesh Pro
        // examples and are copied into Resources so a build can reach them, and the body face is
        // Unity's own built-in sans, which is already what every label used before this.

        /// <summary>Resources path of the display face. Anton: condensed, heavy, all-caps.</summary>
        public const string TitleFontPath = "UI/Fonts/Anton";

        /// <summary>Resources path of the header and button face. Oswald Bold: condensed, legible.</summary>
        public const string HeaderFontPath = "UI/Fonts/Oswald-Bold";

        /// <summary>Which of the three faces a label is written in.</summary>
        public enum FontRole
        {
            /// <summary>Game and screen titles. Anton.</summary>
            Title,

            /// <summary>Section headers and row labels. Oswald Bold.</summary>
            Header,

            /// <summary>Button captions. Oswald Bold - the same face, sized for a control.</summary>
            Button,

            /// <summary>Running text, briefings, status lines. The built-in sans.</summary>
            Body
        }

        private static Font _titleFont;
        private static Font _headerFont;
        private static Font _bodyFont;
        private static bool _fontsResolved;
        private static bool _missingReported;

        /// <summary>The display face, or the body face if it could not be loaded.</summary>
        public static Font TitleFont
        {
            get { ResolveFonts(); return _titleFont != null ? _titleFont : _bodyFont; }
        }

        /// <summary>The header and button face, or the body face if it could not be loaded.</summary>
        public static Font HeaderFont
        {
            get { ResolveFonts(); return _headerFont != null ? _headerFont : _bodyFont; }
        }

        /// <summary>The running-text face. Unity's built-in sans; always present.</summary>
        public static Font BodyFont
        {
            get { ResolveFonts(); return _bodyFont; }
        }

        /// <summary>
        /// Loads the three faces once.
        ///
        /// <para>
        /// A missing face is <b>said out loud</b>, once, naming the path it looked in. A font
        /// that silently falls back is how a project ends up shipping in Arial while everyone
        /// believes it is branded - the same failure mode as a <c>Resources.Load</c> path that
        /// has never existed, which this project has already shipped once.
        /// </para>
        /// </summary>
        private static void ResolveFonts()
        {
            if (_fontsResolved)
                return;
            _fontsResolved = true;

            _bodyFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_bodyFont == null)
                _bodyFont = Font.CreateDynamicFontFromOSFont("Arial", 16);

            _titleFont = Resources.Load<Font>(TitleFontPath);
            _headerFont = Resources.Load<Font>(HeaderFontPath);

            if (_missingReported)
                return;

            if (_titleFont == null || _headerFont == null)
            {
                _missingReported = true;
                Core.CIYCLog.Error(
                    "[CIYC][UI] Branded font missing. Looked for Resources/" + TitleFontPath +
                    " (" + (_titleFont != null ? "found" : "MISSING") + ") and Resources/" +
                    HeaderFontPath + " (" + (_headerFont != null ? "found" : "MISSING") +
                    "). The interface falls back to the built-in sans, which is readable but " +
                    "unbranded. The .ttf files belong under " +
                    "Assets/CatchIfYouCan/Resources/UI/Fonts.");
            }
        }

        /// <summary>Writes the face for a role onto a legacy or TextMeshPro label.</summary>
        public static void ApplyFont(Component text, FontRole role)
        {
            if (text == null)
                return;

            Font font;
            switch (role)
            {
                case FontRole.Title: font = TitleFont; break;
                case FontRole.Header:
                case FontRole.Button: font = HeaderFont; break;
                default: font = BodyFont; break;
            }

#if TMP_PRESENT || UNITY_TEXTMESHPRO
            if (text is TextMeshProUGUI tmp)
            {
                TMP_FontAsset asset = TmpAssetFor(role);
                if (asset != null)
                    tmp.font = asset;
                return;
            }
#endif
            if (text is Text legacy && font != null)
                legacy.font = font;
        }

#if TMP_PRESENT || UNITY_TEXTMESHPRO
        private static TMP_FontAsset _tmpTitle;
        private static TMP_FontAsset _tmpHeader;
        private static bool _tmpResolved;

        /// <summary>
        /// The signed-distance-field equivalents of the same two faces, which TextMesh Pro ships
        /// under its own Resources folder. Same typefaces, so the design does not change with
        /// the define.
        /// </summary>
        private static TMP_FontAsset TmpAssetFor(FontRole role)
        {
            if (!_tmpResolved)
            {
                _tmpResolved = true;
                _tmpTitle = Resources.Load<TMP_FontAsset>("Fonts & Materials/Anton SDF");
                _tmpHeader = Resources.Load<TMP_FontAsset>("Fonts & Materials/Oswald Bold SDF");
            }

            switch (role)
            {
                case FontRole.Title: return _tmpTitle;
                case FontRole.Header:
                case FontRole.Button: return _tmpHeader;
                default: return null;
            }
        }
#endif

        /// <summary>A fresh process re-reads the fonts; a domain reload does not keep them.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            _fontsResolved = false;
            _missingReported = false;
            _titleFont = null;
            _headerFont = null;
            _bodyFont = null;
#if TMP_PRESENT || UNITY_TEXTMESHPRO
            _tmpResolved = false;
            _tmpTitle = null;
            _tmpHeader = null;
#endif
        }

        // ---- surfaces --------------------------------------------------------------------------

        /// <summary>
        /// Paints a panel. <b>Unbordered by default.</b> Every panel carrying a border is what
        /// turned the menus into a grid of boxes; a panel that needs an edge asks for one.
        /// </summary>
        public static void ApplyPanel(Image image, bool bordered = false, float alpha = 0.94f)
        {
            if (image == null) return;
            var c = BackgroundPanel;
            c.a = alpha;
            image.color = c;
            if (bordered)
                ApplyBorder(image.gameObject);
        }

        /// <summary>A neutral hairline. The accent version is <see cref="ApplyAccentBorder"/>.</summary>
        public static void ApplyBorder(GameObject target, float width = PanelBorderWidth)
        {
            ApplyBorder(target, Border, width);
        }

        /// <summary>A green hairline: the selected row, and nothing that is not selected.</summary>
        public static void ApplyAccentBorder(GameObject target, float width = PanelBorderWidth)
        {
            ApplyBorder(target, AccentBorder, width);
        }

        public static void ApplyBorder(GameObject target, Color color, float width)
        {
            if (target == null) return;
            var outline = target.GetComponent<Outline>();
            if (outline == null)
                outline = target.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(width, -width);
            outline.useGraphicAlpha = true;
        }

        public static void ApplyPrimaryAccent(Image image)
        {
            if (image != null)
                image.color = Primary;
        }

        /// <summary>
        /// The four states of a control, and the fade between them.
        ///
        /// <para>
        /// All four are neutral or near-neutral. <c>fadeDuration</c> is zero on purpose: see the
        /// note on this class. Keep it zero. A button that acknowledges a touch on the next
        /// frame feels broken on a phone, and 0.1 s is six frames at 60 Hz.
        /// </para>
        /// </summary>
        public static void ApplyButtonColors(Selectable selectable, bool primary = false)
        {
            if (selectable == null) return;
            var colors = selectable.colors;
            colors.normalColor = primary ? Surface : BackgroundPanel;
            colors.highlightedColor = SurfaceHover;
            colors.pressedColor = SurfacePressed;
            // Equal to the resting colour on purpose. A button that stays lit after it was
            // clicked reads as the current selection, which is what made the board's hierarchy
            // unreadable - three buttons, and the last one touched looked chosen.
            colors.selectedColor = primary ? Surface : BackgroundPanel;
            colors.disabledColor = BackgroundDark;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0f;
            selectable.colors = colors;
        }

        // ---- labels --------------------------------------------------------------------------

        public static void SetTextColor(Component text, Color color)
        {
            if (text == null) return;
#if TMP_PRESENT || UNITY_TEXTMESHPRO
            if (text is TextMeshProUGUI tmp)
            {
                tmp.color = color;
                return;
            }
#endif
            if (text is Text legacy)
                legacy.color = color;
        }

        public static void SetText(Component text, string value)
        {
            if (text == null) return;
#if TMP_PRESENT || UNITY_TEXTMESHPRO
            if (text is TextMeshProUGUI tmp)
            {
                tmp.text = value;
                return;
            }
#endif
            if (text is Text legacy)
                legacy.text = value;
        }

        /// <summary>A screen or game title: the display face, white.</summary>
        public static void StyleTitle(Component text)
        {
            ApplyFont(text, FontRole.Title);
            SetTextColor(text, TextPrimary);
        }

        /// <summary>A section header or a row label: the condensed face, white.</summary>
        public static void StyleHeader(Component text)
        {
            ApplyFont(text, FontRole.Header);
            SetTextColor(text, TextPrimary);
        }

        /// <summary>Running text: the readable face, white.</summary>
        public static void StyleBody(Component text)
        {
            ApplyFont(text, FontRole.Body);
            SetTextColor(text, TextPrimary);
        }

        /// <summary>Secondary information: the readable face, light grey.</summary>
        public static void StyleMuted(Component text)
        {
            ApplyFont(text, FontRole.Body);
            SetTextColor(text, TextMuted);
        }
    }
}
