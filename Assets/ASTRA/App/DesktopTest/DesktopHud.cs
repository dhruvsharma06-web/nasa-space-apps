using System.Collections.Generic;
using System.Text;
using Astra.App;
using Astra.Core.Assessment;
using Astra.Core.Mission;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Astra.DesktopTest
{
    /// <summary>
    /// Desktop/WebGL GUIDE for the browser build (IMGUI, rounded dark-glass panels): welcome screen, phase cards,
    /// mission checklist, "next step" bar, glow on the next control in Practice, crosshair labels, shortcuts, level switch.
    /// Test tooling, not headset UI: XR & Interaction owns the in-world UI (CONTRACT §7).
    /// OWNER: Integration Lead.
    /// </summary>
    public class DesktopHud : MonoBehaviour
    {
        [SerializeField] private LevelController level;
        [SerializeField] private DesktopRig rig;
        [Tooltip("Kept for compatibility with the scene builder; the guide is shown either way.")]
        [SerializeField] private bool compact;

        public static bool PointerOverHud { get; private set; }

        private static readonly Dictionary<ModulePhase, (string title, string line)> PhaseText = new Dictionary<ModulePhase, (string, string)>
        {
            { ModulePhase.Briefing,      ("BRIEFING",      "Read the mission. Next you will see how it is done.") },
            { ModulePhase.Demonstration, ("DEMONSTRATION", "Watch: each control lights up in the order you will use it.") },
            { ModulePhase.Practice,      ("PRACTICE",      "Your turn. The glowing control is your next step. Hints appear after a mistake.") },
            { ModulePhase.Qualification, ("QUALIFICATION", "Scored. No glow, no hints. Follow the checklist on your own.") },
            { ModulePhase.Debrief,       ("DEBRIEF",       "Your result and advice. Press Enter to practise again.") },
        };

        private static readonly Color Accent = new Color(0.25f, 0.61f, 1f);

        private readonly SortedDictionary<int, string> _levels = new SortedDictionary<int, string>();
        private readonly HashSet<string> _done = new HashSet<string>();
        private string _hint = "", _feedback = "";
        private float _feedbackAt;
        private MissionStep _demoStep;
        private bool _welcome = true;
        private float _cardUntil;
        private ModulePhase _cardPhase;
        private GUIStyle _glass, _glassDark, _pill, _h1, _h2, _body, _bodyC, _small, _tag, _btn, _check;

        public void Configure(LevelController lc, DesktopRig r, bool compactHud = false) { level = lc; rig = r; compact = compactHud; }

        private void Awake()
        {
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i));
                if (name.Length > 1 && name[0] == 'L' && char.IsDigit(name[1])) _levels[name[1] - '0'] = name;   // L3_Cabin -> 3
            }
        }

        private void OnEnable()
        {
            if (level == null) { Debug.LogError("[DesktopHud] LevelController is not assigned.", this); enabled = false; return; }
            level.PhaseChanged += OnPhase;
            level.EventRecorded += OnEvent;
            level.DemonstrationStep += OnDemo;
        }

        private void OnDisable()
        {
            if (level == null) return;
            level.PhaseChanged -= OnPhase;
            level.EventRecorded -= OnEvent;
            level.DemonstrationStep -= OnDemo;
        }

        private void Start() { if (_welcome && level != null) level.Pause(); }   // the level waits on the welcome screen

        private void OnDemo(MissionStep s) => _demoStep = s;

        private void OnPhase(ModulePhase p)
        {
            _feedback = ""; _hint = ""; _demoStep = null;
            _cardPhase = p; _cardUntil = Time.unscaledTime + 4f;
            if (p == ModulePhase.Practice || p == ModulePhase.Qualification) { _done.Clear(); TestTarget.ResetAll(); }
        }

        private void OnEvent(SessionEvent e, StepFeedback fb)
        {
            if (fb.Outcome == StepOutcome.Correct && fb.StepId != null) _done.Add(fb.StepId);
            if (level.Phase != ModulePhase.Practice) { _feedback = ""; _hint = ""; return; }
            _feedbackAt = Time.unscaledTime;
            _feedback = fb.Outcome switch
            {
                StepOutcome.Correct => "<color=#30D158><b>✓ Correct</b></color>",
                StepOutcome.Incorrect => "<color=#FF6961><b>✕ Not that one.</b></color> That control is not part of this step.",
                StepOutcome.OutOfOrder => "<color=#FFB340><b>Not yet.</b></color> That step comes later.",
                _ => "",
            };
            _hint = fb.Outcome == StepOutcome.Incorrect || fb.Outcome == StepOutcome.OutOfOrder ? fb.Hint ?? "" : "";
        }

        /// <summary>Closes the welcome screen and starts the level (the Start button and Enter call this).</summary>
        public void StartMission()
        {
            if (!_welcome) return;
            _welcome = false;
            level.Resume();
            _cardPhase = level.Phase; _cardUntil = Time.unscaledTime + 4f;
        }

        private void Update()
        {
            // glow: the demonstrated step, or (Practice only) the next step's control. Never in Qualification.
            string glow = level == null ? null
                : level.Phase == ModulePhase.Demonstration ? _demoStep?.targetId
                : level.Phase == ModulePhase.Practice ? level.Runner?.CurrentStep?.targetId : null;
            foreach (var t in TestTarget.Registry.Values) t.SetDemoHighlight(glow != null && t.TargetId == glow);

            if (_welcome && level != null && !level.IsPaused) level.Pause();   // whichever Start() ran first, the level waits
            var kb = Keyboard.current;
            if (kb == null || level == null) return;
            if (_welcome) { if (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) StartMission(); return; }
            if (kb.spaceKey.wasPressedThisFrame) level.Skip();
            if (kb.enterKey.wasPressedThisFrame) { if (level.Phase == ModulePhase.Debrief) level.Retry(); else level.SkipToPractice(); }
            if (kb.fKey.wasPressedThisFrame) level.Finish();
            if (kb.pKey.wasPressedThisFrame) { if (level.IsPaused) level.Resume(); else level.Pause(); }
            if (kb.rKey.wasPressedThisFrame) { level.Recenter(); if (rig != null) rig.Recenter(); }
            if (kb.backspaceKey.wasPressedThisFrame) level.Restart();
            var digits = new[] { kb.digit0Key, kb.digit1Key, kb.digit2Key, kb.digit3Key, kb.digit4Key, kb.digit5Key, kb.digit6Key, kb.digit7Key };
            for (int n = 0; n < digits.Length; n++)
                if (digits[n].wasPressedThisFrame && _levels.TryGetValue(n, out var scene) && scene != SceneManager.GetActiveScene().name)
                    SceneManager.LoadScene(scene);
        }

        // ------------------------------------------------------------------ styles

        /// <summary>Anti-aliased rounded rectangle, used 9-sliced so panels get real rounded corners and a hairline edge.</summary>
        private static Texture2D Rounded(Color fill, Color edge, int radius)
        {
            int n = radius * 2 + 4;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, radius, n - radius), cy = Mathf.Clamp(y + 0.5f, radius, n - radius);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));   // distance from the inner rect
                float cover = Mathf.Clamp01(radius - d + 0.5f);
                float rim = Mathf.Clamp01(1f - Mathf.Abs(radius - 0.75f - d));                        // 1 px hairline
                var c = Color.Lerp(fill, edge, rim * edge.a);
                c.a = Mathf.Max(fill.a, rim * edge.a) * cover;
                t.SetPixel(x, y, c);
            }
            t.Apply();
            return t;
        }

        private static GUIStyle Box(Texture2D bg, int radius, RectOffset padding)
        {
            var s = new GUIStyle { border = new RectOffset(radius + 2, radius + 2, radius + 2, radius + 2), padding = padding };
            s.normal.background = bg;
            return s;
        }

        private static GUIStyle Text(int size, Color color, FontStyle style = FontStyle.Normal, TextAnchor anchor = TextAnchor.UpperLeft)
        {
            var s = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = style, alignment = anchor, wordWrap = true, richText = true };
            s.normal.textColor = color;
            return s;
        }

        private void Styles()
        {
            if (_h1 != null) return;
            var hair = new Color(1f, 1f, 1f, 0.14f);
            _glass = Box(Rounded(new Color(0.05f, 0.07f, 0.11f, 0.78f), hair, 14), 14, new RectOffset(20, 20, 16, 16));
            _glassDark = Box(Rounded(new Color(0.02f, 0.03f, 0.06f, 0.92f), hair, 20), 20, new RectOffset(36, 36, 30, 30));
            _pill = Box(Rounded(new Color(0.02f, 0.03f, 0.06f, 0.85f), hair, 13), 13, new RectOffset(14, 14, 5, 5));

            _h1 = Text(36, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            _h2 = Text(23, Color.white, FontStyle.Bold);
            _body = Text(16, new Color(0.86f, 0.89f, 0.95f));
            _bodyC = Text(17, new Color(0.86f, 0.89f, 0.95f), FontStyle.Normal, TextAnchor.MiddleCenter);
            _small = Text(13, new Color(0.60f, 0.65f, 0.74f));
            _tag = Text(12, Accent, FontStyle.Bold);
            _check = Text(15, new Color(0.86f, 0.89f, 0.95f));

            _btn = new GUIStyle
            {
                fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, fixedHeight = 50,
                border = new RectOffset(16, 16, 16, 16), margin = new RectOffset(0, 0, 10, 0),
            };
            _btn.normal.background = Rounded(Accent, new Color(1, 1, 1, 0.25f), 14);
            _btn.hover.background = Rounded(new Color(0.38f, 0.7f, 1f), new Color(1, 1, 1, 0.35f), 14);
            _btn.active.background = Rounded(new Color(0.18f, 0.48f, 0.85f), new Color(1, 1, 1, 0.2f), 14);
            _btn.normal.textColor = _btn.hover.textColor = _btn.active.textColor = Color.white;
        }

        // ------------------------------------------------------------------ drawing

        private void OnGUI()
        {
            if (level == null || level.Mission == null) return;
            Styles();
            PointerOverHud = false;
            if (_welcome) { DrawWelcome(); return; }

            DrawCrosshair();
            DrawTopBar();
            DrawChecklist();
            DrawNextStepBar();
            if (Time.unscaledTime < _cardUntil) DrawPhaseCard();
            if (rig != null && !rig.Locked) DrawClickToLook();
        }

        private void DrawWelcome()
        {
            var m = level.Mission;
            var dim = GUI.color; GUI.color = new Color(0, 0, 0, 0.55f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = dim;

            float w = Mathf.Min(780, Screen.width - 40), h = Mathf.Min(600, Screen.height - 40);
            GUILayout.BeginArea(new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h), _glassDark);
            GUILayout.Label("ASTRA ACADEMY  ·  TRAINING SIMULATOR", _tag);
            GUILayout.Space(4);
            GUILayout.Label(m.title, Text(30, Color.white, FontStyle.Bold));
            GUILayout.Space(6);
            GUILayout.Label("You are a junior crew member on the fictional ASTRA Station. Complete the procedure correctly and in order.", _body);
            GUILayout.Space(16);
            GUILayout.Label("HOW THIS LEVEL WORKS", _tag);
            Row("1", "Briefing", "read the mission");
            Row("2", "Demonstration", "watch the controls light up in order");
            Row("3", "Practice", "do it yourself: the next control glows, hints help");
            Row("4", "Qualification", "do it again, scored, no help");
            Row("5", "Debrief", "your result and how to improve");
            GUILayout.Space(14);
            GUILayout.Label("CONTROLS", _tag);
            GUILayout.Label("<b>Click</b> the view, then move the <b>mouse</b> to look  ·  aim the <b>+</b> at a control  ·  <b>click</b> to use it\n<b>Esc</b> frees the mouse  ·  <b>Enter</b> skips to Practice  ·  <b>P</b> pauses", _body);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Start mission   ⏎", _btn)) StartMission();
            GUILayout.Space(6);
            GUILayout.Label("Fictional educational training. Not real procedures or certification.", Text(12, new Color(0.5f, 0.55f, 0.63f), FontStyle.Normal, TextAnchor.MiddleCenter));
            GUILayout.EndArea();
            PointerOverHud = true;
        }

        private void Row(string n, string name, string what)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<color=#409CFF><b>{n}</b></color>", _body, GUILayout.Width(22));
            GUILayout.Label($"<b>{name}</b>  <color=#9AA3B2>{what}</color>", _body);
            GUILayout.EndHorizontal();
        }

        private void DrawCrosshair()
        {
            if (rig == null || !rig.Locked) return;
            var c = new Vector2(Screen.width / 2f, Screen.height / 2f);
            var hovered = rig.Hovered;
            float g = hovered != null ? 7f : 9f, len = hovered != null ? 7f : 6f;
            var old = GUI.color;
            GUI.color = hovered != null ? Accent : new Color(1f, 1f, 1f, 0.9f);
            GUI.DrawTexture(new Rect(c.x - 1, c.y - g - len, 2, len), Texture2D.whiteTexture);   // four ticks with a gap
            GUI.DrawTexture(new Rect(c.x - 1, c.y + g, 2, len), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(c.x - g - len, c.y - 1, len, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(c.x + g, c.y - 1, len, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(c.x - 1, c.y - 1, 2, 2), Texture2D.whiteTexture);
            GUI.color = old;
            if (hovered == null) return;
            var content = new GUIContent($"<b>{LabelOf(hovered.TargetId)}</b>   <color=#9AA3B2>click</color>");
            var size = _body.CalcSize(content);
            var r = new Rect(c.x - size.x / 2f - 14, c.y + 26, size.x + 28, 30);
            GUI.Box(r, GUIContent.none, _pill);
            GUI.Label(new Rect(r.x + 14, r.y + 4, size.x + 4, 24), content, _body);
        }

        private void DrawTopBar()
        {
            var m = level.Mission;
            var r = new Rect(20, 20, 460, 100);
            GUILayout.BeginArea(r, _glass);
            GUILayout.Label($"{PhaseText[level.Phase].title}{(level.IsPaused ? "   ·   PAUSED  (P)" : "")}", _tag);
            GUILayout.Label(m.title, Text(17, Color.white, FontStyle.Bold));
            var keys = new StringBuilder("Esc mouse  ·  Enter practice/retry  ·  P pause  ·  R recenter");
            if (_levels.Count > 1)
            {
                keys.Append("\nLevels: ");
                foreach (var kv in _levels) keys.Append(kv.Value == SceneManager.GetActiveScene().name ? $"<color=#409CFF><b>{kv.Key}</b></color>  " : $"{kv.Key}  ");
            }
            GUILayout.Label(keys.ToString(), _small);
            GUILayout.EndArea();
            if (r.Contains(Event.current.mousePosition)) PointerOverHud = true;
        }

        private void DrawChecklist()
        {
            if (level.Phase == ModulePhase.Briefing) return;
            var steps = new List<MissionStep>(level.Mission.steps);
            steps.Sort((a, b) => a.order.CompareTo(b.order));
            string current = level.Phase == ModulePhase.Demonstration ? _demoStep?.stepId : level.Runner?.CurrentStep?.stepId;
            float w = 370, h = 62 + steps.Count * 30;
            var r = new Rect(Screen.width - w - 20, 20, w, h);
            GUILayout.BeginArea(r, _glass);
            GUILayout.Label($"MISSION CHECKLIST   {_done.Count} / {steps.Count}", _tag);
            GUILayout.Space(4);
            foreach (var s in steps)
            {
                bool done = _done.Contains(s.stepId), now = s.stepId == current;
                string mark = done ? "<color=#30D158>✓</color>" : now ? "<color=#409CFF>▶</color>" : "<color=#5B6474>○</color>";
                string text = now ? $"<b><color=#FFFFFF>{s.instruction}</color></b>" : done ? $"<color=#7D8697>{s.instruction}</color>" : s.instruction;
                GUILayout.Label($"{mark}   {text}{(s.critical ? "" : "  <color=#7D8697><i>optional</i></color>")}", _check);
            }
            GUILayout.EndArea();
            if (r.Contains(Event.current.mousePosition)) PointerOverHud = true;
        }

        private void DrawNextStepBar()
        {
            string title, line;
            switch (level.Phase)
            {
                case ModulePhase.Practice:
                case ModulePhase.Qualification:
                    var step = level.Runner?.CurrentStep;
                    if (step == null) { title = "ALL STEPS DONE"; line = level.Phase == ModulePhase.Practice ? "Qualification starts in a moment." : "Scoring…"; break; }
                    title = level.Phase == ModulePhase.Practice ? "NEXT STEP   ·   find the glowing control" : "NEXT STEP   ·   scored, no help";
                    line = step.instruction;
                    break;
                case ModulePhase.Demonstration:
                    title = "WATCH"; line = _demoStep != null ? _demoStep.instruction : "Demonstration"; break;
                case ModulePhase.Debrief:
                    var res = level.LastResult;
                    title = res == null ? "DEBRIEF" : res.passed ? "<color=#30D158>PASSED</color>" : "<color=#FF6961>NOT PASSED</color>";
                    line = "Read the debrief panel in front of you.";
                    break;
                default:
                    title = "BRIEFING"; line = "Get ready. The demonstration starts in a moment."; break;
            }
            string sub = level.Phase == ModulePhase.Debrief ? "<b>Enter</b> practise again   ·   <b>Backspace</b> start over"
                : level.Phase == ModulePhase.Briefing || level.Phase == ModulePhase.Demonstration ? "<b>Space</b> next   ·   <b>Enter</b> skip to practice"
                : null;
            bool recentFeedback = !string.IsNullOrEmpty(_feedback) && Time.unscaledTime - _feedbackAt < 3f;

            float w = Mathf.Min(920, Screen.width - 40);
            float h = 104 + (string.IsNullOrEmpty(_hint) ? 0 : 30) + (recentFeedback && string.IsNullOrEmpty(_hint) ? 22 : 0);
            var r = new Rect((Screen.width - w) / 2f, Screen.height - h - 22, w, h);
            GUILayout.BeginArea(r, _glass);
            GUILayout.Label(title, _tag);
            GUILayout.Label(line, _h2);
            if (!string.IsNullOrEmpty(_hint)) GUILayout.Label($"{_feedback}   <color=#FFB340><b>HINT</b></color>  {_hint}", _body);
            else if (recentFeedback) GUILayout.Label(_feedback, _body);
            if (sub != null) GUILayout.Label(sub, _small);
            GUILayout.EndArea();
        }

        private void DrawPhaseCard()
        {
            var (title, line) = PhaseText[_cardPhase];
            float t = _cardUntil - Time.unscaledTime;
            float a = Mathf.Clamp01(t / 0.6f) * Mathf.Clamp01((4f - t) / 0.3f);   // fade in 0.3 s, out over the last 0.6 s
            float w = Mathf.Min(700, Screen.width - 40);
            var r = new Rect((Screen.width - w) / 2f, Screen.height * 0.26f - (1f - a) * 12f, w, 140);
            var old = GUI.color; GUI.color = new Color(1, 1, 1, a);
            GUI.Box(r, GUIContent.none, _glassDark);
            GUI.Label(new Rect(r.x, r.y + 18, w, 48), title, _h1);
            GUI.Label(new Rect(r.x + 30, r.y + 70, w - 60, 54), line, _bodyC);
            GUI.color = old;
        }

        private void DrawClickToLook()
        {
            var content = new GUIContent("<b>Click</b> anywhere to look around");
            var size = _body.CalcSize(content);
            var r = new Rect((Screen.width - size.x) / 2f - 16, Screen.height * 0.5f - 16, size.x + 32, 34);
            GUI.Box(r, GUIContent.none, _pill);
            GUI.Label(new Rect(r.x + 16, r.y + 6, size.x + 4, 24), content, _body);
        }

        private static string LabelOf(string targetId)
        {
            var s = System.Text.RegularExpressions.Regex.Replace(targetId ?? "", "_(button|toggle|dial|lever|panel|socket|tool|checklist|display|switch)(_\\d+)?$", "");
            return s.Replace('_', ' ').ToUpperInvariant();
        }
    }
}
