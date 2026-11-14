using System.Collections.Generic;
using Astra.Core.Assessment;
using Astra.XR.Input;
using Astra.XR.Interactables;
using UnityEngine;
using UnityEngine.XR;

namespace Astra.XR.Rig
{
    /// <summary>
    /// The rig's single recenter publisher (IRecenterHandler). With a tracked headset it asks the running
    /// XR input subsystem to start a new reference space; on the desktop it restores the seated view.
    /// Exactly one recentered/system report is raised per completed recenter, with the acting modality.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RigRecenterService : MonoBehaviour, IRecenterHandler
    {
        [SerializeField] private HeadPoseDriver headPose;
        [SerializeField] private DesktopMouseInput desktopInput;

        private static readonly List<XRInputSubsystem> Subsystems = new List<XRInputSubsystem>();

        public void Configure(HeadPoseDriver head, DesktopMouseInput desktop)
        {
            headPose = head;
            desktopInput = desktop;
        }

        public bool TryRecenter(Component interactor)
        {
            if (!isActiveAndEnabled || !InputModalityTag.TryResolve(interactor, out var input))
                return false;

            bool completed;
            if (headPose != null && headPose.IsTracking)
                completed = RecenterHeadset();
            else
            {
                completed = desktopInput != null;
                if (completed)
                    desktopInput.ResetLook();
            }

            if (completed)
                InteractionEvents.Raise(EventNames.Recentered, SystemTargets.System, input);
            return completed;
        }

        private static bool RecenterHeadset()
        {
            SubsystemManager.GetSubsystems(Subsystems);
            foreach (var subsystem in Subsystems)
                if (subsystem.running && subsystem.TryRecenter())
                    return true;
            return false;
        }
    }
}
