using System.Collections.Generic;
using Astra.Core.Assessment;
using Astra.XR.Input;
using Astra.XR.Interactables;
using Astra.XR.Phone;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Astra.XR.Tests.PlayMode
{
    internal sealed class GazeFakeTarget : MonoBehaviour, IGazeTarget
    {
        public string Id = "fake_button";
        public bool Enabled = true;
        public int HoverOn, HoverOff, Activations;
        public string TargetId => Id;
        public bool IsGazeEnabled => Enabled;
        public void SetGazeHover(bool on) { if (on) HoverOn++; else HoverOff++; }
        public void ActivateFromGaze() => Activations++;
    }

    internal sealed class GazeFakePanel : MonoBehaviour, IGazeTarget, IGazeLocatable
    {
        public int Activations;
        public string TargetId => "fake_panel";
        public bool IsGazeEnabled => true;
        public float GazeLocateSeconds => 1f;
        public void SetGazeHover(bool on) { }
        public void ActivateFromGaze() => Activations++;
    }


    public class GazeInteractorTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly List<InteractionReport> reports = new List<InteractionReport>();
        private static readonly Ray Forward = new Ray(Vector3.zero, Vector3.forward);
        private static readonly Ray Up = new Ray(Vector3.zero, Vector3.up);

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
        public void HoverEnterAndExitFireOnceEach()
        {
            var gaze = Gaze(dwell: false);
            var target = Target<GazeFakeTarget>(new Vector3(0f, 0f, 1f));
            for (var i = 0; i < 5; i++) gaze.Tick(Forward, false, false, 0.02f);
            Assert.That(target.HoverOn, Is.EqualTo(1));
            Assert.That(gaze.Hovered, Is.SameAs(target));
            for (var i = 0; i < 5; i++) gaze.Tick(Up, false, false, 0.02f);
            Assert.That(target.HoverOff, Is.EqualTo(1));
            Assert.That(gaze.Hovered, Is.Null);
        }

        [Test]
        public void DwellSelectsAfterConfiguredTimeOnce()
        {
            var gaze = Gaze(dwell: true);
            gaze.DwellSelectSeconds = 1.5f;
            var target = Target<GazeFakeTarget>(new Vector3(0f, 0f, 1f));
            for (var i = 0; i < 14; i++) gaze.Tick(Forward, false, false, 0.1f);
            Assert.That(target.Activations, Is.EqualTo(0), "not before 1.5 s");
            Assert.That(gaze.DwellProgress, Is.GreaterThan(0.8f));
            for (var i = 0; i < 30; i++) gaze.Tick(Forward, false, false, 0.1f);
            Assert.That(target.Activations, Is.EqualTo(1), "once per hover");
        }

        [Test]
        public void TapSelectsTheHoveredTarget()
        {
            var gaze = Gaze(dwell: false);
            var target = Target<GazeFakeTarget>(new Vector3(0f, 0f, 1f));
            gaze.Tick(Forward, false, false, 0.02f);
            gaze.Tick(Forward, true, true, 0.02f);
            gaze.Tick(Forward, false, true, 0.02f);
            gaze.Tick(Forward, false, false, 0.02f);
            Assert.That(target.Activations, Is.EqualTo(1));
        }

        [Test]
        public void DisabledTargetIsIgnored()
        {
            var gaze = Gaze(dwell: true);
            var target = Target<GazeFakeTarget>(new Vector3(0f, 0f, 1f));
            target.Enabled = false;
            for (var i = 0; i < 40; i++) gaze.Tick(Forward, i == 3, i >= 3 && i < 5, 0.1f);
            Assert.That(target.HoverOn, Is.EqualTo(0));
            Assert.That(target.Activations, Is.EqualTo(0));
            Assert.That(gaze.Hovered, Is.Null);
        }

        [Test]
        public void NothingThrowsWithNoTarget()
        {
            var gaze = Gaze(dwell: true);
            var empty = 0;
            gaze.onEmptyTap.AddListener(() => empty++);
            Assert.DoesNotThrow(() =>
            {
                for (var i = 0; i < 10; i++) gaze.Tick(Up, i == 2, i == 2 || i == 3, 0.05f);
            });
            Assert.That(gaze.Hovered, Is.Null);
            Assert.That(empty, Is.EqualTo(1), "a short tap on nothing is an empty tap");
        }

        [Test]
        public void LocatablePanelLocatesAfterOneSecondEvenWithDwellSelectOff()
        {
            var gaze = Gaze(dwell: false);
            var panel = Target<GazeFakePanel>(new Vector3(0f, 0f, 1f));
            for (var i = 0; i < 9; i++) gaze.Tick(Forward, false, false, 0.1f);
            Assert.That(panel.Activations, Is.EqualTo(0));
            for (var i = 0; i < 5; i++) gaze.Tick(Forward, false, false, 0.1f);
            Assert.That(panel.Activations, Is.EqualTo(1));
        }

        [Test]
        public void LongPressRecentersOnceAndIsNotAnEmptyTap()
        {
            var gaze = Gaze(dwell: false);
            var empty = 0;
            gaze.onEmptyTap.AddListener(() => empty++);
            gaze.Tick(Up, true, true, 0.05f);
            for (var i = 0; i < 30; i++) gaze.Tick(Up, false, true, 0.05f);
            gaze.Tick(Up, false, false, 0.05f);
            Assert.That(empty, Is.EqualTo(0));
            Assert.That(reports.Count, Is.EqualTo(1));
            Assert.That(reports[0].EventName, Is.EqualTo(EventNames.Recentered));
            Assert.That(reports[0].TargetId, Is.EqualTo(SystemTargets.System));
            Assert.That(reports[0].Input, Is.EqualTo(InputModality.Gaze));
        }

        [Test]
        public void XrControlTargetReportsGazeModalityOnTap()
        {
            var gaze = Gaze(dwell: false);
            var obj = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            obj.SetActive(false);
            obj.transform.position = new Vector3(0f, 0f, 1f);
            obj.transform.localScale = Vector3.one * 0.1f;
#if UNITY_EDITOR
            LogAssert.Expect(LogType.Error, "[ButtonTarget] Cube: targetId must use lowercase snake_case.");
#endif
            obj.AddComponent<ButtonTarget>().Configure("ready_button");
            obj.SetActive(true);
            Physics.SyncTransforms();
            gaze.Tick(Forward, false, false, 0.02f);
            gaze.Tick(Forward, true, true, 0.02f);
            Assert.That(reports.Count, Is.EqualTo(1));
            Assert.That(reports[0].TargetId, Is.EqualTo("ready_button"));
            Assert.That(reports[0].Input, Is.EqualTo(InputModality.Gaze));
        }

        [Test]
        public void LabelFallsBackToTargetIdWithoutTypeSuffix()
        {
            var target = Target<GazeFakeTarget>(new Vector3(0f, 0f, 5f));
            target.Id = "cabin_lights_toggle";
            Assert.That(GazeInteractor.LabelFor(target), Is.EqualTo("CABIN LIGHTS"));
        }

        private void Record(InteractionReport report) => reports.Add(report);

        private GazeInteractor Gaze(bool dwell)
        {
            var root = Track(new GameObject("gaze rig"));
            root.SetActive(false);
            var actor = new GameObject("gaze actor").AddComponent<InputModalityTag>();
            actor.transform.SetParent(root.transform, false);
            actor.Configure(InputModality.Gaze);
            var gaze = root.AddComponent<GazeInteractor>();
            gaze.Configure(root.transform, actor, null, null);
            gaze.UseGlobalDwellSetting(false);
            gaze.DwellSelectEnabled = dwell;
            root.SetActive(true);
            gaze.enabled = false; // drive it with Tick only
            return gaze;
        }

        private T Target<T>(Vector3 position) where T : Component
        {
            var obj = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            obj.transform.position = position;
            obj.transform.localScale = Vector3.one * 0.2f;
            var target = obj.AddComponent<T>();
            Physics.SyncTransforms();
            return target;
        }

        private GameObject Track(GameObject obj)
        {
            objects.Add(obj);
            return obj;
        }
    }
}
