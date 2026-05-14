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
        private static readonly string[] WatchedLevelIds = { "L07", "L08", "L09", "L10", "L13" };
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
                builder.Append(GetHierarchyPath(renderer.transform))
                    .Append("{enabled=").Append(renderer.enabled)
                    .Append(", type=").Append(renderer.GetType().Name)
                    .Append(", cast=").Append(renderer.shadowCastingMode)
                    .Append(", receive=").Append(renderer.receiveShadows)
                    .Append(", bounds=").Append(FormatVector(renderer.bounds.size))
                    .Append(", material='").Append(renderer.sharedMaterial != null ? renderer.sharedMaterial.name : "<null>").Append('\'');

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
