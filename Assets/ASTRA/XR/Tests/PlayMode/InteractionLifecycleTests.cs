using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Astra.Core.Assessment;
using Astra.XR.Input;
using Astra.XR.Interactables;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;

namespace Astra.XR.Tests.PlayMode
{
    public class InteractionLifecycleTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly List<InteractionReport> reports = new List<InteractionReport>();

        [SetUp]
        public void SetUp() => InteractionEvents.Reported += Record;

        [TearDown]
        public void TearDown()
        {
            InteractionEvents.Reported -= Record;
            foreach (var obj in objects)
                if (obj != null) Object.DestroyImmediate(obj);
            objects.Clear();
            reports.Clear();
        }

        [Test]
        public void DialCannotActivateWithoutTriggerAngle()
        {
            var dial = Control<DialTarget>("dial");
            Assert.That(dial.TryActivate(Actor()), Is.False);
            Assert.That(reports, Is.Empty);
        }

        [UnityTest]
        public IEnumerator DialRetainsOneShotTriggerAcrossCooldown()
        {
            var actor = Actor();
            var dial = Control<DialTarget>("dial");
            Assert.That(dial.SetAngle(90f, actor), Is.True);
            Assert.That(dial.SetAngle(0f, actor), Is.False);
            Assert.That(dial.SetAngle(90f, actor), Is.False);
            yield return new WaitForSecondsRealtime(0.4f);
            AssertReports((EventNames.ControlActivated, "dial"), (EventNames.ControlActivated, "dial"));
            yield return new WaitForSecondsRealtime(0.35f);
            Assert.That(reports.Count, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator DialCancelsPendingTriggerWhenMovedAway()
        {
            var actor = Actor();
            var dial = Control<DialTarget>("dial");
            Assert.That(dial.SetAngle(90f, actor), Is.True);
            dial.SetAngle(0f, actor);
            dial.SetAngle(90f, actor);
            dial.SetAngle(180f, actor);
            yield return new WaitForSecondsRealtime(0.4f);
            AssertReports((EventNames.ControlActivated, "dial"));
        }

        [UnityTest]
        public IEnumerator OtherActorCannotTakePendingDialActivation()
        {
            var actor = Actor();
            var otherActor = Actor();
            otherActor.GetComponentInParent<InputModalityTag>().Configure(InputModality.HandPinch);
            var dial = Control<DialTarget>("dial");
            Assert.That(dial.SetAngle(90f, actor), Is.True);
            dial.SetAngle(0f, actor);
            dial.SetAngle(90f, actor);

            var field = typeof(ControlTarget).GetField("onPressedChanged", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            var callback = (UnityEvent<bool>)field.GetValue(dial);
            var attempted = false;
            var stolen = true;
            callback.AddListener(pressed =>
            {
                if (pressed || attempted)
                    return;
                attempted = true;
                stolen = dial.TryActivate(otherActor);
            });

            yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(attempted, Is.True);
            Assert.That(stolen, Is.False);
            AssertReports((EventNames.ControlActivated, "dial"), (EventNames.ControlActivated, "dial"));
        }

        [UnityTest]
        public IEnumerator DisabledInteractorReleasesHeldToolOnce()
        {
            var actor = Actor();
            var tool = Tool();
            Assert.That(tool.BeginGrab(actor), Is.True);
            actor.gameObject.SetActive(false);
            yield return null;
            yield return null;
            Assert.That(tool.IsHeld, Is.False);
            AssertReports((EventNames.ObjectGrabbed, "tool"), (EventNames.ObjectReleased, "tool"));
        }

        [UnityTest]
        public IEnumerator DestroyedInteractorReleasesHeldToolOnce()
        {
            var actor = Actor();
            var tool = Tool();
            Assert.That(tool.BeginGrab(actor), Is.True);
            Object.Destroy(actor.gameObject);
            yield return null;
            yield return null;
            Assert.That(tool.IsHeld, Is.False);
            AssertReports((EventNames.ObjectGrabbed, "tool"), (EventNames.ObjectReleased, "tool"));
        }

        [Test]
        public void ResetDuringLeverReportLeavesFreshAttemptReady()
        {
            var actor = Actor();
            var lever = Control<LeverTarget>("lever");
            void Reset(InteractionReport report) => lever.ResetForAttempt();
            InteractionEvents.Reported += Reset;
            try { Assert.That(lever.SetPull(1f, actor), Is.True); }
            finally { InteractionEvents.Reported -= Reset; }
            Assert.That(lever.Pull, Is.EqualTo(0f));
            Assert.That(lever.SetPull(1f, actor), Is.True);
            AssertReports((EventNames.ControlActivated, "lever"), (EventNames.ControlActivated, "lever"));
        }

        [Test]
        public void ToggleResetCallbackCannotReenterActivation()
        {
            var actor = Actor();
            var toggle = Control<ToggleTarget>("toggle");
            var field = typeof(ToggleTarget).GetField("onStateChanged", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            var callback = (UnityEvent<bool>)field.GetValue(toggle);
            var attempted = false;
            var activated = true;
            callback.AddListener(state => { if (!state) { attempted = true; activated = toggle.TryActivate(actor); } });
            Assert.That(toggle.TryActivate(actor), Is.True);
            toggle.ResetForAttempt();
            Assert.That(attempted, Is.True);
            Assert.That(activated, Is.False);
            Assert.That(toggle.IsOn, Is.False);
            AssertReports((EventNames.ControlActivated, "toggle"));
        }

        [Test]
        public void ResetDuringPlacementDoesNotChangeSuccessfulReturn()
        {
            var actor = Actor();
            var tool = Tool();
            var obj = NewInactive("socket");
#if UNITY_EDITOR
            LogAssert.Expect(LogType.Error, "[PlacementSocket] socket: socketId and acceptsToolId must follow content ID rules.");
#endif
            var socket = obj.AddComponent<PlacementSocket>();
            socket.Configure("socket", "tool");
            obj.SetActive(true);
            void Reset(InteractionReport report)
            {
                if (report.EventName == EventNames.ObjectPlaced) tool.ResetForAttempt();
            }
            InteractionEvents.Reported += Reset;
            try
            {
                Assert.That(tool.BeginGrab(actor), Is.True);
                Assert.That(socket.TryPlace(tool, actor), Is.True);
            }
            finally { InteractionEvents.Reported -= Reset; }
            Assert.That(socket.Occupant, Is.Null);
            Assert.That(tool.CurrentSocket, Is.Null);
            AssertReports((EventNames.ObjectGrabbed, "tool"), (EventNames.ObjectPlaced, "socket"));
        }

        private void Record(InteractionReport report) => reports.Add(report);

        private Transform Actor()
        {
            var parent = NewInactive("actor");
            parent.AddComponent<InputModalityTag>().Configure(InputModality.ControllerGrip);
            var child = new GameObject("interactor");
            child.transform.SetParent(parent.transform);
            parent.SetActive(true);
            return child.transform;
        }

        private T Control<T>(string id) where T : ControlTarget
        {
            var obj = NewInactive(id);
#if UNITY_EDITOR
            LogAssert.Expect(LogType.Error, $"[{typeof(T).Name}] {id}: targetId must use lowercase snake_case.");
#endif
            var control = obj.AddComponent<T>();
            control.Configure(id);
            obj.SetActive(true);
            return control;
        }

        private GrabbableTool Tool()
        {
            var obj = NewInactive("tool");
#if UNITY_EDITOR
            LogAssert.Expect(LogType.Error, "[GrabbableTool] tool: missing/invalid targetId.");
#endif
            var tool = obj.AddComponent<GrabbableTool>();
            tool.Configure("tool");
            obj.SetActive(true);
            return tool;
        }

        private GameObject NewInactive(string name)
        {
            var obj = new GameObject(name);
            obj.SetActive(false);
            objects.Add(obj);
            return obj;
        }

        private void AssertReports(params (string eventName, string targetId)[] expected)
        {
            Assert.That(reports.Count, Is.EqualTo(expected.Length));
            for (var i = 0; i < expected.Length; i++)
            {
                Assert.That(reports[i].EventName, Is.EqualTo(expected[i].eventName));
                Assert.That(reports[i].TargetId, Is.EqualTo(expected[i].targetId));
                Assert.That(reports[i].Input, Is.EqualTo(InputModality.ControllerGrip));
            }
        }
    }
}
