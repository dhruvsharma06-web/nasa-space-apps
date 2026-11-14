using Astra.Core.Progress;
using UnityEngine;

namespace Astra.UI.Hub
{
    /// <summary>
    /// Supplies the Hub with the contract's local progress file (CONTRACT.md §6) for read-only display.
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
            GetComponent<HubController>().UseProgressStore(store);
            if (store.LastLoadWarning != null)
                Debug.LogWarning("[HubProgressBinding] " + store.LastLoadWarning, this);
        }
    }
}
