using System.Collections;
using System.Collections.Generic;
using Astra.Core.Assessment;
using Astra.XR.Input;
using Astra.XR.Interactables;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Astra.XR.Tests.PlayMode
{
    public sealed class ControlInteractionTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly List<InteractionReport> reports = new List<InteractionReport>();
        private float originalTimeScale;

        [SetUp]
        public void SetUp()
        {
            originalTimeScale = Time.timeScale;
            Time.timeScale = 1f;
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
                    Object.DestroyImmediate(instance);
            objects.Clear();
        }

        [Test]
        public void ChildInteractorResolvesParentTag_AndSimulatorOverrideIsReported()
        {
            var parent = NewObject("InteractorRoot");
            var tag = parent.AddComponent<InputModalityTag>();
            tag.Configure(InputModality.ControllerRay);
            var childObject = NewObject("InteractorChild", parent.transform);
            var child = childObject.AddComponent<InteractorMarker>();
            var button = NewControl<ButtonTarget>("target_button");
            parent.SetActive(true);
            childObject.SetActive(true);

            Assert.IsTrue(button.TryActivate(child));
            AssertReport(0, EventNames.ControlActivated, "target_button", InputModality.ControllerRay);

            button.ResetForAttempt();
            tag.Configure(InputModality.HandPinch, isSimulator: true);
            Assert.IsTrue(button.TryActivate(child));
            AssertReport(1, EventNames.ControlActivated, "target_button", InputModality.Simulator);
        }

        [Test]
        public void MissingOrDisabledModalityTagRejectsActivationWithoutReport()
        {
            var childObject = NewObject("UntaggedInteractor");
            var child = childObject.AddComponent<InteractorMarker>();
            var button = NewControl<ButtonTarget>("target_button");
            childObject.SetActive(true);
            Assert.IsFalse(button.TryActivate(child));
            Assert.AreEqual(0, reports.Count);

            var root = NewObject("DisabledTagRoot");
            var tag = root.AddComponent<InputModalityTag>();
            tag.Configure(InputModality.ControllerGrip);
            var taggedChildObject = NewObject("TaggedChild", root.transform);
            var taggedChild = taggedChildObject.AddComponent<InteractorMarker>();
            root.SetActive(true);
            taggedChildObject.SetActive(true);
            tag.enabled = false;
            Assert.IsFalse(button.TryActivate(taggedChild));
            Assert.AreEqual(0, reports.Count);
        }

        [UnityTest]
        public IEnumerator ButtonEmitsOnceImmediately_ThenRearmsOnUnscaledCooldown()
        {
            var interactor = NewInteractor(InputModality.ControllerRay);
            var button = NewControl<ButtonTarget>("target_button");
            Time.timeScale = 0f;

            Assert.IsTrue(button.TryActivate(interactor));
            Assert.IsTrue(button.IsPressed);
            Assert.IsFalse(button.TryActivate(interactor));
            Assert.AreEqual(1, reports.Count);
            AssertReport(0, EventNames.ControlActivated, "target_button", InputModality.ControllerRay);

            yield return new WaitForSecondsRealtime(0.36f);
            Assert.IsTrue(button.TryActivate(interactor), "Cooldown should use unscaled time.");
            Assert.AreEqual(2, reports.Count);
            AssertReport(1, EventNames.ControlActivated, "target_button", InputModality.ControllerRay);
        }

        [Test]
        public void ToggleCooldownPreventsSecondFlip_AndAttemptResetRestoresStateAndCooldown()
        {
            var interactor = NewInteractor(InputModality.ControllerGrip);
            var toggle = NewControl<ToggleTarget>("toggle_power");

            Assert.IsTrue(toggle.TryActivate(interactor));
            Assert.IsTrue(toggle.IsOn);
            Assert.IsFalse(toggle.TryActivate(interactor));
            Assert.IsTrue(toggle.IsOn);
            Assert.AreEqual(1, reports.Count);

            toggle.ResetForAttempt();
            Assert.IsFalse(toggle.IsOn);
            Assert.IsTrue(toggle.TryActivate(interactor));
            Assert.IsTrue(toggle.IsOn);
            Assert.AreEqual(2, reports.Count);
            AssertReport(1, EventNames.ControlActivated, "toggle_power", InputModality.ControllerGrip);
        }

        [Test]
        public void DialReportsOnlyOnEntryToTriggerDetent()
        {
            var interactor = NewInteractor(InputModality.ControllerRay);
            var dial = NewControl<DialTarget>("dial_selector");

            Assert.IsFalse(dial.SetAngle(20f, interactor));
            Assert.AreEqual(0, reports.Count);
            Assert.IsTrue(dial.SetAngle(90f, interactor));
            Assert.AreEqual(1, reports.Count);
            AssertReport(0, EventNames.ControlActivated, "dial_selector", InputModality.ControllerRay);
            Assert.IsFalse(dial.SetAngle(100f, interactor), "Staying in the trigger detent must not retrigger.");
            Assert.AreEqual(1, reports.Count);
        }

        [UnityTest]
        public IEnumerator LeverRequiresThresholdCrossingAndResetHysteresis()
        {
            var interactor = NewInteractor(InputModality.ControllerGrip);
            var lever = NewControl<LeverTarget>("lever_release");

            Assert.IsFalse(lever.SetPull(0.79f, interactor));
            Assert.IsTrue(lever.SetPull(0.81f, interactor));
            Assert.IsFalse(lever.SetPull(0.95f, interactor));
            Assert.AreEqual(1, reports.Count);
            AssertReport(0, EventNames.ControlActivated, "lever_release", InputModality.ControllerGrip);

            lever.SetPull(0.1f, interactor);
            yield return new WaitForSecondsRealtime(0.36f);
            Assert.IsTrue(lever.SetPull(0.9f, interactor));
            Assert.AreEqual(2, reports.Count);
            AssertReport(1, EventNames.ControlActivated, "lever_release", InputModality.ControllerGrip);
        }

        [UnityTest]
        public IEnumerator LocatableRequiresContinuousDwell_EmitsOnce_AndResetStartsNewAttempt()
        {
            var interactor = NewInteractor(InputModality.ControllerRay);
            var locatableObject = NewObject("locatable_target");
#if UNITY_EDITOR
            LogAssert.Expect(LogType.Error, "[LocatableTarget] locatable_target: targetId must use lowercase snake_case.");
#endif
            var locatable = locatableObject.AddComponent<LocatableTarget>();
            locatable.Configure("target_panel");
            locatableObject.SetActive(true);

            Assert.IsTrue(locatable.BeginDwell(interactor));
            yield return new WaitForSecondsRealtime(0.45f);
            locatable.EndDwell(interactor);
            yield return new WaitForSecondsRealtime(0.7f);
            Assert.IsFalse(locatable.WasLocated);
            Assert.AreEqual(0, reports.Count);

            Assert.IsTrue(locatable.BeginDwell(interactor));
            yield return new WaitForSecondsRealtime(1.1f);
            Assert.IsTrue(locatable.WasLocated);
            Assert.AreEqual(1, reports.Count);
            AssertReport(0, EventNames.TargetLocated, "target_panel", InputModality.ControllerRay);
            Assert.IsFalse(locatable.BeginDwell(interactor));
            Assert.AreEqual(1, reports.Count);

            locatable.ResetForAttempt();
            Assert.IsTrue(locatable.BeginDwell(interactor));
            yield return new WaitForSecondsRealtime(1.1f);
            Assert.IsTrue(locatable.WasLocated);
            Assert.AreEqual(2, reports.Count);
            AssertReport(1, EventNames.TargetLocated, "target_panel", InputModality.ControllerRay);
        }

        private T NewControl<T>(string targetId) where T : ControlTarget
        {
            var instance = NewObject(typeof(T).Name);
#if UNITY_EDITOR
            LogAssert.Expect(LogType.Error, $"[{typeof(T).Name}] {instance.name}: targetId must use lowercase snake_case.");
#endif
            var component = instance.AddComponent<T>();
            component.Configure(targetId);
            instance.SetActive(true);
            return component;
        }

        private InteractorMarker NewInteractor(string modality)
        {
            var root = NewObject("InteractorRoot");
            var tag = root.AddComponent<InputModalityTag>();
            tag.Configure(modality);
            var child = NewObject("Interactor", root.transform);
            var marker = child.AddComponent<InteractorMarker>();
            root.SetActive(true);
            child.SetActive(true);
            return marker;
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

        private void AssertReport(int index, string eventName, string targetId, string input)
        {
            var report = reports[index];
            Assert.AreEqual(eventName, report.EventName);
            Assert.AreEqual(targetId, report.TargetId);
            Assert.AreEqual(input, report.Input);
        }

        private sealed class InteractorMarker : MonoBehaviour { }
    }
}
