using System.Globalization;
using Rescue.Core.State;
using UnityEngine;

namespace Rescue.Unity.BoardPresentation
{
    internal static class BoardContentPlacement
    {
        public static bool TryGetVisibleAnchor(
            BoardGridViewPresenter? gridView,
            Transform fallbackAnchor,
            TileCoord coord,
            out Transform anchor)
        {
            anchor = fallbackAnchor;
            if (gridView is null)
            {
                return false;
            }

            if (!gridView.IsCoordVisible(coord))
            {
                return false;
            }

            return gridView.TryGetCellAnchor(coord, out anchor);
        }

        public static Transform ResolveContentParent(Transform? contentRoot, Transform anchor)
        {
            return contentRoot is not null ? contentRoot : anchor;
        }

        public static Vector3 ResolveCellWorldPositionWithYOffset(
            BoardGridViewPresenter? gridView,
            Transform fallbackTransform,
            TileCoord coord,
            float yOffset)
        {
            Vector3 basePosition = gridView is not null
                ? gridView.GetCellWorldPosition(coord)
                : fallbackTransform.position;

            return basePosition + new Vector3(0f, yOffset, 0f);
        }

        public static Vector3 ResolveSpawnEntryWorldPosition(
            BoardGridViewPresenter? gridView,
            Transform fallbackTransform,
            TileCoord coord,
            float yOffset)
        {
            Vector3 basePosition = gridView is not null
                ? gridView.GetColumnEntryWorldPosition(coord.Col)
                : fallbackTransform.position;

            return basePosition + new Vector3(0f, yOffset, 0f);
        }

        public static string CreateContentName(TileCoord coord, string contentLabel)
        {
            return $"Content_{coord.Row.ToString("00", CultureInfo.InvariantCulture)}_{coord.Col.ToString("00", CultureInfo.InvariantCulture)}_{contentLabel}";
        }

        public static void MoveToAnchor(
            GameObject contentObject,
            BoardGridViewPresenter? gridView,
            Transform? contentRoot,
            Transform fallbackTransform,
            Transform anchor,
            TileCoord coord,
            string contentLabel,
            float yOffset,
            Quaternion baseLocalRotation)
        {
            Transform parent = ResolveContentParent(contentRoot, anchor);
            Transform contentTransform = contentObject.transform;
            if (contentTransform.parent != parent)
            {
                contentTransform.SetParent(parent, worldPositionStays: false);
            }

            ApplyAnchorPose(contentTransform, gridView, fallbackTransform, parent, anchor, coord, yOffset, baseLocalRotation);
            contentObject.name = CreateContentName(coord, contentLabel);
            AttachOrUpdateBoardCellView(contentObject, coord);
        }

        public static void PreserveWorldPoseUnderContentParent(
            GameObject contentObject,
            Transform? contentRoot,
            Transform anchor)
        {
            Transform parent = ResolveContentParent(contentRoot, anchor);
            Transform contentTransform = contentObject.transform;
            if (contentTransform.parent == parent)
            {
                return;
            }

            Vector3 currentWorldPosition = contentTransform.position;
            Quaternion currentWorldRotation = contentTransform.rotation;
            contentTransform.SetParent(parent, worldPositionStays: false);
            contentTransform.SetPositionAndRotation(currentWorldPosition, currentWorldRotation);
        }

        public static void PositionAtWorldPose(GameObject contentObject, Vector3 worldPosition, Quaternion worldRotation)
        {
            contentObject.transform.SetPositionAndRotation(worldPosition, worldRotation);
        }

        public static GameObject SpawnPrefabAtAnchor(
            GameObject prefab,
            BoardGridViewPresenter? gridView,
            Transform? contentRoot,
            Transform fallbackTransform,
            Transform anchor,
            TileCoord coord,
            string contentLabel,
            float yOffset,
            Vector3 scaleMultiplier,
            Quaternion baseLocalRotation)
        {
            Transform parent = ResolveContentParent(contentRoot, anchor);
            GameObject contentObject = UnityEngine.Object.Instantiate(prefab, parent);
            contentObject.name = CreateContentName(coord, contentLabel);
            AttachOrUpdateBoardCellView(contentObject, coord);

            Transform contentTransform = contentObject.transform;
            ApplyAnchorPose(
                contentTransform,
                gridView,
                fallbackTransform,
                parent,
                anchor,
                coord,
                yOffset,
                baseLocalRotation);
            contentTransform.localScale = Vector3.Scale(prefab.transform.localScale, scaleMultiplier);
            return contentObject;
        }

        public static GameObject SpawnPrimitiveAtAnchor(
            PrimitiveType primitiveType,
            BoardGridViewPresenter? gridView,
            Transform? contentRoot,
            Transform fallbackTransform,
            Transform anchor,
            TileCoord coord,
            string contentLabel,
            float yOffset,
            Vector3 localScale)
        {
            Transform parent = ResolveContentParent(contentRoot, anchor);
            GameObject contentObject = GameObject.CreatePrimitive(primitiveType);
            contentObject.name = CreateContentName(coord, contentLabel);
            AttachOrUpdateBoardCellView(contentObject, coord);

            Transform contentTransform = contentObject.transform;
            contentTransform.SetParent(parent, worldPositionStays: false);
            ApplyAnchorPose(
                contentTransform,
                gridView,
                fallbackTransform,
                parent,
                anchor,
                coord,
                yOffset,
                Quaternion.identity);
            contentTransform.localScale = localScale;
            return contentObject;
        }

        public static void AttachOrUpdateBoardCellView(GameObject contentObject, TileCoord coord)
        {
            BoardCellView? cellView = contentObject.GetComponent<BoardCellView>();
            if (cellView is null)
            {
                cellView = contentObject.AddComponent<BoardCellView>();
            }

            cellView.Initialize(coord);
        }

        private static void ApplyAnchorPose(
            Transform contentTransform,
            BoardGridViewPresenter? gridView,
            Transform fallbackTransform,
            Transform parent,
            Transform anchor,
            TileCoord coord,
            float yOffset,
            Quaternion baseLocalRotation)
        {
            if (parent == anchor)
            {
                contentTransform.localPosition = new Vector3(0f, yOffset, 0f);
                contentTransform.localRotation = baseLocalRotation;
                return;
            }

            contentTransform.SetPositionAndRotation(
                ResolveCellWorldPositionWithYOffset(gridView, fallbackTransform, coord, yOffset),
                anchor.rotation * baseLocalRotation);
        }
    }
}
