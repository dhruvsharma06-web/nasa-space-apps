using System;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Astra.UI.Editor
{
    /// <summary>Creates the P1-owned UI assets without modifying project or package settings.</summary>
    public static class UiPrefabBuilder
    {
        private const string OutputDirectory = "Assets/ASTRA/UI/Prefabs";
        // Sizes target the phone headset: ~13 px per degree after HEVC, so body text is >= ~1.4 degrees tall and bold.
        internal static readonly Color Background = new Color(0.03f, 0.055f, 0.095f, 0.95f);
        internal static readonly Color Surface = new Color(0.07f, 0.12f, 0.18f, 1f);
        internal static readonly Color Foreground = new Color(0.97f, 0.98f, 1f, 1f);
        internal static readonly Color Accent = new Color(0.55f, 0.92f, 1f, 1f);
        internal static readonly Color AccentBar = new Color(0.2f, 0.72f, 0.95f, 1f);
        internal static readonly Color HintInk = new Color(1f, 0.84f, 0.5f, 1f);
        internal static readonly Color ButtonFill = new Color(0.13f, 0.3f, 0.47f, 1f);

        [MenuItem("ASTRA/P1/Create shared UI prefabs")]
        public static void BuildUiPrefabs() => BuildInDirectory(OutputDirectory);

        /// <summary>P1's own rebuild: regenerates the shared UI prefabs in place (GUIDs kept).</summary>
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

        private static GameObject BuildInstruction()
        {
            var root = Root("InstructionPanel", 1000f, 410f);
            AccentHeader(root.transform);
            Heading(Label(root.transform, "Heading", "INSTRUCTION", 26, 22f, 34f), Accent);
            var instruction = Label(root.transform, "Instruction", string.Empty, 44, 62f, 150f);
            instruction.fontStyle = FontStyle.Bold;
            Heading(Label(root.transform, "HintHeading", "PRACTICE HINT", 24, 226f, 30f), HintInk);
            var hint = Label(root.transform, "Hint", string.Empty, 34, 260f, 136f);
            root.AddComponent<InstructionPanel>().Configure(instruction, hint);
            return root;
        }

        private static GameObject BuildAlarm()
        {
            var root = Root("AlarmIndicator", 900f, 180f);
            AccentHeader(root.transform);
            var light = Rect("AlarmLight", root.transform);
            light.anchorMin = light.anchorMax = new Vector2(0f, 0.5f);
            light.pivot = new Vector2(0f, 0.5f);
            light.anchoredPosition = new Vector2(35f, 0f);
            light.sizeDelta = new Vector2(70f, 70f);
            var graphic = light.gameObject.AddComponent<Image>();
            graphic.color = new Color(1f, 0.55f, 0.12f, 1f);
            graphic.raycastTarget = false;
            var caption = Label(root.transform, "Caption", string.Empty, 36, 35f, 110f);
            caption.fontStyle = FontStyle.Bold;
            caption.rectTransform.offsetMin = new Vector2(135f, caption.rectTransform.offsetMin.y);
            root.AddComponent<AlarmIndicator>().Configure(caption, graphic, 1.5f);
            return root;
        }

        private static GameObject BuildPhase()
        {
            var root = Root("PhaseBanner", 560f, 100f);
            var label = Label(root.transform, "Phase", string.Empty, 42, 18f, 64f);
            label.alignment = TextAnchor.MiddleCenter;
            label.fontStyle = FontStyle.Bold;
            label.color = Accent;
            root.AddComponent<PhaseBanner>().Configure(label);
            return root;
        }

        private static GameObject BuildDebrief()
        {
            var root = Root("DebriefPanel", 960f, 880f);
            AccentHeader(root.transform);
            Heading(Label(root.transform, "Heading", "DEBRIEF", 26, 22f, 36f), Accent);
            var outcome = Label(root.transform, "Outcome", string.Empty, 54, 60f, 70f);
            outcome.fontStyle = FontStyle.Bold;
            var details = ScrollingText(root.transform, "Details", 145f, 340f, 30);
            var coachHeading = Label(root.transform, "CoachHeading", string.Empty, 26, 500f, 36f);
            coachHeading.fontStyle = FontStyle.Bold;
            var coach = ScrollingText(root.transform, "Coach", 545f, 175f, 28);
            var panel = root.AddComponent<DebriefPanel>();
            panel.Configure(outcome, details, coachHeading, coach);

            var retry = CreateButton(root.transform, "Retry", "RETRY", -175f, 752f);
            var hub = CreateButton(root.transform, "BackToHub", "HUB", 175f, 752f);
            UnityEventTools.AddPersistentListener(retry.onClick, panel.RequestRetry);
            UnityEventTools.AddPersistentListener(hub.onClick, panel.RequestBackToHub);
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
            root.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 2f;
            root.AddComponent<GraphicRaycaster>();
            Rounded(root.AddComponent<Image>(), Background);
            return root;
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

        private static Button CreateButton(Transform parent, string name, string caption, float x, float top)
        {
            var rect = Rect(name, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(x, -top);
            rect.sizeDelta = new Vector2(300f, 96f);
            var button = StyleButton(rect.gameObject);
            var text = Label(rect, name + "Label", caption, 36, 0f, 96f);
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
            colors.highlightedColor = new Color(0.2f, 0.55f, 0.8f, 1f);
            colors.selectedColor = ButtonFill;
            colors.pressedColor = new Color(0.25f, 0.75f, 0.5f, 1f);
            colors.disabledColor = new Color(0.2f, 0.23f, 0.28f, 0.85f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            var outline = Rect("Outline", target.transform);
            outline.anchorMin = Vector2.zero;
            outline.anchorMax = Vector2.one;
            outline.offsetMin = outline.offsetMax = Vector2.zero;
            var frame = outline.gameObject.AddComponent<Image>();
            frame.sprite = P1Art.Outline;
            frame.type = Image.Type.Sliced;
            frame.color = new Color(0.55f, 0.85f, 1f, 0.45f);
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
            bar.anchoredPosition = new Vector2(0f, -6f);
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
