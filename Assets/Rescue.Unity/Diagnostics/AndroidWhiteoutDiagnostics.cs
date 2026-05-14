#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Text;
using Rescue.Unity.BoardPresentation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rescue.Unity.Diagnostics
{
    public static class AndroidWhiteoutDiagnostics
    {
        private static readonly string[] WatchedLevelIds = { "L07", "L08", "L09", "L10", "L11", "L12", "L13" };
        private static readonly string[] WatchedObjectNames =
        {
            "VineGrowthPreview",
            "VineGrowPreview",
            "ForecastRow",
            "FloodedRow",
            "Directional Light",
            "Board Readability Fill",
        };

        public static void LogLevelVisualState(string levelId)
        {
            if (!ShouldLog(levelId))
            {
                return;
            }

            StringBuilder builder = new StringBuilder(1024);
            int qualityLevel = QualitySettings.GetQualityLevel();
            string qualityName = qualityLevel >= 0 && qualityLevel < QualitySettings.names.Length
                ? QualitySettings.names[qualityLevel]
                : "<unknown>";

            builder.Append("[AndroidWhiteoutDiagnostics] level=").Append(levelId)
                .Append(" platform=").Append(Application.platform)
                .Append(" quality=").Append(qualityLevel).Append('/').Append(qualityName)
                .Append(" colorSpace=").Append(QualitySettings.activeColorSpace)
                .Append(" graphicsApi=").Append(SystemInfo.graphicsDeviceType)
                .Append(" graphicsDevice='").Append(SystemInfo.graphicsDeviceName).Append('\'')
                .Append(" screen=").Append(Screen.width).Append('x').Append(Screen.height)
                .Append(" orientation=").Append(Screen.orientation)
                .Append(" cameras=[");

            AppendCameras(builder);
            builder.Append("] canvases=[");
            AppendCanvases(builder);
            builder.Append("] suspiciousRenderers=[");
            AppendSuspiciousRenderers(builder);
            builder.Append(']')
                .Append(" lights=[");

            AppendLights(builder);
            builder.Append("] watchedRenderers=[");
            AppendWatchedRenderers(builder);
            builder.Append(']');

            Debug.Log(builder.ToString());
        }

        private static bool ShouldLog(string levelId)
        {
            if (Application.platform != RuntimePlatform.Android && !Application.isEditor)
            {
                return false;
            }

            for (int i = 0; i < WatchedLevelIds.Length; i++)
            {
                if (string.Equals(levelId, WatchedLevelIds[i], StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static void AppendLights(StringBuilder builder)
        {
            Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude);
            for (int i = 0; i < lights.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append("; ");
                }

                Light light = lights[i];
                builder.Append(light.name)
                    .Append("{enabled=").Append(light.enabled)
                    .Append(", type=").Append(light.type)
                    .Append(", intensity=").Append(light.intensity.ToString("0.###"))
                    .Append(", color=").Append(FormatColor(light.color))
                    .Append(", pos=").Append(FormatVector(light.transform.position))
                    .Append(", rot=").Append(FormatVector(light.transform.eulerAngles))
                    .Append('}');
            }
        }

        private static void AppendCameras(StringBuilder builder)
        {
            Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude);
            for (int i = 0; i < cameras.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append("; ");
                }

                Camera camera = cameras[i];
                builder.Append(GetHierarchyPath(camera.transform))
                    .Append("{enabled=").Append(camera.enabled)
                    .Append(", clear=").Append(camera.clearFlags)
                    .Append(", bg=").Append(FormatColor(camera.backgroundColor))
                    .Append(", cull=").Append(camera.cullingMask)
                    .Append(", depth=").Append(camera.depth.ToString("0.###"))
                    .Append(", near=").Append(camera.nearClipPlane.ToString("0.###"))
                    .Append(", far=").Append(camera.farClipPlane.ToString("0.###"))
                    .Append(", pos=").Append(FormatVector(camera.transform.position))
                    .Append(", rot=").Append(FormatVector(camera.transform.eulerAngles))
                    .Append('}');
            }
        }

        private static void AppendCanvases(StringBuilder builder)
        {
            Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude);
            for (int i = 0; i < canvases.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append("; ");
                }

                Canvas canvas = canvases[i];
                builder.Append(GetHierarchyPath(canvas.transform))
                    .Append("{enabled=").Append(canvas.enabled)
                    .Append(", renderMode=").Append(canvas.renderMode)
                    .Append(", sortingLayer=").Append(canvas.sortingLayerName)
                    .Append(", order=").Append(canvas.sortingOrder)
                    .Append('}');
            }
        }

        private static void AppendSuspiciousRenderers(StringBuilder builder)
        {
            Renderer[] renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude);
            int writtenCount = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (!IsSuspicious(renderer))
                {
                    continue;
                }

                if (writtenCount > 0)
                {
                    builder.Append("; ");
                }

                writtenCount++;
                AppendRendererSummary(builder, renderer);
            }
        }

        private static void AppendWatchedRenderers(StringBuilder builder)
        {
            Renderer[] renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude);
            int writtenCount = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (!IsWatched(renderer))
                {
                    continue;
                }

                if (writtenCount > 0)
                {
                    builder.Append("; ");
                }

                writtenCount++;
                AppendRendererSummary(builder, renderer);
            }
        }

        private static void AppendRendererSummary(StringBuilder builder, Renderer renderer)
        {
            builder.Append(GetHierarchyPath(renderer.transform))
                .Append("{enabled=").Append(renderer.enabled)
                .Append(", type=").Append(renderer.GetType().Name)
                .Append(", layer=").Append(renderer.gameObject.layer)
                .Append(", sortingLayer=").Append(renderer.sortingLayerName)
                .Append(", order=").Append(renderer.sortingOrder)
                .Append(", cast=").Append(renderer.shadowCastingMode)
                .Append(", receive=").Append(renderer.receiveShadows)
                .Append(", bounds=").Append(FormatVector(renderer.bounds.size))
                .Append(", pos=").Append(FormatVector(renderer.transform.position))
                .Append(", material='").Append(renderer.sharedMaterial != null ? renderer.sharedMaterial.name : "<null>").Append('\'');

            if (renderer.sharedMaterial is not null)
            {
                builder.Append(", shader='").Append(renderer.sharedMaterial.shader != null ? renderer.sharedMaterial.shader.name : "<null>").Append('\'');
                if (renderer.sharedMaterial.HasProperty("_Color"))
                {
                    builder.Append(", matColor=").Append(FormatColor(renderer.sharedMaterial.color));
                }
            }

            if (renderer is SpriteRenderer spriteRenderer)
            {
                builder.Append(", spriteColor=").Append(FormatColor(spriteRenderer.color));
            }

            BoardCellView? cellView = renderer.GetComponentInParent<BoardCellView>();
            if (cellView is not null)
            {
                builder.Append(", coord=(").Append(cellView.Coord.Row).Append(',').Append(cellView.Coord.Col).Append(')');
            }

            builder.Append('}');
        }

        private static bool IsWatched(Renderer renderer)
        {
            Transform? current = renderer.transform;
            while (current is not null)
            {
                for (int i = 0; i < WatchedObjectNames.Length; i++)
                {
                    if (current.name.Contains(WatchedObjectNames[i], StringComparison.Ordinal))
                    {
                        return true;
                    }
                }

                current = current.parent;
            }

            return false;
        }

        private static bool IsSuspicious(Renderer renderer)
        {
            if (renderer is null || !renderer.enabled)
            {
                return false;
            }

            Vector3 bounds = renderer.bounds.size;
            bool largeSurface = bounds.x >= 4.0f || bounds.z >= 4.0f || bounds.y >= 4.0f;
            bool whiteTint = false;
            if (renderer is SpriteRenderer spriteRenderer)
            {
                whiteTint = IsBright(spriteRenderer.color);
            }
            else if (renderer.sharedMaterial is not null && renderer.sharedMaterial.HasProperty("_Color"))
            {
                whiteTint = IsBright(renderer.sharedMaterial.color);
            }

            return largeSurface && whiteTint;
        }

        private static bool IsBright(Color color)
        {
            return color.r >= 0.85f && color.g >= 0.85f && color.b >= 0.85f && color.a >= 0.1f;
        }

        private static string GetHierarchyPath(Transform transform)
        {
            StringBuilder builder = new StringBuilder(transform.name);
            Transform? current = transform.parent;
            while (current is not null)
            {
                builder.Insert(0, current.name + "/");
                current = current.parent;
            }

            return builder.ToString();
        }

        private static string FormatColor(Color color)
        {
            return $"({color.r:0.###},{color.g:0.###},{color.b:0.###},{color.a:0.###})";
        }

        private static string FormatVector(Vector3 vector)
        {
            return $"({vector.x:0.###},{vector.y:0.###},{vector.z:0.###})";
        }
    }
}
#endif
