using System;
using System.Collections.Generic;
using System.Reflection;
using Astra.Core.Progress;
using Astra.UI.Hub;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Astra.UI.Tests.PlayMode
{
    public sealed class HubPresentationTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var instance in objects)
                if (instance != null)
                    UnityEngine.Object.DestroyImmediate(instance);
            objects.Clear();
        }

        [Test]
        public void FreshProgressLoadsOnlyL0AndShowsLiteralCatalogTitle()
        {
            var store = new FakeProgressStore(new ProgressState());
            var setup = CreateHub(store, _ => true, _ => { });

            Assert.AreEqual(1, store.LoadCount);
            Assert.AreEqual("Safety & Calibration", setup.Titles[0].text);
            Assert.AreEqual("Available", setup.Statuses[0].text);
            Assert.IsTrue(setup.Buttons[0].interactable);
            for (var i = 1; i < setup.Cards.Length; i++)
            {
                Assert.AreEqual("Locked", setup.Statuses[i].text, $"Level {i} should start locked.");
                Assert.IsFalse(setup.Buttons[i].interactable);
            }
        }

        [Test]
        public void UnlockedLevelWithoutSceneShowsComingSoonAndCannotLoad()
        {
            var progress = new ProgressState();
            progress.unlockedLevels.Add(1);
            var loads = new List<string>();
            var setup = CreateHub(new FakeProgressStore(progress), scene => scene != "L1_Orientation", loads.Add);

            Assert.AreEqual("Coming soon", setup.Statuses[1].text);
            Assert.IsFalse(setup.Buttons[1].interactable);
            setup.Buttons[1].onClick.Invoke();
            Assert.AreEqual(0, loads.Count);
        }

        [Test]
        public void LockedLevelWithoutSceneStillShowsBothStatesAndCannotLoad()
        {
            var loads = new List<string>();
            var setup = CreateHub(new FakeProgressStore(new ProgressState()), _ => false, loads.Add);

            Assert.AreEqual("Locked / Coming soon", setup.Statuses[1].text);
            Assert.IsFalse(setup.Cards[1].IsLoadable);
            Assert.IsFalse(setup.Buttons[1].interactable);
            setup.Buttons[1].onClick.Invoke();
            Assert.AreEqual(0, loads.Count);
        }

        [Test]
        public void LockedCardCannotLoadEvenWhenButtonClickIsInvokedDirectly()
        {
            var loads = new List<string>();
            var setup = CreateHub(new FakeProgressStore(new ProgressState()), _ => true, loads.Add);

            Assert.IsFalse(setup.Buttons[1].interactable);
            setup.Buttons[1].onClick.Invoke();
            Assert.AreEqual(0, loads.Count);
        }

        [Test]
        public void UnlockedSelectionLoadsExactSceneOnceAndBlocksReentryWhileLoading()
        {
            var progress = new ProgressState();
            progress.unlockedLevels.Add(1);
            HubSetup setup = null;
            var loads = new List<string>();
            setup = CreateHub(new FakeProgressStore(progress), _ => true, scene =>
            {
                loads.Add(scene);
                setup.Buttons[1].onClick.Invoke();
            });

            setup.Buttons[1].onClick.Invoke();
            Assert.AreEqual(1, loads.Count);
            Assert.AreEqual("L1_Orientation", loads[0]);
            Assert.IsFalse(setup.Buttons[0].interactable);
            Assert.IsFalse(setup.Buttons[1].interactable);
        }

        [Test]
        public void DisableEnableRefreshesExternalProgressWithoutSavingOrMutatingIt()
        {
            var store = new FakeProgressStore(new ProgressState());
            var setup = CreateHub(store, _ => true, _ => { });

            setup.Hub.enabled = false;
            var supplied = new ProgressState();
            supplied.unlockedLevels.Add(1);
            supplied.records.Add(new LevelRecord
            {
                level = 0,
                missionId = "l0-safety-v1",
                attempts = 1,
                passed = true,
                bestTimeMs = 42000
            });
            var suppliedSnapshot = JsonUtility.ToJson(supplied);
            store.State = supplied;
            setup.Hub.enabled = true;

            Assert.AreEqual(2, store.LoadCount);
            Assert.AreEqual(0, store.SaveCount);
            Assert.AreEqual("Passed", setup.Statuses[0].text);
            Assert.AreEqual("Available", setup.Statuses[1].text);
            Assert.IsTrue(setup.Buttons[1].interactable);
            Assert.AreEqual(suppliedSnapshot, JsonUtility.ToJson(supplied), "Hub refresh must not mutate supplied progress.");
        }

        [Test]
        public void ReconfiguringActiveCardUnbindsOldButtonAndRefreshesLoadability()
        {
            var progress = new ProgressState();
            progress.unlockedLevels.Add(1);
            var loads = new List<string>();
            var setup = CreateHub(new FakeProgressStore(progress), _ => true, loads.Add);
            var card = setup.Cards[0];
            var replacement = CreateCard(0);

            card.enabled = false;
            setup.Buttons[0].onClick.Invoke();
            Assert.AreEqual(0, loads.Count, "Disabled card must detach its old button listener.");
            card.Configure(0, "Safety & Calibration (updated)", replacement.Button, replacement.Title, replacement.Status);
            setup.Hub.enabled = false;
            setup.Hub.enabled = true;
            card.enabled = true;

            Assert.AreEqual("Safety & Calibration (updated)", replacement.Title.text);
            Assert.IsTrue(replacement.Button.interactable);
            setup.Buttons[0].onClick.Invoke();
            Assert.AreEqual(0, loads.Count, "Reconfigured card must detach from the old button.");
            replacement.Button.onClick.Invoke();
            Assert.AreEqual(1, loads.Count);
            Assert.AreEqual("L0_Safety", loads[0]);
        }

        [Test]
        public void ConfigureRejectsMissingDuplicateAndOutOfRangeCards()
        {
            var cards = Array.ConvertAll(CreateCards(), fixture => fixture.Card);
            var hubObject = NewObject("Hub");
            var hub = hubObject.AddComponent<HubController>();
            var notice = NewText("ProgressNotice");

            var missing = new LevelCard[7];
            Array.Copy(cards, missing, missing.Length);
            Assert.Throws<ArgumentException>(() => hub.Configure(missing, notice));

            var duplicate = (LevelCard[])cards.Clone();
            duplicate[7] = duplicate[0];
            Assert.Throws<ArgumentException>(() => hub.Configure(duplicate, notice));

            typeof(LevelCard).GetField("level", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(cards[7], 8);
            Assert.Throws<ArgumentException>(() => hub.Configure(cards, notice));
        }

        private HubSetup CreateHub(FakeProgressStore store, Func<string, bool> availability, Action<string> loader)
        {
            var fixtures = CreateCards();
            var cards = new LevelCard[fixtures.Length];
            var buttons = new Button[fixtures.Length];
            var titles = new Text[fixtures.Length];
            var statuses = new Text[fixtures.Length];
            for (var i = 0; i < cards.Length; i++)
            {
                cards[i] = fixtures[i].Card;
                buttons[i] = fixtures[i].Button;
                titles[i] = fixtures[i].Title;
                statuses[i] = fixtures[i].Status;
            }
            var notice = NewText("ProgressNotice");
            var hubObject = NewObject("Hub");
            var hub = hubObject.AddComponent<HubController>();
            hub.Configure(cards, notice, store, availability, loader);
            for (var i = 0; i < cards.Length; i++)
                cards[i].gameObject.SetActive(true);
            hubObject.SetActive(true);
            return new HubSetup(hub, cards, buttons, titles, statuses);
        }

        private CardFixture[] CreateCards()
        {
            var cards = new CardFixture[8];
            for (var level = 0; level < cards.Length; level++)
                cards[level] = CreateCard(level);
            return cards;
        }

        private CardFixture CreateCard(int level)
        {
            var root = NewObject("LevelCard_" + level);
            var buttonObject = NewObject("SelectButton_" + level, withRectTransform: true);
            var button = buttonObject.AddComponent<Button>();
            buttonObject.SetActive(true);
            var title = NewText("Title_" + level);
            var status = NewText("Status_" + level);
            var card = root.AddComponent<LevelCard>();
            card.Configure(level, LevelCatalog.Levels[level].Title, button, title, status);
            return new CardFixture(card, button, title, status);
        }

        private Text NewText(string objectName)
        {
            var gameObject = NewObject(objectName, withRectTransform: true);
            gameObject.AddComponent<CanvasRenderer>();
            var text = gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Assert.IsNotNull(text.font, "LegacyRuntime.ttf is required by these legacy UGUI tests.");
            gameObject.SetActive(true);
            return text;
        }

        private GameObject NewObject(string objectName, Transform parent = null, bool withRectTransform = false)
        {
            var gameObject = withRectTransform
                ? new GameObject(objectName, typeof(RectTransform))
                : new GameObject(objectName);
            gameObject.SetActive(false);
            if (parent != null)
                gameObject.transform.SetParent(parent, false);
            objects.Add(gameObject);
            return gameObject;
        }

        private sealed class HubSetup
        {
            public readonly HubController Hub;
            public readonly LevelCard[] Cards;
            public readonly Button[] Buttons;
            public readonly Text[] Titles;
            public readonly Text[] Statuses;

            public HubSetup(HubController hub, LevelCard[] cards, Button[] buttons, Text[] titles, Text[] statuses)
            {
                Hub = hub;
                Cards = cards;
                Buttons = buttons;
                Titles = titles;
                Statuses = statuses;
            }
        }

        private sealed class CardFixture
        {
            public readonly LevelCard Card;
            public readonly Button Button;
            public readonly Text Title;
            public readonly Text Status;

            public CardFixture(LevelCard card, Button button, Text title, Text status)
            {
                Card = card;
                Button = button;
                Title = title;
                Status = status;
            }
        }

        private sealed class FakeProgressStore : IProgressStore
        {
            public ProgressState State { get; set; }
            public int LoadCount { get; private set; }
            public int SaveCount { get; private set; }

            public FakeProgressStore(ProgressState state) => State = state;

            public ProgressState Load()
            {
                LoadCount++;
                return State;
            }

            public void Save(ProgressState state) => SaveCount++;
        }
    }
}
