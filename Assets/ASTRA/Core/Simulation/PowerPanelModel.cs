using System;
using Astra.Core.Assessment;
using Astra.Core.Mission;

namespace Astra.Core.Simulation
{
    /// <summary>
    /// Everything the L4/L7 power panel shows, decided in plain C# so it can be tested without Unity.
    /// The Unity script (PowerPanelView) only forwards LevelController events here and draws the result.
    /// <list type="bullet">
    /// <item>Practice / Qualification start: a fresh fault is injected and the caution alarm comes on.</item>
    /// <item>Briefing / Demonstration (including a restart): the panel returns to Nominal, alarm off.</item>
    /// <item>Debrief: nothing changes, so the cadet still sees how they left the panel.</item>
    /// <item>A Correct acknowledge silences the alarm but does not fix the fault (style guide §6).</item>
    /// <item>Correct isolate / cross-tie actions drive the simulation; any other outcome changes nothing.</item>
    /// </list>
    /// </summary>
    public sealed class PowerPanelModel
    {
        public const string DefaultAckTargetId = "caution_ack_button";

        private readonly PowerBusSimulation _simulation;
        private readonly string _ackTargetId;

        /// <param name="simulation">The bus model, created with the level's control ids.</param>
        /// <param name="ackTargetId">The caution ACK button's targetId.</param>
        public PowerPanelModel(PowerBusSimulation simulation, string ackTargetId = DefaultAckTargetId)
        {
            _simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
            if (string.IsNullOrEmpty(ackTargetId))
                throw new ArgumentException("ackTargetId is required", nameof(ackTargetId));
            _ackTargetId = ackTargetId;
        }

        /// <summary>Raised once after any call that changed what the panel shows.</summary>
        public event Action Changed;

        public PowerBusState State => _simulation.State;

        /// <summary>True once the caution has been acknowledged in the current attempt.</summary>
        public bool Acknowledged { get; private set; }

        /// <summary>The caution alarm is on while the bus is faulted and nobody has acknowledged it.</summary>
        public bool AlarmOn => State == PowerBusState.Faulted && !Acknowledged;

        /// <summary>Text for the STATUS display, matching the words used in the mission hints.</summary>
        public string StatusLabel
        {
            get
            {
                switch (State)
                {
                    case PowerBusState.Faulted: return "FAULT";
                    case PowerBusState.Isolated: return "ISOLATED";
                    case PowerBusState.Restored: return "RESTORED";
                    default: return "NOMINAL";
                }
            }
        }

        /// <summary>Forward LevelController.PhaseChanged here.</summary>
        public void OnPhaseChanged(ModulePhase phase)
        {
            switch (phase)
            {
                case ModulePhase.Practice:
                case ModulePhase.Qualification:
                    Acknowledged = false;
                    _simulation.InjectFault();
                    RaiseChanged();
                    break;
                case ModulePhase.Briefing:
                case ModulePhase.Demonstration:
                    Acknowledged = false;
                    _simulation.Reset();
                    RaiseChanged();
                    break;
                // Debrief: keep the final state on screen.
            }
        }

        /// <summary>Forward LevelController.EventRecorded here. Only Correct outcomes change the panel.</summary>
        public void OnEventRecorded(SessionEvent recorded, StepFeedback feedback)
        {
            if (recorded == null || feedback == null || feedback.Outcome != StepOutcome.Correct)
                return;

            var changed = false;
            if (!Acknowledged && State == PowerBusState.Faulted
                && recorded.@event == EventNames.ControlActivated && recorded.targetId == _ackTargetId)
            {
                Acknowledged = true;
                changed = true;
            }
            if (_simulation.Apply(recorded.@event, recorded.targetId))
                changed = true;

            if (changed)
                RaiseChanged();
        }

        private void RaiseChanged() => Changed?.Invoke();
    }
}
