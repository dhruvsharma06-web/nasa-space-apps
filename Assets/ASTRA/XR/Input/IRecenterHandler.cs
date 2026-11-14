using UnityEngine;

namespace Astra.XR.Input
{
    /// <summary>
    /// Completes a user-requested recenter and publishes its single recentered/system report.
    /// Returns false without reporting when recentering cannot be completed.
    /// </summary>
    public interface IRecenterHandler
    {
        bool TryRecenter(Component interactor);
    }
}
