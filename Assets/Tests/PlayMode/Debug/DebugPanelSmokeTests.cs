#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Reflection;
using NUnit.Framework;
using Rescue.Content;
using Rescue.Unity.Debugging;
using Rescue.Unity.Presentation;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rescue.PlayMode.Tests.Debug
{
    public sealed class DebugPanelSmokeTests
    {
        [UnitySetUp]
        public System.Collections.IEnumerator SetUp()
        {
            if (DebugPanel.Instance is not null)
            {
                UnityEngine.Object.DestroyImmediate(DebugPanel.Instance.gameObject);
            }

            DestroyTerminalScreensIfPresent();
            yield return null;
        }

        [UnityTearDown]
        public System.Collections.IEnumerator TearDown()
        {
            if (DebugPanel.Instance is not null)
            {
                UnityEngine.Object.DestroyImmediate(DebugPanel.Instance.gameObject);
            }

            DestroyTerminalScreensIfPresent();
            yield return null;
        }

        [UnityTest]
        public System.Collections.IEnumerator PanelLoadsWithoutCrashingGivenLoadedLevel()
        {
            DebugPanel panel = DebugPanel.EnsureInstance();
            panel.ConfigureForTest(CreateTestLevel(), seed: 17);

            yield return null;

            LogAssert.NoUnexpectedReceived();
            Assert.That(panel.CurrentLevelId, Is.EqualTo("DBG_TEST"));
            Assert.That(panel.CurrentState.ActionCount, Is.EqualTo(0));
            Assert.That(panel.CurrentState.Board.Width, Is.EqualTo(3));
            Assert.That(panel.CurrentWaterForecastSummary, Does.Contain("row 2"));
            Assert.That(panel.IsPanelMinimized, Is.True);
        }

        [UnityTest]
        public System.Collections.IEnumerator PanelStartsMinimizedAndCanBeExpanded()
        {
            DebugPanel panel = DebugPanel.EnsureInstance();
            panel.ConfigureForTest(CreateTestLevel(), seed: 15);

            yield return null;

            Assert.That(panel.IsPanelMinimized, Is.True);
            panel.SetPanelMinimized(false);

            yield return null;

            Assert.That(panel.IsPanelMinimized, Is.False);
        }

        [UnityTest]
        public System.Collections.IEnumerator PanelAutoMinimizesAfterOutsideClick()
        {
            DebugPanel panel = DebugPanel.EnsureInstance();
            panel.ConfigureForTest(CreateTestLevel(), seed: 22);

            yield return null;

            panel.SetPanelMinimized(false);
            panel.SimulateOutsideClickForTest();

            yield return null;

            Assert.That(panel.IsPanelMinimized, Is.True);
        }

        [UnityTest]
        public System.Collections.IEnumerator PanelStaysExpandedAfterInsideClick()
        {
            DebugPanel panel = DebugPanel.EnsureInstance();
            panel.ConfigureForTest(CreateTestLevel(), seed: 27);

            yield return null;

            panel.SetPanelMinimized(false);
            panel.SimulateInsideClickForTest();

            yield return null;

            Assert.That(panel.IsPanelMinimized, Is.False);
        }

        [UnityTest]
        public System.Collections.IEnumerator StepButtonAdvancesByExactlyOneAction()
        {
            DebugPanel panel = DebugPanel.EnsureInstance();
            panel.ConfigureForTest(CreateTestLevel(), seed: 17);

            yield return null;

            LogAssert.NoUnexpectedReceived();
            int before = panel.CurrentState.ActionCount;
            bool stepped = panel.StepOneAction();

            yield return null;

            Assert.That(stepped, Is.True);
            Assert.That(panel.CurrentState.ActionCount, Is.EqualTo(before + 1));
        }

        [UnityTest]
        public System.Collections.IEnumerator ResetReturnsStateToInitialForSameSeed()
        {
            DebugPanel panel = DebugPanel.EnsureInstance();
            panel.ConfigureForTest(CreateTestLevel(), seed: 31);

            yield return null;

            LogAssert.NoUnexpectedReceived();
            string initialJson = panel.ExportFullGameStateJson();
            Assert.That(panel.StepOneAction(), Is.True);

            yield return null;

            panel.ResetLevel();

            yield return null;

            Assert.That(panel.ExportFullGameStateJson(), Is.EqualTo(initialJson));
            Assert.That(panel.CurrentSeed, Is.EqualTo(31));
        }

        [UnityTest]
        public System.Collections.IEnumerator VictoryReplayHidesOverlayAndResetsCurrentLevel()
        {
            DebugPanel panel = DebugPanel.EnsureInstance();
            panel.ConfigureForTest(CreateTestLevel(), seed: 31);
            VictoryScreenPresenter victoryScreen = VictoryScreenPresenter.EnsureInstance();

            yield return null;

            string initialJson = panel.ExportFullGameStateJson();
            Assert.That(panel.StepOneAction(), Is.True);
            victoryScreen.Show();

            panel.ReplayCurrentLevel();

            yield return null;

            Assert.That(victoryScreen.IsVisible, Is.False);
            Assert.That(panel.ExportFullGameStateJson(), Is.EqualTo(initialJson));
        }

        [UnityTest]
        public System.Collections.IEnumerator LossReplayAndTryAgainHideOverlayAndResetCurrentLevel()
        {
            DebugPanel panel = DebugPanel.EnsureInstance();
            panel.ConfigureForTest(CreateTestLevel(), seed: 31);
            LossScreenPresenter lossScreen = LossScreenPresenter.EnsureInstance();

            yield return null;

            string initialJson = panel.ExportFullGameStateJson();
            Assert.That(panel.StepOneAction(), Is.True);
            lossScreen.Show();

            lossScreen.RequestReplay();

            yield return null;

            Assert.That(lossScreen.IsVisible, Is.False);
            Assert.That(panel.ExportFullGameStateJson(), Is.EqualTo(initialJson));

            Assert.That(panel.StepOneAction(), Is.True);
            lossScreen.Show();

            lossScreen.RequestTryAgain();

            yield return null;

            Assert.That(lossScreen.IsVisible, Is.False);
            Assert.That(panel.ExportFullGameStateJson(), Is.EqualTo(initialJson));
        }

        [UnityTest]
        public System.Collections.IEnumerator NextLevelLoadsFollowingStreamingAssetLevel()
        {
            DebugPanel panel = DebugPanel.EnsureInstance();
            panel.LoadLevel(Loader.LoadLevelDefinition("L01"), seed: 7);

            yield return null;

            Assert.That(panel.HasNextLevel(), Is.True);
            Assert.That(panel.LoadNextLevel(), Is.True);

            yield return null;

            Assert.That(panel.CurrentLevelId, Is.EqualTo("L02"));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public System.Collections.IEnumerator StateExportProducesValidJson()
        {
            DebugPanel panel = DebugPanel.EnsureInstance();
            panel.ConfigureForTest(CreateTestLevel(), seed: 9);

            yield return null;

            LogAssert.NoUnexpectedReceived();
            string json = panel.ExportFullGameStateJson();

            Assert.That(IsValidJson(json), Is.True);
        }

        [UnityTest]
        public System.Collections.IEnumerator NearRescueSummaryReflectsOneClearAwayTarget()
        {
            DebugPanel panel = DebugPanel.EnsureInstance();
            panel.ConfigureForTest(CreateNearRescueLevel(), seed: 19);

            yield return null;

            LogAssert.NoUnexpectedReceived();
            Assert.That(panel.CurrentNearRescueSummary, Is.EqualTo("0"));
            Assert.That(panel.ExportFullGameStateJson(), Does.Contain("\"oneClearAway\": true"));
        }

        [UnityTest]
        public System.Collections.IEnumerator DebugPanel_DoesNotThrowWithoutPresenter()
        {
            DebugPanel panel = DebugPanel.EnsureInstance();
            panel.ConfigureForTest(CreateTestLevel(), seed: 23);

            yield return null;

            Assert.DoesNotThrow(() => panel.StepOneAction());
            Assert.DoesNotThrow(() => panel.DebugUndo());
            Assert.DoesNotThrow(() => panel.ResetLevel());
            LogAssert.NoUnexpectedReceived();
        }

        private static bool IsValidJson(string json)
        {
            Assembly jsonAssembly = Assembly.Load(new AssemblyName("System.Text.Json"));
            Type serializerType = jsonAssembly.GetType("System.Text.Json.JsonSerializer", throwOnError: true)
                ?? throw new InvalidOperationException("JsonSerializer type was not found.");
            Type optionsType = jsonAssembly.GetType("System.Text.Json.JsonSerializerOptions", throwOnError: true)
                ?? throw new InvalidOperationException("JsonSerializerOptions type was not found.");
            MethodInfo deserializeMethod = serializerType.GetMethod(
                "Deserialize",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(string), typeof(Type), optionsType },
                modifiers: null)
                ?? throw new MissingMethodException("JsonSerializer.Deserialize(string, Type, JsonSerializerOptions) was not found.");
            object? options = Activator.CreateInstance(optionsType);
            object? value = deserializeMethod.Invoke(null, new object?[] { json, typeof(object), options });
            return value is not null;
        }

        private static void DestroyTerminalScreensIfPresent()
        {
            VictoryScreenPresenter? victoryScreen = UnityEngine.Object.FindFirstObjectByType<VictoryScreenPresenter>();
            if (victoryScreen is not null)
            {
                UnityEngine.Object.DestroyImmediate(victoryScreen.gameObject);
            }

            LossScreenPresenter? lossScreen = UnityEngine.Object.FindFirstObjectByType<LossScreenPresenter>();
            if (lossScreen is not null)
            {
                UnityEngine.Object.DestroyImmediate(lossScreen.gameObject);
            }
        }

        private static LevelJson CreateTestLevel()
        {
            return new LevelJson
            {
                Id = "DBG_TEST",
                Name = "Debug Panel Smoke",
                Board = new BoardJson
                {
                    Width = 3,
                    Height = 3,
                    Tiles = new[]
                    {
                        new[] { "A", "A", "." },
                        new[] { "B", "CR", "." },
                        new[] { ".", "T0", "." },
                    },
                },
                DebrisTypePool = new[] { Rescue.Core.State.DebrisType.A, Rescue.Core.State.DebrisType.B, Rescue.Core.State.DebrisType.C, Rescue.Core.State.DebrisType.D },
                Targets = new[]
                {
                    new TargetJson
                    {
                        Id = "0",
                        Row = 2,
                        Col = 1,
                    },
                },
                Water = new WaterJson
                {
                    RiseInterval = 4,
                },
                Vine = new VineJson
                {
                    GrowthThreshold = 4,
                    GrowthPriority = new[]
                    {
                        new TileCoordJson { Row = 0, Col = 2 },
                    },
                },
                Dock = new DockJson
                {
                    Size = 7,
                    JamEnabled = true,
                },
                Assistance = new AssistanceJson
                {
                    Chance = 0.7d,
                    ConsecutiveEmergencyCap = 2,
                },
                Meta = new MetaJson
                {
                    Intent = "Smoke-test debug panel level.",
                    ExpectedPath = "Step the opening pair.",
                    ExpectedFailMode = "Unexpected panel crash or invalid export.",
                    WhatItProves = "The debug panel can load, step, reset, and export.",
                    IsRuleTeach = false,
                },
            };
        }

        private static LevelJson CreateNearRescueLevel()
        {
            return new LevelJson
            {
                Id = "DBG_NEAR",
                Name = "Debug Near Rescue",
                Board = new BoardJson
                {
                    Width = 3,
                    Height = 3,
                    Tiles = new[]
                    {
                        new[] { ".", ".", "." },
                        new[] { ".", "CR", "." },
                        new[] { ".", "T0", "." },
                    },
                },
                DebrisTypePool = new[] { Rescue.Core.State.DebrisType.A, Rescue.Core.State.DebrisType.B, Rescue.Core.State.DebrisType.C, Rescue.Core.State.DebrisType.D },
                Targets = new[]
                {
                    new TargetJson
                    {
                        Id = "0",
                        Row = 2,
                        Col = 1,
                    },
                },
                Water = new WaterJson
                {
                    RiseInterval = 4,
                },
                Vine = new VineJson
                {
                    GrowthThreshold = 4,
                    GrowthPriority = System.Array.Empty<TileCoordJson>(),
                },
                Dock = new DockJson
                {
                    Size = 7,
                    JamEnabled = false,
                },
                Assistance = new AssistanceJson
                {
                    Chance = 0.5d,
                    ConsecutiveEmergencyCap = 2,
                },
                Meta = new MetaJson
                {
                    Intent = "Surface the one-clear-away proxy in the debug panel.",
                    ExpectedPath = "Observe the target state.",
                    ExpectedFailMode = "Near-rescue state is missing from UI/export.",
                    WhatItProves = "The debug panel exposes one-clear-away target state.",
                    IsRuleTeach = false,
                },
            };
        }
    }
}
#endif
