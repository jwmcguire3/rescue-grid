using System.Collections.Generic;
using Rescue.Core.State;
using UnityEngine;

namespace Rescue.Unity.UI
{
    internal static class DockPiecePoseHelper
    {
        public const float LocalRightOffset = 0.15f;
        public const float LiftHeight = 0.48f;
        public const float RaisedScaleMultiplier = 1.08f;

        public static Vector3 ResolveAnchoredPosition(Transform anchor)
        {
            return anchor.position + (anchor.rotation * new Vector3(LocalRightOffset, 0f, 0f));
        }

        public static Quaternion ResolveAnchoredRotation(Transform anchor, Quaternion rotationOffset)
        {
            return anchor.rotation * rotationOffset;
        }

        public static Vector3 ResolveDockScale(GameObject piecePrefab, float scaleMultiplier)
        {
            return Vector3.Scale(piecePrefab.transform.localScale, Vector3.one * scaleMultiplier);
        }

        public static Vector3 ResolveLiftedPosition(Vector3 basePosition, Vector3 ownerUp)
        {
            return basePosition + (ownerUp * LiftHeight);
        }

        public static Vector3 ResolveRaisedScale(Vector3 baseScale)
        {
            return baseScale * RaisedScaleMultiplier;
        }

        public static string FormatPieceObjectName(int slotIndex, DebrisType debrisType)
        {
            return $"DockPiece_{slotIndex:00}_{debrisType}";
        }

        public static string DescribeTrackedSlots(DockSlotVisualRegistry trackedSlots)
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            builder.Append('[');
            bool appendedAny = false;
            for (int slotIndex = 0; slotIndex < trackedSlots.Capacity; slotIndex++)
            {
                DebrisType? slotType = trackedSlots.GetSlotType(slotIndex);
                GameObject? slotObject = trackedSlots.GetSlotObject(slotIndex);
                if (!slotType.HasValue && slotObject is null)
                {
                    continue;
                }

                if (appendedAny)
                {
                    builder.Append("; ");
                }

                appendedAny = true;
                builder.Append(slotIndex).Append(':')
                    .Append(slotType?.ToString() ?? "-")
                    .Append(" object='").Append(slotObject != null ? slotObject.name : "<null>")
                    .Append("' child='").Append(slotObject != null ? DescribeFirstVisualChild(slotObject) : "<null>")
                    .Append('\'');
            }

            if (!appendedAny)
            {
                builder.Append("empty");
            }

            builder.Append(']');
            return builder.ToString();
        }

        private static string DescribeFirstVisualChild(GameObject root)
        {
            Renderer? renderer = root.GetComponentInChildren<Renderer>(includeInactive: true);
            if (renderer is not null)
            {
                Vector3 boundsCenterLocal = root.transform.InverseTransformPoint(renderer.bounds.center);
                return GetPathRelativeTo(root.transform, renderer.transform)
                    + $" boundsCenterLocal=({boundsCenterLocal.x:0.00},{boundsCenterLocal.y:0.00},{boundsCenterLocal.z:0.00})";
            }

            if (root.transform.childCount > 0)
            {
                return root.transform.GetChild(0).name;
            }

            return "<none>";
        }

        private static string GetPathRelativeTo(Transform root, Transform child)
        {
            Stack<string> segments = new Stack<string>();
            Transform? current = child;
            while (current is not null && current != root)
            {
                segments.Push(current.name);
                current = current.parent;
            }

            return segments.Count == 0 ? root.name : string.Join("/", segments);
        }
    }
}
