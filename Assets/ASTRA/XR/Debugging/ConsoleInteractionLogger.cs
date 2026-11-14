using Astra.XR.Interactables;
using UnityEngine;

namespace Astra.XR.Debugging
{
    /// <summary>Logs interaction reports while enabled for simulator diagnostics.</summary>
    public class ConsoleInteractionLogger : MonoBehaviour
    {
        private void OnEnable()
        {
            InteractionEvents.Reported += LogReport;
        }

        private void OnDisable()
        {
            InteractionEvents.Reported -= LogReport;
        }

        private void LogReport(InteractionReport report)
        {
            Debug.Log(report.ToString(), this);
        }
    }
}
