using Astra.XR.Phone;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Astra.App
{
    /// <summary>
    /// Browser/desktop layer for the Hub: a welcome screen, looking around with the mouse (right-drag, arrow/A-D keys
    /// or screen edges) and no stray gaze label. The phone keeps P1's gaze rig unchanged. OWNER: Integration Lead.
    /// </summary>
    public class HubDesktop : MonoBehaviour
    {
        private static bool _welcomedThisSession;
        private bool _welcome;
        private float _yaw, _pitch, _shown;
        private Transform _head;
        private GUIStyle _h1, _body, _small, _btn, _btnGhost, _tag;
        private Texture2D _panel, _primary, _ghost;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Hook() { SceneManager.sceneLoaded += (s, _) => Attach(s); Attach(SceneManager.GetActiveScene()); }

        private static void Attach(Scene s)
        {
            if (s.name != "Hub" || Application.isMobilePlatform || FindAnyObjectByType<HubDesktop>() != null) return;
            new GameObject("Hub Desktop").AddComponent<HubDesktop>();
        }

        private void Start()
        {
            foreach (var pose in FindObjectsByType<PhoneHeadPose>(FindObjectsSortMode.None)) pose.enabled = false;   // laptop sensors must not steer
            var label = GameObject.Find("Hover Label");
            if (label != null) label.SetActive(false);   // the card itself highlights on hover
            if (Camera.main != null) _head = Camera.main.transform;
            _welcome = !_welcomedThisSession;
            _shown = Time.unscaledTime;
            Cursor.lockState = CursorLockMode.None;
        }

        private void Update()
        {
            var p1Hint = GameObject.Find("Desktop Hint");   // our hint bar replaces P1's
            if (p1Hint != null) p1Hint.SetActive(false);
            // clicks on the welcome screen must not also select a card behind it
            foreach (var g in FindObjectsByType<GazeInteractor>(FindObjectsSortMode.None))
                g.enabled = !_welcome && Time.unscaledTime - _closedAt > 0.25f;
            if (_head == null || _welcome) return;
            var mouse = Mouse.current; var kb = Keyboard.current;
            float dx = 0f, dy = 0f;
            if (mouse != null && mouse.rightButton.isPressed) { var d = mouse.delta.ReadValue(); dx += d.x * 0.12f; dy += d.y * 0.12f; }
            if (kb != null)
            {
                if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) dx -= 70f * Time.unscaledDeltaTime;
                if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) dx += 70f * Time.unscaledDeltaTime;
                if (kb.upArrowKey.isPressed || kb.wKey.isPressed) dy += 50f * Time.unscaledDeltaTime;
                if (kb.downArrowKey.isPressed || kb.sKey.isPressed) dy -= 50f * Time.unscaledDeltaTime;
            }
            if (mouse != null && !mouse.rightButton.isPressed && Application.isFocused)
            {
                float x = mouse.position.ReadValue().x / Mathf.Max(1, Screen.width);
                if (x < 0.04f) dx -= 45f * Time.unscaledDeltaTime;         // gentle edge pan
                else if (x > 0.96f) dx += 45f * Time.unscaledDeltaTime;
            }
            _yaw = Mathf.Clamp(_yaw + dx, -75f, 75f);
            _pitch = Mathf.Clamp(_pitch - dy, -30f, 30f);
            _head.localRotation = Quaternion.Slerp(_head.localRotation, Quaternion.Euler(_pitch, _yaw, 0f), 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
        }

        private static Texture2D Rounded(Color fill, int r)
        {
            int n = r * 2 + 4; var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, r, n - r), cy = Mathf.Clamp(y + 0.5f, r, n - r);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                var c = fill; c.a *= Mathf.Clamp01(r - d + 0.5f); t.SetPixel(x, y, c);
            }
            t.Apply(); return t;
        }

        private void Styles()
        {
            if (_h1 != null) return;
            _panel = Rounded(new Color(0.03f, 0.05f, 0.09f, 0.93f), 22);
            _primary = Rounded(new Color(0.25f, 0.61f, 1f), 14);
            _ghost = Rounded(new Color(1f, 1f, 1f, 0.10f), 14);
            GUIStyle T(int size, Color c, FontStyle f = FontStyle.Normal, TextAnchor a = TextAnchor.UpperLeft)
            { var s = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = f, alignment = a, wordWrap = true, richText = true }; s.normal.textColor = c; return s; }
            _h1 = T(46, Color.white, FontStyle.Bold);
            _tag = T(14, new Color(0.25f, 0.61f, 1f), FontStyle.Bold);
            _body = T(18, new Color(0.86f, 0.89f, 0.95f));
            _small = T(13, new Color(0.6f, 0.65f, 0.74f), FontStyle.Normal, TextAnchor.MiddleCenter);
            GUIStyle B(Texture2D bg) { var s = new GUIStyle { fontSize = 19, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, fixedHeight = 52, border = new RectOffset(16, 16, 16, 16), margin = new RectOffset(0, 12, 8, 0) }; s.normal.background = bg; s.hover.background = bg; s.normal.textColor = s.hover.textColor = Color.white; return s; }
            _btn = B(_primary); _btnGhost = B(_ghost);
        }

        private void OnGUI()
        {
            Styles();
            float scale = Mathf.Clamp(Mathf.Min(Screen.width / 1280f, Screen.height / 720f), 0.6f, 2.5f);
            var old = GUI.matrix; GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float W = Screen.width / scale, H = Screen.height / scale;
            if (_welcome)
            {
                float a = Mathf.Clamp01((Time.unscaledTime - _shown) / 0.5f);
                var c = GUI.color; GUI.color = new Color(0, 0, 0, 0.55f * a); GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture); GUI.color = new Color(1, 1, 1, a);
                float w = 760, h = 470; var r = new Rect((W - w) / 2f, (H - h) / 2f + (1f - a) * 14f, w, h);
                GUI.DrawTexture(r, _panel);
                GUILayout.BeginArea(new Rect(r.x + 44, r.y + 36, w - 88, h - 64));
                GUILayout.Label("FICTIONAL ASTRA STATION  ·  TRAINING ACADEMY", _tag);
                GUILayout.Label("Welcome, Cadet", _h1);
                GUILayout.Space(6);
                GUILayout.Label("Train real-style spacecraft procedures in 8 short levels. Each level teaches one skill: watch it, practise it with help, then pass a scored qualification on your own.", _body);
                GUILayout.Space(10);
                GUILayout.Label("<b>Earn medals</b> (Gold, Silver, Bronze) and unlock the next level as you pass.\n<b>Ask the Flight Instructor</b> anything during practice (press T).\n<b>Look around</b> here: right-drag or arrow keys. <b>Click</b> a level card to start.", _body);
                GUILayout.FlexibleSpace();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Start with Level 0", _btn, GUILayout.Width(300))) { Close(); if (Application.CanStreamedLevelBeLoaded("L0_Safety")) SceneManager.LoadScene("L0_Safety"); }
                if (GUILayout.Button("Explore levels", _btnGhost, GUILayout.Width(220))) Close();
                GUILayout.EndHorizontal();
                GUILayout.Space(8);
                GUILayout.Label("Fictional educational training. Not affiliated with any space agency. Not a real certification.", _small);
                GUILayout.EndArea();
                GUI.color = c;
                var kb = Keyboard.current; if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)) Close();
            }
            else
            {
                var hint = "Right-drag or ← → to look around   ·   Click a level to start   ·   ?  welcome";
                var r = new Rect(W / 2f - 330, H - 46, 660, 30);
                GUI.DrawTexture(r, _ghost);
                GUI.Label(r, hint, _small);
                if (GUI.Button(new Rect(W - 54, 16, 38, 38), "?", _btnGhost)) { _welcome = true; _shown = Time.unscaledTime; }
            }
            GUI.matrix = old;
        }

        private float _closedAt = -10f;
        private void Close() { _welcome = false; _welcomedThisSession = true; _closedAt = Time.unscaledTime; }
    }
}
