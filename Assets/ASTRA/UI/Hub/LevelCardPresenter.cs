using Astra.UI.Gaze;
using Astra.UI.Progress;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Astra.UI.Hub
{
    /// <summary>
    /// Premium presentation for a <see cref="LevelCard"/>: preview image, goal, skill, difficulty dots, duration,
    /// best score + medal, lock overlay ("Pass L&lt;N-1&gt; to unlock") and the recommended-level pulse.
    /// On gaze or mouse hover the card lifts 2 cm toward the cadet, its edge glows and the details expand
    /// (200 ms eased). Only the card moves, never the camera. Presentation only; selection stays in LevelCard.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelCardPresenter : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public const float AnimationSeconds = 0.2f;
        public const float LiftMetres = 0.02f;

        [SerializeField] private RectTransform card;
        [SerializeField] private Graphic edgeGlow;
        [SerializeField] private CanvasGroup details;
        [SerializeField] private RawImage preview;
        [SerializeField] private Text goalLabel;
        [SerializeField] private Text metaLabel;      // skill · difficulty · time
        [SerializeField] private Text bestLabel;
        [SerializeField] private Text medalLabel;
        [SerializeField] private Graphic medalBadge;
        [SerializeField] private GameObject lockOverlay;
        [SerializeField] private Text lockLabel;
        [SerializeField] private CanvasGroup dimmer;
        [SerializeField] private GazeButton gazeButton;

        private Vector3 restPosition;
        private Vector3 restScale = Vector3.one;
        private bool restKnown;
        private bool hovered;
        private bool recommended;
        private bool locked;
        private float amount;

        public bool IsHovered => hovered;
        public bool IsRecommended => recommended;
        public bool IsLockedView => locked;
        public float HoverAmount => amount;

        public void Configure(RectTransform cardRoot, Graphic glow, CanvasGroup detailGroup, RawImage previewImage, Text goal, Text meta,
            Text best, Text medal, Graphic badge, GameObject lockRoot, Text lockText, CanvasGroup dim, GazeButton gaze)
        {
            card = cardRoot;
            edgeGlow = glow;
            details = detailGroup;
            preview = previewImage;
            goalLabel = goal;
            metaLabel = meta;
            bestLabel = best;
            medalLabel = medal;
            medalBadge = badge;
            lockOverlay = lockRoot;
            lockLabel = lockText;
            dimmer = dim;
            gazeButton = gaze;
        }

        private void OnEnable()
        {
            if (gazeButton != null) gazeButton.HoverChanged += SetHovered;
        }

        private void OnDisable()
        {
            if (gazeButton != null) gazeButton.HoverChanged -= SetHovered;
            hovered = false;
            amount = 0f;
            Apply(0f);
        }

        public void OnPointerEnter(PointerEventData eventData) => SetHovered(true);
        public void OnPointerExit(PointerEventData eventData) => SetHovered(false);

        public void SetHovered(bool on) => hovered = on;

        /// <summary>Static card content from the catalog entry and progress.</summary>
        public void Present(LevelCatalog.Entry entry, bool unlocked, int bestScore, Medal medal, Texture previewTexture)
        {
            locked = !unlocked;
            if (goalLabel != null) goalLabel.text = entry.Goal;
            if (metaLabel != null) metaLabel.text = $"{entry.Skill}   {entry.DifficultyDots}   {entry.Duration}";
            if (bestLabel != null) bestLabel.text = bestScore >= 0 ? "BEST " + bestScore : "NOT PLAYED";
            if (medalLabel != null)
            {
                medalLabel.text = medal == Medal.None ? string.Empty : medal.ToString().ToUpperInvariant();
                medalLabel.enabled = medal != Medal.None;
            }
            if (medalBadge != null)
            {
                medalBadge.enabled = medal != Medal.None;
                medalBadge.color = medal == Medal.Gold ? Theme.Gold : medal == Medal.Silver ? Theme.Silver : Theme.Bronze;
            }
            if (preview != null)
            {
                preview.texture = previewTexture;
                preview.enabled = previewTexture != null;
                preview.color = locked ? new Color(0.45f, 0.45f, 0.5f, 1f) : Color.white;
            }
            if (lockOverlay != null) lockOverlay.SetActive(locked);
            if (lockLabel != null) lockLabel.text = entry.Level > 0 ? $"Pass L{entry.Level - 1} to unlock" : string.Empty;
            if (dimmer != null) dimmer.alpha = locked ? 0.55f : 1f;
        }

        public void SetRecommended(bool on) => recommended = on;

        private void Update()
        {
            var goal = hovered && !locked ? 1f : 0f;
            amount = Mathf.MoveTowards(amount, goal, Time.unscaledDeltaTime / AnimationSeconds);
            Apply(Ease(amount));
        }

        private void Apply(float k)
        {
            if (card != null)
            {
                if (!restKnown)
                {
                    restPosition = card.localPosition;
                    restScale = card.localScale;   // the card IS the world canvas (scale ~0.0008): never write Vector3.one
                    restKnown = true;
                }
                // The card's +Z points away from the eye, so "toward the cadet" is its -forward, in metres.
                var parentScale = card.parent != null ? Mathf.Max(1e-6f, card.parent.lossyScale.z) : 1f;
                var towardEye = -(card.localRotation * Vector3.forward);
                card.localPosition = restPosition + towardEye * (LiftMetres * k / parentScale);
                card.localScale = restScale * (1f + 0.04f * k);
            }
            if (details != null) details.alpha = Mathf.Lerp(0.0f, 1f, k);
            if (edgeGlow != null)
            {
                // Recommended: a slow, soft breathing glow (0.5 Hz, never a flash); hover: full glow.
                var pulse = recommended && !locked ? 0.25f + 0.2f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI)) : 0f;
                var alpha = Mathf.Max(pulse, k);
                var c = Theme.Accent;
                edgeGlow.color = new Color(c.r, c.g, c.b, alpha);
                edgeGlow.enabled = alpha > 0.01f;
            }
        }

        private static float Ease(float t) => t * t * (3f - 2f * t);
    }
}
