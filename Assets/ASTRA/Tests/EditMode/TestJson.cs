#if UNITY_5_3_OR_NEWER
using UnityEngine;
#else
using System.Text.Json;
#endif

namespace Astra.Tests
{
    /// <summary>
    /// JSON for tests. Inside Unity: JsonUtility, the serializer the app itself uses (public fields, camelCase).
    /// Outside Unity (a local .NET test harness): System.Text.Json set to the same public-field mapping.
    /// </summary>
    internal static class TestJson
    {
        public static T FromJson<T>(string json)
        {
#if UNITY_5_3_OR_NEWER
            return JsonUtility.FromJson<T>(json);
#else
            return JsonSerializer.Deserialize<T>(json, Options);
#endif
        }

        public static string ToJson<T>(T value)
        {
#if UNITY_5_3_OR_NEWER
            return JsonUtility.ToJson(value);
#else
            return JsonSerializer.Serialize(value, Options);
#endif
        }

#if !UNITY_5_3_OR_NEWER
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions { IncludeFields = true };
#endif
    }
}
