#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Reflection;
using NUnit.Framework;
using Rescue.Unity.BoardPresentation;
using Rescue.Unity.Debugging;
using Rescue.Unity.FX;
using Rescue.Unity.Input;
using Rescue.Unity.Presentation;
using Rescue.Unity.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityObject = UnityEngine.Object;

namespace Rescue.PlayMode.Tests.Smoke
{
    public sealed class DebugGameplayPlaybackSceneWiringSmokeTests
    {
        [UnitySetUp]
        public System.Collections.IEnumerator SetUp()
        {
            if (DebugPanel.Instance is not null)
            {
                UnityObject.DestroyImmediate(DebugPanel.Instance.gameObject);
            }

            VictoryScreenPresenter? victoryScreen = UnityObject.FindFirstObjectByType<VictoryScreenPresenter>();
            if (victoryScreen is not null)
            {
                UnityObject.DestroyImmediate(victoryScreen.gameObject);
            }

            LossScreenPresenter? lossScreen = UnityObject.FindFirstObjectByType<LossScreenPresenter>();
            if (lossScreen is not null)
            {
                UnityObject.DestroyImmediate(lossScreen.gameObject);
            }

            yield return SceneManager.LoadSceneAsync("DebugGameplay", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTearDown]
        public System.Collections.IEnumerator TearDown()
        {
            if (DebugPanel.Instance is not null)
            {
                UnityObject.DestroyImmediate(DebugPanel.Instance.gameObject);
            }

            VictoryScreenPresenter? victoryScreen = UnityObject.FindFirstObjectByType<VictoryScreenPresenter>();
            if (victoryScreen is not null)
            {
                UnityObject.DestroyImmediate(victoryScreen.gameObject);
            }

            LossScreenPresenter? lossScreen = UnityObject.FindFirstObjectByType<LossScreenPresenter>();
            if (lossScreen is not null)
            {
                UnityObject.DestroyImmediate(lossScreen.gameObject);
            }

            yield return null;
        }

        [UnityTest]
        public System.Collections.IEnumerator DebugGameplayScene_HasPlanPlaybackAndFxWiring()
        {
            GameStateViewPresenter gameStateView = FindRequired<GameStateViewPresenter>();
            BoardInputPresenter boardInput = FindRequired<BoardInputPresenter>();
            ActionPlaybackController playbackController = FindRequired<ActionPlaybackController>();
            FxEventRouter fxEventRouter = FindRequired<FxEventRouter>();
            VictoryScreenPresenter victoryScreen = VictoryScreenPresenter.EnsureInstance();
            LossScreenPresenter lossScreen = LossScreenPresenter.EnsureInstance();

            Assert.That(
                GetSerializedReference<GameStateViewPresenter, ActionPlaybackController>(gameStateView, "playbackController")
                    ?? gameStateView.GetComponent<ActionPlaybackController>(),
                Is.SameAs(playbackController),
                "GameStateViewPresenter should use the scene ActionPlaybackController instead of falling back to immediate final sync.");

            Assert.That(
                GetSerializedReference<ActionPlaybackController, BoardGridViewPresenter>(playbackController, "boardGrid"),
                Is.SameAs(FindRequired<BoardGridViewPresenter>()),
                "ActionPlaybackController should have the board grid presenter assigned for playback beat positioning.");
            Assert.That(
                GetSerializedReference<ActionPlaybackController, BoardContentViewPresenter>(playbackController, "boardContent"),
                Is.SameAs(FindRequired<BoardContentViewPresenter>()),
                "ActionPlaybackController should have the board content presenter assigned for removal, blocker, gravity, spawn, and extraction beats.");
            Assert.That(
                GetSerializedReference<ActionPlaybackController, WaterViewPresenter>(playbackController, "waterView"),
                Is.SameAs(FindRequired<WaterViewPresenter>()),
                "ActionPlaybackController should have the water presenter assigned for water rise beats.");
            Assert.That(
                GetSerializedReference<ActionPlaybackController, DockViewPresenter>(playbackController, "dockView"),
                Is.SameAs(FindRequired<DockViewPresenter>()),
                "ActionPlaybackController should have the dock presenter assigned for dock insertion, clear, warning, and jam beats.");
            Assert.That(
                GetSerializedReference<ActionPlaybackController, FxEventRouter>(playbackController, "fxEventRouter"),
                Is.SameAs(fxEventRouter),
                "ActionPlaybackController should route playback beats through the scene FxEventRouter.");

            Assert.That(fxEventRouter.FxRegistry, Is.Not.Null, "DebugGameplay should assign the Phase 1 FX registry asset.");
            if (fxEventRouter.FxRegistry is null)
            {
                throw new AssertionException("DebugGameplay should assign the Phase 1 FX registry asset.");
            }

            Assert.That(fxEventRouter.FxRegistry.WinFx, Is.Not.Null, "DebugGameplay should have terminal win FX assigned.");
            Assert.That(fxEventRouter.FxRegistry.LossFx, Is.Not.Null, "DebugGameplay should have terminal loss FX assigned.");
            Assert.That(
                fxEventRouter.BoardGrid,
                Is.SameAs(FindRequired<BoardGridViewPresenter>()),
                "FxEventRouter should resolve cell and row positions through the scene board grid presenter.");
            Assert.That(
                fxEventRouter.FxRoot != null || fxEventRouter.transform != null,
                Is.True,
                "FxEventRouter should have an FX root assigned or be able to safely use its own transform as the spawn root.");

            Assert.That(
                GetSerializedReference<BoardInputPresenter, GameStateViewPresenter>(boardInput, "gameStateView"),
                Is.SameAs(gameStateView),
                "BoardInputPresenter should route actions through GameStateViewPresenter so playback can lock input and apply Plan 1/2 beats.");
            Assert.That(victoryScreen.IsVisible, Is.False, "Victory screen should be present for terminal wins but hidden before a win.");
            Assert.That(lossScreen.IsVisible, Is.False, "Loss screen should be present for terminal losses but hidden before a loss.");

            LogAssert.NoUnexpectedReceived();
            yield return null;
        }

        private static T FindRequired<T>()
            where T : UnityObject
        {
            T? component = UnityObject.FindFirstObjectByType<T>();
            Assert.That(component, Is.Not.Null, $"Expected DebugGameplay to include {typeof(T).Name}.");
            if (component is null)
            {
                throw new AssertionException($"Expected DebugGameplay to include {typeof(T).Name}.");
            }

            return component;
        }

        private static TReference? GetSerializedReference<TOwner, TReference>(TOwner owner, string fieldName)
            where TOwner : class
            where TReference : class
        {
            return GetPrivateField<TOwner, TReference>(owner, fieldName);
        }

        private static TValue? GetPrivateField<TOwner, TValue>(TOwner owner, string fieldName)
            where TOwner : class
            where TValue : class
        {
            FieldInfo? field = typeof(TOwner).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Expected private field '{fieldName}' on {typeof(TOwner).Name}.");
            if (field is null)
            {
                return null;
            }

            object? value = field.GetValue(owner);
            Assert.That(
                value is null || value is TValue,
                Is.True,
                $"Expected private field '{fieldName}' on {typeof(TOwner).Name} to hold {typeof(TValue).Name}.");
            return value as TValue;
        }
    }
}
#endif
