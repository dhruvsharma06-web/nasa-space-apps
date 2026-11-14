using System;
using System.Collections.Generic;

namespace Astra.Core.Mission
{
    /// <summary>
    /// Explicit finite-state machine for module phases (FR-SIM-01, FR-APP-02).
    /// Only the transitions listed here are legal; anything else throws.
    /// </summary>
    public sealed class ModulePhaseMachine
    {
        private static readonly Dictionary<ModulePhase, ModulePhase[]> Allowed = new Dictionary<ModulePhase, ModulePhase[]>
        {
            { ModulePhase.Briefing,      new[] { ModulePhase.Demonstration, ModulePhase.Practice } },
            { ModulePhase.Demonstration, new[] { ModulePhase.Practice } },
            { ModulePhase.Practice,      new[] { ModulePhase.Qualification, ModulePhase.Briefing } },
            { ModulePhase.Qualification, new[] { ModulePhase.Debrief } },
            { ModulePhase.Debrief,       new[] { ModulePhase.Practice, ModulePhase.Briefing } },
        };

        public ModulePhase Current { get; private set; } = ModulePhase.Briefing;

        public event Action<ModulePhase, ModulePhase> PhaseChanged;

        public bool CanTransitionTo(ModulePhase next) =>
            Allowed.TryGetValue(Current, out var targets) && Array.IndexOf(targets, next) >= 0;

        public void TransitionTo(ModulePhase next)
        {
            if (!CanTransitionTo(next))
                throw new InvalidOperationException($"Illegal phase transition {Current} -> {next}");

            var previous = Current;
            Current = next;
            PhaseChanged?.Invoke(previous, next);
        }

        /// <summary>Restart/retry always returns to Briefing (FR-APP-03, FR-SIM-05).</summary>
        public void Reset() => Current = ModulePhase.Briefing;
    }
}
