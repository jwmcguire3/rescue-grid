using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rescue.Unity.FX
{
    public static class FxDebugFramePlayer
    {
        public static SpriteSequenceFxPlayer? EnsureInspectionPlayer(GameObject instance)
        {
            SpriteSequenceFxPlayer? existingPlayer = instance.GetComponentInChildren<SpriteSequenceFxPlayer>(includeInactive: true);
            if (existingPlayer is not null)
            {
                existingPlayer.DestroyAfterPlayback = false;
                existingPlayer.PausePlayback();
                return existingPlayer;
            }

            SpriteRenderer[] renderers = instance.GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
            if (renderers.Length == 0)
            {
                return null;
            }

            Array.Sort(renderers, CompareRenderersForDebugInspection);
            SpriteRenderer primaryRenderer = renderers[0];
            Sprite[] frames = BuildDebugFrames(renderers);
            SpriteSequenceFxPlayer player = primaryRenderer.gameObject.AddComponent<SpriteSequenceFxPlayer>();
            player.ConfigureForDebugInspection(primaryRenderer, frames);
            return player;
        }

        public static bool HasInspectableRenderer(GameObject prefab)
        {
            return prefab.GetComponentInChildren<SpriteRenderer>(includeInactive: true) is not null;
        }

        public static bool HasFramePlayer(GameObject prefab)
        {
            return prefab.GetComponentInChildren<SpriteSequenceFxPlayer>(includeInactive: true) is not null;
        }

        private static Sprite[] BuildDebugFrames(SpriteRenderer[] renderers)
        {
            List<Sprite> frames = new List<Sprite>(renderers.Length);
            for (int i = 0; i < renderers.Length; i++)
            {
                Sprite? sprite = renderers[i].sprite;
                if (sprite is not null)
                {
                    frames.Add(sprite);
                }
            }

            return frames.ToArray();
        }

        private static int CompareRenderersForDebugInspection(SpriteRenderer left, SpriteRenderer right)
        {
            bool leftIsRoot = left.transform.parent is null;
            bool rightIsRoot = right.transform.parent is null;
            if (leftIsRoot != rightIsRoot)
            {
                return leftIsRoot ? -1 : 1;
            }

            return string.Compare(BuildHierarchyPath(left.transform), BuildHierarchyPath(right.transform), StringComparison.Ordinal);
        }

        private static string BuildHierarchyPath(Transform transform)
        {
            List<string> segments = new List<string>();
            Transform? current = transform;
            while (current is not null)
            {
                int siblingIndex = current.GetSiblingIndex();
                segments.Add($"{siblingIndex:D4}:{current.name}");
                current = current.parent;
            }

            segments.Reverse();
            return string.Join("/", segments);
        }
    }
}
