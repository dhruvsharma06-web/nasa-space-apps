using System;
using Astra.Core.Assessment;

namespace Astra.Core.Simulation
{
    /// <summary>
    /// Fictional, simplified electrical fault used by L4 and L7 (FR-SIM-03, FR-SIM-05).
    /// <code>
    /// Nominal --InjectFault()--> Faulted
    /// Faulted --control_activated power_bus_b_selector--> Isolated
    /// Isolated --control_activated crosstie_breaker--> Restored
    /// any state --Reset()--> Nominal
    /// </code>
    /// Anything else changes nothing. Deterministic: the state changes only through these calls, never through
    /// time, frame rate or randomness. The level script calls Apply only for events the mission runner judged
    /// Correct, so a breaker pressed out of order never restores power. AI output never reaches it (FR-SIM-04).
    /// </summary>
    public sealed class PowerBusSimulation
    {
        public const string DefaultIsolateTargetId = "power_bus_b_selector";
        public const string DefaultCrossTieTargetId = "crosstie_breaker";

        private readonly string _isolateTargetId;
        private readonly string _crossTieTargetId;

        /// <summary>Uses the L7 target ids (power_bus_b_selector, crosstie_breaker).</summary>
        public PowerBusSimulation() : this(DefaultIsolateTargetId, DefaultCrossTieTargetId)
        {
        }

        /// <summary>For a level whose controls have other target ids.</summary>
        public PowerBusSimulation(string isolateTargetId, string crossTieTargetId)
        {
            if (string.IsNullOrEmpty(isolateTargetId))
                throw new ArgumentException("isolateTargetId is required", nameof(isolateTargetId));
            if (string.IsNullOrEmpty(crossTieTargetId))
                throw new ArgumentException("crossTieTargetId is required", nameof(crossTieTargetId));
            if (isolateTargetId == crossTieTargetId)
                throw new ArgumentException($"isolate and cross-tie controls must differ (both '{isolateTargetId}')");

            _isolateTargetId = isolateTargetId;
            _crossTieTargetId = crossTieTargetId;
        }

        public PowerBusState State { get; private set; } = PowerBusState.Nominal;

        /// <summary>Raised once per real change, with the new state. Not raised when a call changes nothing.</summary>
        public event Action<PowerBusState> StateChanged;

        /// <summary>
        /// Starts a fresh fault scenario: from any state, the bus becomes Faulted.
        /// Call it at the start of each Practice and Qualification attempt.
        /// </summary>
        public void InjectFault() => SetState(PowerBusState.Faulted);

        /// <summary>Applies one event the runner judged Correct. Returns true if the state changed.</summary>
        public bool Apply(string eventName, string targetId)
        {
            if (eventName != EventNames.ControlActivated)
                return false;
            if (State == PowerBusState.Faulted && targetId == _isolateTargetId)
                return SetState(PowerBusState.Isolated);
            if (State == PowerBusState.Isolated && targetId == _crossTieTargetId)
                return SetState(PowerBusState.Restored);
            return false;
        }

        /// <summary>Back to Nominal (restart, exit to Hub).</summary>
        public void Reset() => SetState(PowerBusState.Nominal);

        private bool SetState(PowerBusState next)
        {
            if (next == State)
                return false;
            State = next;
            StateChanged?.Invoke(next);
            return true;
        }
    }
}
