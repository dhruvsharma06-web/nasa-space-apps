using System;
using UnityEngine;
using UnityEngine.UI;

namespace Astra.UI
{
    /// <summary>World-space instruction and practice hint display.</summary>
    public class InstructionPanel : MonoBehaviour
    {
        [SerializeField] private Text instructionLabel;
        [SerializeField] private Text hintLabel;

        public void Configure(Text instruction, Text hint)
        {
            if (instruction == null) throw new ArgumentNullException(nameof(instruction));
            if (hint == null) throw new ArgumentNullException(nameof(hint));
            if (instruction == hint) throw new ArgumentException("Instruction and hint labels must be distinct.");
            instructionLabel = instruction;
            hintLabel = hint;
            instructionLabel.supportRichText = false;
            hintLabel.supportRichText = false;
        }

        private void Awake()
        {
            if (!ValidateLabels())
            {
                enabled = false;
                return;
            }
            instructionLabel.supportRichText = false;
            hintLabel.supportRichText = false;
            ClearHint();
        }

        public virtual void ShowInstruction(string text)
        {
            if (!ValidateLabels()) return;
            instructionLabel.text = text ?? string.Empty;
            instructionLabel.enabled = true;
        }

        public virtual void ShowHint(string text)
        {
            if (!ValidateLabels()) return;
            hintLabel.text = text ?? string.Empty;
            hintLabel.enabled = !string.IsNullOrWhiteSpace(text);
        }

        public virtual void ClearHint()
        {
            if (!ValidateLabels()) return;
            hintLabel.text = string.Empty;
            hintLabel.enabled = false;
        }

        private bool ValidateLabels()
        {
            if (instructionLabel == null)
            {
                Debug.LogError("[InstructionPanel] Missing instructionLabel.", this);
                return false;
            }
            if (hintLabel == null)
            {
                Debug.LogError("[InstructionPanel] Missing hintLabel.", this);
                return false;
            }
            if (instructionLabel == hintLabel)
            {
                Debug.LogError("[InstructionPanel] instructionLabel and hintLabel must be distinct.", this);
                return false;
            }
            return true;
        }
    }
}
