using NUnit.Framework;
using Rescue.Unity.Presentation;
using UnityEngine;

namespace Rescue.Unity.Presentation.Tests
{
    public sealed class PortraitGameSceneLayoutTests
    {
        private GameObject? cameraObject;
        private GameObject? dockStageObject;
        private Texture2D? spriteTexture;
        private Sprite? sprite;

        [TearDown]
        public void TearDown()
        {
            if (sprite is not null)
            {
                Object.DestroyImmediate(sprite);
            }

            if (spriteTexture is not null)
            {
                Object.DestroyImmediate(spriteTexture);
            }

            if (cameraObject is not null)
            {
                Object.DestroyImmediate(cameraObject);
            }

            if (dockStageObject is not null)
            {
                Object.DestroyImmediate(dockStageObject);
            }
        }

        [Test]
        public void ApplyCameraLayout_PortraitAspectSetsPlayablePortraitFraming()
        {
            Camera camera = CreateCamera();

            PortraitGameSceneLayout.ApplyCameraLayout(camera, 9.0f / 16.0f);

            AssertCameraMatchesGameplayFraming(camera);
        }

        [Test]
        public void ApplyCameraLayout_LandscapeAspectStillSetsPlayableFraming()
        {
            Camera camera = CreateCamera();

            PortraitGameSceneLayout.ApplyCameraLayout(camera, 16.0f / 9.0f);

            AssertCameraMatchesGameplayFraming(camera);
        }

        [Test]
        public void ApplyCameraLayout_FitsCameraChildBackgroundUprightAsCoverArt()
        {
            Camera camera = CreateCamera();
            GameObject background = new GameObject("SceneBackground");
            background.transform.SetParent(camera.transform, false);
            SpriteRenderer renderer = background.AddComponent<SpriteRenderer>();
            spriteTexture = new Texture2D(1600, 900);
            sprite = Sprite.Create(spriteTexture, new Rect(0f, 0f, 1600f, 900f), new Vector2(0.5f, 0.5f), 100f);
            renderer.sprite = sprite;

            PortraitGameSceneLayout.ApplyCameraLayout(camera, 9.0f / 16.0f);

            Assert.That(background.transform.localPosition, Is.EqualTo(new Vector3(0f, 0f, PortraitGameSceneLayout.BackgroundCameraSpaceDistance)));
            Assert.That(Quaternion.Angle(background.transform.localRotation, Quaternion.identity), Is.LessThan(0.1f));
            Assert.That(background.transform.localScale.x, Is.GreaterThan(0f));
            Assert.That(background.transform.localScale.y, Is.GreaterThan(0f));
            Assert.That(background.transform.localScale.x, Is.EqualTo(background.transform.localScale.y).Within(0.001f));
            Assert.That(
                renderer.sprite.bounds.size.y * background.transform.localScale.y,
                Is.GreaterThan(camera.orthographicSize * 2f));
        }

        [Test]
        public void ResolveBoardPortraitScale_LeavesSmallBoardsAtDefaultScaleInLandscape()
        {
            Vector3 scale = PortraitGameSceneLayout.ResolveBoardPortraitScale(6, 16.0f / 9.0f);

            Assert.That(scale, Is.EqualTo(PortraitGameSceneLayout.BoardPortraitScale));
        }

        [Test]
        public void ResolveBoardPortraitScale_FitsSixColumnBoardsInStandardPortrait()
        {
            const float aspect = 9.0f / 16.0f;

            Vector3 scale = PortraitGameSceneLayout.ResolveBoardPortraitScale(6, aspect);

            float viewportWidth = PortraitGameSceneLayout.CameraPortraitOrthographicSize * 2.0f * aspect;
            float boardWidth = 6.0f * scale.x;
            Assert.That(scale.x, Is.LessThan(PortraitGameSceneLayout.BoardPortraitScale.x));
            Assert.That(boardWidth, Is.LessThan(viewportWidth + 0.001f));
        }

        [Test]
        public void ResolveBoardPortraitScale_FitsNineColumnBoardsInNarrowPortrait()
        {
            const float aspect = 9.0f / 20.0f;

            Vector3 scale = PortraitGameSceneLayout.ResolveBoardPortraitScale(9, aspect);

            float viewportWidth = PortraitGameSceneLayout.CameraPortraitOrthographicSize * 2.0f * aspect;
            float boardWidth = 9.0f * scale.x;
            Assert.That(scale.x, Is.LessThan(PortraitGameSceneLayout.BoardPortraitScale.x));
            Assert.That(boardWidth, Is.LessThan(viewportWidth + 0.001f));
        }

        [Test]
        public void ResolveBoardPortraitScale_FitsEightColumnBoardsInNarrowPortrait()
        {
            const float aspect = 9.0f / 20.0f;

            Vector3 scale = PortraitGameSceneLayout.ResolveBoardPortraitScale(8, aspect);

            float viewportWidth = PortraitGameSceneLayout.CameraPortraitOrthographicSize * 2.0f * aspect;
            float boardWidth = 8.0f * scale.x;
            Assert.That(scale.x, Is.LessThan(PortraitGameSceneLayout.BoardPortraitScale.x));
            Assert.That(boardWidth, Is.LessThan(viewportWidth + 0.001f));
        }

        [Test]
        public void ResolveDockPortraitScale_MatchesSevenColumnBoardWidth()
        {
            const float aspect = 9.0f / 16.0f;

            Vector3 boardScale = PortraitGameSceneLayout.ResolveBoardPortraitScale(7, aspect);
            Vector3 dockScale = PortraitGameSceneLayout.ResolveDockPortraitScale(7, aspect);

            Assert.That(7.0f * dockScale.x, Is.EqualTo(7.0f * boardScale.x).Within(0.001f));
        }

        [Test]
        public void ResolveDockPortraitScale_ChangesWithBoardWidth()
        {
            const float aspect = 16.0f / 9.0f;

            Vector3 sixColumnDockScale = PortraitGameSceneLayout.ResolveDockPortraitScale(6, aspect);
            Vector3 nineColumnDockScale = PortraitGameSceneLayout.ResolveDockPortraitScale(9, aspect);

            Assert.That(Mathf.Abs(sixColumnDockScale.x - nineColumnDockScale.x), Is.GreaterThan(0.001f));
            Assert.That(7.0f * sixColumnDockScale.x, Is.EqualTo(6.0f * PortraitGameSceneLayout.ResolveBoardPortraitScale(6, aspect).x).Within(0.001f));
            Assert.That(7.0f * nineColumnDockScale.x, Is.EqualTo(9.0f * PortraitGameSceneLayout.ResolveBoardPortraitScale(9, aspect).x).Within(0.001f));
        }

        [Test]
        public void ResolveDockPortraitScale_UsesFittedBoardScaleInNarrowPortrait()
        {
            const float aspect = 9.0f / 20.0f;

            Vector3 boardScale = PortraitGameSceneLayout.ResolveBoardPortraitScale(9, aspect);
            Vector3 dockScale = PortraitGameSceneLayout.ResolveDockPortraitScale(9, aspect);

            Assert.That(boardScale.x, Is.LessThan(PortraitGameSceneLayout.BoardPortraitScale.x));
            Assert.That(7.0f * dockScale.x, Is.EqualTo(9.0f * boardScale.x).Within(0.001f));
        }

        [Test]
        public void ApplyDockStageLayout_ScalesDockRootToBoardWidth()
        {
            dockStageObject = new GameObject("DockRoot");

            PortraitGameSceneLayout.ApplyDockStageLayout(6);

            Vector3 expectedScale = PortraitGameSceneLayout.ResolveDockPortraitScale(6, PortraitGameSceneLayout.ResolveScreenAspect());
            Assert.That(dockStageObject.transform.localPosition, Is.EqualTo(PortraitGameSceneLayout.DockPortraitPosition));
            Assert.That(Quaternion.Angle(dockStageObject.transform.localRotation, PortraitGameSceneLayout.DockPortraitRotation), Is.LessThan(0.1f));
            Assert.That(dockStageObject.transform.localScale, Is.EqualTo(expectedScale));
            Assert.That(7.0f * dockStageObject.transform.localScale.x, Is.EqualTo(6.0f * PortraitGameSceneLayout.ResolveBoardPortraitScale(6, PortraitGameSceneLayout.ResolveScreenAspect()).x).Within(0.001f));
        }

        private Camera CreateCamera()
        {
            cameraObject = new GameObject("Main Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = false;
            camera.orthographicSize = 3.0f;
            camera.transform.SetPositionAndRotation(new Vector3(2f, 3f, 4f), Quaternion.Euler(12f, 34f, 56f));
            return camera;
        }

        private static void AssertCameraMatchesGameplayFraming(Camera camera)
        {
            Assert.That(camera.orthographic, Is.True);
            Assert.That(camera.orthographicSize, Is.EqualTo(PortraitGameSceneLayout.CameraPortraitOrthographicSize).Within(0.001f));
            Assert.That(Vector3.Distance(camera.transform.position, PortraitGameSceneLayout.CameraPortraitPosition), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(camera.transform.rotation, PortraitGameSceneLayout.CameraPortraitRotation), Is.LessThan(0.1f));
        }
    }
}
