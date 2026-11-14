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
        private static readonly Color Background = new Color(0.035f, 0.065f, 0.11f, 0.98f);
        private static readonly Color Surface = new Color(0.07f, 0.12f, 0.18f, 1f);
        private static readonly Color Foreground = new Color(0.96f, 0.97f, 1f, 1f);

        [MenuItem("ASTRA/P1/Create shared UI prefabs")]
        public static void BuildUiPrefabs()
        {
            BuildInDirectory(OutputDirectory);
        }

        public static void BuildInDirectory(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory) || !directory.StartsWith("Assets/ASTRA/UI/", StringComparison.Ordinal) ||
                directory.Contains("\\") || directory.EndsWith("/", StringComparison.Ordinal))
                throw new ArgumentException("UI output must be a folder under Assets/ASTRA/UI/.", nameof(directory));
            foreach (var segment in directory.Split('/'))
                if (segment == "." || segment == ".." || string.IsNullOrWhiteSpace(segment))
                    throw new ArgumentException("UI output must not escape its owned asset tree.", nameof(directory));

            EnsureDirectory(directory);
            SaveIfMissing(directory, "InstructionPanel", BuildInstruction);
            SaveIfMissing(directory, "AlarmIndicator", BuildAlarm);
            SaveIfMissing(directory, "PhaseBanner", BuildPhase);
            SaveIfMissing(directory, "DebriefPanel", BuildDebrief);
            AssetDatabase.SaveAssets();
        }

        private static void SaveIfMissing(string directory, string name, Func<GameObject> create)
        {
            var path = directory + "/" + name + ".prefab";
            // Regeneration must not silently overwrite a teammate's authored prefab adjustments.
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
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
            var root = Root("InstructionPanel", 900f, 330f);
            Label(root.transform, "Heading", "INSTRUCTIONS", 24, 24f, 36f);
            var instruction = Label(root.transform, "Instruction", string.Empty, 32, 70f, 112f);
            Label(root.transform, "HintHeading", "PRACTICE HINT", 22, 200f, 30f);
            var hint = Label(root.transform, "Hint", string.Empty, 28, 240f, 68f);
            root.AddComponent<InstructionPanel>().Configure(instruction, hint);
            return root;
        }

        private static GameObject BuildAlarm()
        {
            var root = Root("AlarmIndicator", 900f, 180f);
            var light = Rect("AlarmLight", root.transform);
            light.anchorMin = light.anchorMax = new Vector2(0f, 0.5f);
            light.pivot = new Vector2(0f, 0.5f);
            light.anchoredPosition = new Vector2(35f, 0f);
            light.sizeDelta = new Vector2(70f, 70f);
            var graphic = light.gameObject.AddComponent<Image>();
            graphic.color = new Color(1f, 0.55f, 0.12f, 1f);
            graphic.raycastTarget = false;
            var caption = Label(root.transform, "Caption", string.Empty, 30, 35f, 110f);
            caption.rectTransform.offsetMin = new Vector2(135f, caption.rectTransform.offsetMin.y);
            root.AddComponent<AlarmIndicator>().Configure(caption, graphic, 1.5f);
            return root;
        }

        private static GameObject BuildPhase()
        {
            var root = Root("PhaseBanner", 500f, 110f);
            var label = Label(root.transform, "Phase", string.Empty, 32, 25f, 60f);
            label.alignment = TextAnchor.MiddleCenter;
            root.AddComponent<PhaseBanner>().Configure(label);
            return root;
        }

        private static GameObject BuildDebrief()
        {
            var root = Root("DebriefPanel", 900f, 820f);
            Label(root.transform, "Heading", "DEBRIEF", 24, 22f, 36f);
            var outcome = Label(root.transform, "Outcome", string.Empty, 44, 60f, 65f);
            var details = ScrollingText(root.transform, "Details", 145f, 320f, 25);
            var coachHeading = Label(root.transform, "CoachHeading", string.Empty, 24, 485f, 35f);
            var coach = ScrollingText(root.transform, "Coach", 530f, 140f, 24);
            var panel = root.AddComponent<DebriefPanel>();
            panel.Configure(outcome, details, coachHeading, coach);

            var retry = CreateButton(root.transform, "Retry", "RETRY", -170f, 705f);
            var hub = CreateButton(root.transform, "BackToHub", "HUB", 170f, 705f);
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
            root.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 1f;
            root.AddComponent<GraphicRaycaster>();
            root.AddComponent<Image>().color = Background;
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
            viewport.gameObject.AddComponent<Image>().color = Surface;
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
            rect.sizeDelta = new Vector2(280f, 80f);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.15f, 0.3f, 0.46f, 1f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var text = Label(rect, name + "Label", caption, 30, 0f, 80f);
            text.alignment = TextAnchor.MiddleCenter;
            return button;
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
