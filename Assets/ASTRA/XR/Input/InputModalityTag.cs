using System;
using Astra.Core.Assessment;
using UnityEngine;

namespace Astra.XR.Input
{
    /// <summary>The actual input route of an interactor, including simulated controllers.</summary>
    [DisallowMultipleComponent]
    public sealed class InputModalityTag : MonoBehaviour
    {
        [SerializeField] private string modality = InputModality.ControllerRay;
        [SerializeField] private bool simulatorInput;

        public string Modality => simulatorInput ? InputModality.Simulator : modality;

        public void Configure(string value, bool isSimulator = false)
        {
            if (!IsKnown(value))
                throw new ArgumentException("Input modality must be a CONTRACT.md InputModality value.", nameof(value));

            modality = value;
            simulatorInput = isSimulator;
        }

        public static bool IsKnown(string value)
        {
            return value == InputModality.ControllerRay || value == InputModality.ControllerGrip ||
                   value == InputModality.HandPinch || value == InputModality.HandGrab ||
                   value == InputModality.Simulator || value == InputModality.Gaze;
        }

        public static bool TryResolve(Component interactor, out string input)
        {
            input = null;
            if (interactor == null || !interactor.gameObject.activeInHierarchy ||
                (interactor is Behaviour behaviour && !behaviour.isActiveAndEnabled))
                return false;

            var tag = interactor.GetComponentInParent<InputModalityTag>();
            if (tag == null || !tag.isActiveAndEnabled || !IsKnown(tag.Modality))
                return false;

            input = tag.Modality;
            return true;
        }

        private void Awake()
        {
            if (!IsKnown(Modality))
            {
                Debug.LogError($"[InputModalityTag] {name}: invalid modality '{modality}'.", this);
                enabled = false;
            }
        }

        private void OnValidate()
        {
            if (!IsKnown(modality))
                Debug.LogError($"[InputModalityTag] {name}: modality must use an InputModality constant.", this);
        }
    }
}
