using System;
using Astra.Core.Mission;
using NUnit.Framework;

namespace Astra.Tests
{
    public class ModulePhaseMachineTests
    {
        [Test]
        public void StartsInBriefing()
        {
            Assert.AreEqual(ModulePhase.Briefing, new ModulePhaseMachine().Current);
        }

        [Test]
        public void CannotSkipStraightToDebrief()
        {
            var fsm = new ModulePhaseMachine();
            Assert.Throws<InvalidOperationException>(() => fsm.TransitionTo(ModulePhase.Debrief));
        }

        [Test]
        public void FullHappyPathReachesDebrief()
        {
            var fsm = new ModulePhaseMachine();
            fsm.TransitionTo(ModulePhase.Demonstration);
            fsm.TransitionTo(ModulePhase.Practice);
            fsm.TransitionTo(ModulePhase.Qualification);
            fsm.TransitionTo(ModulePhase.Debrief);
            Assert.AreEqual(ModulePhase.Debrief, fsm.Current);
        }
    }
}
