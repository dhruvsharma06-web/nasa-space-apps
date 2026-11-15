using System.Collections.Generic;
using Astra.Core.Assessment;
using Astra.XR.Input;
using Astra.XR.Interactables;
using Astra.XR.Rig;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Astra.XR.Tests.PlayMode
{
    public class GazeTapTests
    {
        // Fixtures run far from the origin so leftover colliders / world canvases from other fixtures in the same
        // PlayMode run can never sit on the ray (they made the tap tests pass alone but fail in the full suite).
        private static readonly Vector3 Origin = new Vector3(4000f, 4000f, 4000f);
        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly List<InteractionReport> reports = new List<InteractionReport>();

        [SetUp]
        public void SetUp()
        {
            InteractionEvents.Reported += Record;
            WorldUiRaycast.Invalidate();
        }

        [TearDown]
        public void TearDown()
        {
            InteractionEvents.Reported -= Record;
            foreach (var obj in objects)
                if (obj != null) Object.DestroyImmediate(obj);
            objects.Clear();
            reports.Clear();
            WorldUiRaycast.Invalidate();
        }

        [Test]
        public void TapOnControlActivatesOnceAsRayAndDoesNotOpenMenu()
        {
            var gaze = Gaze(out var actor);
            Button("start_button", new Vector3(0f, 0f, 1f));
            var ray = new Ray(Origin, Vector3.forward);

            Assert.That(gaze.Step(ray, true, false, actor), Is.EqualTo(GazeTap.Outcome.None));
            Assert.That(gaze.PointsAtTarget, Is.True);
            Assert.That(gaze.Step(ray, true, true, actor), Is.EqualTo(GazeTap.Outcome.Interacted));
            gaze.Step(ray, true, false, actor);

            Assert.That(reports.Count, Is.EqualTo(1));
            Assert.That(reports[0].EventName, Is.EqualTo(EventNames.ControlActivated));
            Assert.That(reports[0].TargetId, Is.EqualTo("start_button"));
            Assert.That(reports[0].Input, Is.EqualTo(InputModality.ControllerRay));
        }

        [Test]
        public void TapOnEmptySpaceRequestsMenuWithoutAnyReport()
        {
            var gaze = Gaze(out var actor);
            Button("start_button", new Vector3(0f, 0f, 1f));
            var away = new Ray(Origin, Vector3.up);
            Assert.That(gaze.Step(away, true, true, actor), Is.EqualTo(GazeTap.Outcome.Menu));
            Assert.That(reports, Is.Empty);
        }

        [Test]
        public void TapGrabsToolThenSecondTapOverSocketPlacesIt()
        {
            var gaze = Gaze(out var actor);
            var tool = Tool("practice_tool", new Vector3(0f, 0f, 0.5f));
            Socket("tool_socket", "practice_tool", new Vector3(0.3f, 0f, 0.5f));
            Physics.SyncTransforms();

            var atTool = new Ray(Origin, Vector3.forward);
            Assert.That(gaze.Step(atTool, true, true, actor), Is.EqualTo(GazeTap.Outcome.Interacted));
            Assert.That(tool.IsHeld, Is.True);

            var atSocket = new Ray(Origin, new Vector3(0.3f, 0f, 0.5f).normalized);
            gaze.Step(atSocket, true, false, actor);
            Assert.That(tool.IsHeld, Is.True, "the tool stays held between taps");
            Assert.That(gaze.Step(atSocket, true, true, actor), Is.EqualTo(GazeTap.Outcome.Interacted));

            Assert.That(tool.IsHeld, Is.False);
            Assert.That(tool.CurrentSocket, Is.Not.Null);
            Assert.That(reports.Count, Is.EqualTo(2));
            Assert.That(reports[0].EventName, Is.EqualTo(EventNames.ObjectGrabbed));
            Assert.That(reports[0].Input, Is.EqualTo(InputModality.ControllerRay));
            Assert.That(reports[1].EventName, Is.EqualTo(EventNames.ObjectPlaced));
            Assert.That(reports[1].TargetId, Is.EqualTo("tool_socket"));
        }

        [Test]
        public void SecondTapAwayFromSocketReleasesAndNeverOpensMenu()
        {
            var gaze = Gaze(out var actor);
            var tool = Tool("practice_tool", new Vector3(0f, 0f, 0.5f));
            Physics.SyncTransforms();
            var ray = new Ray(Origin, Vector3.forward);
            gaze.Step(ray, true, true, actor);
            var up = new Ray(Origin, Vector3.up);
            gaze.Step(up, true, false, actor);
            Assert.That(gaze.Step(up, true, true, actor), Is.EqualTo(GazeTap.Outcome.Interacted));
            Assert.That(tool.IsHeld, Is.False);
            Assert.That(reports.Count, Is.EqualTo(2));
            Assert.That(reports[1].EventName, Is.EqualTo(EventNames.ObjectReleased));
        }

        [Test]
        public void TrackingLossWhileHoldingEndsGrabOnce()
        {
            var gaze = Gaze(out var actor);
            var tool = Tool("practice_tool", new Vector3(0f, 0f, 0.5f));
            Physics.SyncTransforms();
            var ray = new Ray(Origin, Vector3.forward);
            gaze.Step(ray, true, true, actor);
            Assert.That(gaze.Step(ray, false, true, actor), Is.EqualTo(GazeTap.Outcome.None));
            gaze.Step(ray, false, false, actor);
            Assert.That(tool.IsHeld, Is.False);
            Assert.That(reports.Count, Is.EqualTo(2));
            Assert.That(reports[1].EventName, Is.EqualTo(EventNames.ObjectReleased));
        }

        [Test]
        public void TapOnWorldUiButtonClicksItOnceWithTheGazeActor()
        {
            var gaze = Gaze(out var actor);
            var button = UiButton(new Vector3(0f, 0f, 1f));
            Component seen = null;
            var clicks = 0;
            button.onClick.AddListener(() =>
            {
                clicks++;
                seen = UiActionActor.Current;
            });
            WorldUiRaycast.Invalidate();
            var ray = new Ray(Origin, Vector3.forward);
            Assert.That(gaze.Step(ray, true, true, actor), Is.EqualTo(GazeTap.Outcome.Interacted));
            gaze.Step(ray, true, false, actor);
            Assert.That(clicks, Is.EqualTo(1));
            Assert.That(seen, Is.SameAs(actor));
        }

        private void Record(InteractionReport report) => reports.Add(report);

        private GazeTap Gaze(out InputModalityTag actor)
        {
            var root = Track(new GameObject("gaze"));
            var ray = root.AddComponent<RayInteractor>();
            actor = Track(new GameObject("gaze actor")).AddComponent<InputModalityTag>();
            actor.transform.SetParent(root.transform);
            actor.Configure(InputModality.ControllerRay);
            return new GazeTap(ray);
        }

        private Button UiButton(Vector3 position)
        {
            var canvasObject = Track(new GameObject("canvas", typeof(RectTransform)));
            var rect = (RectTransform)canvasObject.transform;
            rect.position = Origin + position;
            rect.sizeDelta = new Vector2(200f, 100f);
            rect.localScale = Vector3.one * 0.001f;
            canvasObject.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var buttonObject = new GameObject("button", typeof(RectTransform));
            var buttonRect = (RectTransform)buttonObject.transform;
            buttonRect.SetParent(rect, false);
            buttonRect.sizeDelta = new Vector2(200f, 100f);
            var image = buttonObject.AddComponent<Image>();
            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            Physics.SyncTransforms();
            WorldUiRaycast.Invalidate();
            return button;
        }

        private void Button(string id, Vector3 position)
        {
            var obj = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            obj.name = id;
            obj.SetActive(false);
            obj.transform.position = Origin + position;
            obj.transform.localScale = Vector3.one * 0.1f;
#if UNITY_EDITOR
            LogAssert.Expect(LogType.Error, $"[ButtonTarget] {id}: targetId must use lowercase snake_case.");
#endif
            obj.AddComponent<ButtonTarget>().Configure(id);
            obj.SetActive(true);
            Physics.SyncTransforms();
        }

        private GrabbableTool Tool(string id, Vector3 position)
        {
            var obj = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            obj.name = id;
            obj.SetActive(false);
            obj.transform.position = Origin + position;
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
            obj.transform.position = Origin + position;
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
