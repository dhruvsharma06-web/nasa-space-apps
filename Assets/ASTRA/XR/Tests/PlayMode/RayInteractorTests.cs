using System.Collections.Generic;
using Astra.Core.Assessment;
using Astra.XR.Input;
using Astra.XR.Interactables;
using Astra.XR.Rig;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Astra.XR.Tests.PlayMode
{
    public class RayInteractorTests
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
        public void SelectPressActivatesPointedControlOnceWithActorModality()
        {
            var ray = Interactor(out var pinch, out _);
            Button("start_button", new Vector3(0f, 0f, 1f));
            var frame = Frame(Vector3.forward, pinch, null);
            frame.SelectDown = true;
            ray.Step(frame);
            frame.SelectDown = false;
            ray.Step(frame);
            Assert.That(reports.Count, Is.EqualTo(1));
            Assert.That(reports[0].EventName, Is.EqualTo(EventNames.ControlActivated));
            Assert.That(reports[0].TargetId, Is.EqualTo("start_button"));
            Assert.That(reports[0].Input, Is.EqualTo(InputModality.HandPinch));
        }

        [Test]
        public void GrabCarryAndReleaseOverSocketPlacesWithGrabModality()
        {
            var ray = Interactor(out var pinch, out var grab);
            var tool = Tool("practice_tool", new Vector3(0f, 0f, 0.5f));
            Socket("tool_socket", "practice_tool", new Vector3(0.3f, 0f, 0.5f));
            Physics.SyncTransforms();

            var frame = Frame(Vector3.forward, pinch, grab);
            frame.GrabDown = true;
            frame.GrabHeld = true;
            ray.Step(frame);
            Assert.That(tool.IsHeld, Is.True);

            frame.GrabDown = false;
            frame.Ray = new Ray(Vector3.zero, new Vector3(0.3f, 0f, 0.5f).normalized);
            ray.Step(frame);
            frame.GrabHeld = false;
            ray.Step(frame);

            Assert.That(tool.IsHeld, Is.False);
            Assert.That(tool.CurrentSocket, Is.Not.Null);
            Assert.That(reports.Count, Is.EqualTo(2));
            Assert.That(reports[0].EventName, Is.EqualTo(EventNames.ObjectGrabbed));
            Assert.That(reports[0].Input, Is.EqualTo(InputModality.HandGrab));
            Assert.That(reports[1].EventName, Is.EqualTo(EventNames.ObjectPlaced));
            Assert.That(reports[1].TargetId, Is.EqualTo("tool_socket"));
        }

        [Test]
        public void TrackingLossCancelsHeldGrabOnce()
        {
            var ray = Interactor(out var pinch, out var grab);
            var tool = Tool("practice_tool", new Vector3(0f, 0f, 0.5f));
            Physics.SyncTransforms();
            var frame = Frame(Vector3.forward, pinch, grab);
            frame.GrabDown = true;
            frame.GrabHeld = true;
            ray.Step(frame);
            frame.Valid = false;
            ray.Step(frame);
            ray.Step(frame);
            Assert.That(tool.IsHeld, Is.False);
            Assert.That(reports.Count, Is.EqualTo(2));
            Assert.That(reports[1].EventName, Is.EqualTo(EventNames.ObjectReleased));
        }

        private void Record(InteractionReport report) => reports.Add(report);

        private static RayInteractor.Frame Frame(Vector3 direction, Component select, Component grab) => new RayInteractor.Frame
        {
            Ray = new Ray(Vector3.zero, direction),
            Valid = true,
            SelectActor = select,
            GrabActor = grab,
            ClickUi = true
        };

        private RayInteractor Interactor(out InputModalityTag pinch, out InputModalityTag grab)
        {
            var root = Track(new GameObject("hand"));
            var ray = root.AddComponent<RayInteractor>();
            pinch = Track(new GameObject("pinch")).AddComponent<InputModalityTag>();
            pinch.transform.SetParent(root.transform);
            pinch.Configure(InputModality.HandPinch);
            grab = Track(new GameObject("grab")).AddComponent<InputModalityTag>();
            grab.transform.SetParent(root.transform);
            grab.Configure(InputModality.HandGrab);
            return ray;
        }

        private void Button(string id, Vector3 position)
        {
            var obj = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            obj.name = id;
            obj.SetActive(false);
            obj.transform.position = position;
            obj.transform.localScale = Vector3.one * 0.1f;
#if UNITY_EDITOR
            LogAssert.Expect(LogType.Error, $"[ButtonTarget] {id}: targetId must use lowercase snake_case.");
#endif
            obj.AddComponent<ButtonTarget>().Configure(id);
            obj.SetActive(true);
        }

        private GrabbableTool Tool(string id, Vector3 position)
        {
            var obj = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            obj.name = id;
            obj.SetActive(false);
            obj.transform.position = position;
            obj.transform.localScale = Vector3.one * 0.06f;
#if UNITY_EDITOR
            LogAssert.Expect(LogType.Error, $"[GrabbableTool] {id}: missing/invalid targetId.");
#endif
            var tool = obj.AddComponent<GrabbableTool>();
            tool.Configure(id);
            obj.SetActive(true);
            return tool;
        }

        private void Socket(string id, string accepts, Vector3 position)
        {
            var obj = Track(new GameObject(id));
            obj.SetActive(false);
            obj.transform.position = position;
            var trigger = obj.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(0.2f, 0.2f, 0.2f);
#if UNITY_EDITOR
            LogAssert.Expect(LogType.Error, $"[PlacementSocket] {id}: socketId and acceptsToolId must follow content ID rules.");
#endif
            obj.AddComponent<PlacementSocket>().Configure(id, accepts);
            obj.SetActive(true);
        }

        private GameObject Track(GameObject obj)
        {
            objects.Add(obj);
            return obj;
        }
    }
}
