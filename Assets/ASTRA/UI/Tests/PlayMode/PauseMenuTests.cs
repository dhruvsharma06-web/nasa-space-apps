using System;
using System.Collections.Generic;
using Astra.Core.Assessment;
using Astra.Core.Mission;
using Astra.UI;
using Astra.XR.Input;
using Astra.XR.Interactables;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Astra.UI.Tests.PlayMode
{
    public sealed class PauseMenuTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly List<InteractionReport> reports = new List<InteractionReport>();
        private float originalTimeScale;

        [SetUp]
        public void SetUp()
        {
            originalTimeScale = Time.timeScale;
            reports.Clear();
            InteractionEvents.Reported += reports.Add;
        }

        [TearDown]
        public void TearDown()
        {
            InteractionEvents.Reported -= reports.Add;
            Time.timeScale = originalTimeScale;
            foreach (var instance in objects)
                if (instance != null)
                    UnityEngine.Object.DestroyImmediate(instance);
            objects.Clear();
        }

        [Test]
        public void PauseResumeUseActorModalityAndIgnoreRepeatsAndReentrantCalls()
        {
            Time.timeScale = 0.37f;
            var setup = CreateMenu(InputModality.ControllerRay);
            setup.Menu.SetPhase(ModulePhase.Practice);
            var pauseCallbacks = 0;
            var resumeCallbacks = 0;
            var reentrantPauseResult = true;
            var reentrantResumeResult = true;
            setup.Menu.onPause.AddListener(() =>
            {
                pauseCallbacks++;
                reentrantPauseResult = setup.Menu.TryPause(setup.Actor);
            });
            setup.Menu.onResume.AddListener(() =>
            {
                resumeCallbacks++;
                reentrantResumeResult = setup.Menu.TryResume(setup.Actor);
            });

            Assert.IsTrue(setup.Menu.TryPause(setup.Actor));
            Assert.IsTrue(setup.Menu.IsPaused);
            Assert.IsTrue(setup.MenuRoot.activeSelf);
            Assert.IsFalse(setup.Menu.TryPause(setup.Actor));
            Assert.IsFalse(reentrantPauseResult);
            Assert.AreEqual(1, pauseCallbacks);
            AssertReport(0, EventNames.Paused, InputModality.ControllerRay);

            Assert.IsTrue(setup.Menu.TryResume(setup.Actor));
            Assert.IsFalse(setup.Menu.IsPaused);
            Assert.IsFalse(setup.MenuRoot.activeSelf);
            Assert.IsFalse(setup.Menu.TryResume(setup.Actor));
            Assert.IsFalse(reentrantResumeResult);
            Assert.AreEqual(1, resumeCallbacks);
            AssertReport(1, EventNames.Resumed, InputModality.ControllerRay);
            Assert.AreEqual(2, reports.Count);
            Assert.AreEqual(0.37f, Time.timeScale);
        }

        [Test]
        public void ParameterlessPauseBindingsUseConfiguredTagAndSimulatorModality()
        {
            var setup = CreateMenu(InputModality.ControllerRay, isSimulator: true);
            setup.Menu.SetPhase(ModulePhase.Qualification);
            setup.Menu.Pause();
            setup.Menu.Pause();
            setup.Menu.Resume();

            Assert.AreEqual(2, reports.Count);
            AssertReport(0, EventNames.Paused, InputModality.Simulator);
            AssertReport(1, EventNames.Resumed, InputModality.Simulator);
        }

        [Test]
        public void FinishIsQualificationOnlyAndCanBeRequestedAgainAfterAttemptReset()
        {
            var setup = CreateMenu(InputModality.ControllerGrip);
            var finishCallbacks = 0;
            setup.Menu.onFinish.AddListener(() => finishCallbacks++);

            setup.Menu.SetPhase(ModulePhase.Practice);
            Assert.IsFalse(setup.FinishButton.activeSelf);
            setup.Menu.RequestFinish();
            Assert.AreEqual(0, finishCallbacks);
            Assert.AreEqual(0, reports.Count);

            setup.Menu.SetPhase(ModulePhase.Qualification);
            Assert.IsTrue(setup.FinishButton.activeSelf);
            setup.Menu.RequestFinish();
            setup.Menu.RequestFinish();
            Assert.IsFalse(setup.FinishButton.activeSelf);
            Assert.AreEqual(1, finishCallbacks);
            Assert.AreEqual(0, reports.Count, "Finish is a UI callback; it is not a scored interaction event.");

            setup.Menu.ResetForAttempt(ModulePhase.Qualification);
            Assert.IsTrue(setup.FinishButton.activeSelf);
            setup.Menu.RequestFinish();
            Assert.AreEqual(2, finishCallbacks);
            Assert.AreEqual(0, reports.Count);
        }

        [Test]
        public void DebriefPhaseClearsPausedOverlayWithoutSyntheticResume()
        {
            var setup = CreateMenu(InputModality.ControllerRay);
            setup.Menu.SetPhase(ModulePhase.Qualification);
            Assert.IsTrue(setup.Menu.TryPause(setup.Actor));
            Assert.AreEqual(1, reports.Count);

            setup.Menu.SetPhase(ModulePhase.Debrief);
            Assert.IsFalse(setup.Menu.IsPaused);
            Assert.IsFalse(setup.MenuRoot.activeSelf);
            Assert.AreEqual(1, reports.Count);
            AssertReport(0, EventNames.Paused, InputModality.ControllerRay);
        }

        [Test]
        public void AttemptResetClearsOverlayWithoutEventsOrCallbacks()
        {
            var setup = CreateMenu(InputModality.HandPinch);
            var pauseCallbacks = 0;
            var resumeCallbacks = 0;
            setup.Menu.onPause.AddListener(() => pauseCallbacks++);
            setup.Menu.onResume.AddListener(() => resumeCallbacks++);
            setup.Menu.SetPhase(ModulePhase.Practice);
            Assert.IsTrue(setup.Menu.TryPause(setup.Actor));
            var countBeforeReset = reports.Count;

            setup.Menu.ResetForAttempt(ModulePhase.Briefing);
            Assert.IsFalse(setup.Menu.IsPaused);
            Assert.IsFalse(setup.MenuRoot.activeSelf);
            Assert.AreEqual(countBeforeReset, reports.Count);
            Assert.AreEqual(1, pauseCallbacks);
            Assert.AreEqual(0, resumeCallbacks);
        }

        [Test]
        public void MissingOrDisabledInputTagRejectsPauseAndRecenterWithoutEvents()
        {
            var setup = CreateMenu(InputModality.ControllerRay, withRecenter: true);
            var untaggedActor = CreateActor(null, InputModality.ControllerRay);
            Assert.IsFalse(setup.Menu.TryPause(untaggedActor));
            Assert.IsFalse(setup.Menu.TryRecenter(untaggedActor));

            setup.Tag.enabled = false;
            Assert.IsFalse(setup.Menu.TryPause(setup.Actor));
            Assert.IsFalse(setup.Menu.TryRecenter(setup.Actor));
            Assert.AreEqual(0, reports.Count);
            Assert.AreEqual(0, setup.RecenterHandler.CallCount);
        }

        [Test]
        public void RecenterPublishesOnceOnlyAfterHandlerSuccessAndInvokesCallback()
        {
            var setup = CreateMenu(InputModality.ControllerGrip, withRecenter: true);
            var callbackCount = 0;
            setup.Menu.onRecenter.AddListener(() => callbackCount++);

            setup.RecenterHandler.Succeeds = false;
            Assert.IsFalse(setup.Menu.TryRecenter(setup.Actor));
            Assert.AreEqual(0, reports.Count);
            Assert.AreEqual(0, callbackCount);

            setup.RecenterHandler.Succeeds = true;
            Assert.IsTrue(setup.Menu.TryRecenter(setup.Actor));
            Assert.AreEqual(1, reports.Count);
            AssertReport(0, EventNames.Recentered, InputModality.ControllerGrip);
            Assert.AreEqual(1, callbackCount);
            Assert.AreEqual(setup.Actor, setup.RecenterHandler.LastInteractor);
        }

        [Test]
        public void MissingOrInvalidRecenterHandlerDoesNotPublish()
        {
            var setup = CreateMenu(InputModality.ControllerRay);
            Assert.IsFalse(setup.Menu.CanRecenter);
            Assert.IsFalse(setup.Menu.TryRecenter(setup.Actor));
            Assert.AreEqual(0, reports.Count);
            Assert.Throws<ArgumentException>(() => setup.Menu.Configure(
                setup.MenuRoot, setup.FinishButton, setup.Tag, setup.Menu));
            Assert.AreEqual(0, reports.Count);
        }

        [Test]
        public void InvalidRootHierarchyIsRejectedBeforeActivation()
        {
            var parent = NewObject("InvalidHierarchy");
            var menuRoot = NewObject("MenuRoot", parent.transform);
            var finish = NewObject("FinishButton", menuRoot.transform);
            var tagRoot = NewObject("TagRoot");
            var tag = tagRoot.AddComponent<InputModalityTag>();
            tag.Configure(InputModality.ControllerRay);
            var menuObject = NewObject("InvalidPauseMenu", menuRoot.transform);
            var menu = menuObject.AddComponent<PauseMenu>();
            Assert.Throws<ArgumentException>(() => menu.Configure(menuRoot, finish, tag));
            Assert.AreEqual(0, reports.Count);
        }

        private SetupState CreateMenu(string modality, bool isSimulator = false, bool withRecenter = false)
        {
            var menuObject = NewObject("PauseMenu");
            var menuRoot = NewObject("MenuRoot");
            var finish = NewObject("FinishButton", menuRoot.transform);
            var tagRoot = NewObject("InteractorRoot");
            var tag = tagRoot.AddComponent<InputModalityTag>();
            tag.Configure(modality, isSimulator);
            var actor = CreateActor(tagRoot, modality);
            var handler = withRecenter ? menuObject.AddComponent<PauseMenuTestRecenterHandler>() : null;
            if (handler != null)
                handler.Configure(true);
            var menu = menuObject.AddComponent<PauseMenu>();
            menu.Configure(menuRoot, finish, tag, handler);

            tagRoot.SetActive(true);
            menuObject.SetActive(true);
            Assert.IsTrue(menu.enabled);
            return new SetupState(menu, menuRoot, finish, tag, actor, handler);
        }

        private Component CreateActor(GameObject tagRoot, string modality)
        {
            if (tagRoot == null)
            {
                var untaggedRoot = NewObject("UntaggedActor");
                var actor = untaggedRoot.AddComponent<PauseMenuTestActor>();
                untaggedRoot.SetActive(true);
                return actor;
            }

            var actorObject = NewObject("Actor", tagRoot.transform);
            var component = actorObject.AddComponent<PauseMenuTestActor>();
            tagRoot.SetActive(true);
            actorObject.SetActive(true);
            return component;
        }

        private GameObject NewObject(string objectName, Transform parent = null)
        {
            var instance = new GameObject(objectName);
            instance.SetActive(false);
            if (parent != null)
                instance.transform.SetParent(parent, false);
            objects.Add(instance);
            return instance;
        }

        private void AssertReport(int index, string eventName, string input)
        {
            var report = reports[index];
            Assert.AreEqual(eventName, report.EventName);
            Assert.AreEqual(SystemTargets.System, report.TargetId);
            Assert.AreEqual(input, report.Input);
        }

        private sealed class SetupState
        {
            public readonly PauseMenu Menu;
            public readonly GameObject MenuRoot;
            public readonly GameObject FinishButton;
            public readonly InputModalityTag Tag;
            public readonly Component Actor;
            public readonly PauseMenuTestRecenterHandler RecenterHandler;

            public SetupState(PauseMenu menu, GameObject menuRoot, GameObject finishButton,
                InputModalityTag tag, Component actor, PauseMenuTestRecenterHandler recenterHandler)
            {
                Menu = menu;
                MenuRoot = menuRoot;
                FinishButton = finishButton;
                Tag = tag;
                Actor = actor;
                RecenterHandler = recenterHandler;
            }
        }
    }

    public sealed class PauseMenuTestActor : MonoBehaviour
    {
        public Component Attach(GameObject target) => target.AddComponent<PauseMenuTestActor>();
    }

    public sealed class PauseMenuTestRecenterHandler : MonoBehaviour, IRecenterHandler
    {
        public bool Succeeds { get; set; }
        public int CallCount { get; private set; }
        public Component LastInteractor { get; private set; }

        public void Configure(bool succeeds) => Succeeds = succeeds;

        public bool TryRecenter(Component interactor)
        {
            CallCount++;
            LastInteractor = interactor;
            if (!Succeeds || !InputModalityTag.TryResolve(interactor, out var input))
                return false;
            InteractionEvents.Raise(EventNames.Recentered, SystemTargets.System, input);
            return true;
        }
    }
}
