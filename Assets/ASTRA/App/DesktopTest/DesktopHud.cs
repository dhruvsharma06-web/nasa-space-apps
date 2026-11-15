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
    /// Desktop/WebGL GUIDE for the browser build (IMGUI, rounded dark-glass panels): welcome page, phase cards,
    /// mission checklist, "next step" bar, glow on the next control in Practice, crosshair labels, debrief card,
    /// the Practice-only "Flight Instructor" chat (typed or spoken), shortcuts, level switch.
    /// Test tooling, not headset UI: XR and Interaction owns the in-world UI (CONTRACT section 7).
    /// OWNER: Integration Lead.
    /// </summary>
    public class DesktopHud : MonoBehaviour
    {
        [SerializeField] private LevelController level;
        [SerializeField] private DesktopRig rig;
        [Tooltip("Kept for compatibility with the scene builder; the guide is shown either way.")]
        [SerializeField] private bool compact;

        public static bool PointerOverHud { get; private set; }

        private const int MaxQuestions = 8;
        private const int MaxQuestionChars = 300;
        private const string InputName = "astraChatInput";

        private static readonly Dictionary<ModulePhase, (string title, string line)> PhaseText = new Dictionary<ModulePhase, (string, string)>
        {
            { ModulePhase.Briefing,      ("BRIEFING",      "Read the mission. Next you will see how it is done.") },
            { ModulePhase.Demonstration, ("DEMONSTRATION", "Watch: each control lights up in the order you will use it.") },
            { ModulePhase.Practice,      ("PRACTICE",      "Your turn. The glowing control is your next step. Hints appear after a mistake.") },
            { ModulePhase.Qualification, ("QUALIFICATION", "Scored. No glow, no hints. Follow the checklist on your own.") },
            { ModulePhase.Debrief,       ("DEBRIEF",       "Your result and advice.") },
        };

        private static readonly string[] StripNames = { "Briefing", "Demonstration", "Practice", "Qualification", "Debrief" };
        private static readonly string[] StripCaps = { "Read the mission goals", "Watch the controls light", "Try it with help", "Prove it, no help", "See score and advice" };
        private static readonly string[] StripIcons = { "≡", "►", "◊", "■", "●" };
        private static readonly string[] QuickQuestions = { "What do I do next?", "Why does this step matter?", "I'm stuck. Can you help?" };

        private static readonly Color Accent = new Color(0.25f, 0.61f, 1f);        // #409CFF
        private static readonly Color Green = new Color(0.19f, 0.82f, 0.35f);      // #30D158
        private static readonly Color Amber = new Color(1f, 0.70f, 0.25f);         // #FFB340
        private static readonly Color Red = new Color(1f, 0.41f, 0.38f);           // #FF6961
        private static readonly Color Muted = new Color(0.60f, 0.65f, 0.74f);

        private struct ChatMsg { public bool user; public bool ai; public bool info; public string text; }

        private readonly SortedDictionary<int, string> _levels = new SortedDictionary<int, string>();
        private readonly HashSet<string> _done = new HashSet<string>();
        private readonly List<ChatMsg> _chat = new List<ChatMsg>();
        private readonly List<Rect> _rects = new List<Rect>();
        private string _hint = "", _feedback = "";
        private float _feedbackAt;
        private MissionStep _demoStep;
        private bool _welcome = true;
        private float _welcomeAt, _hudAt, _phaseAt, _debriefAt;
        private int _asks, _attempt;
        private bool _asking, _chatOpen, _typing, _focusInput, _voiceOn;
        private string _input = "";
        private Vector2 _scroll;
        private float _checklistBottom = 140f;
        private Astra.Integrations.Coach.CoachProvider _coach;
        private BrowserSpeech _speech;
        private float _cardUntil;
        private ModulePhase _cardPhase;
        private float _a = 1f;
        private GUIStyle _glass, _glassDark, _pill, _chip, _h1, _h2, _body, _bodyC, _small, _smallC, _tag, _tagC, _btn, _btnGhost, _btnGreen, _check, _bar, _field, _chipBtn;

        public void Configure(LevelController lc, DesktopRig r, bool compactHud = false) { level = lc; rig = r; compact = compactHud; }

        private void Awake()
        {
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i));
                if (name.Length > 1 && name[0] == 'L' && char.IsDigit(name[1])) _levels[name[1] - '0'] = name;   // L3_Cabin -> 3
            }
            _welcomeAt = Time.unscaledTime;
        }

        private void OnEnable()
        {
            if (level == null) { Debug.LogError("[DesktopHud] LevelController is not assigned.", this); enabled = false; return; }
            level.PhaseChanged += OnPhase;
            level.EventRecorded += OnEvent;
            level.DemonstrationStep += OnDemo;
            if (_speech == null) _speech = BrowserSpeech.Create();
            _speech.Heard += OnHeard;
        }

        private void OnDisable()
        {
            if (_speech != null) _speech.Heard -= OnHeard;
            if (level == null) return;
            level.PhaseChanged -= OnPhase;
            level.EventRecorded -= OnEvent;
            level.DemonstrationStep -= OnDemo;
        }

        private void Start() { if (_welcome && level != null) level.Pause(); }   // the level waits on the welcome screen

        private void OnDemo(MissionStep s) => _demoStep = s;

        private void OnHeard(string text)
        {
            if (!_chatOpen || level == null || level.Phase != ModulePhase.Practice) return;
            _input = text.Length > MaxQuestionChars ? text.Substring(0, MaxQuestionChars) : text;
            _focusInput = true;
        }

        private void OnPhase(ModulePhase p)
        {
            _feedback = ""; _hint = ""; _demoStep = null; _asks = 0; _attempt++; _asking = false;
            _chat.Clear(); _input = "";
            if (_chatOpen) CloseChat();                     // closes when Qualification starts (and on any phase change)
            _cardPhase = p; _cardUntil = Time.unscaledTime + 4f; _phaseAt = Time.unscaledTime;
            if (p == ModulePhase.Debrief) _debriefAt = Time.unscaledTime;
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
            _hudAt = Time.unscaledTime;
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
            if (_typing) return;                                                // typing in the chat: no shortcuts
            bool scored = level.Phase == ModulePhase.Qualification;   // no level switching during a scored attempt
            var digits = new[] { kb.digit0Key, kb.digit1Key, kb.digit2Key, kb.digit3Key, kb.digit4Key, kb.digit5Key, kb.digit6Key, kb.digit7Key };
            for (int n = 0; n < digits.Length; n++)
                if (!scored && digits[n].wasPressedThisFrame && _levels.TryGetValue(n, out var scene) && scene != SceneManager.GetActiveScene().name)
                { SceneManager.LoadScene(scene); return; }
            if (!scored && kb.nKey.wasPressedThisFrame) { level.NextLevel(); return; }
            if (!scored && kb.hKey.wasPressedThisFrame) { level.BackToHub(); return; }
            if (_welcome) { if (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) StartMission(); return; }
            if (kb.tKey.wasPressedThisFrame && level.Phase == ModulePhase.Practice) { if (_chatOpen) CloseChat(); else OpenChat(); return; }
            if (kb.spaceKey.wasPressedThisFrame) level.Skip();
            if (kb.enterKey.wasPressedThisFrame) { if (level.Phase == ModulePhase.Debrief) level.Retry(); else level.SkipToPractice(); }
            if (kb.fKey.wasPressedThisFrame) level.Finish();
            if (kb.qKey.wasPressedThisFrame && level.Phase == ModulePhase.Practice) { OpenChat(); Ask(QuickQuestions[0]); }
            if (kb.lKey.wasPressedThisFrame && level.Phase == ModulePhase.Debrief) { _feedback = Astra.App.SessionLogDownload.Save(level); _feedbackAt = Time.unscaledTime; }
            if (kb.pKey.wasPressedThisFrame) { if (level.IsPaused) level.Resume(); else level.Pause(); }
            if (kb.rKey.wasPressedThisFrame) { level.Recenter(); if (rig != null) rig.Recenter(); }
            if (kb.backspaceKey.wasPressedThisFrame) level.Restart();
        }

        // ------------------------------------------------------------------ flight instructor (Practice only)

        private void OpenChat()
        {
            if (level == null || level.Phase != ModulePhase.Practice) return;
            _chatOpen = true; _focusInput = true;
            if (_chat.Count == 0) _chat.Add(new ChatMsg { info = true, text = "Hi, I'm your flight instructor. Ask me about this step, or tap a question below." });
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;   // free the mouse so the text field can be used
        }

        private void CloseChat()
        {
            _chatOpen = false; _typing = false;
            GUIUtility.keyboardControl = 0;
            if (_speech != null) { _speech.CancelSpeech(); _speech.StopListening(); }
        }

        private static string Safe(string s) => (s ?? "").Replace('<', '‹').Replace('>', '›');

        private void PushMsg(ChatMsg m) { _chat.Add(m); _scroll.y = 100000f; }

        /// <summary>Sends a question through the coach (AI when available, offline otherwise). Max 8 per attempt, Practice only.</summary>
        private void Ask(string question)
        {
            if (level == null || level.Phase != ModulePhase.Practice || level.Runner == null || _asking) return;
            question = (question ?? "").Trim();
            if (question.Length == 0) return;
            if (question.Length > MaxQuestionChars) question = question.Substring(0, MaxQuestionChars);
            if (_asks >= MaxQuestions)
            {
                PushMsg(new ChatMsg { info = true, text = "You have used all " + MaxQuestions + " questions for this attempt. Use the checklist and the glowing control." });
                return;
            }
            if (_coach == null) _coach = FindAnyObjectByType<Astra.Integrations.Coach.CoachProvider>();
            if (_coach == null || _coach.AskClient == null)
            {
                PushMsg(new ChatMsg { info = true, text = "The instructor is not available right now. Look at the glowing control and read its label." });
                return;
            }
            _asks++; _asking = true;
            PushMsg(new ChatMsg { user = true, text = question });
            int attempt = _attempt;
            var req = new Astra.Integrations.Coach.CoachAskRequest
            {
                sessionId = level.Runner.SessionId, missionId = level.Mission.missionId, phase = "Practice",
                currentStepId = level.Runner.CurrentStep != null ? level.Runner.CurrentStep.stepId : null,
                question = question,
            };
            _coach.AskClient.AskCoach(req, level.Mission, r =>
            {
                if (attempt != _attempt) return;                    // phase changed meanwhile: drop the answer
                _asking = false;
                string answer = r != null && !string.IsNullOrEmpty(r.answer) ? r.answer : "Look at the glowing control and read its label.";
                PushMsg(new ChatMsg { ai = _coach != null && _coach.IsAi, text = answer });
                if (_voiceOn && _speech != null) _speech.Speak(answer);
            });
        }

        private void SendInput()
        {
            string q = (_input ?? "").Trim();
            if (q.Length == 0 || _asking) return;
            _input = "";
            Ask(q);
            _focusInput = true;
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

        private static GUIStyle Button(Color fill, Color hover, Color active, Color edge, int size)
        {
            var b = new GUIStyle { fontSize = size, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, border = new RectOffset(16, 16, 16, 16), richText = true, clipping = TextClipping.Clip };
            b.normal.background = Rounded(fill, edge, 14);
            b.hover.background = Rounded(hover, edge, 14);
            b.active.background = Rounded(active, edge, 14);
            b.normal.textColor = b.hover.textColor = b.active.textColor = Color.white;
            return b;
        }

        private void Styles()
        {
            if (_h1 != null) return;
            var hair = new Color(1f, 1f, 1f, 0.14f);
            _glass = Box(Rounded(new Color(0.05f, 0.07f, 0.11f, 0.78f), hair, 14), 14, new RectOffset(20, 20, 16, 16));
            _glassDark = Box(Rounded(new Color(0.02f, 0.03f, 0.06f, 0.92f), hair, 20), 20, new RectOffset(36, 36, 30, 30));
            _pill = Box(Rounded(new Color(0.02f, 0.03f, 0.06f, 0.85f), hair, 13), 13, new RectOffset(14, 14, 5, 5));
            _chip = Box(Rounded(new Color(1f, 1f, 1f, 0.09f), new Color(1f, 1f, 1f, 0.18f), 11), 11, new RectOffset(0, 0, 0, 0));
            _bar = Box(Rounded(Color.white, new Color(1, 1, 1, 0), 3), 3, new RectOffset(0, 0, 0, 0));

            _h1 = Text(36, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            _h2 = Text(23, Color.white, FontStyle.Bold);
            _body = Text(16, new Color(0.86f, 0.89f, 0.95f));
            _bodyC = Text(17, new Color(0.86f, 0.89f, 0.95f), FontStyle.Normal, TextAnchor.MiddleCenter);
            _small = Text(13, Muted);
            _smallC = Text(13, Muted, FontStyle.Normal, TextAnchor.MiddleCenter);
            _tag = Text(12, Accent, FontStyle.Bold);
            _tagC = Text(12, Accent, FontStyle.Bold, TextAnchor.MiddleCenter);
            _check = Text(15, new Color(0.86f, 0.89f, 0.95f));

            _btn = Button(Accent, new Color(0.38f, 0.70f, 1f), new Color(0.18f, 0.48f, 0.85f), new Color(1, 1, 1, 0.25f), 18);
            _btnGreen = Button(new Color(0.15f, 0.64f, 0.28f), Green, new Color(0.12f, 0.5f, 0.22f), new Color(1, 1, 1, 0.25f), 18);
            _btnGhost = Button(new Color(1, 1, 1, 0.09f), new Color(1, 1, 1, 0.18f), new Color(1, 1, 1, 0.05f), new Color(1, 1, 1, 0.2f), 17);
            _chipBtn = Button(new Color(0.25f, 0.61f, 1f, 0.14f), new Color(0.25f, 0.61f, 1f, 0.30f), new Color(0.25f, 0.61f, 1f, 0.10f), new Color(0.25f, 0.61f, 1f, 0.45f), 12);
            _chipBtn.fontStyle = FontStyle.Normal;

            _field = new GUIStyle(GUI.skin.textField) { fontSize = 14, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip, padding = new RectOffset(12, 12, 6, 6) };
            var fieldBg = Rounded(new Color(1, 1, 1, 0.08f), new Color(1, 1, 1, 0.2f), 11);
            _field.normal.background = fieldBg; _field.focused.background = Rounded(new Color(1, 1, 1, 0.12f), new Color(Accent.r, Accent.g, Accent.b, 0.8f), 11); _field.hover.background = fieldBg;
            _field.border = new RectOffset(13, 13, 13, 13);
            _field.normal.textColor = _field.focused.textColor = _field.hover.textColor = Color.white;
        }

        // ------------------------------------------------------------------ drawing

        private void OnGUI()
        {
            if (level == null || level.Mission == null) return;
            Styles();
            PointerOverHud = false;
            float s = Mathf.Clamp(Mathf.Min(Screen.width / 1280f, Screen.height / 720f), 0.6f, 2.5f);   // scale up on bigger screens
            var oldM = GUI.matrix; var oldC = GUI.color;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(s, s, 1f));
            float W = Screen.width / s, H = Screen.height / s;

            if (_welcome) DrawWelcome(W, H);
            else
            {
                _a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.unscaledTime - _hudAt) / 0.3f));
                DrawCrosshair(W, H);
                GUI.color = new Color(1, 1, 1, _a);
                DrawTopBar(W);
                if (level.Phase != ModulePhase.Debrief) DrawChecklist(W);
                if (level.Phase == ModulePhase.Debrief) DrawDebriefCard(W, H);
                else DrawNextStepBar(W, H);
                if (level.Phase == ModulePhase.Practice && _chatOpen) DrawChat(W, H);
                GUI.color = new Color(1, 1, 1, 1);
                if (Time.unscaledTime < _cardUntil && level.Phase != ModulePhase.Debrief) DrawPhaseCard(W, H);
                if (rig != null && !rig.Locked && !_chatOpen && level.Phase != ModulePhase.Debrief) DrawClickToLook(W, H);
            }
            GUI.color = oldC; GUI.matrix = oldM;
            if (!_chatOpen && _typing) _typing = false;
        }

        private static void Fill(Rect r, Color c)
        {
            var old = GUI.color; GUI.color = new Color(c.r, c.g, c.b, c.a * old.a);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        private void Tinted(Rect r, Color c, GUIStyle st)
        {
            var old = GUI.color; GUI.color = new Color(c.r, c.g, c.b, c.a * old.a);
            GUI.Box(r, GUIContent.none, st);
            GUI.color = old;
        }

        /// <summary>Flows labels into rows (left to right, wrapping). Fills <see cref="_rects"/>; returns the total height.</summary>
        private float Flow(float maxW, string[] labels, GUIStyle st, float padX, float h, float gap, bool center)
        {
            _rects.Clear();
            var widths = new float[labels.Length];
            for (int i = 0; i < labels.Length; i++) widths[i] = Mathf.Min(maxW, st.CalcSize(new GUIContent(labels[i])).x + padX * 2f);
            float y = 0f, x = 0f; int rowStart = 0;
            for (int i = 0; i <= labels.Length; i++)
            {
                bool end = i == labels.Length;
                if (!end && (x == 0f || x + gap + widths[i] <= maxW)) { x += (x == 0f ? 0f : gap) + widths[i]; continue; }
                float sx = center ? (maxW - x) / 2f : 0f;
                for (int j = rowStart; j < i; j++) { _rects.Add(new Rect(sx, y, widths[j], h)); sx += widths[j] + gap; }
                if (end) { y += h; break; }
                y += h + gap; rowStart = i; x = widths[i];
            }
            return y;
        }

        private void DrawWelcome(float W, float H)
        {
            var m = level.Mission;
            float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.unscaledTime - _welcomeAt) / 0.5f));   // smooth fade-in
            GUI.color = new Color(0, 0, 0, 0.6f * a);
            GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture);
            GUI.color = new Color(1, 1, 1, a);

            float w = Mathf.Min(900, W - 40), h = Mathf.Min(640, H - 40);
            var panel = new Rect((W - w) / 2f, (H - h) / 2f + (1f - a) * 14f, w, h);
            GUI.Box(panel, GUIContent.none, _glassDark);
            GUI.BeginGroup(panel);
            float iw = w - 72f, x0 = 36f, y = 30f;

            GUI.Label(new Rect(x0, y, iw, 18), $"TRAINING SIMULATOR   ·   LEVEL {(level.LevelNumber >= 0 ? level.LevelNumber : m.level)}", _tagC);
            GUI.Label(new Rect(x0, y + 20, iw, 60), "ASTRA ACADEMY", Text(46, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter));
            Fill(new Rect(w / 2f - 24, y + 84, 48, 3), Accent);
            GUI.Label(new Rect(x0, y + 98, iw, 34), m.title, Text(24, new Color(0.92f, 0.95f, 1f), FontStyle.Bold, TextAnchor.MiddleCenter));
            int n = m.steps != null ? m.steps.Count : 0;
            GUI.Label(new Rect(x0 + 30, y + 136, iw - 60, 48),
                $"Your goal: complete the {n}-step procedure correctly and in order. You get help while you learn, and none during the scored run.", _bodyC);

            // 5-step phase strip
            float sy = y + 202f, cw = iw / 5f;
            Fill(new Rect(x0 + cw / 2f, sy + 23f, iw - cw, 2f), new Color(1, 1, 1, 0.14f));
            for (int i = 0; i < 5; i++)
            {
                float cx = x0 + cw * i + cw / 2f;
                var circ = new Rect(cx - 24, sy, 48, 48);
                Tinted(circ, i == 2 ? Accent : new Color(0.16f, 0.2f, 0.28f, 1f), _chip);
                GUI.Label(circ, StripIcons[i], Text(20, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter));
                GUI.Label(new Rect(cx - cw / 2f, sy + 56, cw, 20), $"{i + 1}  {StripNames[i]}", Text(14, Color.white, FontStyle.Bold, TextAnchor.UpperCenter));
                GUI.Label(new Rect(cx - cw / 2f + 6, sy + 76, cw - 12, 36), StripCaps[i], Text(12, Muted, FontStyle.Normal, TextAnchor.UpperCenter));
            }

            // controls as key chips
            float cy = sy + 134f;
            GUI.Label(new Rect(x0, cy, iw, 16), "CONTROLS", _tagC);
            var chips = new[]
            {
                "<b>Click</b>  <color=#9AA3B2>look and use</color>", "<b>Esc</b>  <color=#9AA3B2>free the mouse</color>",
                "<b>Enter</b>  <color=#9AA3B2>skip to Practice</color>", "<b>P</b>  <color=#9AA3B2>pause</color>",
                "<b>T</b>  <color=#9AA3B2>ask instructor</color>", "<b>N</b>  <color=#9AA3B2>next level</color>", "<b>0–7</b>  <color=#9AA3B2>pick level</color>",
            };
            var chipText = Text(13, Color.white, FontStyle.Normal, TextAnchor.MiddleCenter);
            chipText.wordWrap = false;
            float fh = Flow(iw, chips, chipText, 14, 30, 8, true);
            for (int i = 0; i < chips.Length; i++)
            {
                var r = new Rect(x0 + _rects[i].x, cy + 24 + _rects[i].y, _rects[i].width, 30);
                GUI.Box(r, GUIContent.none, _chip);
                GUI.Label(r, chips[i], chipText);
            }

            float by = Mathf.Max(cy + 24 + fh + 22f, h - 138f);
            if (GUI.Button(new Rect(w / 2f - 170, by, 340, 54), "Start mission   ⏎", _btn)) StartMission();
            GUI.Label(new Rect(x0, by + 62, iw, 20), "Talk to your instructor anytime in Practice (press <b>T</b>).", _smallC);
            GUI.Label(new Rect(x0, by + 82, iw, 18), "Fictional educational training. Not real procedures or certification.", Text(12, new Color(0.5f, 0.55f, 0.63f), FontStyle.Normal, TextAnchor.MiddleCenter));
            GUI.EndGroup();
            GUI.color = new Color(1, 1, 1, 1);
            PointerOverHud = true;
        }

        private void DrawCrosshair(float W, float H)
        {
            if (rig == null || !rig.Locked) return;
            var c = new Vector2(W / 2f, H / 2f);
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

        private int CurrentIndex(List<MissionStep> steps)
        {
            string current = level.Phase == ModulePhase.Demonstration ? _demoStep?.stepId : level.Runner?.CurrentStep?.stepId;
            if (current == null) return -1;
            return steps.FindIndex(s => s.stepId == current);
        }

        private void DrawTopBar(float W)
        {
            var m = level.Mission;
            var steps = new List<MissionStep>(m.steps); steps.Sort((a, b) => a.order.CompareTo(b.order));
            int lv = level.LevelNumber >= 0 ? level.LevelNumber : m.level;
            var r = new Rect(20, 20, Mathf.Min(520, W - 420), 78);
            GUI.Box(r, GUIContent.none, _glass);
            GUI.BeginGroup(r);
            GUI.Label(new Rect(20, 12, 100, 16), $"LEVEL {lv}", _tag);
            GUI.Label(new Rect(20, 28, r.width - 200, 26), m.title, Text(18, Color.white, FontStyle.Bold));
            bool qual = level.Phase == ModulePhase.Qualification;
            string ph = PhaseText[level.Phase].title + (level.IsPaused ? "  ·  PAUSED" : "");
            var pill = new Rect(r.width - 20 - 160, 14, 160, 24);
            Tinted(pill, new Color(1, 1, 1, 0.08f), _bar);
            GUI.Label(pill, ph, Text(11, qual ? Amber : Accent, FontStyle.Bold, TextAnchor.MiddleCenter));
            int cur = CurrentIndex(steps);
            bool scored = level.Phase == ModulePhase.Practice || level.Phase == ModulePhase.Qualification;
            float dx = 20;
            for (int i = 0; i < steps.Count && dx + 12 < r.width - 20; i++)
            {
                bool done = scored && _done.Contains(steps[i].stepId);
                Color c = done ? Green : i == cur ? Accent : new Color(1, 1, 1, 0.22f);
                Tinted(new Rect(dx, 60, 10, 10), c, _bar);
                dx += 15;
            }
            GUI.EndGroup();
            if (r.Contains(Event.current.mousePosition)) PointerOverHud = true;

            // quiet shortcut hint under the bar
            var keys = new StringBuilder("Esc mouse  ·  Enter practice/retry  ·  N next  ·  H hub  ·  P pause");
            if (_levels.Count > 1)
            {
                keys.Append("   ·   Levels: ");
                foreach (var kv in _levels) keys.Append(kv.Value == SceneManager.GetActiveScene().name ? $"<color=#409CFF><b>{kv.Key}</b></color> " : $"{kv.Key} ");
            }
            GUI.Label(new Rect(26, r.yMax + 4, Mathf.Min(640, W - 420), 20), keys.ToString(), Text(11, new Color(0.7f, 0.75f, 0.84f, 0.8f)));
        }

        private void DrawChecklist(float W)
        {
            if (level.Phase == ModulePhase.Briefing) return;
            var steps = new List<MissionStep>(level.Mission.steps);
            steps.Sort((a, b) => a.order.CompareTo(b.order));
            string current = level.Phase == ModulePhase.Demonstration ? _demoStep?.stepId : level.Runner?.CurrentStep?.stepId;
            float w = 370, inner = w - 40;
            Rect r;
            if (level.Phase == ModulePhase.Qualification)
            {
                // progress only: no step text and no help after Practice
                r = new Rect(W - w - 20, 20, w, 108);
                GUILayout.BeginArea(r, _glass);
                int stepNo = Mathf.Min(_done.Count + 1, Mathf.Max(1, steps.Count));
                GUILayout.Label($"STEP {stepNo} OF {steps.Count}", Text(20, Color.white, FontStyle.Bold));
                var track = GUILayoutUtility.GetRect(inner, 10);
                Tinted(track, new Color(1, 1, 1, 0.14f), _bar);
                float frac = steps.Count == 0 ? 0 : Mathf.Clamp01(_done.Count / (float)steps.Count);
                if (frac > 0) Tinted(new Rect(track.x, track.y, Mathf.Max(10, track.width * frac), 10), Accent, _bar);
                GUILayout.Space(8);
                GUILayout.Label("Scored run: no step text, no hints.", _small);
                GUILayout.EndArea();
            }
            else
            {
                float h = 62;
                foreach (var s in steps) h += _check.CalcHeight(new GUIContent("✓   " + s.instruction + (s.critical ? "" : "  optional")), inner) + 2;
                r = new Rect(W - w - 20, 20, w, h);
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
            }
            _checklistBottom = r.yMax;
            if (r.Contains(Event.current.mousePosition)) PointerOverHud = true;
        }

        private void DrawNextStepBar(float W, float H)
        {
            string title, line;
            bool practice = level.Phase == ModulePhase.Practice;
            switch (level.Phase)
            {
                case ModulePhase.Practice:
                case ModulePhase.Qualification:
                    var step = level.Runner?.CurrentStep;
                    if (!practice) { title = "QUALIFICATION   ·   scored"; line = step == null ? "Scoring…" : "Complete the procedure on your own."; break; }
                    if (step == null) { title = "ALL STEPS DONE"; line = "Qualification starts in a moment."; break; }
                    title = "NEXT STEP   ·   find the glowing control";
                    line = step.instruction;
                    break;
                case ModulePhase.Demonstration:
                    title = "WATCH"; line = _demoStep != null ? _demoStep.instruction : "Demonstration"; break;
                default:
                    title = "BRIEFING"; line = "Get ready. The demonstration starts in a moment."; break;
            }
            string sub = level.Phase == ModulePhase.Briefing || level.Phase == ModulePhase.Demonstration ? "<b>Space</b> next   ·   <b>Enter</b> skip to practice"
                : practice ? $"<b>T</b> talk to your instructor  ({MaxQuestions - _asks} questions left)"
                : null;
            bool recentFeedback = practice && !string.IsNullOrEmpty(_feedback) && Time.unscaledTime - _feedbackAt < 3f;
            bool hint = practice && !string.IsNullOrEmpty(_hint);

            bool left = practice && _chatOpen;
            float w = left ? Mathf.Max(420f, W - 40f - 380f - 16f) : Mathf.Min(920f, W - 40f);
            float h = 104 + (hint ? 30 : 0) + (recentFeedback && !hint ? 22 : 0);
            var r = new Rect(left ? 20f : (W - w) / 2f, H - h - 22, w, h);
            GUILayout.BeginArea(r, _glass);
            GUILayout.Label(title, _tag);
            GUILayout.Label(line, _h2);
            if (hint) GUILayout.Label($"{_feedback}   <color=#FFB340><b>HINT</b></color>  {_hint}", _body);
            else if (recentFeedback) GUILayout.Label(_feedback, _body);
            if (sub != null) GUILayout.Label(sub, _small);
            GUILayout.EndArea();
            if (r.Contains(Event.current.mousePosition)) PointerOverHud = true;
            if (practice && !_chatOpen)
            {
                var b = new Rect(r.xMax - 196, r.yMax - 46, 176, 30);
                if (GUI.Button(b, "Talk to instructor", _chipBtn)) OpenChat();
            }
        }

        // ------------------------------------------------------------------ chat panel

        private void DrawChat(float W, float H)
        {
            float w = 380f, inner = w - 40f;
            float top = Mathf.Max(_checklistBottom + 12f, 130f);
            float h = Mathf.Max(260f, H - 22f - top);
            top = H - 22f - h;
            var r = new Rect(W - w - 20, top, w, h);

            // Enter sends, Esc leaves the field (handled before the text field consumes the key)
            var ev = Event.current;
            if (ev.type == EventType.KeyDown && GUI.GetNameOfFocusedControl() == InputName)
            {
                if (ev.keyCode == KeyCode.Return || ev.keyCode == KeyCode.KeypadEnter) { SendInput(); ev.Use(); }
                else if (ev.keyCode == KeyCode.Escape) { GUI.FocusControl(null); GUIUtility.keyboardControl = 0; ev.Use(); }
            }

            GUILayout.BeginArea(r, _glass);
            GUILayout.BeginHorizontal();
            GUILayout.Label("FLIGHT INSTRUCTOR   ·   PRACTICE", _tag);
            GUILayout.FlexibleSpace();
            if (_speech != null && _speech.SynthesisSupported)
            {
                if (GUILayout.Button(_voiceOn ? "Voice on" : "Voice off", _chipBtn, GUILayout.Width(74), GUILayout.Height(24)))
                { _voiceOn = !_voiceOn; if (!_voiceOn) _speech.CancelSpeech(); }
                GUILayout.Space(4);
            }
            bool close = GUILayout.Button("✕", _chipBtn, GUILayout.Width(28), GUILayout.Height(24));
            GUILayout.EndHorizontal();
            GUILayout.Space(6);

            _scroll = GUILayout.BeginScrollView(_scroll, false, false, GUILayout.ExpandHeight(true));
            foreach (var m in _chat)
            {
                string head = m.user ? "<color=#9AA3B2><b>You</b></color>"
                    : m.info ? "<color=#9AA3B2><b>Instructor</b></color>"
                    : "<color=#409CFF><b>Instructor</b></color>   " + (m.ai ? "<color=#30D158><b>AI</b></color>" : "<color=#FFB340><b>Offline</b></color>");
                GUILayout.Label(head, Text(12, Color.white));
                GUILayout.Label(Safe(m.text), Text(14, m.user ? Color.white : new Color(0.86f, 0.89f, 0.95f)));
                GUILayout.Space(8);
            }
            if (_asking) GUILayout.Label("<color=#9AA3B2>Instructor is typing…</color>", Text(13, Muted));
            GUILayout.EndScrollView();

            // quick-question chips
            float fh = Flow(inner, QuickQuestions, _chipBtn, 12, 26, 6, false);
            var area = GUILayoutUtility.GetRect(inner, fh);
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && !_asking && _asks < MaxQuestions;
            for (int i = 0; i < QuickQuestions.Length && i < _rects.Count; i++)
                if (GUI.Button(new Rect(area.x + _rects[i].x, area.y + _rects[i].y, _rects[i].width, 26), QuickQuestions[i], _chipBtn)) Ask(QuickQuestions[i]);
            GUI.enabled = enabled;
            GUILayout.Space(6);

            GUILayout.BeginHorizontal();
            GUI.SetNextControlName(InputName);
            _input = GUILayout.TextField(_input ?? "", MaxQuestionChars, _field, GUILayout.Height(34));
            if (_speech != null && _speech.RecognitionSupported)
            {
                bool lis = _speech.Listening;
                if (GUILayout.Button(lis ? "<color=#FF6961>● Listening</color>" : "Mic", _btnGhost, GUILayout.Width(lis ? 100 : 50), GUILayout.Height(34)))
                { if (lis) _speech.StopListening(); else _speech.StartListening(); }
            }
            GUI.enabled = enabled && !_asking && !string.IsNullOrWhiteSpace(_input) && _asks < MaxQuestions;
            if (GUILayout.Button("Send", _btn, GUILayout.Width(64), GUILayout.Height(34))) SendInput();
            GUI.enabled = enabled;
            GUILayout.EndHorizontal();
            GUILayout.Label($"{MaxQuestions - _asks} of {MaxQuestions} questions left this attempt   ·   Enter sends   ·   Esc leaves the box", Text(11, Muted));
            GUILayout.EndArea();

            _typing = GUI.GetNameOfFocusedControl() == InputName;
            if (_focusInput && ev.type == EventType.Repaint) { GUI.FocusControl(InputName); _focusInput = false; }
            if (r.Contains(ev.mousePosition)) PointerOverHud = true;
            if (close) CloseChat();
        }

        // ------------------------------------------------------------------ debrief

        private void DrawDebriefCard(float W, float H)
        {
            var res = level.LastResult;
            var rep = level.LastReport;
            bool passed = res != null && res.passed;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.unscaledTime - _phaseAt) / 0.28f));
            var old = GUI.color; GUI.color = new Color(1, 1, 1, t * _a);
            int count = rep != null ? rep.Lines.Count : 0;
            float w = Mathf.Min(620, W - 40);
            const float fixedH = 372f;
            float h = Mathf.Min(H - 40f, fixedH + Mathf.Min(count * 26f + 8f, 190f));
            var r = new Rect((W - w) / 2f, (H - h) / 2f + (1f - t) * 14f, w, h);
            GUI.Box(r, GUIContent.none, _glassDark);
            GUI.BeginGroup(r);
            float iw = w - 72f, x0 = 36f, y = 28f;

            GUI.Label(new Rect(x0, y, iw, 18), res == null ? "DEBRIEF" : passed ? "PASSED" : "NOT PASSED", Text(13, passed ? Green : res == null ? Accent : Red, FontStyle.Bold, TextAnchor.MiddleCenter));
            y += 22;
            if (rep != null)
            {
                float k = Mathf.Clamp01((Time.unscaledTime - _debriefAt - 0.25f) / 1.2f);
                k = 1f - (1f - k) * (1f - k) * (1f - k);                       // ease-out count-up
                int shown = Mathf.RoundToInt(rep.Score * k);
                GUI.Label(new Rect(x0, y, iw, 96), $"<b>{shown}</b>", Text(84, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter));
                GUI.Label(new Rect(x0, y + 92, iw, 20), "out of 100", _smallC);
                y += 120;
                string medal = rep.Medal == Astra.UI.Progress.Medal.None ? "No medal" : rep.Medal + " medal";
                Color mc = rep.Medal == Astra.UI.Progress.Medal.Gold ? new Color(1f, 0.84f, 0.04f)
                    : rep.Medal == Astra.UI.Progress.Medal.Silver ? new Color(0.78f, 0.78f, 0.8f)
                    : rep.Medal == Astra.UI.Progress.Medal.Bronze ? new Color(0.84f, 0.52f, 0.25f) : Muted;
                bool best = level.LastWasBest;
                float pw = 160f, gap = 10f, total = pw + (best ? gap + 110f : 0f), px = (w - total) / 2f;
                var mr = new Rect(px, y, pw, 28);
                Tinted(mr, new Color(mc.r, mc.g, mc.b, 0.6f), _chip);
                GUI.Label(mr, medal, Text(13, mc, FontStyle.Bold, TextAnchor.MiddleCenter));
                if (best)
                {
                    var br = new Rect(px + pw + gap, y, 110, 28);
                    Tinted(br, Amber, _chip);
                    GUI.Label(br, "NEW BEST", Text(12, Amber, FontStyle.Bold, TextAnchor.MiddleCenter));
                }
                y += 42;
                float linesH = Mathf.Max(0f, h - fixedH);
                if (count > 0 && linesH > 0f)
                {
                    GUILayout.BeginArea(new Rect(x0, y, iw, linesH + 4));
                    foreach (var l in rep.Lines)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Label(Safe(l.Text), Text(14, new Color(0.86f, 0.89f, 0.95f)));
                        if (l.Points != 0)
                            GUILayout.Label($"<color=#{(l.Points > 0 ? "30D158" : "FF6961")}><b>{(l.Points > 0 ? "+" : "")}{l.Points}</b></color>", Text(14, Color.white, FontStyle.Bold, TextAnchor.UpperRight), GUILayout.Width(56));
                        GUILayout.EndHorizontal();
                    }
                    GUILayout.EndArea();
                }
            }

            // buttons
            float by = h - 112f, bw = (iw - 20f) / 3f;
            if (GUI.Button(new Rect(x0, by, bw, 50), "Retry", passed ? _btnGhost : _btn)) level.Retry();
            if (GUI.Button(new Rect(x0 + bw + 10, by, bw, 50), "Next level", passed ? _btnGreen : _btnGhost)) level.NextLevel();
            if (GUI.Button(new Rect(x0 + (bw + 10) * 2, by, bw, 50), "Hub", _btnGhost)) level.BackToHub();
            GUI.Label(new Rect(x0, by + 58, iw, 18), "<b>Enter</b> retry   ·   <b>N</b> next level   ·   <b>H</b> hub   ·   <b>L</b> save session log", _smallC);
            if (!string.IsNullOrEmpty(_feedback) && Time.unscaledTime - _feedbackAt < 4f)
                GUI.Label(new Rect(x0, by - 22, iw, 18), Safe(_feedback), _smallC);
            GUI.EndGroup();
            GUI.color = old;
            if (r.Contains(Event.current.mousePosition)) PointerOverHud = true;
        }

        private void DrawPhaseCard(float W, float H)
        {
            var (title, line) = PhaseText[_cardPhase];
            float t = _cardUntil - Time.unscaledTime;
            float a = Mathf.Clamp01(t / 0.6f) * Mathf.Clamp01((4f - t) / 0.3f);   // fade in 0.3 s, out over the last 0.6 s
            float w = Mathf.Min(700, W - 40);
            var r = new Rect((W - w) / 2f, H * 0.26f - (1f - a) * 12f, w, 140);
            var old = GUI.color; GUI.color = new Color(1, 1, 1, a);
            GUI.Box(r, GUIContent.none, _glassDark);
            GUI.Label(new Rect(r.x, r.y + 18, w, 48), title, _h1);
            GUI.Label(new Rect(r.x + 30, r.y + 70, w - 60, 54), line, _bodyC);
            GUI.color = old;
        }

        private void DrawClickToLook(float W, float H)
        {
            var content = new GUIContent("<b>Click</b> anywhere to look around");
            var size = _body.CalcSize(content);
            var r = new Rect((W - size.x) / 2f - 16, H * 0.5f - 16, size.x + 32, 34);
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
