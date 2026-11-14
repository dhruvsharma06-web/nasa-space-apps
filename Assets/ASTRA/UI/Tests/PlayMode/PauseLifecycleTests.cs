using System;
using System.Collections.Generic;
using Astra.Core.Mission;
using Astra.XR.Input;
using Astra.XR.Interactables;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Astra.UI.Tests.PlayMode
{
    public sealed class PauseLifecycleTests
    {
        private GameObject root;
        private readonly List<InteractionReport> reports = new List<InteractionReport>();

        [SetUp]
        public void SetUp()
        {
            reports.Clear();
            InteractionEvents.Reported += reports.Add;
        }

        [TearDown]
        public void TearDown()
        {
            InteractionEvents.Reported -= reports.Add;
            if (root != null)
                UnityEngine.Object.DestroyImmediate(root);
        }

        [Test]
        public void OpeningMenuCannotPublishIntoAnAttemptResetByItsOnEnable()
        {
            var menu = Create(out var panel, out _, out var actor);
            var probe = panel.AddComponent<PauseLifecycleProbe>();
            probe.Enabled = () => menu.ResetForAttempt(ModulePhase.Practice);

            Assert.That(menu.TryPause(actor), Is.False);
            Assert.That(menu.IsPaused, Is.False);
            Assert.That(panel.activeSelf, Is.False);
            Assert.That(reports, Is.Empty);
        }

        [Test]
        public void FinishButtonDisableCannotScoreAnAttemptThatWasJustReset()
        {
            var menu = Create(out _, out var finish, out var actor);
            menu.SetPhase(ModulePhase.Qualification);
            Assert.That(menu.TryPause(actor), Is.True);
            var probe = finish.AddComponent<PauseLifecycleProbe>();
            probe.Disabled = () => menu.ResetForAttempt(ModulePhase.Practice);
            var finishCount = 0;
            menu.onFinish.AddListener(() => finishCount++);

            menu.RequestFinish();

            Assert.That(finishCount, Is.Zero);
            Assert.That(menu.IsPaused, Is.False);
            Assert.That(finish.activeSelf, Is.False);
            Assert.That(reports.Count, Is.EqualTo(1));
            probe.Disabled = null;
        }

        [Test]
        public void InvalidResetPhaseDoesNotPartiallyClearTheCurrentAttempt()
        {
            var menu = Create(out var panel, out _, out var actor);
            menu.SetPhase(ModulePhase.Qualification);
            Assert.That(menu.TryPause(actor), Is.True);
            LogAssert.Expect(LogType.Error, $"[PauseMenu] {menu.name}: invalid phase '999'.");

            menu.ResetForAttempt((ModulePhase)999);

            Assert.That(menu.IsPaused, Is.True);
            Assert.That(panel.activeSelf, Is.True);
            Assert.That(reports.Count, Is.EqualTo(1));
        }

        [Test]
        public void RestartAndExitCallbacksCannotRecursivelyRequestThemselves()
        {
            var menu = Create(out _, out _, out _);
            var restartCount = 0;
            var exitCount = 0;
            menu.onRestart.AddListener(() => { restartCount++; menu.RequestRestart(); });
            menu.onExit.AddListener(() => { exitCount++; menu.RequestExit(); });

            menu.RequestRestart();
            menu.RequestExit();

            Assert.That(restartCount, Is.EqualTo(1));
            Assert.That(exitCount, Is.EqualTo(1));
            Assert.That(reports, Is.Empty);
        }

        private PauseMenu Create(out GameObject panel, out GameObject finish, out Component actor)
        {
            root = new GameObject("PauseLifecycleRoot");
            root.SetActive(false);
            panel = new GameObject("Menu");
            panel.transform.SetParent(root.transform, false);
            panel.SetActive(false);
            finish = new GameObject("Finish");
            finish.transform.SetParent(panel.transform, false);
            var input = new GameObject("Interactor");
            input.transform.SetParent(root.transform, false);
            var tag = input.AddComponent<InputModalityTag>();
            tag.Configure(Astra.Core.Assessment.InputModality.ControllerRay);
            actor = input.transform;
            var menu = root.AddComponent<PauseMenu>();
            menu.Configure(panel, finish, tag);
            root.SetActive(true);
            menu.SetPhase(ModulePhase.Practice);
            return menu;
        }
    }

    public sealed class PauseLifecycleProbe : MonoBehaviour
    {
        public Action Enabled;
        public Action Disabled;

        private void OnEnable() => Enabled?.Invoke();
        private void OnDisable() => Disabled?.Invoke();
    }
}
