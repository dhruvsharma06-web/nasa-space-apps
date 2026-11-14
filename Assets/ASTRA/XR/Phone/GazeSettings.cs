using UnityEngine;

namespace Astra.XR.Phone
{
    /// <summary>
    /// Player-wide gaze preferences (Hub settings card): dwell-to-select on/off. Stored in PlayerPrefs, which works on
    /// Android and in WebGL. L0 forces dwell on for its own interactor (see <c>L0SafetyView</c>).
    /// </summary>
    public static class GazeSettings
    {
        private const string DwellKey = "astra.gaze.dwellSelect";
        private static bool? cached;

        public static bool DwellSelect
        {
            get
            {
                if (!cached.HasValue)
                    cached = PlayerPrefs.GetInt(DwellKey, 0) == 1;
                return cached.Value;
            }
            set
            {
                cached = value;
                PlayerPrefs.SetInt(DwellKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => cached = null;
    }
}
