using System;
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace Astra.DesktopTest
{
    /// <summary>
    /// Browser voice for the desktop guide (Web Speech API through AstraSpeech.jslib). WebGL only: in the Editor and
    /// on other platforms every call is a no-op and the support flags are false, so the HUD hides the voice controls.
    /// The component lives on a GameObject named <see cref="ObjectName"/> because the jslib reports with SendMessage.
    /// </summary>
    public class BrowserSpeech : MonoBehaviour
    {
        public const string ObjectName = "AstraBrowserSpeech";

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int AstraSpeech_RecognitionSupported();
        [DllImport("__Internal")] private static extern int AstraSpeech_SynthesisSupported();
        [DllImport("__Internal")] private static extern void AstraSpeech_StartListening(string goName);
        [DllImport("__Internal")] private static extern void AstraSpeech_StopListening();
        [DllImport("__Internal")] private static extern void AstraSpeech_Speak(string text);
        [DllImport("__Internal")] private static extern void AstraSpeech_CancelSpeech();
#endif

        public bool RecognitionSupported { get; private set; }
        public bool SynthesisSupported { get; private set; }
        public bool Listening { get; private set; }
        /// <summary>Raised with the recognised text (already trimmed to 300 chars).</summary>
        public event Action<string> Heard;
        /// <summary>Raised with the browser error code (not-allowed, no-speech, network, ...).</summary>
        public event Action<string> Failed;

        public static BrowserSpeech Create()
        {
            var existing = GameObject.Find(ObjectName);
            if (existing != null)
            {
                var found = existing.GetComponent<BrowserSpeech>();
                if (found != null) return found;
            }
            var go = new GameObject(ObjectName);
            DontDestroyOnLoad(go);
            return go.AddComponent<BrowserSpeech>();
        }

        private void Awake()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                RecognitionSupported = AstraSpeech_RecognitionSupported() == 1;
                SynthesisSupported = AstraSpeech_SynthesisSupported() == 1;
            }
            catch (Exception) { RecognitionSupported = SynthesisSupported = false; }
#endif
        }

        public void StartListening()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (RecognitionSupported) AstraSpeech_StartListening(gameObject.name);
#endif
        }

        public void StopListening()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (RecognitionSupported) AstraSpeech_StopListening();
#endif
        }

        public void Speak(string text)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (SynthesisSupported && !string.IsNullOrEmpty(text)) AstraSpeech_Speak(text);
#endif
        }

        public void CancelSpeech()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (SynthesisSupported) AstraSpeech_CancelSpeech();
#endif
        }

        // ---- SendMessage targets (called from the jslib) ----
        public void OnSpeechResult(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            text = text.Trim();
            if (text.Length > 300) text = text.Substring(0, 300);
            Heard?.Invoke(text);
        }

        public void OnSpeechState(string on) { Listening = on == "1"; }
        public void OnSpeechError(string error) { Listening = false; Failed?.Invoke(error); }

        private void OnDestroy() { CancelSpeech(); }
    }
}
