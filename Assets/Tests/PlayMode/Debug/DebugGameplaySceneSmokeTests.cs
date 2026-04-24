#if UNITY_EDITOR || DEVELOPMENT_BUILD
using NUnit.Framework;
using Rescue.Content;
using Rescue.Core.State;
using Rescue.Unity.Debugging;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Rescue.PlayMode.Tests.Debug
{
    public sealed class DebugGameplaySceneSmokeTests
    {
        [UnitySetUp]
        public System.Collections.IEnumerator SetUp()
        {
            if (DebugPanel.Instance is not null)
            {
                Object.DestroyImmediate(DebugPanel.Instance.gameObject);
            }

            yield return SceneManager.LoadSceneAsync("DebugGameplay", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTearDown]
        public System.Collections.IEnumerator TearDown()
        {
            if (DebugPanel.Instance is not null)
            {
                Object.DestroyImmediate(DebugPanel.Instance.gameObject);
            }

            yield return null;
        }

        [UnityTest]
        public System.Collections.IEnumerator DebugGameplayScene_RendersBoardAndDockFromDebugPanelState()
        {
            DebugPanel panel = DebugPanel.Instance ?? DebugPanel.EnsureInstance();
            panel.ConfigureForTest(CreateTestLevel(), seed: 17);

            yield return null;

            Transform boardRoot = GameObject.Find("BoardRoot").transform;
            Transform boardContentRoot = GameObject.Find("BoardContentRoot").transform;
            Transform waterRoot = GameObject.Find("WaterRoot").transform;
            Transform dockPieces = GameObject.Find("DockPieces").transform;

            LogAssert.NoUnexpectedReceived();
            Assert.That(boardRoot.childCount, Is.GreaterThan(0), "Expected the grid presenter to generate board anchors.");
            Assert.That(boardContentRoot.childCount, Is.GreaterThan(0), "Expected the content presenter to generate visible board content.");
            Assert.That(waterRoot.childCount, Is.GreaterThan(0), "Expected the water presenter to generate forecast/flood overlays.");
            Assert.That(dockPieces.childCount, Is.EqualTo(0), "Dock should start empty before stepping.");

            Assert.That(panel.StepOneAction(), Is.True);

            yield return null;

            Assert.That(dockPieces.childCount, Is.GreaterThan(0), "Expected the dock presenter to generate piece visuals after a step.");
        }

        private static LevelJson CreateTestLevel()
        {
            return new LevelJson
            {
                Id = "DBG_SCENE",
                Name = "Debug Gameplay Scene Smoke",
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
                DebrisTypePool = new[] { DebrisType.A, DebrisType.B, DebrisType.C, DebrisType.D },
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
                    Intent = "Verify the debug gameplay scene drives the runtime presenter rig.",
                    ExpectedPath = "Load the scene and step the opening pair.",
                    ExpectedFailMode = "Only the debug UI appears while the gameplay presenter roots stay empty.",
                    WhatItProves = "The scene hosts a functional GameStateViewPresenter rig alongside the DebugPanel flow.",
                    IsRuleTeach = false,
                },
            };
        }
    }
}
#endif
