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
    public class ToolSocketTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly List<InteractionReport> reports = new List<InteractionReport>();
        private float originalTimeScale;

        [SetUp]
        public void SetUp()
        {
            originalTimeScale = Time.timeScale;
            InteractionEvents.Reported += Record;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = originalTimeScale;
            InteractionEvents.Reported -= Record;
            foreach (var obj in objects)
                if (obj != null) Object.DestroyImmediate(obj);
            objects.Clear();
            reports.Clear();
        }

        [Test]
        public void ReleaseThenPlaceProducesOneOutcomePerGrab()
        {
            var actor = Actor();
            var tool = Tool("tool_a");
            var socket = Socket("socket_a", "tool_a");
            Assert.That(tool.BeginGrab(actor), Is.True);
            Assert.That(tool.EndGrab(actor), Is.True);
            Assert.That(tool.EndGrab(actor), Is.False);
            Assert.That(tool.BeginGrab(actor), Is.True);
            Assert.That(socket.TryPlace(tool, actor), Is.True);
            Assert.That(tool.EndGrab(actor), Is.False);
            Assert.That(socket.Occupant, Is.SameAs(tool));
            Assert.That(tool.CurrentSocket, Is.SameAs(socket));
            AssertReports((EventNames.ObjectGrabbed, "tool_a"), (EventNames.ObjectReleased, "tool_a"),
                (EventNames.ObjectGrabbed, "tool_a"), (EventNames.ObjectPlaced, "socket_a"));
        }

        [Test]
        public void WrongToolReleasesWithoutOccupyingSocket()
        {
            var actor = Actor();
            var tool = Tool("wrong_tool");
            var socket = Socket("socket_a", "tool_a");
            Assert.That(tool.BeginGrab(actor), Is.True);
            Assert.That(socket.TryPlace(tool, actor), Is.False);
            Assert.That(socket.Occupant, Is.Null);
            Assert.That(tool.CurrentSocket, Is.Null);
            Assert.That(tool.IsHeld, Is.False);
            AssertReports((EventNames.ObjectGrabbed, "wrong_tool"), (EventNames.ObjectReleased, "wrong_tool"));
        }

        [Test]
        public void OccupiedSocketDoesNotReplaceTool()
        {
            var actor = Actor();
            var first = Tool("tool_a");
            var second = Tool("tool_a");
            var socket = Socket("socket_a", "tool_a");
            Assert.That(first.BeginGrab(actor), Is.True);
            Assert.That(socket.TryPlace(first, actor), Is.True);
            Assert.That(second.BeginGrab(actor), Is.True);
            Assert.That(socket.TryPlace(second, actor), Is.False);
            Assert.That(socket.Occupant, Is.SameAs(first));
            Assert.That(first.CurrentSocket, Is.SameAs(socket));
            Assert.That(second.CurrentSocket, Is.Null);
            AssertReports((EventNames.ObjectGrabbed, "tool_a"), (EventNames.ObjectPlaced, "socket_a"),
                (EventNames.ObjectGrabbed, "tool_a"), (EventNames.ObjectReleased, "tool_a"));
        }

        [Test]
        public void OtherHolderCannotGrabOrReleaseHeldTool()
        {
            var owner = Actor();
            var other = Actor();
            var tool = Tool("tool_a");
            Assert.That(tool.BeginGrab(owner), Is.True);
            Assert.That(tool.BeginGrab(other), Is.False);
            Assert.That(tool.EndGrab(other), Is.False);
            Assert.That(tool.IsHeld, Is.True);
            AssertReports((EventNames.ObjectGrabbed, "tool_a"));
        }

        [Test]
        public void ResetDetachesAndReturnsHomeWithoutReport()
        {
            var actor = Actor();
            var tool = Tool("tool_a");
            var home = tool.transform.position;
            var socket = Socket("socket_a", "tool_a");
            socket.transform.position = new Vector3(2, 0, 0);
            Assert.That(tool.BeginGrab(actor), Is.True);
            Assert.That(socket.TryPlace(tool, actor), Is.True);
            reports.Clear();
            tool.ResetForAttempt();
            Assert.That(tool.CurrentSocket, Is.Null);
            Assert.That(socket.Occupant, Is.Null);
            Assert.That(tool.transform.position, Is.EqualTo(home));
            Assert.That(reports, Is.Empty);
        }

        [Test]
        public void DisablingSocketClearsBothReferencesWithoutReport()
        {
            var actor = Actor();
            var tool = Tool("tool_a");
            var socket = Socket("socket_a", "tool_a");
            Assert.That(tool.BeginGrab(actor), Is.True);
            Assert.That(socket.TryPlace(tool, actor), Is.True);
            reports.Clear();
            socket.gameObject.SetActive(false);
            Assert.That(socket.Occupant, Is.Null);
            Assert.That(tool.CurrentSocket, Is.Null);
            Assert.That(reports, Is.Empty);
        }

        [UnityTest]
        public IEnumerator OutOfReachReturnsAfterThreeUnscaledSecondsWithoutReport()
        {
            var actor = Actor();
            var tool = Tool("tool_a");
            var home = tool.transform.position;
            Assert.That(tool.BeginGrab(actor), Is.True);
            tool.transform.position = home + Vector3.right * 2f;
            Time.timeScale = 0f;
            Assert.That(tool.EndGrab(actor), Is.True);
            reports.Clear();
            yield return new WaitForSecondsRealtime(3.2f);
            yield return null;
            Assert.That(Vector3.Distance(tool.transform.position, home), Is.LessThan(0.001f));
            Assert.That(reports, Is.Empty);
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

        private GrabbableTool Tool(string id)
        {
            var obj = NewInactive(id);
#if UNITY_EDITOR
            LogAssert.Expect(LogType.Error, $"[GrabbableTool] {id}: missing/invalid targetId.");
#endif
            var tool = obj.AddComponent<GrabbableTool>();
            tool.Configure(id);
            obj.SetActive(true);
            return tool;
        }

        private PlacementSocket Socket(string id, string acceptedId)
        {
            var obj = NewInactive(id);
#if UNITY_EDITOR
            LogAssert.Expect(LogType.Error, $"[PlacementSocket] {id}: socketId and acceptsToolId must follow content ID rules.");
#endif
            var socket = obj.AddComponent<PlacementSocket>();
            socket.Configure(id, acceptedId);
            obj.SetActive(true);
            return socket;
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
