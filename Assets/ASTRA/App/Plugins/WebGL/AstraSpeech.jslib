mergeInto(LibraryManager.library, {
  $AstraSend: function (go, method, arg) {
    var send = (typeof SendMessage === "function") ? SendMessage : (Module && Module.SendMessage);
    if (send) send(go, method, arg);
  },
  // Browser Web Speech API bridge for the desktop guide. Everything is feature-detected and fails silently.
  AstraSpeech_RecognitionSupported: function () {
    return (typeof window !== "undefined" && (window.SpeechRecognition || window.webkitSpeechRecognition)) ? 1 : 0;
  },
  AstraSpeech_SynthesisSupported: function () {
    return (typeof window !== "undefined" && window.speechSynthesis && window.SpeechSynthesisUtterance) ? 1 : 0;
  },
  // Listens once; reports to SendMessage(goName, "OnSpeechResult" | "OnSpeechState" | "OnSpeechError", string).
  AstraSpeech_StartListening: function (goNamePtr) {
    var go = UTF8ToString(goNamePtr);
    var SR = window.SpeechRecognition || window.webkitSpeechRecognition;
    if (!SR) { AstraSend(go, "OnSpeechError", "unsupported"); return; }
    try {
      if (window.__astraRec) { try { window.__astraRec.abort(); } catch (e) {} }
      var rec = new SR();
      rec.lang = "en-US"; rec.interimResults = false; rec.maxAlternatives = 1; rec.continuous = false;
      rec.onstart = function () { AstraSend(go, "OnSpeechState", "1"); };
      rec.onend = function () { AstraSend(go, "OnSpeechState", "0"); };
      rec.onerror = function (e) { AstraSend(go, "OnSpeechError", (e && e.error) ? String(e.error) : "error"); };
      rec.onresult = function (e) {
        try {
          var t = e.results[0][0].transcript;
          AstraSend(go, "OnSpeechResult", String(t).substring(0, 300));
        } catch (err) { AstraSend(go, "OnSpeechError", "result"); }
      };
      window.__astraRec = rec;
      rec.start();
    } catch (e) { AstraSend(go, "OnSpeechError", "start"); }
  },
  AstraSpeech_StopListening: function () {
    try { if (window.__astraRec) window.__astraRec.stop(); } catch (e) {}
  },
  AstraSpeech_Speak: function (textPtr) {
    try {
      if (!window.speechSynthesis || !window.SpeechSynthesisUtterance) return;
      var text = UTF8ToString(textPtr);
      window.speechSynthesis.cancel();
      var u = new SpeechSynthesisUtterance(text);
      u.lang = "en-US"; u.rate = 1.0; u.pitch = 1.0;
      window.speechSynthesis.speak(u);
    } catch (e) {}
  },
  AstraSpeech_CancelSpeech: function () {
    try { if (window.speechSynthesis) window.speechSynthesis.cancel(); } catch (e) {}
  },
  AstraSpeech_RecognitionSupported__deps: ['$AstraSend'],
  AstraSpeech_SynthesisSupported__deps: ['$AstraSend'],
  AstraSpeech_StartListening__deps: ['$AstraSend'],
  AstraSpeech_StopListening__deps: ['$AstraSend'],
  AstraSpeech_Speak__deps: ['$AstraSend'],
  AstraSpeech_CancelSpeech__deps: ['$AstraSend']
});
