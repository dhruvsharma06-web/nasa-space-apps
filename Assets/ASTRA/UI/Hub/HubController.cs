using System;
using Astra.Core.Progress;
using Astra.UI.Progress;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Astra.UI.Hub
{
    /// <summary>Read-only progress presentation and guarded scene selection. Core owns persistence/unlocks.</summary>
    public sealed class HubController : MonoBehaviour
    {
        [SerializeField] private LevelCard[] cards;
        [SerializeField] private Text progressNotice;
        [SerializeField] private LevelCardPresenter[] presenters = new LevelCardPresenter[0];
        [SerializeField] private Texture[] previews = new Texture[0];
        private IAcademyProgress academy;
        private IProgressStore progressStore;
        private ProgressState progress;
        private Func<string, bool> sceneAvailable = Application.CanStreamedLevelBeLoaded;
        private Action<string> loadScene = name => SceneManager.LoadScene(name);
        private bool subscribed;
        private bool loading;

        public void Configure(LevelCard[] levelCards, Text notice, IProgressStore store = null,
            Func<string, bool> availability = null, Action<string> loader = null)
        {
            if (!AreCardsValid(levelCards) || notice == null)
                throw new ArgumentException("Hub requires one card for each level 0–7 and a progressNotice label.");
            Unsubscribe();
            cards = (LevelCard[])levelCards.Clone();
            progressNotice = notice;
            progressStore = store;
            sceneAvailable = availability ?? Application.CanStreamedLevelBeLoaded;
            loadScene = loader ?? (name => SceneManager.LoadScene(name));
            loading = false;
            if (isActiveAndEnabled)
            {
                Subscribe();
                Refresh();
            }
        }

        /// <summary>Unlocks, best scores and medals from P2's progress API (placeholder until it ships).</summary>
        public IAcademyProgress Academy => academy;

        /// <summary>Raised after every Refresh (header, settings and cards update from it).</summary>
        public event Action Refreshed;

        /// <summary>The next recommended level: the lowest unlocked, not-yet-passed level (-1 when all are passed).</summary>
        public int RecommendedLevel { get; private set; } = -1;

        /// <summary>Optional premium presenters (one per card, any order) and preview textures indexed by level.</summary>
        public void ConfigurePresentation(LevelCardPresenter[] cardPresenters, Texture[] previewTextures)
        {
            presenters = cardPresenters ?? new LevelCardPresenter[0];
            previews = previewTextures ?? new Texture[0];
        }

        public void UseAcademyProgress(IAcademyProgress progressApi)
        {
            academy = progressApi ?? throw new ArgumentNullException(nameof(progressApi));
            Refresh();
        }

        /// <summary>Judges' "Demo: unlock all": every level with a scene in the build becomes selectable.</summary>
        public void DemoUnlockAll()
        {
            (academy ?? AcademyProgress.Current).UnlockAll();
            if (academy == null) academy = AcademyProgress.Current;
            Refresh();
        }

        public void UseProgressStore(IProgressStore store)
        {
            progressStore = store ?? throw new ArgumentNullException(nameof(store));
            Refresh();
        }

        public void Refresh()
        {
            if (!ValidateReferences())
                return;
            progressNotice.supportRichText = false;
            try
            {
                progress = progressStore != null ? progressStore.Load() : new ProgressState();
                if (progress == null)
                    throw new InvalidOperationException("Progress store returned null.");
                progressNotice.text = progressStore == null ? "Preview: progress is not saved." : "Progress loaded.";
            }
            catch (Exception exception)
            {
                progress = new ProgressState();
                progressNotice.text = "Progress unavailable. Try again.";
                Debug.LogException(new InvalidOperationException("[HubController] Could not load progress; original save was not modified.", exception), this);
            }

            for (var i = 0; i < cards.Length; i++)
            {
                var card = cards[i];
                var unlocked = IsUnlocked(card.Level);
                var passed = false;
                if (progress.records != null)
                    for (var j = 0; j < progress.records.Count; j++)
                        if (progress.records[j] != null && progress.records[j].level == card.Level && progress.records[j].passed)
                            passed = true;
                if (!passed && academy != null && academy.IsPassed(card.Level))
                    passed = true;
                card.SetState(unlocked, sceneAvailable(LevelCatalog.Levels[card.Level].SceneName), passed);
                card.SetBusy(loading);
            }
            PresentCards();
            Refreshed?.Invoke();
        }

        private void PresentCards()
        {
            RecommendedLevel = -1;
            for (var level = 0; level < LevelCatalog.Levels.Count; level++)
            {
                var done = academy != null ? academy.IsPassed(level) : IsPassedInState(level);
                if (IsUnlocked(level) && !done && sceneAvailable(LevelCatalog.Levels[level].SceneName))
                {
                    RecommendedLevel = level;
                    break;
                }
            }
            if (presenters == null) return;
            foreach (var presenter in presenters)
            {
                if (presenter == null) continue;
                var card = presenter.GetComponentInParent<LevelCard>() ?? presenter.GetComponentInChildren<LevelCard>(true);
                if (card == null) continue;
                var level = card.Level;
                var entry = LevelCatalog.Levels[level];
                var preview = previews != null && level < previews.Length ? previews[level] : null;
                presenter.Present(entry, IsUnlocked(level), academy != null ? academy.BestScore(level) : -1,
                    academy != null ? academy.BestMedal(level) : Medal.None, preview);
                presenter.SetRecommended(level == RecommendedLevel);
            }
        }

        private bool IsPassedInState(int level)
        {
            if (progress?.records == null) return false;
            foreach (var record in progress.records)
                if (record != null && record.level == level && record.passed) return true;
            return false;
        }

        private void Awake()
        {
            if (!ValidateReferences())
                enabled = false;
        }

        private void OnEnable()
        {
            if (!isActiveAndEnabled)
                return;
            loading = false;
            Subscribe();
            Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
            loading = false;
        }

        private void HandleSelection(int level)
        {
            if (!isActiveAndEnabled || loading || level < 0 || level >= LevelCatalog.Levels.Count || !IsUnlocked(level))
                return;
            var scene = LevelCatalog.Levels[level].SceneName;
            if (!sceneAvailable(scene))
            {
                Refresh();
                return;
            }

            loading = true;
            for (var i = 0; i < cards.Length; i++)
                cards[i].SetBusy(true);
            try
            {
                loadScene(scene);
            }
            catch (Exception exception)
            {
                loading = false;
                Debug.LogException(new InvalidOperationException("[HubController] Could not load scene '" + scene + "'.", exception), this);
                if (this != null && isActiveAndEnabled)
                {
                    Refresh();
                    progressNotice.text = "Level unavailable. Try again.";
                }
            }
        }

        private bool IsUnlocked(int level) => (academy != null && academy.IsUnlocked(level)) ||
                                              (progress != null && progress.unlockedLevels != null &&
                                               progress.unlockedLevels.Contains(level));

        private void Subscribe()
        {
            if (subscribed || !AreCardsValid(cards))
                return;
            for (var i = 0; i < cards.Length; i++)
                cards[i].Selected += HandleSelection;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || cards == null)
                return;
            for (var i = 0; i < cards.Length; i++)
                if (cards[i] != null)
                    cards[i].Selected -= HandleSelection;
            subscribed = false;
        }

        private bool ValidateReferences()
        {
            var valid = AreCardsValid(cards);
            if (!valid)
                Debug.LogError($"[HubController] {name}: cards must contain each level 0–7 exactly once.", this);
            if (progressNotice == null)
                Debug.LogError($"[HubController] {name}: missing progressNotice reference.", this);
            return valid && progressNotice != null;
        }

        private static bool AreCardsValid(LevelCard[] value)
        {
            if (value == null || value.Length != LevelCatalog.Levels.Count)
                return false;
            var mask = 0;
            for (var i = 0; i < value.Length; i++)
            {
                if (value[i] == null || value[i].Level < 0 || value[i].Level >= LevelCatalog.Levels.Count)
                    return false;
                var bit = 1 << value[i].Level;
                if ((mask & bit) != 0)
                    return false;
                mask |= bit;
            }
            return mask == 255;
        }
    }
}
