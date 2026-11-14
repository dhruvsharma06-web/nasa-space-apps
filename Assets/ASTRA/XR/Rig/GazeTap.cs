using UnityEngine;

namespace Astra.XR.Rig
{
    /// <summary>
    /// Phone-headset gaze + tap interaction, independent of how the head ray and tap are read.
    /// The head ray points; one tap acts on what it points at:
    /// a control or UI button is pressed, a tool is picked up (it then follows the gaze) and the next tap drops it
    /// or places it in the socket it is over. A tap on empty space returns <see cref="Outcome.Menu"/> so the route
    /// can open the pause menu. Gazing at a locatable target dwells on it like any pointer.
    /// Each tap produces at most one interaction through <see cref="RayInteractor"/>; this class never scores.
    /// </summary>
    public sealed class GazeTap
    {
        public enum Outcome
        {
            None,
            Interacted,
            Menu
        }

        private readonly RayInteractor interactor;

        public GazeTap(RayInteractor rayInteractor)
        {
            interactor = rayInteractor;
        }

        public bool Holding => interactor != null && interactor.HeldTool != null;
        public bool PointsAtTarget => interactor != null && interactor.PointsAtTarget;
        public bool HasHit => interactor != null && interactor.HasHit;
        public Vector3 HitPoint => interactor != null ? interactor.HitPoint : default;

        /// <param name="ray">World-space head (gaze) ray.</param>
        /// <param name="valid">Head tracking is valid this frame; false cancels hover and any held tool.</param>
        /// <param name="tap">A tap started this frame.</param>
        /// <param name="actor">Component whose <c>InputModalityTag</c> names the reported modality.</param>
        public Outcome Step(Ray ray, bool valid, bool tap, Component actor)
        {
            if (interactor == null)
                return Outcome.None;
            var holding = Holding;
            interactor.Step(new RayInteractor.Frame
            {
                Ray = ray,
                Valid = valid,
                // While holding, the tap ends the grab (release or place) and nothing else.
                SelectDown = valid && tap && !holding,
                GrabDown = false,
                GrabHeld = !(holding && tap),
                SelectGrabs = true,
                ClickUi = true,
                SelectActor = actor,
                GrabActor = actor
            });
            if (!valid || !tap)
                return Outcome.None;
            if (holding || interactor.PointsAtTarget)
                return Outcome.Interacted;
            return Outcome.Menu;
        }

        public void Cancel()
        {
            if (interactor != null)
                interactor.Cancel();
        }
    }
}
