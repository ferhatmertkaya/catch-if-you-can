using UnityEngine;
using UnityEngine.UI;

#if TMP_PRESENT || UNITY_TEXTMESHPRO
using TMPro;
#endif

namespace CatchIfYouCan.UI
{
    public static class UITheme
    {
        public static readonly Color Primary = Hex("#57FF68");
        public static readonly Color Secondary = Hex("#19D77B");
        public static readonly Color BackgroundDark = Hex("#07100C");
        public static readonly Color BackgroundPanel = Hex("#101513");
        public static readonly Color Warning = Hex("#FF5252");
        public static readonly Color TextPrimary = Hex("#E8FFF0");
        public static readonly Color TextMuted = Hex("#8FAF98");
        public static readonly Color Border = Hex("#19D77B");
        public static readonly Color Overlay = new Color(0.03f, 0.06f, 0.05f, 0.88f);

        public const float PanelBorderWidth = 2f;
        public const float CornerRadiusFeel = 12f;
        public const float ButtonHeight = 56f;
        public const float LargeButtonHeight = 72f;

        public static Color Hex(string hex)
        {
            if (ColorUtility.TryParseHtmlString(hex, out Color c))
                return c;
            return Color.white;
        }

        public static void ApplyPanel(Image image, bool bordered = true, float alpha = 0.92f)
        {
            if (image == null) return;
            var c = BackgroundPanel;
            c.a = alpha;
            image.color = c;
            if (bordered)
                ApplyBorder(image.gameObject);
        }

        public static void ApplyBorder(GameObject target, float width = PanelBorderWidth)
        {
            if (target == null) return;
            var outline = target.GetComponent<Outline>();
            if (outline == null)
                outline = target.AddComponent<Outline>();
            outline.effectColor = Border;
            outline.effectDistance = new Vector2(width, -width);
            outline.useGraphicAlpha = true;
        }

        public static void ApplyPrimaryAccent(Image image)
        {
            if (image != null)
                image.color = Primary;
        }

        public static void ApplyButtonColors(Selectable selectable, bool primary = false)
        {
            if (selectable == null) return;
            var colors = selectable.colors;
            colors.normalColor = primary ? Secondary : BackgroundPanel;
            colors.highlightedColor = Primary;
            colors.pressedColor = Hex("#0FA85C");
            colors.selectedColor = Secondary;
            colors.disabledColor = TextMuted;
            selectable.colors = colors;
        }

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

        public static void StyleTitle(Component text)
        {
            SetTextColor(text, Primary);
        }

        public static void StyleBody(Component text)
        {
            SetTextColor(text, TextPrimary);
        }

        public static void StyleMuted(Component text)
        {
            SetTextColor(text, TextMuted);
        }
    }
}
