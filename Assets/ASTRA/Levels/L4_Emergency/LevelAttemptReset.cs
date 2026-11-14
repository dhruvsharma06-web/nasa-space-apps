using System.Collections.Generic;
using Astra.App;
using Astra.Core.Mission;
using Astra.XR.Interactables;
using UnityEngine;

namespace Astra.Levels
{
    /// <summary>
    /// Puts this level's controls back to their starting state at the start of every attempt (OWNER: Core Logic;
    /// used by L4 and L7). Without it, the BUS B dial would still point at ISOLATE when Qualification begins.
    /// Calls P1's ResetForAttempt() on each control, locatable target and tool when the LevelController enters
    /// Briefing (restart), Practice or Qualification. Resetting produces no learner events (XR README).
    /// </summary>
    public class LevelAttemptReset : MonoBehaviour
    {
        [SerializeField] private LevelController levelController;

        [Tooltip("Also reset every control, locatable target and tool found under this object.")]
        [SerializeField] private bool includeChildren = true;

        [Header("Extra objects to reset (outside this object's children)")]
        [SerializeField] private ControlTarget[] controls = new ControlTarget[0];
        [SerializeField] private LocatableTarget[] locatables = new LocatableTarget[0];
        [SerializeField] private GrabbableTool[] tools = new GrabbableTool[0];

        private readonly List<ControlTarget> _controls = new List<ControlTarget>();
        private readonly List<LocatableTarget> _locatables = new List<LocatableTarget>();
        private readonly List<GrabbableTool> _tools = new List<GrabbableTool>();

        /// <summary>How many objects are reset each attempt (for checks in the Inspector or tests).</summary>
        public int Count => _controls.Count + _locatables.Count + _tools.Count;

        private void Awake()
        {
            if (levelController == null)
                Debug.LogError($"[LevelAttemptReset] '{name}': 'Level Controller' is not assigned; controls will not reset between attempts.", this);

            // Collected once here, never per frame.
            AddUnique(_controls, controls);
            AddUnique(_locatables, locatables);
            AddUnique(_tools, tools);
            if (includeChildren)
            {
                AddUnique(_controls, GetComponentsInChildren<ControlTarget>(true));
                AddUnique(_locatables, GetComponentsInChildren<LocatableTarget>(true));
                AddUnique(_tools, GetComponentsInChildren<GrabbableTool>(true));
            }
            if (Count == 0)
                Debug.LogWarning($"[LevelAttemptReset] '{name}': nothing to reset. Put the panel's controls under this object or list them.", this);
        }

        private void OnEnable()
        {
            if (levelController != null)
                levelController.PhaseChanged += OnPhaseChanged;
        }

        private void OnDisable()
        {
            if (levelController != null)
                levelController.PhaseChanged -= OnPhaseChanged;
        }

        private void OnPhaseChanged(ModulePhase phase)
        {
            if (phase == ModulePhase.Briefing || phase == ModulePhase.Practice || phase == ModulePhase.Qualification)
                ResetAll();
        }

        /// <summary>Resets every collected object. Safe to call at any time.</summary>
        public void ResetAll()
        {
            foreach (var control in _controls)
            {
                if (control != null)
                    control.ResetForAttempt();
            }
            foreach (var locatable in _locatables)
            {
                if (locatable != null)
                    locatable.ResetForAttempt();
            }
            foreach (var tool in _tools)
            {
                if (tool != null)
                    tool.ResetForAttempt();
            }
        }

        private static void AddUnique<T>(List<T> into, T[] items) where T : Object
        {
            if (items == null)
                return;
            foreach (var item in items)
            {
                if (item != null && !into.Contains(item))
                    into.Add(item);
            }
        }
    }
}
