using Astra.Core.Progress;
using Astra.UI.Progress;
using UnityEngine;

namespace Astra.UI.Hub
{
    /// <summary>
    /// Supplies the Hub with the contract's local progress file (CONTRACT.md §6) and the academy progress adapter for read-only display.
    /// The Hub never saves; the LevelController records attempts and unlocks.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(HubController))]
    public sealed class HubProgressBinding : MonoBehaviour
    {
        private void Start()
        {
            var store = new FileProgressStore(Application.persistentDataPath, state => JsonUtility.ToJson(state, true),
                JsonUtility.FromJson<ProgressState>);
            var hub = GetComponent<HubController>();
            hub.UseProgressStore(store);
            // P2's ProgressService (when supplied) wins; otherwise the placeholder reads the same progress file.
            if (!AcademyProgress.IsSupplied)
                AcademyProgress.Current = new PlayerPrefsAcademyProgress(store);
            hub.UseAcademyProgress(AcademyProgress.Current);
            if (store.LastLoadWarning != null)
                Debug.LogWarning("[HubProgressBinding] " + store.LastLoadWarning, this);
        }
    }
}
