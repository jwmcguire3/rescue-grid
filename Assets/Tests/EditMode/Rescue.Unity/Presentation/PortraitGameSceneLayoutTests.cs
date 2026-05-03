using NUnit.Framework;
using Rescue.Unity.Presentation;
using UnityEngine;

namespace Rescue.Unity.Presentation.Tests
{
    public sealed class PortraitGameSceneLayoutTests
    {
        private GameObject? cameraObject;
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
        }

        [Test]
        public void ApplyCameraLayout_PortraitAspectSetsPlayablePortraitFraming()
        {
            Camera camera = CreateCamera();

            PortraitGameSceneLayout.ApplyCameraLayout(camera, 9.0f / 16.0f);

            Assert.That(camera.orthographic, Is.True);
            Assert.That(camera.orthographicSize, Is.EqualTo(PortraitGameSceneLayout.CameraPortraitOrthographicSize).Within(0.001f));
            Assert.That(Vector3.Distance(camera.transform.position, PortraitGameSceneLayout.CameraPortraitPosition), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(camera.transform.rotation, PortraitGameSceneLayout.CameraPortraitRotation), Is.LessThan(0.1f));
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
        public void ResolveBoardPortraitScale_LeavesSmallBoardsAtDefaultScale()
        {
            Vector3 scale = PortraitGameSceneLayout.ResolveBoardPortraitScale(6, 9.0f / 20.0f);

            Assert.That(scale, Is.EqualTo(PortraitGameSceneLayout.BoardPortraitScale));
        }

        [Test]
        public void ResolveBoardPortraitScale_FitsNineColumnBoardsInNarrowPortrait()
        {
            const float aspect = 9.0f / 20.0f;

            Vector3 scale = PortraitGameSceneLayout.ResolveBoardPortraitScale(9, aspect);

            float viewportWidth = PortraitGameSceneLayout.CameraPortraitOrthographicSize * 2.0f * aspect;
            float boardWidth = 9.0f * scale.x;
            Assert.That(scale.x, Is.LessThan(PortraitGameSceneLayout.BoardPortraitScale.x));
            Assert.That(boardWidth, Is.LessThan(viewportWidth));
        }

        private Camera CreateCamera()
        {
            cameraObject = new GameObject("Main Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            return camera;
        }
    }
}
