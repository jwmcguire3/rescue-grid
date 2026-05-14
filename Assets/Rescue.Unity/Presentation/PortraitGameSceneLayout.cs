using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rescue.Unity.Presentation
{
    public static class PortraitGameSceneLayout
    {
        private const string GameSceneName = "Game";
        private const string DebugGameplaySceneName = "DebugGameplay";
        private const string MainCameraName = "Main Camera";
        private const string SceneBackgroundName = "SceneBackground";
        private const string BoardStageRootName = "BoardStageRoot";
        private const string DockRootName = "DockRoot";
        private const float PortraitAspectThreshold = 0.8f;
        private const float BoardPortraitViewportWidthUsage = 0.94f;
        private const float BoardCellSize = 1.0f;
        private const float MinimumBoardPortraitScale = 0.66f;

        public static readonly Vector3 CameraPortraitPosition = new Vector3(0f, 20.0f, -14.0f);
        public static readonly Quaternion CameraPortraitRotation = Quaternion.Euler(60.0f, 0.0f, 0.0f);
        public const float CameraPortraitOrthographicSize = 7.2f;
        public const int MobileTargetFrameRate = 60;

        public static readonly Vector3 BoardPortraitPosition = new Vector3(0f, -0.25f, -2.3f);
        public static readonly Quaternion BoardPortraitRotation = Quaternion.identity;
        public static readonly Vector3 BoardPortraitScale = new Vector3(1.1f, 1.1f, 1.1f);

        public static readonly Vector3 DockPortraitPosition = new Vector3(0f, -0.5f, -9.85f);
        public static readonly Quaternion DockPortraitRotation = Quaternion.Euler(15.0f, 0.0f, 0.0f);
        public static readonly Vector3 DockPortraitScale = new Vector3(1.35f, 1.35f, 1.35f);

        public const float BackgroundCameraSpaceDistance = 60.0f;
        public const float BackgroundCoverPadding = 1.04f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            ApplyToScene(SceneManager.GetActiveScene());
        }

        public static void ApplyToScene(Scene scene)
        {
            if (!ShouldApplyToScene(scene))
            {
                return;
            }

            ApplyRuntimeOrientation();
            ApplyRuntimeFramePacing();
            ApplyActiveCameraLayout();
            ApplyStageTransform(BoardStageRootName, BoardPortraitPosition, BoardPortraitRotation, BoardPortraitScale);
            ApplyStageTransform(DockRootName, DockPortraitPosition, DockPortraitRotation, DockPortraitScale);
        }

        public static void ApplyActiveCameraLayout()
        {
            ApplyCameraLayout(Camera.main ?? FindNamedCamera(MainCameraName), ResolveScreenAspect());
        }

        public static void ApplyBoardStageLayout(int boardWidth)
        {
            GameObject? boardStage = GameObject.Find(BoardStageRootName);
            if (boardStage is null)
            {
                return;
            }

            ApplyStageTransform(
                BoardStageRootName,
                BoardPortraitPosition,
                BoardPortraitRotation,
                ResolveBoardPortraitScale(boardWidth, ResolveScreenAspect()));
        }

        public static Vector3 ResolveBoardPortraitScale(int boardWidth, float aspect)
        {
            float defaultUniformScale = BoardPortraitScale.x;
            if (aspect <= 0f || aspect >= PortraitAspectThreshold || boardWidth <= 0)
            {
                return BoardPortraitScale;
            }

            float usableViewportWidth = CameraPortraitOrthographicSize * 2.0f * aspect * BoardPortraitViewportWidthUsage;
            float boardWorldWidthAtUnitScale = boardWidth * BoardCellSize;
            if (usableViewportWidth <= 0f || boardWorldWidthAtUnitScale <= 0f)
            {
                return BoardPortraitScale;
            }

            float fittedUniformScale = usableViewportWidth / boardWorldWidthAtUnitScale;
            float uniformScale = Mathf.Clamp(
                Mathf.Min(defaultUniformScale, fittedUniformScale),
                MinimumBoardPortraitScale,
                defaultUniformScale);

            return new Vector3(uniformScale, uniformScale, uniformScale);
        }

        public static void ApplyCameraLayout(Camera? camera, float aspect)
        {
            if (camera is null)
            {
                return;
            }

            camera.orthographic = true;
            camera.transform.SetPositionAndRotation(CameraPortraitPosition, CameraPortraitRotation);
            camera.orthographicSize = CameraPortraitOrthographicSize;

            FitBackgroundToCamera(camera, aspect);
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ApplyToScene(scene);
        }

        private static void ApplyRuntimeOrientation()
        {
            if (!Application.isMobilePlatform)
            {
                return;
            }

            Screen.orientation = ScreenOrientation.Portrait;
            Screen.autorotateToPortrait = true;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = false;
            Screen.autorotateToLandscapeRight = false;
        }

        private static void ApplyRuntimeFramePacing()
        {
            if (!Application.isMobilePlatform)
            {
                return;
            }

            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = MobileTargetFrameRate;
        }

        public static bool ShouldApplyToScene(Scene scene)
        {
            return IsGameplayLayoutScene(scene.name);
        }

        private static bool IsGameplayLayoutScene(string sceneName)
        {
            return string.Equals(sceneName, GameSceneName, StringComparison.Ordinal) ||
                string.Equals(sceneName, DebugGameplaySceneName, StringComparison.Ordinal);
        }

        private static void ApplyStageTransform(string objectName, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            GameObject? target = GameObject.Find(objectName);
            if (target is null)
            {
                return;
            }

            target.transform.localPosition = position;
            target.transform.localRotation = rotation;
            target.transform.localScale = scale;
        }

        private static void FitBackgroundToCamera(Camera camera, float aspect)
        {
            Transform background = camera.transform.Find(SceneBackgroundName);
            if (background is null)
            {
                return;
            }

            background.localPosition = new Vector3(0f, 0f, BackgroundCameraSpaceDistance);
            background.localRotation = Quaternion.identity;

            SpriteRenderer? spriteRenderer = background.GetComponent<SpriteRenderer>();
            if (spriteRenderer is null || spriteRenderer.sprite is null || !camera.orthographic)
            {
                background.localScale = AbsScale(background.localScale);
                return;
            }

            float safeAspect = aspect > 0f ? aspect : 1f;
            Vector2 spriteSize = spriteRenderer.sprite.bounds.size;
            if (spriteSize.x <= 0f || spriteSize.y <= 0f)
            {
                background.localScale = AbsScale(background.localScale);
                return;
            }

            float viewHeight = camera.orthographicSize * 2.0f;
            float viewWidth = viewHeight * safeAspect;
            float coverScale = Mathf.Max(viewWidth / spriteSize.x, viewHeight / spriteSize.y) * BackgroundCoverPadding;
            background.localScale = new Vector3(coverScale, coverScale, Mathf.Abs(background.localScale.z));
        }

        public static float ResolveScreenAspect()
        {
            return Screen.height > 0 ? (float)Screen.width / Screen.height : 1f;
        }

        private static Camera? FindNamedCamera(string cameraName)
        {
            Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
            {
                if (string.Equals(cameras[i].name, cameraName, StringComparison.Ordinal))
                {
                    return cameras[i];
                }
            }

            return null;
        }

        private static Vector3 AbsScale(Vector3 scale)
        {
            return new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        }
    }
}
