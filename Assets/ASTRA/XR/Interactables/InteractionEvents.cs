using System;
using UnityEngine;

namespace Astra.XR.Interactables
{
    /// <summary>One player interaction, before scoring (CONTRACT.md §3).</summary>
    public readonly struct InteractionReport
    {
        public readonly string EventName;
        public readonly string TargetId;
        public readonly string Input;

        public InteractionReport(string eventName, string targetId, string input)
        {
            EventName = eventName;
            TargetId = targetId;
            Input = input;
        }

        public override string ToString() => $"{EventName} {TargetId} ({Input})";
    }

    /// <summary>
    /// The single bus every interactable and menu raises on. The LevelController subscribes and
    /// forwards to the mission runner. Subscribers must unsubscribe in OnDisable.
    /// </summary>
    public static class InteractionEvents
    {
        public static event Action<InteractionReport> Reported;

        public static void Raise(string eventName, string targetId, string input) =>
            Reported?.Invoke(new InteractionReport(eventName, targetId, input));

        // Clears stale subscribers when "Enter Play Mode Options" disables domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Reported = null;
    }
}
