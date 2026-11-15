using System;
using UnityEngine;
using UnityEngine.UI;

namespace Astra.UI.Hub
{
    /// <summary>Displays a level's availability and raises a selection request.</summary>
    public sealed class LevelCard : MonoBehaviour
    {
        [SerializeField] private int level;
        [SerializeField] private Button selectButton;
        [SerializeField] private Text titleLabel;
        [SerializeField] private Text statusLabel;
        private Button boundButton;
        private bool busy;
        private bool selecting;

        public int Level => level;
        public bool IsLoadable { get; private set; }
        public event Action<int> Selected;

        public void Configure(int levelNumber, string title, Button button, Text titleText, Text statusText)
        {
            if (levelNumber < 0 || levelNumber >= LevelCatalog.Levels.Count) throw new ArgumentOutOfRangeException(nameof(levelNumber));
            if (button == null) throw new ArgumentNullException(nameof(button));
            if (titleText == null) throw new ArgumentNullException(nameof(titleText));
            if (statusText == null) throw new ArgumentNullException(nameof(statusText));
            if (titleText == statusText) throw new ArgumentException("Title and status labels must be distinct.");
            Unbind();
            level = levelNumber;
            selectButton = button;
            titleLabel = titleText;
            statusLabel = statusText;
            titleLabel.supportRichText = false;
            statusLabel.supportRichText = false;
            titleLabel.text = title ?? string.Empty;
            statusLabel.text = string.Empty;
            IsLoadable = false;
            busy = false;
            UpdateButton();
            if (isActiveAndEnabled) Bind();
        }

        public void SetState(bool unlocked, bool sceneAvailable, bool passed)
        {
            IsLoadable = unlocked && sceneAvailable;
            if (statusLabel == null)
                Debug.LogError($"[LevelCard] {name}: missing statusLabel reference.", this);
            else
            {
                statusLabel.supportRichText = false;
                statusLabel.text = !sceneAvailable
                    ? unlocked ? "Coming soon" : "Locked / Coming soon"
                    : !unlocked ? "Locked" : passed ? "Passed" : "Available";
            }
            UpdateButton();
        }

        public void SetBusy(bool value)
        {
            busy = value;
            UpdateButton();
        }

        private void Awake()
        {
            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }
            titleLabel.supportRichText = false;
            statusLabel.supportRichText = false;
            UpdateButton();
        }

        private void OnEnable()
        {
            if (selectButton != null) Bind();
        }

        private void OnDisable() => Unbind();

        private void Bind()
        {
            if (boundButton == selectButton) return;
            Unbind();
            boundButton = selectButton;
            if (boundButton != null) boundButton.onClick.AddListener(HandleClick);
        }

        private void Unbind()
        {
            if (boundButton != null) boundButton.onClick.RemoveListener(HandleClick);
            boundButton = null;
        }

        private void HandleClick()
        {
            if (!isActiveAndEnabled || selecting || busy || !IsLoadable || selectButton == null || !selectButton.interactable)
                return;
            selecting = true;
            try { Selected?.Invoke(level); }
            finally { selecting = false; }
        }

        private void UpdateButton()
        {
            if (selectButton != null) selectButton.interactable = IsLoadable && !busy;
            else Debug.LogError($"[LevelCard] {name}: missing selectButton reference.", this);
        }

        private bool ValidateReferences()
        {
            var valid = true;
            if (level < 0 || level >= LevelCatalog.Levels.Count) { Debug.LogError($"[LevelCard] {name}: level must be 0–{LevelCatalog.Levels.Count - 1}.", this); valid = false; }
            if (selectButton == null) { Debug.LogError($"[LevelCard] {name}: missing selectButton reference.", this); valid = false; }
            if (titleLabel == null) { Debug.LogError($"[LevelCard] {name}: missing titleLabel reference.", this); valid = false; }
            if (statusLabel == null) { Debug.LogError($"[LevelCard] {name}: missing statusLabel reference.", this); valid = false; }
            if (titleLabel != null && titleLabel == statusLabel) { Debug.LogError($"[LevelCard] {name}: titleLabel and statusLabel must be distinct.", this); valid = false; }
            return valid;
        }
    }
}
