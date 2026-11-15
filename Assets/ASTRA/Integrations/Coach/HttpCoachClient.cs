using System;
using System.Text;
using Astra.Core.Mission;
using UnityEngine;
using UnityEngine.Networking;

namespace Astra.Integrations.Coach
{
    /// <summary>
    /// Posts to the coach proxy (CONTRACT.md §9, v1.3): /api/v1/coach/debrief and /api/v1/coach/ask.
    /// On any error, timeout, non-2xx (including 429) or invalid response it falls back to
    /// <see cref="OfflineCoach"/> for that call. It never holds an API key (NFR-SEC-02).
    /// onDone is called exactly once, never with null, and nothing here throws.
    /// </summary>
    public sealed class HttpCoachClient : ICoachClient, ICoachAskClient
    {
        public const int TimeoutSeconds = 6;
        public const string DebriefPath = "/api/v1/coach/debrief";
        public const string AskPath = "/api/v1/coach/ask";
        public const int MaxQuestionChars = 300;

        /// <summary>Shown to the trainee before the first Ask (CONTRACT.md §9.2, NFR-SEC-01).</summary>
        public const string AskConsentText = "Your question is sent to Google Gemini. Do not include personal information.";

        private readonly string _baseUrl;
        private readonly OfflineCoach _offline;

        /// <summary>True when the last answer came from the proxy and is not an offline answer.</summary>
        public bool LastAnswerWasAi { get; private set; }

        /// <summary>True when the last call fell back to OfflineCoach (error, timeout, non-2xx, invalid body).</summary>
        public bool LastAnswerWasOffline { get; private set; }

        /// <summary>False until the first call has completed.</summary>
        public bool HasAnswered { get; private set; }

        public HttpCoachClient(string baseUrl, OfflineCoach offline = null)
        {
            _baseUrl = (baseUrl ?? string.Empty).Trim().TrimEnd('/');
            _offline = offline ?? new OfflineCoach();
        }

        // ---------------- ICoachClient ----------------

        public void RequestDebrief(CoachRequest request, MissionDefinition mission, Action<CoachResponse> onDone)
        {
            if (onDone == null) return;
            string json = null;
            try { json = request != null ? JsonUtility.ToJson(request) : null; }
            catch (Exception) { json = null; }

            void Finish(CoachResponse r)
            {
                HasAnswered = true;
                if (r == null)
                {
                    LastAnswerWasAi = false;
                    LastAnswerWasOffline = true;
                    onDone(_offline.Debrief(request, mission));
                    return;
                }
                LastAnswerWasOffline = false;
                LastAnswerWasAi = IsAiModel(r.modelVersion);
                onDone(r);
            }

            if (json == null) { Finish(null); return; }
            Send(DebriefPath, json,
                body => Finish(ParseDebrief(body)),
                () => Finish(null));
        }

        // ---------------- ICoachAskClient ----------------

        public void AskCoach(CoachAskRequest request, MissionDefinition mission, Action<CoachAskResponse> onDone)
        {
            if (onDone == null) return;

            void Finish(CoachAskResponse r)
            {
                HasAnswered = true;
                if (r == null)
                {
                    LastAnswerWasAi = false;
                    LastAnswerWasOffline = true;
                    onDone(_offline.Ask(request, mission));
                    return;
                }
                LastAnswerWasOffline = false;
                LastAnswerWasAi = IsAiModel(r.modelVersion);
                onDone(r);
            }

            // Ask is Practice only: never send anything for another phase or an unusable question.
            if (request == null || request.phase != "Practice" || string.IsNullOrWhiteSpace(request.question))
            {
                Finish(null);
                return;
            }

            string json = BuildAskJson(request);
            Send(AskPath, json,
                body => Finish(ParseAsk(body)),
                () => Finish(null));
        }

        // ---------------- JSON ----------------

        /// <summary>
        /// Hand-built so currentStepId can be a real JSON null (JsonUtility writes null strings as "").
        /// </summary>
        internal static string BuildAskJson(CoachAskRequest r)
        {
            string question = r.question.Trim();
            if (question.Length > MaxQuestionChars) question = question.Substring(0, MaxQuestionChars).Trim();
            var sb = new StringBuilder(256);
            sb.Append("{\"sessionId\":").Append(Quote(r.sessionId));
            sb.Append(",\"missionId\":").Append(Quote(r.missionId));
            sb.Append(",\"phase\":").Append(Quote(r.phase));
            sb.Append(",\"currentStepId\":").Append(string.IsNullOrEmpty(r.currentStepId) ? "null" : Quote(r.currentStepId));
            sb.Append(",\"question\":").Append(Quote(question));
            sb.Append('}');
            return sb.ToString();
        }

        private static string Quote(string s)
        {
            if (s == null) return "null";
            var sb = new StringBuilder(s.Length + 2);
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        private static CoachResponse ParseDebrief(string body)
        {
            try
            {
                var r = JsonUtility.FromJson<CoachResponse>(body);
                if (r == null || !r.advisory) return null;
                if (string.IsNullOrEmpty(r.modelVersion)) return null;
                if (r.hints == null) r.hints = new System.Collections.Generic.List<string>();
                // A valid empty answer has no summary and a reason; otherwise a summary is required.
                if (string.IsNullOrEmpty(r.summary) && string.IsNullOrEmpty(r.reason)) return null;
                return r;
            }
            catch (Exception) { return null; }
        }

        private static CoachAskResponse ParseAsk(string body)
        {
            try
            {
                var r = JsonUtility.FromJson<CoachAskResponse>(body);
                if (r == null || !r.advisory) return null;
                if (string.IsNullOrEmpty(r.answer) || string.IsNullOrEmpty(r.modelVersion)) return null;
                return r;
            }
            catch (Exception) { return null; }
        }

        private static bool IsAiModel(string modelVersion) =>
            modelVersion != null && !modelVersion.StartsWith("offline", StringComparison.Ordinal);

        // ---------------- transport ----------------

        /// <summary>
        /// POSTs json. Exactly one of onSuccess (2xx, body) or onFailure is invoked, even if setup throws.
        /// Uses the request's own timeout, so no MonoBehaviour is needed.
        /// </summary>
        private void Send(string path, string json, Action<string> onSuccess, Action onFailure)
        {
            if (string.IsNullOrEmpty(_baseUrl)) { onFailure(); return; }

            UnityWebRequest req = null;
            bool done = false;
            try
            {
                req = new UnityWebRequest(_baseUrl + path, UnityWebRequest.kHttpVerbPOST);
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = TimeoutSeconds;

                var op = req.SendWebRequest();
                var captured = req;
                op.completed += _ =>
                {
                    if (done) return;
                    done = true;
                    string body = null;
                    bool ok = false;
                    try
                    {
                        ok = captured.result == UnityWebRequest.Result.Success
                             && captured.responseCode >= 200 && captured.responseCode < 300;
                        if (ok) body = captured.downloadHandler.text;
                    }
                    catch (Exception) { ok = false; }
                    try { captured.Dispose(); } catch (Exception) { }

                    if (ok) onSuccess(body); else onFailure();
                };
                // WebGL ignores UnityWebRequest.timeout: our own watchdog ends a stalled call and falls back offline
                CoachWatchdog.After(TimeoutSeconds + 0.5f, () =>
                {
                    if (done) return;
                    done = true;
                    try { captured.Abort(); captured.Dispose(); } catch (Exception) { }
                    onFailure();
                });
            }
            catch (Exception)
            {
                if (done) return;
                done = true;
                try { if (req != null) req.Dispose(); } catch (Exception) { }
                onFailure();
            }
        }
    }
}
