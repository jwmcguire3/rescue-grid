#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Reflection;
using NUnit.Framework;
using Rescue.Content;
using Rescue.Unity.Debugging;
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

            yield return null;
        }

        [UnityTearDown]
        public System.Collections.IEnumerator TearDown()
        {
            if (DebugPanel.Instance is not null)
            {
                UnityEngine.Object.DestroyImmediate(DebugPanel.Instance.gameObject);
            }

            yield return null;
        }

        [UnityTest]
        public System.Collections.IEnumerator PanelLoadsWithoutCrashingGivenLoadedLevel()
        {
            LogAssert.Expect(LogType.Warning, "No Theme Style Sheet set to PanelSettings , UI will not render properly");
            DebugPanel panel = DebugPanel.EnsureInstance();
            panel.ConfigureForTest(CreateTestLevel(), seed: 17);

            yield return null;

            Assert.That(panel.CurrentLevelId, Is.EqualTo("DBG_TEST"));
            Assert.That(panel.CurrentState.ActionCount, Is.EqualTo(0));
            Assert.That(panel.CurrentState.Board.Width, Is.EqualTo(3));
            Assert.That(panel.CurrentWaterForecastSummary, Does.Contain("row 2"));
        }

        [UnityTest]
        public System.Collections.IEnumerator StepButtonAdvancesByExactlyOneAction()
        {
            LogAssert.Expect(LogType.Warning, "No Theme Style Sheet set to PanelSettings , UI will not render properly");
            DebugPanel panel = DebugPanel.EnsureInstance();
            panel.ConfigureForTest(CreateTestLevel(), seed: 17);

            yield return null;

            int before = panel.CurrentState.ActionCount;
            bool stepped = panel.StepOneAction();

            yield return null;

            Assert.That(stepped, Is.True);
            Assert.That(panel.CurrentState.ActionCount, Is.EqualTo(before + 1));
        }

        [UnityTest]
        public System.Collections.IEnumerator ResetReturnsStateToInitialForSameSeed()
        {
            LogAssert.Expect(LogType.Warning, "No Theme Style Sheet set to PanelSettings , UI will not render properly");
            DebugPanel panel = DebugPanel.EnsureInstance();
            panel.ConfigureForTest(CreateTestLevel(), seed: 31);

            yield return null;

            string initialJson = panel.ExportFullGameStateJson();
            Assert.That(panel.StepOneAction(), Is.True);

            yield return null;

            panel.ResetLevel();

            yield return null;

            Assert.That(panel.ExportFullGameStateJson(), Is.EqualTo(initialJson));
            Assert.That(panel.CurrentSeed, Is.EqualTo(31));
        }

        [UnityTest]
        public System.Collections.IEnumerator StateExportProducesValidJson()
        {
            LogAssert.Expect(LogType.Warning, "No Theme Style Sheet set to PanelSettings , UI will not render properly");
            DebugPanel panel = DebugPanel.EnsureInstance();
            panel.ConfigureForTest(CreateTestLevel(), seed: 9);

            yield return null;

            string json = panel.ExportFullGameStateJson();

            Assert.That(IsValidJson(json), Is.True);
        }

        [UnityTest]
        public System.Collections.IEnumerator NearRescueSummaryReflectsOneClearAwayTarget()
        {
            LogAssert.Expect(LogType.Warning, "No Theme Style Sheet set to PanelSettings , UI will not render properly");
            DebugPanel panel = DebugPanel.EnsureInstance();
            panel.ConfigureForTest(CreateNearRescueLevel(), seed: 19);

            yield return null;

            Assert.That(panel.CurrentNearRescueSummary, Is.EqualTo("0"));
            Assert.That(panel.ExportFullGameStateJson(), Does.Contain("\"oneClearAway\":true"));
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
