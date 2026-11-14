using System;
using System.Collections.Generic;
using System.IO;
using Astra.Core.Assessment;
using Astra.Core.Mission;
#if UNITY_5_3_OR_NEWER
using UnityEngine;
#endif

namespace Astra.Tests
{
    /// <summary>
    /// Loads repo files (fixtures/ and mission JSON) for tests, parsed with TestJson.
    /// Inside Unity the repo root comes from Application.dataPath; outside Unity (a local .NET test harness)
    /// from the ASTRA_REPO_ROOT environment variable.
    /// JsonUtility cannot parse a top-level array, so arrays are wrapped as {"items": [...]} on both paths.
    /// </summary>
    internal static class FixtureLoader
    {
        public const string L7MissionId = "l7-restore-power-v1";

        private static string _repoRoot;

        public static string RepoRoot => _repoRoot ??= FindRepoRoot();

        public static MissionDefinition Mission(string missionId) =>
            FromJson<MissionDefinition>(ReadRepoFile("Assets", "ASTRA", "Content", "Missions", missionId + ".json"));

        public static List<SessionEvent> Events(string fixtureName) =>
            ListFromJson<SessionEvent>(ReadRepoFile("fixtures", fixtureName));

        public static SessionResult Result(string fixtureName) =>
            FromJson<SessionResult>(ReadRepoFile("fixtures", fixtureName));

        public static ScoringCasesFile ScoringCases() =>
            FromJson<ScoringCasesFile>(ReadRepoFile("fixtures", "scoring-cases.json"));

        public static ScoringCase FindScoringCase(string name)
        {
            foreach (var c in ScoringCases().cases)
            {
                if (c.name == name)
                    return c;
            }
            throw new ArgumentException($"no scoring case named '{name}' in fixtures/scoring-cases.json");
        }

        public static string ReadRepoFile(params string[] parts) =>
            File.ReadAllText(Path.Combine(RepoRoot, Path.Combine(parts)));

        public static List<T> ListFromJson<T>(string jsonArray) =>
            FromJson<JsonList<T>>("{\"items\":" + jsonArray + "}").items;

        public static T FromJson<T>(string json) => TestJson.FromJson<T>(json);

        private static string FindRepoRoot()
        {
#if UNITY_5_3_OR_NEWER
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
#else
            var root = Environment.GetEnvironmentVariable("ASTRA_REPO_ROOT");
            if (string.IsNullOrEmpty(root) || !Directory.Exists(Path.Combine(root, "fixtures")))
                throw new InvalidOperationException(
                    "Outside Unity, set ASTRA_REPO_ROOT to the repo root (the folder that contains fixtures/).");
            return root;
#endif
        }
    }

    [Serializable]
    public sealed class JsonList<T>
    {
        public List<T> items = new List<T>();
    }

    /// <summary>Shape of fixtures/scoring-cases.json (test-only).</summary>
    [Serializable]
    public sealed class ScoringCasesFile
    {
        public string missionId;
        public List<ScoringCase> cases = new List<ScoringCase>();
    }

    [Serializable]
    public sealed class ScoringCase
    {
        public string name;
        public string description;
        public string sessionId;
        public List<SessionEvent> events = new List<SessionEvent>();
        public SessionResult expected;
    }
}
