using System.Text.RegularExpressions;
using Astra.Core.Assessment;
using Astra.Core.Mission;
using NUnit.Framework;

namespace Astra.Tests
{
    /// <summary>
    /// The measurable rules of docs/content-style-guide.md, checked on the missions Core Logic owns (L4, L7).
    /// Wording quality is still reviewed by Content &amp; AI Coach; this only catches limits a script can count.
    /// </summary>
    public class MissionStyleTests
    {
        private static readonly Regex IdPattern = new Regex("^[a-z0-9_]{1,64}$");
        private static readonly Regex SentenceEnd = new Regex(@"[.!?](\s|$)");

        public static readonly string[] OwnedMissions = { "l4-emergency-v1", "l7-restore-power-v1" };

        [TestCaseSource(nameof(OwnedMissions))]
        public void InstructionsAreAtMost15Words(string missionId)
        {
            foreach (var step in FixtureLoader.Mission(missionId).steps)
                Assert.LessOrEqual(Words(step.instruction), 15, $"{missionId}/{step.stepId}: '{step.instruction}'");
        }

        [TestCaseSource(nameof(OwnedMissions))]
        public void HintsAreAtMost25WordsAndTwoSentences(string missionId)
        {
            foreach (var step in FixtureLoader.Mission(missionId).steps)
            {
                Assert.LessOrEqual(Words(step.hint), 25, $"{missionId}/{step.stepId} hint length");
                Assert.LessOrEqual(SentenceEnd.Matches(step.hint).Count, 2, $"{missionId}/{step.stepId} hint sentences");
            }
        }

        [TestCaseSource(nameof(OwnedMissions))]
        public void HintsNeverJustRepeatTheInstruction(string missionId)
        {
            foreach (var step in FixtureLoader.Mission(missionId).steps)
                Assert.AreNotEqual(step.instruction.Trim(), step.hint.Trim(), $"{missionId}/{step.stepId}");
        }

        [TestCaseSource(nameof(OwnedMissions))]
        public void InstructionsAreProfessional(string missionId)
        {
            foreach (var step in FixtureLoader.Mission(missionId).steps)
            {
                StringAssert.DoesNotContain("!", step.instruction, $"{missionId}/{step.stepId}");
                StringAssert.DoesNotContain("!", step.hint, $"{missionId}/{step.stepId}");
                StringAssert.EndsWith(".", step.instruction, $"{missionId}/{step.stepId} ends with a full stop");
            }
        }

        [TestCaseSource(nameof(OwnedMissions))]
        public void IdsAreSnakeCase(string missionId)
        {
            foreach (var step in FixtureLoader.Mission(missionId).steps)
            {
                Assert.IsTrue(IdPattern.IsMatch(step.stepId), $"stepId '{step.stepId}'");
                Assert.IsTrue(IdPattern.IsMatch(step.targetId), $"targetId '{step.targetId}'");
            }
        }

        [TestCaseSource(nameof(OwnedMissions))]
        public void MissionIsFictionalAndHasAContractShape(string missionId)
        {
            var mission = FixtureLoader.Mission(missionId);
            Assert.IsTrue(mission.fictional, "fictional must be true");
            Assert.AreEqual(missionId, mission.missionId);
            StringAssert.StartsWith($"l{mission.level}-", mission.missionId);

            var hasCritical = false;
            foreach (var step in mission.steps)
            {
                Assert.Contains(step.expectedEvent, EventNames.All, $"{step.stepId} expectedEvent");
                hasCritical |= step.critical;
            }
            Assert.IsTrue(hasCritical, "at least one critical step");
        }

        [TestCaseSource(nameof(OwnedMissions))]
        public void TheLastStepIsACriticalConfirmation(string missionId)
        {
            // Style guide §8: the final report/confirmation step is usually critical.
            var steps = new MissionRunner(FixtureLoader.Mission(missionId), "s").Mission.steps;
            MissionStep last = null;
            foreach (var step in steps)
            {
                if (last == null || step.order > last.order)
                    last = step;
            }
            Assert.IsTrue(last.critical, $"{missionId}: last step '{last.stepId}' should be critical");
            Assert.AreEqual(EventNames.StatusReported, last.expectedEvent, $"{missionId}: last step reports status");
        }

        private static int Words(string text) =>
            text.Split(new[] { ' ', '\t', '\n' }, System.StringSplitOptions.RemoveEmptyEntries).Length;
    }
}
