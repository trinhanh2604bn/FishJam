using System.Collections;
using System.Text.RegularExpressions;
using FishPuzzle.Domain;
using FishPuzzle.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FishPuzzle.Tests.PlayMode
{
    public sealed class GameplaySceneBootstrapTests
    {
        [UnitySetUp]
        public IEnumerator LoadGameplayScene()
        {
            yield return SceneManager.LoadSceneAsync("Gameplay", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator GameplayScene_LoadsSuccessfully()
        {
            var scene = SceneManager.GetActiveScene();
            Assert.That(scene.IsValid(), Is.True);
            Assert.That(scene.isLoaded, Is.True);
            Assert.That(scene.name, Is.EqualTo("Gameplay"));
            Assert.That(Object.FindAnyObjectByType<LevelSceneBootstrapper>(), Is.Not.Null);
            Assert.That(CountMissingScripts(), Is.EqualTo(0));
            yield return null;
        }

        [UnityTest]
        public IEnumerator GameplayScene_UsesLevel001()
        {
            var bootstrap = Bootstrap();
            Assert.That(bootstrap.LevelId, Is.EqualTo("level_001"));
            Assert.That(bootstrap.Level, Is.Not.Null);
            Assert.That(bootstrap.Level.TotalFishRequired, Is.EqualTo(36));
            Assert.That(bootstrap.Level.PileLayout, Is.Not.Null);
            yield return null;
        }

        [UnityTest]
        public IEnumerator GameplayScene_HasFourTankSlots()
        {
            var board = Bootstrap().TankBoard;
            Assert.That(board, Is.Not.Null);
            Assert.That(board.Slots.Count, Is.EqualTo(4));
            yield return null;
        }

        [UnityTest]
        public IEnumerator GameplayScene_HasTwoUnlockedAndTwoLockedTanks()
        {
            var slots = Bootstrap().TankBoard.Slots;
            Assert.That(slots[0].IsPresentedAsUnlocked, Is.True);
            Assert.That(slots[1].IsPresentedAsUnlocked, Is.True);
            Assert.That(slots[2].IsPresentedAsUnlocked, Is.False);
            Assert.That(slots[3].IsPresentedAsUnlocked, Is.False);
            Assert.That(slots[2].transform.Find("LockedOverlay").gameObject.activeSelf, Is.True);
            Assert.That(slots[3].transform.Find("LockedOverlay").gameObject.activeSelf, Is.True);
            Assert.That(slots[0].transform.Find("LockedOverlay").gameObject.activeSelf, Is.False);
            Assert.That(slots[1].transform.Find("LockedOverlay").gameObject.activeSelf, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator WaitingTray_HasExactlyFiveVisualSlots()
        {
            var tray = Bootstrap().WaitingTray;
            Assert.That(tray, Is.Not.Null);
            Assert.That(tray.VisualSlotCount, Is.EqualTo(5));
            Assert.That(tray.PresentedOccupantCount, Is.EqualTo(0));
            yield return null;
        }

        [UnityTest]
        public IEnumerator BubblePile_HasTenVisibleBubbles()
        {
            Assert.That(Bootstrap().VisibleBubbleCount, Is.EqualTo(10));
            Assert.That(Object.FindObjectsByType<BubbleView>(FindObjectsInactive.Include).Length, Is.EqualTo(10));
            yield return null;
        }

        [UnityTest]
        public IEnumerator BubblePile_HasTwoPendingBubbleDefinitions()
        {
            var bootstrap = Bootstrap();
            Assert.That(bootstrap.PendingBubbleCount, Is.EqualTo(2));
            Assert.That(bootstrap.NextBubbleQueueIndex, Is.EqualTo(10));
            Assert.That(bootstrap.Level.BubbleQueue.Count, Is.EqualTo(12));
            Assert.That(bootstrap.Level.BubbleQueue[10].BubbleId, Is.EqualTo("L001_B011"));
            Assert.That(bootstrap.Level.BubbleQueue[11].BubbleId, Is.EqualTo("L001_B012"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator BubblePile_UsesQueueOrderForInitialSlotMapping()
        {
            var pile = Bootstrap().BubblePile;
            Assert.That(pile.TryGetBubble(0, out var first), Is.True);
            Assert.That(pile.TryGetBubble(9, out var last), Is.True);
            Assert.That(first.BubbleId, Is.EqualTo("L001_B001"));
            Assert.That(last.BubbleId, Is.EqualTo("L001_B010"));
            Assert.That(first.SourceDefinition, Is.SameAs(Bootstrap().Level.BubbleQueue[0]));
            Assert.That(last.SourceDefinition, Is.SameAs(Bootstrap().Level.BubbleQueue[9]));
            for (var slot = 0; slot < 10; slot++)
            {
                Assert.That(pile.TryGetBubble(slot, out var bubble), Is.True, "slot " + slot);
                Assert.That(bubble.BubbleId, Is.EqualTo(Bootstrap().Level.BubbleQueue[slot].BubbleId));
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator VisibleBubbles_HaveExpectedFishViews()
        {
            var bubbles = Object.FindObjectsByType<BubbleView>(FindObjectsInactive.Include);
            var fish = 0;
            for (var i = 0; i < bubbles.Length; i++)
            {
                fish += bubbles[i].FishViews.Count;
            }

            Assert.That(bubbles.Length, Is.EqualTo(10));
            Assert.That(fish, Is.EqualTo(30));
            yield return null;
        }

        [UnityTest]
        public IEnumerator VisibleFishViews_HaveSprites()
        {
            var fish = Object.FindObjectsByType<FishView>(FindObjectsInactive.Include);
            Assert.That(fish.Length, Is.EqualTo(30));
            for (var i = 0; i < fish.Length; i++)
            {
                Assert.That(fish[i].CurrentSprite, Is.Not.Null, fish[i].name);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator GlobalProgress_StartsAtZeroOfThirtySix()
        {
            var bootstrap = Bootstrap();
            Assert.That(bootstrap.CollectedFishDisplay, Is.EqualTo(0));
            Assert.That(bootstrap.TotalFishRequired, Is.EqualTo(36));
            Assert.That(bootstrap.GlobalProgressLabel, Is.EqualTo("0 / 36"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Bootstrap_DoesNotDuplicateObjectsOnSecondInitialization()
        {
            var bootstrap = Bootstrap();
            var bubbles = Object.FindObjectsByType<BubbleView>(FindObjectsInactive.Include).Length;
            var tanks = Object.FindObjectsByType<TankSlotView>(FindObjectsInactive.Include).Length;
            var fish = Object.FindObjectsByType<FishView>(FindObjectsInactive.Include).Length;
            LogAssert.Expect(LogType.Warning, new Regex("Initialize was requested again"));
            bootstrap.Initialize();
            Assert.That(Object.FindObjectsByType<BubbleView>(FindObjectsInactive.Include).Length, Is.EqualTo(bubbles));
            Assert.That(Object.FindObjectsByType<TankSlotView>(FindObjectsInactive.Include).Length, Is.EqualTo(tanks));
            Assert.That(Object.FindObjectsByType<FishView>(FindObjectsInactive.Include).Length, Is.EqualTo(fish));
            Assert.That(bubbles, Is.EqualTo(10));
            Assert.That(tanks, Is.EqualTo(4));
            Assert.That(fish, Is.EqualTo(30));
            yield return null;
        }

        private static LevelSceneBootstrapper Bootstrap()
        {
            var bootstrap = Object.FindAnyObjectByType<LevelSceneBootstrapper>();
            Assert.That(bootstrap, Is.Not.Null);
            Assert.That(bootstrap.IsInitialized, Is.True);
            return bootstrap;
        }

        private static int CountMissingScripts()
        {
            var missing = 0;
            var transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
            for (var i = 0; i < transforms.Length; i++)
            {
                var components = transforms[i].GetComponents<Component>();
                for (var c = 0; c < components.Length; c++)
                {
                    if (components[c] == null)
                    {
                        missing++;
                    }
                }
            }

            return missing;
        }
    }
}
