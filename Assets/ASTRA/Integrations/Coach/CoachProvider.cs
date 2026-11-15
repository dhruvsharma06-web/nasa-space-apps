using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace Astra.Integrations.Coach
{
    /// <summary>
    /// Scene component the LevelController asks for a coach (CONTRACT.md §9).
    /// OWNER: Content & AI Coach. Offline only by default (empty proxyUrl). When proxyUrl is set, Start does
    /// GET {proxyUrl}/health (3 s timeout) and switches to HttpCoachClient if it answers 2xx.
    /// Until then (and on any failure) the client is the OfflineCoach, so Client is never null.
    /// </summary>
    public class CoachProvider : MonoBehaviour
    {
        public const int HealthTimeoutSeconds = 3;

        [Tooltip("Base URL of the coach proxy, e.g. http://192.168.1.20:8080. Empty = offline coach only.")]
        [SerializeField] private string proxyUrl = string.Empty;

        private readonly OfflineCoach _offline = new OfflineCoach();
        private HttpCoachClient _http;

        /// <summary>The active debrief coach. Never null.</summary>
        public virtual ICoachClient Client
        {
            get { return _http != null ? (ICoachClient)_http : _offline; }
        }

        /// <summary>The active ask-the-coach client (Practice only). Never null.</summary>
        public virtual ICoachAskClient AskClient
        {
            get { return _http != null ? (ICoachAskClient)_http : _offline; }
        }

        /// <summary>
        /// True when the proxy is active and its last answer (if any) came from the AI, so UI can tag it
        /// "AI coach (advisory)". False for the offline coach, or after a call fell back to offline.
        /// </summary>
        public bool IsAi
        {
            get { return _http != null && (!_http.HasAnswered || _http.LastAnswerWasAi); }
        }

        /// <summary>True once the health check passed and the proxy client is in use.</summary>
        public bool ProxyActive { get { return _http != null; } }

        private void Start()
        {
            if (string.IsNullOrWhiteSpace(proxyUrl)) return;
            string url = proxyUrl.Trim().TrimEnd('/');
            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                Debug.LogWarning("[CoachProvider] proxyUrl is not a valid http(s) URL; using the offline coach.");
                return;
            }
            StartCoroutine(HealthCheck(url));
        }

        private IEnumerator HealthCheck(string url)
        {
            using (var req = UnityWebRequest.Get(url + "/health"))
            {
                req.timeout = HealthTimeoutSeconds;
                var op = req.SendWebRequest();
                float deadline = Time.realtimeSinceStartup + HealthTimeoutSeconds;   // WebGL ignores req.timeout
                while (!op.isDone && Time.realtimeSinceStartup < deadline) yield return null;
                if (!op.isDone) { req.Abort(); Debug.Log("[CoachProvider] proxy health check timed out; using the offline coach."); yield break; }
                bool ok = req.result == UnityWebRequest.Result.Success && req.responseCode >= 200 && req.responseCode < 300;
                if (ok) _http = new HttpCoachClient(url, _offline);
                else Debug.Log("[CoachProvider] proxy health check failed; using the offline coach.");
            }
        }
    }
}
