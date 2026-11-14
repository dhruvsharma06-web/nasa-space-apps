using UnityEngine;

namespace Astra.UI
{
    /// <summary>Desktop QUIT button target. Has no effect in the Editor.</summary>
    public sealed class ApplicationQuit : MonoBehaviour
    {
        public void Quit()
        {
            Debug.Log("[ApplicationQuit] Quit requested.");
            Application.Quit();
        }
    }
}
