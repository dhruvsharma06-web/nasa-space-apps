using System;
using Astra.XR.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Astra.UI.Gaze
{
    /// <summary>
    /// Makes a world-space UGUI <see cref="Button"/> gaze-selectable: a BoxCollider sized to the RectTransform lets the
    /// gaze ray find it, hover drives the button's highlighted state, and a tap or dwell invokes <c>onClick</c>.
    /// The GazeInteractor pushes its gaze-tagged actor around the call, so handlers that report events (PauseMenu)
    /// report <c>input: "gaze"</c>. Button handlers raise their own contract events; this class raises none itself.
    /// </summary>
    [RequireComponent(typeof(Button))]
    [DisallowMultipleComponent]
    public sealed class GazeButton : MonoBehaviour, IGazeTarget, IGazeLabel
    {
        [SerializeField] private string targetId = "ui_button";
        [SerializeField, Min(0.001f)] private float depthMetres = 0.01f;
        [SerializeField] private string labelOverride;

        private Button button;
        private BoxCollider box;
        private RectTransform rect;

        public string TargetId => targetId;
        public bool IsGazeEnabled => isActiveAndEnabled && Button != null && Button.IsInteractable();
        public bool IsHovered { get; private set; }
        public event Action<bool> HoverChanged;

        public string GazeLabel
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(labelOverride)) return labelOverride;
                var text = GetComponentInChildren<Text>(true);
                return text != null ? text.text : null;
            }
        }

        private Button Button => button != null ? button : button = GetComponent<Button>();

        public void Configure(string id, string label = null)
        {
            targetId = id;
            labelOverride = label;
        }

        private void Awake() => FitCollider();
        private void OnRectTransformDimensionsChange() => FitCollider();

        private void OnDisable()
        {
            if (IsHovered)
                SetGazeHover(false);
        }

        /// <summary>Sizes the BoxCollider to the RectTransform (in canvas units; the canvas scale makes it metres).</summary>
        public void FitCollider()
        {
            if (rect == null) rect = transform as RectTransform;
            if (rect == null) return;
            if (box == null) box = GetComponent<BoxCollider>();
            if (box == null) box = gameObject.AddComponent<BoxCollider>();
            var scale = Mathf.Abs(rect.lossyScale.z) > 1e-6f ? Mathf.Abs(rect.lossyScale.z) : Mathf.Abs(rect.lossyScale.x);
            var depth = scale > 1e-6f ? depthMetres / scale : 1f;
            var r = rect.rect;
            box.center = new Vector3(r.center.x, r.center.y, 0f);
            box.size = new Vector3(r.width, r.height, depth);
            box.isTrigger = false;
        }

        public void SetGazeHover(bool on)
        {
            if (IsHovered == on) return;
            IsHovered = on;
            var data = new PointerEventData(EventSystem.current);
            if (on) Button.OnPointerEnter(data);
            else Button.OnPointerExit(data);
            HoverChanged?.Invoke(on);
        }

        public void ActivateFromGaze()
        {
            if (!IsGazeEnabled) return;
            Button.onClick.Invoke();
        }
    }
}
