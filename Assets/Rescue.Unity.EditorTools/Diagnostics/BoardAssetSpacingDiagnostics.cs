using System;
using System.Collections.Generic;
using System.Text;
using Rescue.Core.State;
using Rescue.Unity.EditorTools.Art.Prefabs;
using UnityEditor;
using UnityEngine;

namespace Rescue.Unity.EditorTools.Diagnostics
{
    public static class BoardAssetSpacingDiagnostics
    {
        private const float MinimumFillRatio = 0.88f;
        private const float MaximumFillRatio = 1.02f;

        private static readonly RepresentativeAssetDefinition[] RepresentativeAssets =
        {
            new RepresentativeAssetDefinition("Dry tile", "Assets/Rescue.Unity/Art/Prefabs/Phase1/Board/DryTile_Phase1.prefab"),
            new RepresentativeAssetDefinition("Debris C", "Assets/Rescue.Unity/Art/Prefabs/Phase1/Pieces/Debris_C_Phase1.prefab"),
            new RepresentativeAssetDefinition("Crate", "Assets/Rescue.Unity/Art/Prefabs/Phase1/Blockers/Crate_Phase1.prefab"),
            new RepresentativeAssetDefinition("Puppy target", "Assets/Rescue.Unity/Art/Prefabs/Phase1/Targets/PuppyTarget_Phase1.prefab"),
        };

        [MenuItem("Rescue Grid/Debug/Report Representative Asset Spacing")]
        public static void ReportRepresentativeAssetSpacing()
        {
            IReadOnlyList<AssetSpacingReport> reports = AnalyzeRepresentativePhase1Prefabs();
            string reportText = BuildRepresentativeReportText(reports);
            Debug.Log(reportText);
            EditorGUIUtility.systemCopyBuffer = reportText;
        }

        [MenuItem("Rescue Grid/Debug/Report Live Debris Spacing At (1,1)")]
        public static void ReportLiveDebrisSpacing()
        {
            LiveAssetSpacingReport report = AnalyzeLiveDebrisSpacing(new TileCoord(1, 1));
            string reportText = report.ToDisplayString();
            Debug.Log(reportText);
            EditorGUIUtility.systemCopyBuffer = reportText;
        }

        [MenuItem("Rescue Grid/Debug/Report Live Debris Spacing At (1,1)", true)]
        public static bool ValidateReportLiveDebrisSpacing()
        {
            return EditorApplication.isPlaying;
        }

        public static IReadOnlyList<AssetSpacingReport> AnalyzeRepresentativePhase1Prefabs(float cellSize = Phase1PlaceholderPrefabFactory.DefaultBoardCellSize)
        {
            List<AssetSpacingReport> reports = new List<AssetSpacingReport>(RepresentativeAssets.Length);
            for (int i = 0; i < RepresentativeAssets.Length; i++)
            {
                RepresentativeAssetDefinition asset = RepresentativeAssets[i];
                reports.Add(AnalyzePrefabAsset(asset.Label, asset.PrefabAssetPath, cellSize));
            }

            return reports;
        }

        public static AssetSpacingReport AnalyzePrefabAsset(string label, string prefabAssetPath, float cellSize = Phase1PlaceholderPrefabFactory.DefaultBoardCellSize)
        {
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabAssetPath)
                ?? throw new InvalidOperationException($"Could not load prefab asset '{prefabAssetPath}'.");
            GameObject prefabRoot = PrefabUtility.InstantiatePrefab(prefabAsset) as GameObject
                ?? UnityEngine.Object.Instantiate(prefabAsset);
            try
            {
                Transform visual = prefabRoot.transform.Find("Visual") ?? prefabRoot.transform;
                Bounds combinedBounds = Phase1PlaceholderPrefabFactory.GetCombinedRendererBounds(prefabRoot);
                Vector3 wrapperCenterLocal = combinedBounds.center;
                Vector3 visualCenterLocal = visual.parent == prefabRoot.transform
                    ? combinedBounds.center - visual.localPosition
                    : combinedBounds.center;
                Vector3 localBoundsSize = combinedBounds.size;
                Vector3 worldBoundsSize = ScaleSize(prefabRoot.transform.lossyScale, combinedBounds.size);
                Vector2 planarFootprint = CalculateDominantPlanarFootprint(combinedBounds.size);
                float footprintX = planarFootprint.x;
                float footprintZ = planarFootprint.y;
                float footprint = Mathf.Max(footprintX, footprintZ);
                float fillRatio = footprint / Mathf.Max(cellSize, 0.0001f);

                return new AssetSpacingReport(
                    label,
                    prefabAssetPath,
                    prefabRoot.transform.localPosition,
                    visual.localPosition,
                    wrapperCenterLocal,
                    visualCenterLocal,
                    localBoundsSize,
                    worldBoundsSize,
                    footprintX,
                    footprintZ,
                    cellSize - footprintX,
                    cellSize - footprintZ,
                    fillRatio,
                    ClassifyFill(fillRatio));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(prefabRoot);
            }
        }

        public static LiveAssetSpacingReport AnalyzeLiveDebrisSpacing(TileCoord coord)
        {
            DebrisTraceReport trace = DebrisInstanceTracer.TraceLiveDebris(coord);
            LiveBoundsSnapshot snapshot = CalculateCombinedRendererBounds(trace.Content.Path);
            float cellSize = Phase1PlaceholderPrefabFactory.DefaultBoardCellSize;
            float footprintX = snapshot.WorldBoundsSize.x;
            float footprintZ = snapshot.WorldBoundsSize.z;
            float footprint = Mathf.Max(footprintX, footprintZ);
            float fillRatio = footprint / Mathf.Max(cellSize, 0.0001f);

            return new LiveAssetSpacingReport(
                trace.Coord,
                trace.Anchor.Path,
                trace.Anchor.WorldPosition,
                snapshot.WorldBoundsCenter,
                snapshot.WorldBoundsSize,
                footprintX,
                footprintZ,
                cellSize - footprintX,
                cellSize - footprintZ,
                fillRatio,
                ClassifyFill(fillRatio));
        }

        private static LiveBoundsSnapshot CalculateCombinedRendererBounds(string hierarchyPath)
        {
            GameObject liveObject = GameObject.Find(hierarchyPath.Substring(hierarchyPath.LastIndexOf("/", StringComparison.Ordinal) + 1))
                ?? throw new InvalidOperationException($"Could not find live object for '{hierarchyPath}'.");
            Bounds localBounds = Phase1PlaceholderPrefabFactory.GetCombinedRendererBounds(liveObject);
            return new LiveBoundsSnapshot(
                liveObject.transform.TransformPoint(localBounds.center),
                ScaleSize(liveObject.transform.lossyScale, localBounds.size));
        }

        private static Vector3 ScaleSize(Vector3 lossyScale, Vector3 localSize)
        {
            return new Vector3(
                Mathf.Abs(lossyScale.x) * localSize.x,
                Mathf.Abs(lossyScale.y) * localSize.y,
                Mathf.Abs(lossyScale.z) * localSize.z);
        }

        private static Vector2 CalculateDominantPlanarFootprint(Vector3 boundsSize)
        {
            float[] dimensions = { boundsSize.x, boundsSize.y, boundsSize.z };
            Array.Sort(dimensions);
            return new Vector2(dimensions[2], dimensions[1]);
        }

        private static string ClassifyFill(float fillRatio)
        {
            if (fillRatio < MinimumFillRatio)
            {
                return "underfilled";
            }

            if (fillRatio > MaximumFillRatio)
            {
                return "overfilled";
            }

            return "within tolerance";
        }

        private static string BuildRepresentativeReportText(IReadOnlyList<AssetSpacingReport> reports)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Representative Asset Spacing Report");
            for (int i = 0; i < reports.Count; i++)
            {
                AssetSpacingReport report = reports[i];
                builder.AppendLine($"- {report.Label}: fillRatio={report.FillRatio:0.000}, footprintX={report.FootprintX:0.000}, footprintZ={report.FootprintZ:0.000}, gapX={report.GapX:0.000}, gapZ={report.GapZ:0.000}, verdict={report.Verdict}");
            }

            return builder.ToString();
        }

        public sealed class AssetSpacingReport
        {
            public AssetSpacingReport(
                string label,
                string assetPath,
                Vector3 wrapperLocalPosition,
                Vector3 visualLocalPosition,
                Vector3 wrapperBoundsCenterLocal,
                Vector3 visualBoundsCenterLocal,
                Vector3 localBoundsSize,
                Vector3 worldBoundsSize,
                float footprintX,
                float footprintZ,
                float gapX,
                float gapZ,
                float fillRatio,
                string verdict)
            {
                Label = label;
                AssetPath = assetPath;
                WrapperLocalPosition = wrapperLocalPosition;
                VisualLocalPosition = visualLocalPosition;
                WrapperBoundsCenterLocal = wrapperBoundsCenterLocal;
                VisualBoundsCenterLocal = visualBoundsCenterLocal;
                LocalBoundsSize = localBoundsSize;
                WorldBoundsSize = worldBoundsSize;
                FootprintX = footprintX;
                FootprintZ = footprintZ;
                GapX = gapX;
                GapZ = gapZ;
                FillRatio = fillRatio;
                Verdict = verdict;
            }

            public string Label { get; }
            public string AssetPath { get; }
            public Vector3 WrapperLocalPosition { get; }
            public Vector3 VisualLocalPosition { get; }
            public Vector3 WrapperBoundsCenterLocal { get; }
            public Vector3 VisualBoundsCenterLocal { get; }
            public Vector3 LocalBoundsSize { get; }
            public Vector3 WorldBoundsSize { get; }
            public float FootprintX { get; }
            public float FootprintZ { get; }
            public float GapX { get; }
            public float GapZ { get; }
            public float FillRatio { get; }
            public string Verdict { get; }
        }

        public sealed class LiveAssetSpacingReport
        {
            public LiveAssetSpacingReport(
                TileCoord coord,
                string anchorPath,
                Vector3 anchorWorldPosition,
                Vector3 boundsCenter,
                Vector3 boundsSize,
                float footprintX,
                float footprintZ,
                float gapX,
                float gapZ,
                float fillRatio,
                string verdict)
            {
                Coord = coord;
                AnchorPath = anchorPath;
                AnchorWorldPosition = anchorWorldPosition;
                BoundsCenter = boundsCenter;
                BoundsSize = boundsSize;
                FootprintX = footprintX;
                FootprintZ = footprintZ;
                GapX = gapX;
                GapZ = gapZ;
                FillRatio = fillRatio;
                Verdict = verdict;
            }

            public TileCoord Coord { get; }
            public string AnchorPath { get; }
            public Vector3 AnchorWorldPosition { get; }
            public Vector3 BoundsCenter { get; }
            public Vector3 BoundsSize { get; }
            public float FootprintX { get; }
            public float FootprintZ { get; }
            public float GapX { get; }
            public float GapZ { get; }
            public float FillRatio { get; }
            public string Verdict { get; }

            public string ToDisplayString()
            {
                return $"Live Asset Spacing Report\nTileCoord({Coord.Row}, {Coord.Col})\nAnchor: {AnchorPath} at {AnchorWorldPosition}\nBounds center: {BoundsCenter}\nBounds size: {BoundsSize}\nFootprintX: {FootprintX:0.000}\nFootprintZ: {FootprintZ:0.000}\nGapX: {GapX:0.000}\nGapZ: {GapZ:0.000}\nFillRatio: {FillRatio:0.000}\nVerdict: {Verdict}";
            }
        }

        private readonly struct RepresentativeAssetDefinition
        {
            public RepresentativeAssetDefinition(string label, string prefabAssetPath)
            {
                Label = label;
                PrefabAssetPath = prefabAssetPath;
            }

            public string Label { get; }
            public string PrefabAssetPath { get; }
        }

        private readonly struct LiveBoundsSnapshot
        {
            public LiveBoundsSnapshot(Vector3 worldBoundsCenter, Vector3 worldBoundsSize)
            {
                WorldBoundsCenter = worldBoundsCenter;
                WorldBoundsSize = worldBoundsSize;
            }

            public Vector3 WorldBoundsCenter { get; }
            public Vector3 WorldBoundsSize { get; }
        }
    }
}
