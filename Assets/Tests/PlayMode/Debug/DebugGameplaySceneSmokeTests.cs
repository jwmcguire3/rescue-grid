#if UNITY_EDITOR || DEVELOPMENT_BUILD
using NUnit.Framework;
using Rescue.Content;
using Rescue.Core.State;
using Rescue.Unity.BoardPresentation;
using Rescue.Unity.Debugging;
using Rescue.Unity.Presentation;
using Rescue.PlayMode.Tests.Smoke;
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
            GameStateViewPresenter? presenter = Object.FindFirstObjectByType<GameStateViewPresenter>();

            Assert.That(presenter, Is.Not.Null, "Expected DebugGameplay to include a GameStateViewPresenter.");
            if (presenter is null)
            {
                throw new AssertionException("Expected DebugGameplay to include a GameStateViewPresenter.");
            }

            BoardContentViewPresenter? contentPresenter = Object.FindFirstObjectByType<BoardContentViewPresenter>();
            Assert.That(contentPresenter, Is.Not.Null, "Expected DebugGameplay to include a BoardContentViewPresenter.");
            if (contentPresenter is null)
            {
                throw new AssertionException("Expected DebugGameplay to include a BoardContentViewPresenter.");
            }

            GameState currentState = presenter.CurrentState ?? throw new AssertionException("DebugPanel did not load a test state.");
            DaisyTargetSceneAssertions.AssertLiveTargetsAreDaisyBacked(currentState, contentPresenter);
            LogAssert.NoUnexpectedReceived();
            Assert.That(boardRoot.childCount, Is.GreaterThan(0), "Expected the grid presenter to generate board anchors.");
            Assert.That(boardContentRoot.childCount, Is.GreaterThan(0), "Expected the content presenter to generate visible board content.");
            Assert.That(waterRoot.childCount, Is.GreaterThan(0), "Expected the water presenter to generate forecast/flood overlays.");
            Assert.That(dockRoot.Find("SharedDockVisualInstance"), Is.Not.Null, "Expected the dock presenter to spawn the shared dock runtime visual.");
            AssertBoardFitsGameplayViewport(boardRoot, 3, 3);
            AssertCameraRaysHitVisibleCells(boardRoot, 3, 3);
            Assert.That(dockRoot.Find("DockVisual"), Is.Null, "Legacy dock mesh stand-ins should not be scene-authored.");
            Assert.That(dockPieces is null || dockPieces.childCount == 0, Is.True, "Dock should start empty before stepping.");

            for (int slotIndex = 0; slotIndex < 7; slotIndex++)
            {
                Assert.That(dockRoot.Find($"Slot_{slotIndex:00}"), Is.Null, "Legacy dock slot stand-ins should not exist as direct scene children.");
            }

            Assert.That(panel.StepOneAction(), Is.True);

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

            string diagnostics = BuildCameraDiagnostics(camera);
            TestContext.Out.WriteLine(diagnostics);
            Assert.That(camera.name, Is.EqualTo("Main Camera"), diagnostics);
            Assert.That(camera.CompareTag("MainCamera"), Is.True, diagnostics);
            Assert.That(camera.enabled, Is.True, diagnostics);
            Assert.That(camera.gameObject.activeInHierarchy, Is.True, diagnostics);
            Assert.That(camera.targetDisplay, Is.EqualTo(0), diagnostics);
            Assert.That(camera.targetTexture, Is.Null, diagnostics);
            Assert.That(camera.orthographic, Is.True, $"DebugGameplay camera should stay orthographic for grid readability.\n{diagnostics}");
            Assert.That(camera.orthographicSize, Is.EqualTo(PortraitGameSceneLayout.CameraPortraitOrthographicSize).Within(0.001f), diagnostics);
            Assert.That(Vector3.Distance(camera.transform.position, PortraitGameSceneLayout.CameraPortraitPosition), Is.LessThan(0.001f), diagnostics);
            Assert.That(Quaternion.Angle(camera.transform.rotation, PortraitGameSceneLayout.CameraPortraitRotation), Is.LessThan(0.1f), diagnostics);
            Assert.That(camera.transform.forward.y, Is.LessThan(-0.85f), $"DebugGameplay camera should keep a readable downward angle.\n{diagnostics}");
            Assert.That(camera.transform.forward.z, Is.GreaterThan(0.45f), $"DebugGameplay camera should use the front-table presentation pitch instead of a straight-down diagnostic view.\n{diagnostics}");
            Assert.That(camera.transform.up.z, Is.GreaterThan(0.80f), $"DebugGameplay camera up should keep board rows reading top-to-bottom.\n{diagnostics}");
            AssertNoCompetingGameplayCamera(camera, diagnostics);
        }

        private static void AssertBoardStageLayout(Transform boardRoot, Transform boardContentRoot, Transform waterRoot, Transform dockRoot)
        {
            Transform? viewRoot = GameObject.Find("GameStateViewRoot")?.transform;
            Transform? stageRoot = boardRoot.parent;
            Assert.That(stageRoot, Is.Not.Null, "BoardRoot should be parented under BoardStageRoot.");
            if (stageRoot is null)
            {
                throw new AssertionException("BoardRoot should be parented under BoardStageRoot.");
            }

            string diagnostics = BuildRootDiagnostics(viewRoot, stageRoot, boardRoot, boardContentRoot, waterRoot, dockRoot, Camera.main?.transform);
            TestContext.Out.WriteLine(diagnostics);
            Assert.That(viewRoot, Is.Not.Null, diagnostics);
            Assert.That(Quaternion.Angle(viewRoot!.localRotation, Quaternion.identity), Is.LessThan(0.1f), "GameStateViewRoot should not rotate board/input space.");
            Assert.That(stageRoot.name, Is.EqualTo("BoardStageRoot"));
            Assert.That(boardContentRoot.parent, Is.SameAs(stageRoot), "Board content should share the board stage transform.");
            Assert.That(waterRoot.parent, Is.SameAs(stageRoot), "Water overlays should share the board stage transform.");
            Assert.That(dockRoot.parent, Is.Not.SameAs(stageRoot), "DockRoot should stay separate so its staging can be tuned independently.");
            Assert.That(Vector3.Distance(stageRoot.localPosition, PortraitGameSceneLayout.BoardPortraitPosition), Is.LessThan(0.001f), diagnostics);
            Assert.That(Quaternion.Angle(stageRoot.localRotation, PortraitGameSceneLayout.BoardPortraitRotation), Is.LessThan(0.1f), $"BoardStageRoot should keep the gameplay/input coordinate contract aligned.\n{diagnostics}");
            Assert.That(Vector3.Distance(stageRoot.localScale, PortraitGameSceneLayout.BoardPortraitScale), Is.LessThan(0.001f), diagnostics);
            Assert.That(Vector3.Distance(boardRoot.localPosition, Vector3.zero), Is.LessThan(0.001f), diagnostics);
            Assert.That(Quaternion.Angle(boardRoot.localRotation, Quaternion.identity), Is.LessThan(0.1f), diagnostics);
            Assert.That(Vector3.Distance(boardContentRoot.localPosition, Vector3.zero), Is.LessThan(0.001f), diagnostics);
            Assert.That(Quaternion.Angle(boardContentRoot.localRotation, Quaternion.identity), Is.LessThan(0.1f), diagnostics);
            Assert.That(Vector3.Distance(waterRoot.localPosition, Vector3.zero), Is.LessThan(0.001f), diagnostics);
            Assert.That(Quaternion.Angle(waterRoot.localRotation, Quaternion.identity), Is.LessThan(0.1f), diagnostics);
            Assert.That(Vector3.Distance(dockRoot.localPosition, PortraitGameSceneLayout.DockPortraitPosition), Is.LessThan(0.001f), diagnostics);
            Assert.That(Quaternion.Angle(dockRoot.localRotation, PortraitGameSceneLayout.DockPortraitRotation), Is.LessThan(0.1f), $"DockRoot should match the staged dock tilt.\n{diagnostics}");
            Assert.That(Vector3.Distance(dockRoot.localScale, PortraitGameSceneLayout.DockPortraitScale), Is.LessThan(0.001f), diagnostics);
            AssertPlanarAxesAgree(stageRoot, dockRoot, diagnostics);
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
            string diagnostics = BuildProjectionDiagnostics(camera, topLeft, topRight, bottomLeft, bottomRight);
            TestContext.Out.WriteLine(diagnostics);

            AssertViewportPointVisible(topLeftViewport, "top-left", diagnostics);
            AssertViewportPointVisible(topRightViewport, "top-right", diagnostics);
            AssertViewportPointVisible(bottomLeftViewport, "bottom-left", diagnostics);
            AssertViewportPointVisible(bottomRightViewport, "bottom-right", diagnostics);

            Assert.That(topRightViewport.x, Is.GreaterThan(topLeftViewport.x), $"Top board row should project left-to-right.\n{diagnostics}");
            Assert.That(Mathf.Abs(topRightViewport.y - topLeftViewport.y), Is.LessThan(0.01f), $"Top board row should project horizontally.\n{diagnostics}");
            Assert.That(Mathf.Abs(bottomLeftViewport.x - topLeftViewport.x), Is.LessThan(0.01f), $"Left board column should project vertically.\n{diagnostics}");
            Assert.That(topLeftViewport.y, Is.GreaterThan(bottomLeftViewport.y), $"Board rows should advance top-to-bottom.\n{diagnostics}");
        }

        private static void AssertViewportPointVisible(Vector3 viewportPoint, string label, string diagnostics)
        {
            Assert.That(viewportPoint.z, Is.GreaterThan(0f), $"{label} board corner should be in front of the camera.\n{diagnostics}");
            Assert.That(viewportPoint.x, Is.InRange(0.05f, 0.95f), $"{label} board corner should stay inside the gameplay viewport horizontally.\n{diagnostics}");
            Assert.That(viewportPoint.y, Is.InRange(0.05f, 0.95f), $"{label} board corner should stay inside the gameplay viewport vertically.\n{diagnostics}");
        }

        private static void AssertCameraRaysHitVisibleCells(Transform boardRoot, int boardWidth, int boardHeight)
        {
            Camera? camera = Camera.main;
            Assert.That(camera, Is.Not.Null, "DebugGameplay.unity should include a tagged Main Camera.");
            if (camera is null)
            {
                throw new AssertionException("DebugGameplay.unity should include a tagged Main Camera.");
            }

            Physics.SyncTransforms();
            AssertCameraRayHitsCell(camera, boardRoot.Find("Cell_00_00"), new TileCoord(0, 0), "top-left");
            AssertCameraRayHitsCell(camera, boardRoot.Find($"Cell_00_{boardWidth - 1:00}"), new TileCoord(0, boardWidth - 1), "top-right");
            AssertCameraRayHitsCell(camera, boardRoot.Find($"Cell_{boardHeight - 1:00}_00"), new TileCoord(boardHeight - 1, 0), "bottom-left");
            AssertCameraRayHitsCell(camera, boardRoot.Find($"Cell_{boardHeight - 1:00}_{boardWidth - 1:00}"), new TileCoord(boardHeight - 1, boardWidth - 1), "bottom-right");
            int centerRow = boardHeight / 2;
            int centerCol = boardWidth / 2;
            AssertCameraRayHitsCell(camera, boardRoot.Find($"Cell_{centerRow:00}_{centerCol:00}"), new TileCoord(centerRow, centerCol), "center");
        }

        private static void AssertCameraRayHitsCell(Camera camera, Transform? anchor, TileCoord expectedCoord, string label)
        {
            Assert.That(anchor, Is.Not.Null, $"Expected {label} board cell.");
            if (anchor is null)
            {
                throw new AssertionException($"Expected {label} board cell.");
            }

            Vector3 screen = camera.WorldToScreenPoint(anchor.position);
            Ray ray = camera.ScreenPointToRay(screen);
            Assert.That(Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, ~0, QueryTriggerInteraction.Ignore), Is.True, $"{label} ray should hit a board cell from screen={FormatVector(screen)}.");
            BoardCellView? cellView = hit.collider.GetComponentInParent<BoardCellView>();
            Assert.That(cellView, Is.Not.Null, $"{label} ray hit '{hit.collider.name}' without a BoardCellView parent.");
            Assert.That(cellView!.Coord, Is.EqualTo(expectedCoord), $"{label} ray hit '{hit.collider.name}' at {FormatVector(hit.point)}.");
        }

        private static void AssertNoCompetingGameplayCamera(Camera mainCamera, string diagnostics)
        {
            Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera candidate = cameras[i];
                if (!IsGameplayRenderCamera(candidate, mainCamera))
                {
                    continue;
                }

                Assert.That(candidate.depth, Is.LessThan(mainCamera.depth), $"An enabled camera can render over Camera.main.\n{diagnostics}");
            }
        }

        private static bool IsGameplayRenderCamera(Camera candidate, Camera mainCamera)
        {
            if (candidate == mainCamera ||
                !candidate.enabled ||
                !candidate.gameObject.activeInHierarchy ||
                candidate.targetTexture is not null ||
                candidate.targetDisplay != mainCamera.targetDisplay)
            {
                return false;
            }

            if (candidate.hideFlags != HideFlags.None ||
                candidate.gameObject.hideFlags != HideFlags.None)
            {
                return false;
            }

            Scene candidateScene = candidate.gameObject.scene;
            Scene mainScene = mainCamera.gameObject.scene;
            if (!candidateScene.IsValid() || !candidateScene.isLoaded)
            {
                return false;
            }

            return candidateScene == mainScene ||
                string.Equals(candidateScene.name, "DontDestroyOnLoad", System.StringComparison.Ordinal);
        }

        private static void AssertPlanarAxesAgree(Transform boardStageRoot, Transform dockRoot, string diagnostics)
        {
            Vector3 boardRight = Vector3.ProjectOnPlane(boardStageRoot.right, Vector3.up).normalized;
            Vector3 dockRight = Vector3.ProjectOnPlane(dockRoot.right, Vector3.up).normalized;
            Vector3 boardForward = Vector3.ProjectOnPlane(boardStageRoot.forward, Vector3.up).normalized;
            Vector3 dockForward = Vector3.ProjectOnPlane(dockRoot.forward, Vector3.up).normalized;
            Assert.That(Vector3.Dot(boardRight, dockRight), Is.GreaterThan(0.99f), $"Board and dock right axes should agree.\n{diagnostics}");
            Assert.That(Vector3.Dot(boardForward, dockForward), Is.GreaterThan(0.96f), $"Board and dock forward axes should agree in the gameplay plane.\n{diagnostics}");
        }

        private static string BuildCameraDiagnostics(Camera mainCamera)
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            builder.AppendLine($"[CameraDiagnostics] activeScene={SceneManager.GetActiveScene().name} screen={Screen.width}x{Screen.height}");
            Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera camera = cameras[i];
                builder.AppendLine(
                    $"camera[{i}] name='{camera.name}' path='{GetHierarchyPath(camera.transform)}' scene='{camera.gameObject.scene.name}' scenePath='{camera.gameObject.scene.path}' hideFlags={camera.hideFlags} objectHideFlags={camera.gameObject.hideFlags} isMain={camera == mainCamera} tag='{camera.tag}' active={camera.gameObject.activeInHierarchy} enabled={camera.enabled} depth={camera.depth:0.###} display={camera.targetDisplay} targetTexture={(camera.targetTexture == null ? "<null>" : camera.targetTexture.name)} ortho={camera.orthographic} orthoSize={camera.orthographicSize:0.###} pos={FormatVector(camera.transform.position)} euler={FormatVector(camera.transform.eulerAngles)} forward={FormatVector(camera.transform.forward)} up={FormatVector(camera.transform.up)}");
            }

            return builder.ToString();
        }

        private static string BuildRootDiagnostics(
            Transform? viewRoot,
            Transform boardStageRoot,
            Transform boardRoot,
            Transform boardContentRoot,
            Transform waterRoot,
            Transform dockRoot,
            Transform? cameraRoot)
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            builder.AppendLine("[RootDiagnostics]");
            AppendTransformDiagnostics(builder, "GameStateViewRoot", viewRoot);
            AppendTransformDiagnostics(builder, "BoardStageRoot", boardStageRoot);
            AppendTransformDiagnostics(builder, "BoardRoot", boardRoot);
            AppendTransformDiagnostics(builder, "BoardContentRoot", boardContentRoot);
            AppendTransformDiagnostics(builder, "WaterRoot", waterRoot);
            AppendTransformDiagnostics(builder, "DockRoot", dockRoot);
            AppendTransformDiagnostics(builder, "Main Camera", cameraRoot);
            return builder.ToString();
        }

        private static string BuildProjectionDiagnostics(Camera camera, Transform topLeft, Transform topRight, Transform bottomLeft, Transform bottomRight)
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            builder.AppendLine("[ProjectionDiagnostics]");
            AppendProjectionDiagnostics(builder, camera, "Cell_00_00", topLeft);
            AppendProjectionDiagnostics(builder, camera, topRight.name, topRight);
            AppendProjectionDiagnostics(builder, camera, bottomLeft.name, bottomLeft);
            AppendProjectionDiagnostics(builder, camera, bottomRight.name, bottomRight);
            return builder.ToString();
        }

        private static void AppendProjectionDiagnostics(System.Text.StringBuilder builder, Camera camera, string label, Transform transform)
        {
            builder.AppendLine($"{label}: world={FormatVector(transform.position)} viewport={FormatVector(camera.WorldToViewportPoint(transform.position))} screen={FormatVector(camera.WorldToScreenPoint(transform.position))}");
        }

        private static void AppendTransformDiagnostics(System.Text.StringBuilder builder, string label, Transform? transform)
        {
            if (transform is null)
            {
                builder.AppendLine($"{label}: <missing>");
                return;
            }

            builder.AppendLine($"{label}: parent='{(transform.parent is null ? "<none>" : transform.parent.name)}' localPos={FormatVector(transform.localPosition)} localEuler={FormatVector(transform.localEulerAngles)} localScale={FormatVector(transform.localScale)} worldPos={FormatVector(transform.position)} worldEuler={FormatVector(transform.eulerAngles)} right={FormatVector(transform.right)} forward={FormatVector(transform.forward)}");
        }

        private static string GetHierarchyPath(Transform transform)
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder(transform.name);
            Transform? current = transform.parent;
            while (current is not null)
            {
                builder.Insert(0, $"{current.name}/");
                current = current.parent;
            }

            return builder.ToString();
        }

        private static string FormatVector(Vector3 value)
        {
            return $"({value.x:0.###},{value.y:0.###},{value.z:0.###})";
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
