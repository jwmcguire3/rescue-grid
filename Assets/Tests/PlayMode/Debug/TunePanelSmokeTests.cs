#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using NUnit.Framework;
using Rescue.Content;
using Rescue.Unity.Debugging;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Rescue.PlayMode.Tests.Debug
{
    public sealed class TunePanelSmokeTests
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
            DeletePresetAsset("TunePanelSmoke");
            if (DebugPanel.Instance is not null)
            {
                UnityEngine.Object.DestroyImmediate(DebugPanel.Instance.gameObject);
            }

            yield return null;
        }

        [UnityTest]
        public System.Collections.IEnumerator ChangingATunableReloadsTheLevel()
        {
            DebugPanel panel = DebugPanel.EnsureInstance();
            panel.ConfigureForTest(CreateTestLevel(), seed: 41);

            yield return null;

            Assert.That(panel.StepOneAction(), Is.True);

            yield return null;

            int revisionBefore = panel.LoadRevision;
            panel.ApplyTuneOverrides(new LevelTuningOverrides(WaterRiseInterval: 2));

            yield return null;

            Assert.That(panel.LoadRevision, Is.GreaterThan(revisionBefore));
            Assert.That(panel.CurrentState.ActionCount, Is.EqualTo(0));
            Assert.That(panel.CurrentState.Water.RiseInterval, Is.EqualTo(2));
        }

        [UnityTest]
        public System.Collections.IEnumerator SavingAndLoadingPresetRoundTripsOverrides()
        {
            DebugPanel panel = DebugPanel.EnsureInstance();
            panel.ConfigureForTest(CreateTestLevel(), seed: 12);

            yield return null;

            LevelTuningOverrides expected = new LevelTuningOverrides(
                WaterRiseInterval: 3,
                InitialFloodedRows: 1,
                AssistanceChance: 0.25d,
                ForceEmergencyAssistance: true,
                DockJamEnabled: false,
                DockSize: 9,
                DefaultCrateHp: 2,
                VineGrowthThreshold: 2);

            panel.ApplyTuneOverrides(expected);

            yield return null;

            string assetPath = panel.SaveTunePreset("TunePanelSmoke");

            yield return null;

            Assert.That(assetPath, Is.EqualTo("Assets/Editor/TuningPresets/TunePanelSmoke.asset"));

            panel.ApplyTuneOverrides(LevelTuningOverrides.None);

            yield return null;

            panel.LoadTunePreset("TunePanelSmoke");

            yield return null;

            Assert.That(panel.CurrentTuningOverrides, Is.EqualTo(expected));
            Assert.That(panel.CurrentState.Water.RiseInterval, Is.EqualTo(3));
            Assert.That(panel.CurrentState.Dock.Size, Is.EqualTo(9));
            Assert.That(panel.CurrentState.Vine.GrowthThreshold, Is.EqualTo(2));
        }

        [UnityTest]
        public System.Collections.IEnumerator CurrentSeedIsPreservedAcrossTuneReloadUnlessChangedExplicitly()
        {
            DebugPanel panel = DebugPanel.EnsureInstance();
            panel.ConfigureForTest(CreateTestLevel(), seed: 31);

            yield return null;

            panel.ApplyTuneOverrides(new LevelTuningOverrides(InitialFloodedRows: 1));

            yield return null;

            Assert.That(panel.CurrentSeed, Is.EqualTo(31));
            Assert.That(panel.CurrentState.Water.FloodedRows, Is.EqualTo(1));
        }

        private static LevelJson CreateTestLevel()
        {
            return new LevelJson
            {
                Id = "DBG_TUNE",
                Name = "Tune Smoke",
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
                    GrowthPriority = Array.Empty<TileCoordJson>(),
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
                    Intent = "Exercise tune panel smoke flows.",
                    ExpectedPath = "Change a tunable and reload.",
                    ExpectedFailMode = "Tune reload or preset flow breaks.",
                    WhatItProves = "Tune tab hot reload and presets are functional.",
                    IsRuleTeach = false,
                },
            };
        }

        private static void DeletePresetAsset(string presetName)
        {
#if UNITY_EDITOR
            string assetPath = $"Assets/Editor/TuningPresets/{presetName}.asset";
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) is not null)
            {
                AssetDatabase.DeleteAsset(assetPath);
                AssetDatabase.Refresh();
            }
#endif
        }
    }
}
#endif
