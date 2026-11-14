using UnityEngine;

namespace Astra.UI
{
    /// <summary>Shared in-world UI palette (matches the browser guide) and phone-legibility sizes.</summary>
    public static class Theme
    {
        public static readonly Color Accent = new Color(0.251f, 0.612f, 1f);      // #409CFF
        public static readonly Color Success = new Color(0.188f, 0.820f, 0.345f); // #30D158
        public static readonly Color Warning = new Color(1f, 0.702f, 0.251f);     // #FFB340
        public static readonly Color Error = new Color(1f, 0.412f, 0.380f);       // #FF6961
        public static readonly Color Ink = new Color(0.97f, 0.98f, 1f);
        public static readonly Color InkDim = new Color(0.78f, 0.84f, 0.92f);
        public static readonly Color AccentInk = new Color(0.62f, 0.82f, 1f);     // accent for text (contrast)
        public static readonly Color WarningInk = new Color(1f, 0.82f, 0.5f);
        public static readonly Color Glass = new Color(0.02f, 0.035f, 0.06f, 0.86f);
        public static readonly Color ButtonFill = new Color(0.12f, 0.2f, 0.32f, 1f);
        public static readonly Color Gold = new Color(1f, 0.8f, 0.25f);
        public static readonly Color Silver = new Color(0.8f, 0.84f, 0.9f);
        public static readonly Color Bronze = new Color(0.85f, 0.55f, 0.3f);

        /// <summary>Phone legibility (Irusu at ~0.55 m): body text ≥ 12 mm, titles ≥ 18 mm tall.</summary>
        public const float MinBodyMetres = 0.012f;
        public const float MinTitleMetres = 0.018f;
    }
}
