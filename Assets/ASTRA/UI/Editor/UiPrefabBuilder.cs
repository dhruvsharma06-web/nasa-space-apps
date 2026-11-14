using System;
using Astra.UI.Gaze;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Astra.UI.Editor
{
    /// <summary>
    /// Creates the P1-owned UI assets without modifying project or package settings.
    /// Prefabs are transparent-backed: the level factory adds the dark-glass backplate and glowing edge, and scales each
    /// panel to its physical width (PhaseBanner 0.34 m, InstructionPanel 0.42 m, DebriefPanel 0.46 m, AlarmIndicator 0.28 m).
    /// Font sizes are chosen from those widths so body text is ≥ 12 mm and titles ≥ 18 mm tall (cap height) on the phone.
    /// Every button carries a <see cref="GazeButton"/>.
    /// </summary>
    public static class UiPrefabBuilder
    {
        private const string OutputDirectory = "Assets/ASTRA/UI/Prefabs";
        internal static readonly Color Background = new Color(0.02f, 0.035f, 0.06f, 0f);   // transparent: factory backplate
        internal static readonly Color Glass = new Color(0.02f, 0.035f, 0.06f, 0.9f);       // for panels without a backplate
        internal static readonly Color Surface = new Color(0.07f, 0.12f, 0.18f, 0.6f);
        internal static readonly Color Foreground = new Color(0.97f, 0.98f, 1f, 1f);
        internal static readonly Color Accent = Theme.AccentInk;
        internal static readonly Color AccentBar = Theme.Accent;
        internal static readonly Color HintInk = Theme.WarningInk;
        internal static readonly Color ButtonFill = Theme.ButtonFill;

        [MenuItem("ASTRA/P1/Create shared UI prefabs")]
        public static void BuildUiPrefabs() => BuildInDirectory(OutputDirectory);

        /// <summary>P1's own rebuild: regenerates the shared UI prefabs in place (GUIDs kept).</summary>
        [MenuItem("ASTRA/P1/Rebuild shared UI prefabs (overwrites)")]
        public static void RebuildUiPrefabs() => BuildInDirectory(OutputDirectory, true);

        public static void BuildInDirectory(string directory) => BuildInDirectory(directory, false);

        public static void BuildInDirectory(string directory, bool overwrite)
        {
            if (string.IsNullOrWhiteSpace(directory) || !directory.StartsWith("Assets/ASTRA/UI/", StringComparison.Ordinal) ||
                directory.Contains("\\") || directory.EndsWith("/", StringComparison.Ordinal))
                throw new ArgumentException("UI output must be a folder under Assets/ASTRA/UI/.", nameof(directory));
            foreach (var segment in directory.Split('/'))
                if (segment == "." || segment == ".." || string.IsNullOrWhiteSpace(segment))
                    throw new ArgumentException("UI output must not escape its owned asset tree.", nameof(directory));

            EnsureDirectory(directory);
            P1Art.EnsureArt();
            Save(directory, "InstructionPanel", BuildInstruction, overwrite);
            Save(directory, "AlarmIndicator", BuildAlarm, overwrite);
            Save(directory, "PhaseBanner", BuildPhase, overwrite);
            Save(directory, "DebriefPanel", BuildDebrief, overwrite);
            Save(directory, "Certificate", BuildCertificate, overwrite);
            AssetDatabase.SaveAssets();
        }

        private static void Save(string directory, string name, Func<GameObject> create, bool overwrite)
        {
            var path = directory + "/" + name + ".prefab";
            // Regeneration must not silently overwrite a teammate's authored prefab adjustments.
            if (!overwrite && AssetDatabase.LoadMainAssetAtPath(path) != null)
                return;

            var instance = create();
            try
            {
                instance.SetActive(true);
                if (PrefabUtility.SaveAsPrefabAsset(instance, path) == null)
                    throw new InvalidOperationException("Could not save UI prefab: " + path);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        // 1000 px → 0.42 m: 0.42 mm/px. Instruction 46 px bold (cap ≈ 13.5 mm), hint 42 px (≈ 12.4 mm).
        private static GameObject BuildInstruction()
        {
            var root = Root("InstructionPanel", 1000f, 500f);
            AccentHeader(root.transform);
            Heading(Label(root.transform, "Heading", "INSTRUCTION", 30, 18f, 38f), Accent);
            var instruction = Label(root.transform, "Instruction", string.Empty, 46, 60f, 170f);
            instruction.fontStyle = FontStyle.Bold;
            Heading(Label(root.transform, "HintHeading", "PRACTICE HINT", 30, 236f, 36f), HintInk);
            var hint = Label(root.transform, "Hint", string.Empty, 42, 276f, 210f);
            root.AddComponent<InstructionPanel>().Configure(instruction, hint);
            return root;
        }

        // 900 px → 0.28 m: 0.31 mm/px. Caption 60 px bold (cap ≈ 13 mm).
        private static GameObject BuildAlarm()
        {
            var root = Root("AlarmIndicator", 900f, 180f);
            AccentHeader(root.transform);
            var light = Rect("AlarmLight", root.transform);
            light.anchorMin = light.anchorMax = new Vector2(0f, 0.5f);
            light.pivot = new Vector2(0f, 0.5f);
            light.anchoredPosition = new Vector2(30f, 0f);
            light.sizeDelta = new Vector2(90f, 90f);
            var graphic = light.gameObject.AddComponent<Image>();
            Rounded(graphic, Theme.Warning);
            graphic.raycastTarget = false;
            var caption = Label(root.transform, "Caption", string.Empty, 60, 20f, 150f);
            caption.fontStyle = FontStyle.Bold;
            caption.rectTransform.offsetMin = new Vector2(145f, caption.rectTransform.offsetMin.y);
            root.AddComponent<AlarmIndicator>().Configure(caption, graphic, 1.5f);
            return root;
        }

        // 560 px → 0.34 m: 0.61 mm/px. Phase title 52 px bold (cap ≈ 22 mm).
        private static GameObject BuildPhase()
        {
            var root = Root("PhaseBanner", 560f, 100f);
            var label = Label(root.transform, "Phase", string.Empty, 52, 14f, 72f);
            label.alignment = TextAnchor.MiddleCenter;
            label.fontStyle = FontStyle.Bold;
            label.color = Accent;
            root.AddComponent<PhaseBanner>().Configure(label);
            return root;
        }

        // 960 px → 0.46 m: 0.48 mm/px. Body 36 px (cap ≈ 12.1 mm), result 64 px bold (≈ 21 mm), score 96 px.
        private static GameObject BuildDebrief()
        {
            var root = Root("DebriefPanel", 960f, 1060f);
            AccentHeader(root.transform);
            Heading(Label(root.transform, "Heading", "DEBRIEF", 30, 16f, 38f), Accent);
            var outcome = Label(root.transform, "Outcome", string.Empty, 64, 58f, 80f);
            outcome.fontStyle = FontStyle.Bold;

            // Score row: score (counts up), medal badge with a soft glow, NEW BEST.
            var score = Placed(root.transform, "Score", "0", 96, -300f, 140f, 300f, 112f, TextAnchor.MiddleLeft);
            score.fontStyle = FontStyle.Bold;
            var glow = Box(root.transform, "MedalGlow", 60f, 140f, 280f, 120f, new Color(1f, 0.8f, 0.25f, 0f));
            var badge = Box(root.transform, "MedalBadge", 60f, 150f, 240f, 92f, Theme.Gold);
            var medal = Placed(badge.transform, "MedalLabel", string.Empty, 44, 0f, 0f, 240f, 92f, TextAnchor.MiddleCenter, true);
            medal.fontStyle = FontStyle.Bold;
            var outline = medal.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            outline.effectDistance = new Vector2(2f, -2f);
            var newBest = Placed(root.transform, "NewBest", "NEW BEST", 40, 320f, 154f, 240f, 80f, TextAnchor.MiddleCenter);
            newBest.fontStyle = FontStyle.Bold;
            newBest.color = HintInk;

            var lines = new Text[5];
            for (var i = 0; i < lines.Length; i++)
                lines[i] = Label(root.transform, "Breakdown" + i, string.Empty, 36, 266f + i * 46f, 44f);

            var details = ScrollingText(root.transform, "Details", 500f, 220f, 36);
            var coachHeading = Label(root.transform, "CoachHeading", string.Empty, 30, 730f, 38f);
            coachHeading.fontStyle = FontStyle.Bold;
            coachHeading.color = Accent;
            var coach = ScrollingText(root.transform, "Coach", 772f, 150f, 36);
            var panel = root.AddComponent<DebriefPanel>();
            panel.Configure(outcome, details, coachHeading, coach);
            panel.ConfigureReveal(score, medal, badge, glow, newBest, lines);

            var retry = CreateButton(root.transform, "Retry", "RETRY", -260f, 942f, 400f);
            var next = CreateButton(root.transform, "NextLevel", "NEXT LEVEL", 110f, 942f, 260f);
            var hub = CreateButton(root.transform, "BackToHub", "HUB", 360f, 942f, 200f);
            UnityEventTools.AddPersistentListener(retry.onClick, panel.RequestRetry);
            UnityEventTools.AddPersistentListener(next.onClick, panel.RequestNextLevel);
            UnityEventTools.AddPersistentListener(hub.onClick, panel.RequestBackToHub);
            Gaze(retry, "debrief_retry", "RETRY");
            Gaze(next, "debrief_next_level", "NEXT LEVEL");
            Gaze(hub, "debrief_hub", "HUB");
            panel.ConfigureButtons(retry, next, hub);
            return root;
        }

        /// <summary>Graduation certificate (shown over the debrief of the last level). Has its own glass background.</summary>
        private static GameObject BuildCertificate()
        {
            var root = Root("Certificate", 1200f, 860f);
            var certificate = root.AddComponent<CertificatePanel>();
            var panel = Rect("CertificateRoot", root.transform);
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.offsetMin = panel.offsetMax = Vector2.zero;
            Rounded(panel.gameObject.AddComponent<Image>(), Glass);
            AccentHeader(panel);
            var crest = Box(panel, "Crest", 0f, 40f, 150f, 150f, new Color(1f, 1f, 1f, 0.9f));
            crest.sprite = P1Art.Outline;
            Placed(crest.transform, "CrestText", "ASTRA\nSTATION", 26, 0f, 0f, 150f, 150f, TextAnchor.MiddleCenter, true).fontStyle = FontStyle.Bold;
            var heading = Label(panel, "Heading", CertificatePanel.Heading, 56, 200f, 70f);
            heading.alignment = TextAnchor.MiddleCenter;
            heading.fontStyle = FontStyle.Bold;
            var callsign = Label(panel, "Callsign", "CADET", 64, 280f, 80f);
            callsign.alignment = TextAnchor.MiddleCenter;
            callsign.fontStyle = FontStyle.Bold;
            callsign.color = Accent;
            var date = Label(panel, "Date", string.Empty, 34, 366f, 44f);
            date.alignment = TextAnchor.MiddleCenter;
            var score = Label(panel, "Score", string.Empty, 40, 416f, 52f);
            score.alignment = TextAnchor.MiddleCenter;
            score.fontStyle = FontStyle.Bold;
            var medals = new Text[8];
            var badges = new Graphic[8];
            for (var i = 0; i < 8; i++)
            {
                var badge = Box(panel, "Medal" + i, -490f + i * 140f, 500f, 120f, 120f, new Color(0.25f, 0.28f, 0.33f));
                badges[i] = badge;
                medals[i] = Placed(badge.transform, "Label", "L" + i + "\n-", 26, 0f, 0f, 120f, 120f, TextAnchor.MiddleCenter, true);
                medals[i].fontStyle = FontStyle.Bold;
                var o = medals[i].gameObject.AddComponent<UnityEngine.UI.Outline>();
                o.effectColor = new Color(0f, 0f, 0f, 0.8f);
            }
            var disclaimer = Label(panel, "Disclaimer", CertificatePanel.Disclaimer, 30, 650f, 40f);
            disclaimer.alignment = TextAnchor.MiddleCenter;
            disclaimer.color = new Color(0.82f, 0.86f, 0.92f);
            var close = CreateButton(panel, "Close", "CLOSE", 0f, 720f, 280f);
            UnityEventTools.AddPersistentListener(close.onClick, certificate.Close);
            Gaze(close, "certificate_close", "CLOSE");
            certificate.Configure(panel.gameObject, callsign, date, score, medals, badges);
            return root;
        }

        private static GameObject Root(string name, float width, float height)
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.SetActive(false);
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(width, height);
            rect.localScale = Vector3.one * 0.001f;
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            root.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 3f;
            root.AddComponent<GraphicRaycaster>();
            var image = root.AddComponent<Image>();
            Rounded(image, Background);
            image.raycastTarget = false;
            return root;
        }

        internal static void Gaze(Button button, string id, string label)
        {
            var gaze = button.gameObject.AddComponent<GazeButton>();
            gaze.Configure(id, label);
            gaze.FitCollider();
        }

        private static Text Label(Transform parent, string name, string value, int size, float top, float height)
        {
            var rect = Rect(name, parent);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -top);
            rect.sizeDelta = new Vector2(-70f, height);
            return CreateText(rect, value, size);
        }

        /// <summary>Text centred at (x, -top) from the parent's top centre, or at (x, y) from its centre when <paramref name="centred"/>.</summary>
        private static Text Placed(Transform parent, string name, string value, int size, float x, float top, float width, float height,
            TextAnchor anchor, bool centred = false)
        {
            var rect = Rect(name, parent);
            rect.anchorMin = rect.anchorMax = centred ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, 1f);
            rect.pivot = centred ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, 1f);
            rect.anchoredPosition = centred ? new Vector2(x, top) : new Vector2(x, -top);
            rect.sizeDelta = new Vector2(width, height);
            var text = CreateText(rect, value, size);
            text.alignment = anchor;
            return text;
        }

        private static Image Box(Transform parent, string name, float x, float top, float width, float height, Color color)
        {
            var rect = Rect(name, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(x, -top);
            rect.sizeDelta = new Vector2(width, height);
            var image = rect.gameObject.AddComponent<Image>();
            Rounded(image, color);
            image.raycastTarget = false;
            return image;
        }

        private static Text CreateText(RectTransform rect, string value, int size)
        {
            var text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null)
                throw new InvalidOperationException("Unity's built-in LegacyRuntime.ttf font is unavailable.");
            text.text = value;
            text.fontSize = size;
            text.color = Foreground;
            text.supportRichText = false;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static Text ScrollingText(Transform parent, string name, float top, float height, int size)
        {
            var viewport = Rect(name + "Viewport", parent);
            viewport.anchorMin = new Vector2(0f, 1f);
            viewport.anchorMax = new Vector2(1f, 1f);
            viewport.pivot = new Vector2(0.5f, 1f);
            viewport.anchoredPosition = new Vector2(0f, -top);
            viewport.sizeDelta = new Vector2(-70f, height);
            Rounded(viewport.gameObject.AddComponent<Image>(), Surface);
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = Rect(name + "Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(-30f, height);
            var text = CreateText(content, string.Empty, size);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            return text;
        }

        private static Button CreateButton(Transform parent, string name, string caption, float x, float top, float width)
        {
            var rect = Rect(name, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(x, -top);
            rect.sizeDelta = new Vector2(width, 100f);
            var button = StyleButton(rect.gameObject);
            var text = Label(rect, name + "Label", caption, 36, 0f, 100f);
            text.rectTransform.sizeDelta = new Vector2(-24f, 100f);
            text.alignment = TextAnchor.MiddleCenter;
            text.fontStyle = FontStyle.Bold;
            return button;
        }

        /// <summary>Rounded button whose fill comes from the colour block, with clear hover/press states.</summary>
        internal static Button StyleButton(GameObject target)
        {
            var image = target.AddComponent<Image>();
            Rounded(image, Color.white);
            var button = target.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = ButtonFill;
            colors.highlightedColor = Color.Lerp(ButtonFill, Theme.Accent, 0.6f);
            colors.selectedColor = ButtonFill;
            colors.pressedColor = Theme.Success;
            colors.disabledColor = new Color(0.16f, 0.18f, 0.22f, 0.85f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            var outline = Rect("Edge", target.transform);
            outline.anchorMin = Vector2.zero;
            outline.anchorMax = Vector2.one;
            outline.offsetMin = outline.offsetMax = Vector2.zero;
            var frame = outline.gameObject.AddComponent<Image>();
            frame.sprite = P1Art.Outline;
            frame.type = Image.Type.Sliced;
            frame.color = new Color(Theme.Accent.r, Theme.Accent.g, Theme.Accent.b, 0.55f);
            frame.raycastTarget = false;
            return button;
        }

        internal static void Rounded(Image image, Color color)
        {
            image.sprite = P1Art.Rounded;
            image.type = Image.Type.Sliced;
            image.color = color;
        }

        private static void Heading(Text text, Color color)
        {
            text.fontStyle = FontStyle.Bold;
            text.color = color;
        }

        /// <summary>Thin accent bar along the top edge that marks a panel as ASTRA UI.</summary>
        internal static void AccentHeader(Transform parent)
        {
            var bar = Rect("AccentBar", parent);
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.anchoredPosition = new Vector2(0f, -4f);
            bar.sizeDelta = new Vector2(-60f, 6f);
            var image = bar.gameObject.AddComponent<Image>();
            image.color = AccentBar;
            image.raycastTarget = false;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static void EnsureDirectory(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            var separator = path.LastIndexOf('/');
            var parent = path.Substring(0, separator);
            EnsureDirectory(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(separator + 1));
        }
    }
}
