using System;
using Astra.Core.Assessment;
using Astra.Core.Mission;
using Astra.Integrations.Coach;
using UnityEngine;

namespace Astra.App
{
    /// <summary>
    /// One per level scene. Loads the mission, runs the phases, forwards InteractionEvents to the
    /// mission runner, scores Qualification, shows the debrief, saves progress (CONTRACT.md §8).
    /// OWNER: Integration Lead. Public surface is fixed by the contract; the body is a stub until implemented.
    /// Level-specific scripts may subscribe to PhaseChanged / EventRecorded but must not drive the runner.
    /// </summary>
    public class LevelController : MonoBehaviour
    {
#pragma warning disable CS0414, CS0649 // used once the Integration Lead implements this class
        [SerializeField] private TextAsset missionJson;
        [SerializeField] private CoachProvider coachProvider;
        [SerializeField] private float briefingSeconds = 6f;
        [SerializeField] private float demoSecondsPerStep = 4f;
#pragma warning restore CS0414, CS0649

#pragma warning disable CS0067 // events are raised once the Integration Lead implements this class
        public event Action<ModulePhase> PhaseChanged;
        public event Action<SessionEvent, StepFeedback> EventRecorded;
        public event Action<SessionResult> QualificationScored;
#pragma warning restore CS0067

        public MissionDefinition Mission { get; private set; }
        public ModulePhase Phase { get; private set; } = ModulePhase.Briefing;

        protected virtual void Awake()
        {
            if (missionJson != null)
                Mission = JsonUtility.FromJson<MissionDefinition>(missionJson.text);
        }
    }
}
