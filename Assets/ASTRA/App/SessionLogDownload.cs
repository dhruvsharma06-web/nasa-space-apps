using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Astra.App
{
    /// <summary>
    /// Saves the last session log so an instructor can load it into the dashboard: a file download in the
    /// browser, a file in persistentDataPath/SessionLogs elsewhere (already written by the LevelController).
    /// OWNER: Integration Lead.
    /// </summary>
    public static class SessionLogDownload
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void AstraDownloadText(string fileName, string text);
#endif

        /// <summary>Returns a short status line for the UI.</summary>
        public static string Save(LevelController level)
        {
            if (level == null || string.IsNullOrEmpty(level.LastSessionLogJson)) return "No session log yet: finish a qualification first.";
            string name = $"astra-session-{level.Mission.missionId}-{System.DateTime.Now:yyyyMMdd-HHmmss}.json";
#if UNITY_WEBGL && !UNITY_EDITOR
            AstraDownloadText(name, level.LastSessionLogJson);
            return "Session log downloaded: " + name;
#else
            return "Session log saved in " + Path.Combine(Application.persistentDataPath, "SessionLogs");
#endif
        }
    }
}
