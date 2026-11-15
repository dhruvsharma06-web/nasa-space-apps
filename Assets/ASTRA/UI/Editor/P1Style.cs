using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Astra.UI.Editor
{
    /// <summary>
    /// The ASTRA cockpit look, shared by every P1 generator: Barlow (text) and IBM Plex Mono (labels, numbers),
    /// dark glass with hairline edges, small radii, and the accent used sparingly. Fonts are OFL (see UI/Fonts).
    /// </summary>
    public static class P1Style
    {
        public enum Role { Body, Medium, Title, Mono }

        private const string FontDir = "Assets/ASTRA/UI/Fonts/";
        private static readonly Dictionary<Role, string> FontFiles = new Dictionary<Role, string>
        {
            { Role.Body, "Barlow-Regular.ttf" },
            { Role.Medium, "Barlow-Medium.ttf" },
            { Role.Title, "Barlow-SemiBold.ttf" },
            { Role.Mono, "IBMPlexMono-Medium.ttf" }
        };

        // Palette (white text on dark glass; secondary text stays ≥ 0.7 brighter than the glass for the phone).
        public static readonly Color Ink = new Color(0.94f, 0.96f, 0.99f);
        public static readonly Color InkSecondary = new Color(0.76f, 0.82f, 0.9f);
        public static readonly Color InkMuted = new Color(0.56f, 0.62f, 0.7f);
        public static readonly Color Glass = new Color(0.035f, 0.05f, 0.07f, 0.86f);
        public static readonly Color GlassRaised = new Color(0.07f, 0.09f, 0.125f, 0.94f);
        public static readonly Color Hairline = new Color(1f, 1f, 1f, 0.12f);
        public static readonly Color AccentLine = new Color(Theme.Accent.r, Theme.Accent.g, Theme.Accent.b, 0.55f);
        public static readonly Color ButtonFill = new Color(0.1f, 0.13f, 0.18f, 0.92f);

        public static Font Font(Role role)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(FontDir + FontFiles[role]);
            return font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        /// <summary>Applies a typographic role: weight comes from the font file, never synthetic bold.</summary>
        public static Text Type(Text text, Role role, Color? color = null)
        {
            text.font = Font(role);
            text.fontStyle = FontStyle.Normal;
            if (color.HasValue) text.color = color.Value;
            text.lineSpacing = role == Role.Body ? 1.05f : 1f;
            return text;
        }

        /// <summary>Panel or chip background: small-radius sliced sprite.</summary>
        public static Image Surface(Image image, Color color)
        {
            image.sprite = P1Art.Panel != null ? P1Art.Panel : P1Art.Rounded;
            image.type = Image.Type.Sliced;
            image.color = color;
            return image;
        }

        /// <summary>A hairline edge stretched over <paramref name="parent"/> (drawn on top, never a raycast target).</summary>
        public static Image Edge(Transform parent, Color color, string name = "Edge")
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = P1Art.Hairline != null ? P1Art.Hairline : P1Art.Outline;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 0.5f;   // 4 px edge: thin lines shimmer in a compressed headset stream
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Button colour states: quiet glass fill, accent on hover, success on press.</summary>
        public static ColorBlock ButtonColors(ColorBlock colors, bool primary = false)
        {
            colors.normalColor = primary ? Theme.Accent : ButtonFill;
            colors.highlightedColor = primary ? Color.Lerp(Theme.Accent, Color.white, 0.2f) : Color.Lerp(ButtonFill, Theme.Accent, 0.55f);
            colors.selectedColor = colors.normalColor;
            colors.pressedColor = Theme.Success;
            colors.disabledColor = new Color(0.08f, 0.1f, 0.13f, 0.7f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.1f;
            return colors;
        }
    }
}
