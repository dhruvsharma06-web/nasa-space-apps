using System.Collections;
using System.Collections.Generic;
using Astra.Core.Assessment;
using Astra.Core.Mission;
using Astra.DesktopTest;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Astra.App.Tests.PlayMode
{
    /// <summary>
    /// Plays every level in the build like a first-time user: welcome screen waits, Start, skip to Practice,
    /// aim at each step's control from the seat (nothing may block the line of sight), click it, and the
    /// checklist must advance; then Qualification the same way, and the Debrief must say PASSED.
    /// Editor PlayMode evidence only, not headset evidence.
    /// </summary>
    public class FirstTimeUserPlaythroughTests
    {
        private static IEnumerable<string> LevelScenes()
        {
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i));
                if (name.Length > 1 && name[0] == 'L' && char.IsDigit(name[1])) yield return name;
            }
        }

        [UnityTest]
        public IEnumerator EveryLevel_CanBeCompletedByAimingAndClickingTheNextControl()
        {
            var failures = new List<string>();
            int levels = 0;
            foreach (var scene in LevelScenes())
            {
                levels++;
                SceneManager.LoadScene(scene);
                for (int f = 0; f < 3; f++) yield return null;
                var level = Object.FindAnyObjectByType<LevelController>();
                var hud = Object.FindAnyObjectByType<DesktopHud>();
                var cam = Camera.main;
                if (level == null || hud == null || cam == null) { failures.Add($"{scene}: missing LevelController/DesktopHud/camera"); continue; }

                // 1. the welcome screen holds the level
                yield return new WaitForSecondsRealtime(1.5f);
                if (level.Phase != ModulePhase.Briefing || !level.IsPaused) failures.Add($"{scene}: level did not wait on the welcome screen (phase {level.Phase}, paused {level.IsPaused})");
                hud.StartMission();
                yield return null;
                if (level.IsPaused) failures.Add($"{scene}: Start did not resume the level");

                // 2. practice, then 3. qualification
                level.SkipToPractice();
                yield return null;
                if (level.Phase != ModulePhase.Practice) { failures.Add($"{scene}: Skip to practice failed (phase {level.Phase})"); continue; }
                yield return PlayAllSteps(scene, "Practice", level, cam, failures);

                float until = Time.realtimeSinceStartup + 10f;
                while (level.Phase != ModulePhase.Qualification && Time.realtimeSinceStartup < until) yield return null;
                if (level.Phase != ModulePhase.Qualification) { failures.Add($"{scene}: Qualification never started (phase {level.Phase})"); continue; }
                yield return null;
                yield return PlayAllSteps(scene, "Qualification", level, cam, failures);

                until = Time.realtimeSinceStartup + 3f;
                while (level.Phase == ModulePhase.Qualification && Time.realtimeSinceStartup < until) yield return null;
                if (level.Phase == ModulePhase.Qualification) level.Finish();
                yield return null;
                if (level.Phase != ModulePhase.Debrief) { failures.Add($"{scene}: no Debrief (phase {level.Phase})"); continue; }
                if (level.LastResult == null || !level.LastResult.passed)
                    failures.Add($"{scene}: a perfect run did not pass (omitted {string.Join(",", level.LastResult?.omittedCriticalSteps ?? new List<string>())})");
            }
            Assert.Greater(levels, 0, "no level scenes in the build settings");
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        private static IEnumerator PlayAllSteps(string scene, string phase, LevelController level, Camera cam, List<string> failures)
        {
            for (int guard = 0; guard < 40; guard++)
            {
                var step = level.Runner?.CurrentStep;
                if (step == null) yield break;
                if (!TestTarget.Registry.TryGetValue(step.targetId, out var target)) { failures.Add($"{scene}/{phase}: step '{step.stepId}' has no control '{step.targetId}' in the scene"); yield break; }

                if (target.TargetKind == TestTarget.Kind.Socket && TestTarget.Held == null && TestTarget.Registry.TryGetValue(target.Accepts ?? "", out var tool))
                    yield return Click(scene, phase, tool, cam, failures);   // pick up the tool first, like a user would

                yield return Click(scene, phase, target, cam, failures);
                yield return null;
                var after = level.Runner?.CurrentStep;
                if (after != null && after.stepId == step.stepId) { failures.Add($"{scene}/{phase}: clicking '{step.targetId}' did not complete step '{step.stepId}'"); yield break; }
            }
            failures.Add($"{scene}/{phase}: steps never finished");
        }

        /// <summary>Aims from the seat at the control and clicks it: the first thing the ray hits must be that control.</summary>
        private static IEnumerator Click(string scene, string phase, TestTarget target, Camera cam, List<string> failures)
        {
            Physics.SyncTransforms();
            var col = target.GetComponent<Collider>();
            var aim = col != null && col.enabled ? col.bounds.center : target.transform.position;
            var dir = aim - cam.transform.position;
            float angle = Vector3.Angle(Vector3.ProjectOnPlane(dir, Vector3.up), Vector3.forward);
            if (angle > 100f) failures.Add($"{scene}/{phase}: '{target.TargetId}' is behind the user ({angle:0}°)");
            if (Physics.Raycast(cam.transform.position, dir.normalized, out var hit, 4f))
            {
                var hitTarget = hit.collider.GetComponentInParent<TestTarget>();
                if (hitTarget != target) failures.Add($"{scene}/{phase}: aiming at '{target.TargetId}' hits '{(hitTarget != null ? hitTarget.TargetId : hit.collider.name)}' instead");
            }
            else failures.Add($"{scene}/{phase}: '{target.TargetId}' cannot be hit from the seat");
            target.Activate();
            yield return null;
        }
    }
}
