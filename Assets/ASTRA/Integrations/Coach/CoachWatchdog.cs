using System;
using System.Collections;
using UnityEngine;

namespace Astra.Integrations.Coach
{
    /// <summary>Hidden helper that runs a callback after a real-time delay (timeouts that also work in WebGL).</summary>
    internal sealed class CoachWatchdog : MonoBehaviour
    {
        private static CoachWatchdog _instance;

        public static void After(float seconds, Action action)
        {
            if (_instance == null)
            {
                var go = new GameObject("CoachWatchdog") { hideFlags = HideFlags.HideAndDontSave };
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<CoachWatchdog>();
            }
            _instance.StartCoroutine(_instance.Run(seconds, action));
        }

        private IEnumerator Run(float seconds, Action action)
        {
            yield return new WaitForSecondsRealtime(seconds);
            action();
        }
    }
}
