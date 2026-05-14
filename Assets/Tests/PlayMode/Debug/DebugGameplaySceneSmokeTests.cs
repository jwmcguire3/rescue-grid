#if UNITY_EDITOR || DEVELOPMENT_BUILD
using NUnit.Framework;
using Rescue.Content;
using Rescue.Core.State;
using Rescue.Unity.Debugging;
using Rescue.Unity.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

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
        public System.Collections.IEnumerator DebugGameplayScene_StartsWithEmptyRuntimeRootsAndNoLegacyDockStandIns()
        {
            Transform boardRoot = GameObject.Find("BoardRoot").transform;
            Transform boardContentRoot = GameObject.Find("BoardContentRoot").transform;
            Transform waterRoot = GameObject.Find("WaterRoot").transform;
            Transform dockRoot = GameObject.Find("DockRoot").transform;

            Assert.That(boardRoot.childCount, Is.EqualTo(0));
            Assert.That(boardContentRoot.childCount, Is.EqualTo(0));
            Assert.That(waterRoot.childCount, Is.EqualTo(0));
            Assert.That(dockRoot.childCount, Is.EqualTo(0));
            Assert.That(GameObject.Find("DockPieces"), Is.Null);
            Assert.That(dockRoot.Find("DockVisual"), Is.Null);
            AssertCameraUsesFrontTableOrthographicView();
            AssertBoardStageLayout(boardRoot, boardContentRoot, waterRoot, dockRoot);
            AssertDirectionalLightMatchesStaging();

            for (int slotIndex = 0; slotIndex < 7; slotIndex++)
            {
                Assert.That(dockRoot.Find($"Slot_{slotIndex:00}"), Is.Null);
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
            Transform dockRoot = GameObject.Find("DockRoot").transform;
            Transform? dockPieces = dockRoot.Find("DockPieces");

            LogAssert.NoUnexpectedReceived();
            Assert.That(boardRoot.childCount, Is.GreaterThan(0), "Expected the grid presenter to generate board anchors.");
            Assert.That(boardContentRoot.childCount, Is.GreaterThan(0), "Expected the content presenter to generate visible board content.");
            Assert.That(waterRoot.childCount, Is.GreaterThan(0), "Expected the water presenter to generate forecast/flood overlays.");
            Assert.That(dockRoot.Find("SharedDockVisualInstance"), Is.Not.Null, "Expected the dock presenter to spawn the shared dock runtime visual.");
            AssertBoardFitsGameplayViewport(boardRoot, 3, 3);
            Assert.That(dockRoot.Find("DockVisual"), Is.Null, "Legacy dock mesh stand-ins should not be scene-authored.");
            Assert.That(dockPieces is null || dockPieces.childCount == 0, Is.True, "Dock should start empty before stepping.");

            for (int slotIndex = 0; slotIndex < 7; slotIndex++)
            {
                Assert.That(dockRoot.Find($"Slot_{slotIndex:00}"), Is.Null, "Legacy dock slot stand-ins should not exist as direct scene children.");
            }

            Assert.That(panel.StepOneAction(), Is.True);

            GameStateViewPresenter? presenter = Object.FindFirstObjectByType<GameStateViewPresenter>();
            Assert.That(presenter, Is.Not.Null, "Expected DebugGameplay to include a GameStateViewPresenter.");
            float timeoutAt = Time.realtimeSinceStartup + 2f;
            while (presenter is not null && presenter.IsPlaybackActive && Time.realtimeSinceStartup < timeoutAt)
            {
                yield return null;
            }

            dockPieces = dockRoot.Find("DockPieces");
            Assert.That(dockPieces, Is.Not.Null, "Expected the dock presenter to materialize a runtime piece container after stepping.");
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
                        new[] { ".", "T0", "B" },
                    },
                },
                DebrisTypePool = new[] { DebrisType.A, DebrisType.B, DebrisType.C, DebrisType.D, DebrisType.E },
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

        private static void AssertCameraUsesFrontTableOrthographicView()
        {
            Camera? camera = Camera.main;
            Assert.That(camera, Is.Not.Null, "DebugGameplay.unity should include a tagged Main Camera.");
            if (camera is null)
            {
                throw new AssertionException("DebugGameplay.unity should include a tagged Main Camera.");
            }

            Assert.That(camera.orthographic, Is.True, "DebugGameplay camera should stay orthographic for grid readability.");
            Assert.That(camera.orthographicSize, Is.EqualTo(PortraitGameSceneLayout.CameraPortraitOrthographicSize).Within(0.001f));
            Assert.That(Vector3.Distance(camera.transform.position, PortraitGameSceneLayout.CameraPortraitPosition), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(camera.transform.rotation, PortraitGameSceneLayout.CameraPortraitRotation), Is.LessThan(0.1f));
            Assert.That(camera.transform.forward.y, Is.LessThan(-0.99f), "DebugGameplay camera should look straight down so the board starts square in frame.");
        }

        private static void AssertBoardStageLayout(Transform boardRoot, Transform boardContentRoot, Transform waterRoot, Transform dockRoot)
        {
            Transform? stageRoot = boardRoot.parent;
            Assert.That(stageRoot, Is.Not.Null, "BoardRoot should be parented under BoardStageRoot.");
            if (stageRoot is null)
            {
                throw new AssertionException("BoardRoot should be parented under BoardStageRoot.");
            }

            Assert.That(stageRoot.name, Is.EqualTo("BoardStageRoot"));
            Assert.That(boardContentRoot.parent, Is.SameAs(stageRoot), "Board content should share the board stage transform.");
            Assert.That(waterRoot.parent, Is.SameAs(stageRoot), "Water overlays should share the board stage transform.");
            Assert.That(dockRoot.parent, Is.Not.SameAs(stageRoot), "DockRoot should stay separate so its staging can be tuned independently.");
            Assert.That(Vector3.Distance(stageRoot.localPosition, PortraitGameSceneLayout.BoardPortraitPosition), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(stageRoot.localRotation, PortraitGameSceneLayout.BoardPortraitRotation), Is.LessThan(0.1f), "BoardStageRoot should keep the gameplay/input coordinate contract aligned.");
            Assert.That(Vector3.Distance(stageRoot.localScale, PortraitGameSceneLayout.BoardPortraitScale), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(dockRoot.localPosition, PortraitGameSceneLayout.DockPortraitPosition), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(dockRoot.localRotation, PortraitGameSceneLayout.DockPortraitRotation), Is.LessThan(0.1f), "DockRoot should match the staged dock tilt.");
            Assert.That(Vector3.Distance(dockRoot.localScale, PortraitGameSceneLayout.DockPortraitScale), Is.LessThan(0.001f));
        }

        private static void AssertBoardFitsGameplayViewport(Transform boardRoot, int boardWidth, int boardHeight)
        {
            Camera? camera = Camera.main;
            Assert.That(camera, Is.Not.Null, "DebugGameplay.unity should include a tagged Main Camera.");
            if (camera is null)
            {
                throw new AssertionException("DebugGameplay.unity should include a tagged Main Camera.");
            }

            Transform topLeft = boardRoot.Find("Cell_00_00") ?? throw new AssertionException("Expected top-left board cell.");
            Transform topRight = boardRoot.Find($"Cell_00_{boardWidth - 1:00}") ?? throw new AssertionException("Expected top-right board cell.");
            Transform bottomLeft = boardRoot.Find($"Cell_{boardHeight - 1:00}_00") ?? throw new AssertionException("Expected bottom-left board cell.");
            Transform bottomRight = boardRoot.Find($"Cell_{boardHeight - 1:00}_{boardWidth - 1:00}") ?? throw new AssertionException("Expected bottom-right board cell.");

            Vector3 topLeftViewport = camera.WorldToViewportPoint(topLeft.position);
            Vector3 topRightViewport = camera.WorldToViewportPoint(topRight.position);
            Vector3 bottomLeftViewport = camera.WorldToViewportPoint(bottomLeft.position);
            Vector3 bottomRightViewport = camera.WorldToViewportPoint(bottomRight.position);

            AssertViewportPointVisible(topLeftViewport, "top-left");
            AssertViewportPointVisible(topRightViewport, "top-right");
            AssertViewportPointVisible(bottomLeftViewport, "bottom-left");
            AssertViewportPointVisible(bottomRightViewport, "bottom-right");

            Assert.That(topRightViewport.x, Is.GreaterThan(topLeftViewport.x), "Top board row should project left-to-right.");
            Assert.That(Mathf.Abs(topRightViewport.y - topLeftViewport.y), Is.LessThan(0.01f), "Top board row should project horizontally.");
            Assert.That(Mathf.Abs(bottomLeftViewport.x - topLeftViewport.x), Is.LessThan(0.01f), "Left board column should project vertically.");
            Assert.That(topLeftViewport.y, Is.GreaterThan(bottomLeftViewport.y), "Board rows should advance top-to-bottom.");
        }

        private static void AssertViewportPointVisible(Vector3 viewportPoint, string label)
        {
            Assert.That(viewportPoint.z, Is.GreaterThan(0f), $"{label} board corner should be in front of the camera.");
            Assert.That(viewportPoint.x, Is.InRange(0.05f, 0.95f), $"{label} board corner should stay inside the gameplay viewport horizontally.");
            Assert.That(viewportPoint.y, Is.InRange(0.05f, 0.95f), $"{label} board corner should stay inside the gameplay viewport vertically.");
        }

        private static void AssertDirectionalLightMatchesStaging()
        {
            Light[] lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            Light? light = System.Array.Find(lights, candidate => candidate.name == "Directional Light");
            Assert.That(light, Is.Not.Null, "DebugGameplay.unity should include the staged Directional Light.");
            if (light is null)
            {
                throw new AssertionException("DebugGameplay.unity should include the staged Directional Light.");
            }

            Assert.That(light.type, Is.EqualTo(LightType.Directional));
            Assert.That(Vector3.Distance(light.transform.localPosition, new Vector3(0f, 3f, 0f)), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(light.transform.localRotation, Quaternion.Euler(60f, 60f, 0f)), Is.LessThan(0.1f));
            Assert.That(Vector3.Distance(light.transform.localScale, Vector3.one), Is.LessThan(0.001f));
            Assert.That(light.color.r, Is.EqualTo(1f).Within(0.001f));
            Assert.That(light.color.g, Is.EqualTo(217f / 255f).Within(0.001f));
            Assert.That(light.color.b, Is.EqualTo(173f / 255f).Within(0.001f));
            Assert.That(light.intensity, Is.EqualTo(1f).Within(0.001f));
            Assert.That(light.bounceIntensity, Is.EqualTo(1f).Within(0.001f));
            Assert.That(light.shadows, Is.EqualTo(LightShadows.Soft));
            Assert.That(light.cookie, Is.Null);
            Assert.That(light.flare, Is.Null);
            Assert.That(light.renderMode, Is.EqualTo(LightRenderMode.Auto));
            Assert.That(light.cullingMask, Is.EqualTo(-1));
            Assert.That(light.lightmapBakeType, Is.EqualTo(LightmapBakeType.Baked));
#if UNITY_EDITOR
            SerializedObject serializedLight = new SerializedObject(light);
            Assert.That(serializedLight.FindProperty("m_DrawHalo").boolValue, Is.False);
#endif
        }
    }
}
#endif
