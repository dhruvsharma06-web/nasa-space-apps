using System;
using UnityEngine;
using UnityEngine.UI;

namespace Astra.UI
{
    /// <summary>
    /// A persistent caption plus optional visual flashing, independent of sound and scaled time.
    /// </summary>
    public class AlarmIndicator : MonoBehaviour
    {
        [SerializeField] private Text captionLabel;
        [SerializeField] private Graphic alarmLight;
        [SerializeField, Range(0f, 3f)] private float flashesPerSecond = 1.5f;
        private double phaseStarted;

        public bool IsAlarmActive { get; private set; }

        public void Configure(Text caption, Graphic light, float flashRate = 1.5f)
        {
            if (caption == null)
                throw new ArgumentNullException(nameof(caption));
            if (light == null)
                throw new ArgumentNullException(nameof(light));
            if (caption == light)
                throw new ArgumentException("The persistent caption and flashing light must be distinct graphics.");
            if (!IsValidRate(flashRate))
                throw new ArgumentOutOfRangeException(nameof(flashRate), "Alarm rate must be finite and between 0 and 3 flashes per second.");

            captionLabel = caption;
            alarmLight = light;
            flashesPerSecond = flashRate;
        }

        public virtual void SetAlarm(bool on, string caption)
        {
            if (!ValidateReferences())
                return;

            if (on && !IsAlarmActive)
                phaseStarted = Time.unscaledTimeAsDouble;
            IsAlarmActive = on;
            captionLabel.supportRichText = false;
            captionLabel.text = on
                ? string.IsNullOrWhiteSpace(caption) ? "ALARM ACTIVE" : caption
                : string.Empty;
            captionLabel.enabled = on;
            if (!on || !isActiveAndEnabled || flashesPerSecond == 0f)
                alarmLight.enabled = on;
            else
                RefreshLight();
        }

        protected virtual void Awake()
        {
            if (!ValidateReferences() || !IsValidRate(flashesPerSecond))
            {
                if (!IsValidRate(flashesPerSecond))
                    Debug.LogError($"[AlarmIndicator] {name}: flashesPerSecond must be finite and between 0 and 3.", this);
                enabled = false;
                return;
            }
            SetAlarm(false, string.Empty);
        }

        protected virtual void Update()
        {
            if (IsAlarmActive && flashesPerSecond > 0f && alarmLight != null)
                RefreshLight();
        }

        protected virtual void OnDisable()
        {
            // A disabled flasher leaves a steady warning instead of hiding an active alarm.
            if (alarmLight != null)
                alarmLight.enabled = IsAlarmActive;
        }

        private void RefreshLight()
        {
            var halfCycles = Math.Floor((Time.unscaledTimeAsDouble - phaseStarted) * flashesPerSecond * 2.0);
            var visible = halfCycles % 2.0 == 0.0;
            if (alarmLight.enabled != visible)
                alarmLight.enabled = visible;
        }

        private bool ValidateReferences()
        {
            if (captionLabel == null)
                Debug.LogError($"[AlarmIndicator] {name}: missing captionLabel reference.", this);
            if (alarmLight == null)
                Debug.LogError($"[AlarmIndicator] {name}: missing alarmLight reference.", this);
            if (captionLabel != null && captionLabel == alarmLight)
                Debug.LogError($"[AlarmIndicator] {name}: captionLabel and alarmLight must be distinct.", this);
            return captionLabel != null && alarmLight != null && captionLabel != alarmLight;
        }

        private static bool IsValidRate(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f && value <= 3f;
    }
}
