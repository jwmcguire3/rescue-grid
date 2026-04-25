using System;
using System.Collections.Generic;
using System.Text;
using Rescue.Core.State;
using Rescue.Unity.Art.Registries;
using Rescue.Unity.BoardPresentation;
using Rescue.Unity.Debugging;
using Rescue.Unity.Presentation;
using UnityEditor;
using UnityEngine;

namespace Rescue.Unity.EditorTools.Diagnostics
{
    public static class DebrisInstanceTracer
    {
        private const float PositionEpsilon = 0.01f;
        private const float RotationEpsilonDegrees = 0.5f;
        private const float ScaleEpsilon = 0.01f;

        [MenuItem("Rescue Grid/Debug/Trace Debris C At (1,1)")]
        public static void TraceDefaultDebris()
        {
            DebrisTraceReport report = TraceLiveDebris(new TileCoord(1, 1));
            string reportText = report.ToDisplayString();
            Debug.Log(reportText);
            EditorGUIUtility.systemCopyBuffer = reportText;
        }

        [MenuItem("Rescue Grid/Debug/Trace Debris C At (1,1)", true)]
        public static bool ValidateTraceDefaultDebris()
        {
            return EditorApplication.isPlaying;
        }

        public static DebrisTraceReport TraceLiveDebris(TileCoord coord)
        {
            DebugPanel debugPanel = ResolveDebugPanel();
            GameState state = debugPanel.CurrentState;
            Tile tile = state.Board.Tiles[coord.Row][coord.Col];
            if (tile is not DebrisTile debrisTile)
            {
                throw new InvalidOperationException($"Expected debris at ({coord.Row}, {coord.Col}) but found {tile.GetType().Name}.");
            }

            GameStateViewPresenter viewPresenter = ResolveViewPresenter();
            BoardGridViewPresenter gridPresenter = ResolvePresenterField<BoardGridViewPresenter>(viewPresenter, "boardGrid");
            BoardContentViewPresenter contentPresenter = ResolvePresenterField<BoardContentViewPresenter>(viewPresenter, "boardContent");

            float cellSize = ReadPrivateField<float>(gridPresenter, "cellSize");
            bool centerBoard = ReadPrivateField<bool>(gridPresenter, "centerBoard");
            Vector3 expectedAnchorLocalPosition = CalculateExpectedAnchorLocalPosition(coord, state.Board.Width, state.Board.Height, cellSize, centerBoard);

            if (!gridPresenter.TryGetCellAnchor(coord, out Transform anchor))
            {
                throw new InvalidOperationException($"Could not resolve anchor for ({coord.Row}, {coord.Col}).");
            }

            float contentYOffset = ReadPrivateField<float>(contentPresenter, "contentYOffset");
            Transform? configuredContentRoot = ReadPrivateField<Transform?>(contentPresenter, "contentRoot");
            PieceVisualRegistry pieceRegistry = ResolvePresenterField<PieceVisualRegistry>(contentPresenter, "pieceRegistry");
            GameObject prefab = pieceRegistry.GetPrefab(debrisTile.Type)
                ?? throw new InvalidOperationException($"No prefab was registered for debris type {debrisTile.Type}.");

            Transform contentParent = configuredContentRoot ?? anchor;
            string contentName = $"Content_{coord.Row:00}_{coord.Col:00}_Debris_{debrisTile.Type}";
            Transform contentTransform = FindNamedChild(contentParent, contentName)
                ?? throw new InvalidOperationException($"Could not find spawned content '{contentName}' under '{contentParent.name}'.");

            Vector3 expectedContentWorldPosition = anchor.position + new Vector3(0f, contentYOffset, 0f);
            TransformSnapshot anchorSnapshot = CreateSnapshot(anchor, expectedAnchorLocalPosition, anchor.position, "Cell anchor");
            TransformSnapshot contentSnapshot = CreateSnapshot(
                contentTransform,
                contentTransform.localPosition,
                expectedContentWorldPosition,
                "Spawned content");
            List<TransformNodeReport> liveInstanceNodes = InspectTransformTree(contentTransform, BuildHierarchyPath(contentTransform));
            TransformNodeReport? firstLiveOffset = FindFirstOffsetTransform(liveInstanceNodes);

            string prefabAssetPath = AssetDatabase.GetAssetPath(prefab);
            if (string.IsNullOrWhiteSpace(prefabAssetPath))
            {
                throw new InvalidOperationException($"Unable to resolve prefab asset path for '{prefab.name}'.");
            }

            PrefabTraceReport prefabTrace = TracePrefabAsset(prefabAssetPath);
            ModelTraceReport modelTrace = TraceModelAsset(prefabTrace.ModelAssetPath);
            string rootCause = ClassifyRootCause(contentSnapshot, prefabTrace, modelTrace);

            return new DebrisTraceReport(
                coord,
                state.Board.Width,
                state.Board.Height,
                tile.GetType().Name,
                debrisTile.Type,
                anchorSnapshot,
                contentSnapshot,
                liveInstanceNodes,
                firstLiveOffset,
                prefabAssetPath,
                prefabTrace,
                modelTrace,
                rootCause);
        }

        public static PrefabTraceReport TracePrefabAsset(string prefabAssetPath)
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabAssetPath);
            try
            {
                List<TransformNodeReport> nodes = InspectTransformTree(prefabRoot.transform, prefabRoot.name);
                TransformNodeReport? firstNonIdentity = FindFirstMeaningfulTransform(nodes);
                string modelAssetPath = ResolveModelAssetPath(prefabRoot.transform);

                return new PrefabTraceReport(prefabRoot.name, prefabAssetPath, modelAssetPath, nodes, firstNonIdentity);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        public static ModelTraceReport TraceModelAsset(string modelAssetPath)
        {
            if (string.IsNullOrWhiteSpace(modelAssetPath))
            {
                throw new InvalidOperationException("Model asset path is required to trace the FBX hierarchy.");
            }

            GameObject sourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(modelAssetPath)
                ?? throw new InvalidOperationException($"Could not load model asset '{modelAssetPath}'.");

            GameObject modelRoot = PrefabUtility.InstantiatePrefab(sourceModel) as GameObject
                ?? UnityEngine.Object.Instantiate(sourceModel);
            try
            {
                List<TransformNodeReport> nodes = InspectTransformTree(modelRoot.transform, modelRoot.name);
                TransformNodeReport? firstOffset = FindFirstOffsetTransform(nodes);
                return new ModelTraceReport(modelRoot.name, modelAssetPath, nodes, firstOffset);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(modelRoot);
            }
        }

        private static Vector3 CalculateExpectedAnchorLocalPosition(TileCoord coord, int width, int height, float cellSize, bool centerBoard)
        {
            Vector3 originOffset = centerBoard
                ? new Vector3(-((width - 1) * cellSize * 0.5f), 0f, (height - 1) * cellSize * 0.5f)
                : Vector3.zero;

            return originOffset + new Vector3(coord.Col * cellSize, 0f, -coord.Row * cellSize);
        }

        private static TransformSnapshot CreateSnapshot(Transform transform, Vector3 expectedLocalPosition, Vector3 expectedWorldPosition, string label)
        {
            return new TransformSnapshot(
                label,
                BuildHierarchyPath(transform),
                transform.parent is null ? "<none>" : transform.parent.name,
                transform.localPosition,
                expectedLocalPosition,
                transform.position,
                expectedWorldPosition,
                transform.localRotation.eulerAngles,
                transform.rotation.eulerAngles,
                transform.localScale,
                transform.position - expectedWorldPosition);
        }

        private static GameStateViewPresenter ResolveViewPresenter()
        {
            GameStateViewPresenter? presenter = UnityEngine.Object.FindFirstObjectByType<GameStateViewPresenter>();
            return presenter ?? throw new InvalidOperationException("Could not find a live GameStateViewPresenter.");
        }

        private static DebugPanel ResolveDebugPanel()
        {
            DebugPanel? debugPanel = DebugPanel.Instance ?? UnityEngine.Object.FindFirstObjectByType<DebugPanel>();
            return debugPanel ?? throw new InvalidOperationException("Could not find a live DebugPanel.");
        }

        private static T ResolvePresenterField<T>(object instance, string fieldName) where T : class
        {
            T? value = ReadPrivateField<T?>(instance, fieldName);
            return value ?? throw new InvalidOperationException($"Expected '{fieldName}' on '{instance.GetType().Name}'.");
        }

        private static T ReadPrivateField<T>(object instance, string fieldName)
        {
            System.Reflection.FieldInfo? field = instance.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            if (field is null)
            {
                throw new InvalidOperationException($"Could not find field '{fieldName}' on '{instance.GetType().Name}'.");
            }

            object? value = field.GetValue(instance);
            return value is null ? default! : (T)value;
        }

        private static Transform? FindNamedChild(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (string.Equals(child.name, name, StringComparison.Ordinal))
                {
                    return child;
                }
            }

            return null;
        }

        private static string ResolveModelAssetPath(Transform prefabRoot)
        {
            if (prefabRoot.childCount == 0)
            {
                return string.Empty;
            }

            Transform visualChild = prefabRoot.GetChild(0);
            UnityEngine.Object? sourceObject = PrefabUtility.GetCorrespondingObjectFromSource(visualChild.gameObject);
            return sourceObject is null ? string.Empty : AssetDatabase.GetAssetPath(sourceObject);
        }

        private static List<TransformNodeReport> InspectTransformTree(Transform root, string rootPath)
        {
            List<TransformNodeReport> nodes = new List<TransformNodeReport>();
            InspectTransformTreeRecursive(root, rootPath, nodes);
            return nodes;
        }

        private static void InspectTransformTreeRecursive(Transform current, string currentPath, ICollection<TransformNodeReport> nodes)
        {
            Vector3? directRendererBoundsCenterLocal = TryGetDirectRendererBoundsCenterLocal(current);
            nodes.Add(new TransformNodeReport(
                current.name,
                currentPath,
                current.localPosition,
                current.localRotation.eulerAngles,
                current.localScale,
                directRendererBoundsCenterLocal));

            for (int i = 0; i < current.childCount; i++)
            {
                Transform child = current.GetChild(i);
                InspectTransformTreeRecursive(child, $"{currentPath}/{child.name}", nodes);
            }
        }

        private static Vector3? TryGetDirectRendererBoundsCenterLocal(Transform transform)
        {
            Renderer[] renderers = transform.GetComponents<Renderer>();
            if (renderers.Length == 0)
            {
                return null;
            }

            Bounds combinedBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                combinedBounds.Encapsulate(renderers[i].bounds);
            }

            return transform.InverseTransformPoint(combinedBounds.center);
        }

        private static TransformNodeReport? FindFirstMeaningfulTransform(IReadOnlyList<TransformNodeReport> nodes)
        {
            for (int i = 1; i < nodes.Count; i++)
            {
                TransformNodeReport node = nodes[i];
                if (IsMeaningfullyTransformed(node))
                {
                    return node;
                }
            }

            return null;
        }

        private static TransformNodeReport? FindFirstOffsetTransform(IReadOnlyList<TransformNodeReport> nodes)
        {
            for (int i = 1; i < nodes.Count; i++)
            {
                TransformNodeReport node = nodes[i];
                if (HasMeaningfulOffset(node))
                {
                    return node;
                }
            }

            return null;
        }

        private static bool IsMeaningfullyTransformed(TransformNodeReport node)
        {
            return HasMeaningfulPosition(node.LocalPosition)
                || HasMeaningfulRotation(node.LocalEulerAngles)
                || HasMeaningfulScale(node.LocalScale);
        }

        private static bool HasMeaningfulOffset(TransformNodeReport node)
        {
            return HasMeaningfulPosition(node.LocalPosition)
                || (node.DirectRendererBoundsCenterLocal.HasValue && HasMeaningfulPosition(node.DirectRendererBoundsCenterLocal.Value));
        }

        private static bool HasMeaningfulPosition(Vector3 position)
        {
            return position.magnitude > PositionEpsilon;
        }

        private static bool HasMeaningfulScale(Vector3 scale)
        {
            return Mathf.Abs(scale.x - 1f) > ScaleEpsilon
                || Mathf.Abs(scale.y - 1f) > ScaleEpsilon
                || Mathf.Abs(scale.z - 1f) > ScaleEpsilon;
        }

        private static bool HasMeaningfulRotation(Vector3 eulerAngles)
        {
            return EffectiveAngle(eulerAngles.x) > RotationEpsilonDegrees
                || EffectiveAngle(eulerAngles.y) > RotationEpsilonDegrees
                || EffectiveAngle(eulerAngles.z) > RotationEpsilonDegrees;
        }

        private static float EffectiveAngle(float angle)
        {
            float normalized = Mathf.Repeat(angle, 360f);
            return Mathf.Min(normalized, 360f - normalized);
        }

        private static string ClassifyRootCause(TransformSnapshot contentSnapshot, PrefabTraceReport prefabTrace, ModelTraceReport modelTrace)
        {
            if (contentSnapshot.WorldDelta.magnitude > PositionEpsilon)
            {
                return "content placement under BoardContentRoot";
            }

            if (modelTrace.FirstOffsetTransform is not null)
            {
                return modelTrace.FirstOffsetTransform.DirectRendererBoundsCenterLocal.HasValue
                    ? "mesh bounds not centered on transform"
                    : "imported FBX child transform";
            }

            if (prefabTrace.FirstNonIdentityTransform is not null)
            {
                return "wrapper prefab normalization";
            }

            return "board anchor math";
        }

        private static string BuildHierarchyPath(Transform transform)
        {
            Stack<string> segments = new Stack<string>();
            Transform? current = transform;
            while (current is not null)
            {
                segments.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", segments);
        }
    }

    public sealed class DebrisTraceReport
    {
        public DebrisTraceReport(
            TileCoord coord,
            int boardWidth,
            int boardHeight,
            string tileKind,
            DebrisType debrisType,
            TransformSnapshot anchor,
            TransformSnapshot content,
            IReadOnlyList<TransformNodeReport> liveInstanceNodes,
            TransformNodeReport? firstLiveOffset,
            string prefabAssetPath,
            PrefabTraceReport prefabTrace,
            ModelTraceReport modelTrace,
            string rootCause)
        {
            Coord = coord;
            BoardWidth = boardWidth;
            BoardHeight = boardHeight;
            TileKind = tileKind;
            DebrisType = debrisType;
            Anchor = anchor;
            Content = content;
            LiveInstanceNodes = liveInstanceNodes;
            FirstLiveOffsetTransform = firstLiveOffset;
            PrefabAssetPath = prefabAssetPath;
            PrefabTrace = prefabTrace;
            ModelTrace = modelTrace;
            RootCause = rootCause;
        }

        public TileCoord Coord { get; }

        public int BoardWidth { get; }

        public int BoardHeight { get; }

        public string TileKind { get; }

        public DebrisType DebrisType { get; }

        public TransformSnapshot Anchor { get; }

        public TransformSnapshot Content { get; }

        public IReadOnlyList<TransformNodeReport> LiveInstanceNodes { get; }

        public TransformNodeReport? FirstLiveOffsetTransform { get; }

        public string PrefabAssetPath { get; }

        public PrefabTraceReport PrefabTrace { get; }

        public ModelTraceReport ModelTrace { get; }

        public string RootCause { get; }

        public string ToDisplayString()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Debris Trace Report");
            builder.AppendLine($"Tile identity: TileCoord({Coord.Row}, {Coord.Col}), {TileKind}, debrisType={DebrisType}, board={BoardWidth}x{BoardHeight}");
            builder.AppendLine();
            builder.AppendLine("Anchor");
            builder.AppendLine($"Path: {Anchor.Path}");
            builder.AppendLine($"Parent: {Anchor.ParentName}");
            builder.AppendLine($"Actual localPosition: {Anchor.LocalPosition}");
            builder.AppendLine($"Expected localPosition: {Anchor.ExpectedLocalPosition}");
            builder.AppendLine($"Actual worldPosition: {Anchor.WorldPosition}");
            builder.AppendLine($"Expected worldPosition: {Anchor.ExpectedWorldPosition}");
            builder.AppendLine($"Local rotation: {Anchor.LocalEulerAngles}");
            builder.AppendLine($"Local scale: {Anchor.LocalScale}");
            builder.AppendLine();
            builder.AppendLine("Content");
            builder.AppendLine($"Path: {Content.Path}");
            builder.AppendLine($"Parent: {Content.ParentName}");
            builder.AppendLine($"Actual localPosition: {Content.LocalPosition}");
            builder.AppendLine($"Actual worldPosition: {Content.WorldPosition}");
            builder.AppendLine($"Expected worldPosition: {Content.ExpectedWorldPosition}");
            builder.AppendLine($"World delta from expected spawn: {Content.WorldDelta}");
            builder.AppendLine($"Local rotation: {Content.LocalEulerAngles}");
            builder.AppendLine($"Local scale: {Content.LocalScale}");
            builder.AppendLine();
            builder.AppendLine($"Live instance first offset transform: {FormatNode(FirstLiveOffsetTransform)}");
            AppendNodes(builder, "Live instance chain", LiveInstanceNodes);
            builder.AppendLine();
            builder.AppendLine($"Prefab asset: {PrefabAssetPath}");
            builder.AppendLine($"Prefab first non-identity transform: {FormatNode(PrefabTrace.FirstNonIdentityTransform)}");
            AppendNodes(builder, "Prefab chain", PrefabTrace.Nodes);
            builder.AppendLine();
            builder.AppendLine($"Model asset: {ModelTrace.ModelAssetPath}");
            builder.AppendLine($"Model first offset transform: {FormatNode(ModelTrace.FirstOffsetTransform)}");
            AppendNodes(builder, "FBX chain", ModelTrace.Nodes);
            builder.AppendLine();
            builder.AppendLine($"Final verdict: first bad transform is {FormatNode(FirstLiveOffsetTransform) ?? FormatNode(ModelTrace.FirstOffsetTransform) ?? FormatNode(PrefabTrace.FirstNonIdentityTransform) ?? Content.Path}; introduced offset is {FirstLiveOffsetTransform?.DirectRendererBoundsCenterLocal ?? ModelTrace.FirstOffsetTransform?.DirectRendererBoundsCenterLocal ?? Content.WorldDelta}.");
            builder.AppendLine($"Root cause classification: {RootCause}");
            return builder.ToString();
        }

        private static void AppendNodes(StringBuilder builder, string heading, IReadOnlyList<TransformNodeReport> nodes)
        {
            builder.AppendLine(heading);
            for (int i = 0; i < nodes.Count; i++)
            {
                TransformNodeReport node = nodes[i];
                builder.Append($"- {node.Path}: localPos={node.LocalPosition}, localEuler={node.LocalEulerAngles}, localScale={node.LocalScale}");
                if (node.DirectRendererBoundsCenterLocal.HasValue)
                {
                    builder.Append($", rendererBoundsCenterLocal={node.DirectRendererBoundsCenterLocal.Value}");
                }

                builder.AppendLine();
            }
        }

        private static string? FormatNode(TransformNodeReport? node)
        {
            return node is null ? null : $"{node.Path} (localPos={node.LocalPosition}, boundsCenterLocal={node.DirectRendererBoundsCenterLocal?.ToString() ?? "n/a"})";
        }
    }

    public sealed class PrefabTraceReport
    {
        public PrefabTraceReport(string prefabName, string prefabAssetPath, string modelAssetPath, IReadOnlyList<TransformNodeReport> nodes, TransformNodeReport? firstNonIdentityTransform)
        {
            PrefabName = prefabName;
            PrefabAssetPath = prefabAssetPath;
            ModelAssetPath = modelAssetPath;
            Nodes = nodes;
            FirstNonIdentityTransform = firstNonIdentityTransform;
        }

        public string PrefabName { get; }

        public string PrefabAssetPath { get; }

        public string ModelAssetPath { get; }

        public IReadOnlyList<TransformNodeReport> Nodes { get; }

        public TransformNodeReport? FirstNonIdentityTransform { get; }
    }

    public sealed class ModelTraceReport
    {
        public ModelTraceReport(string modelName, string modelAssetPath, IReadOnlyList<TransformNodeReport> nodes, TransformNodeReport? firstOffsetTransform)
        {
            ModelName = modelName;
            ModelAssetPath = modelAssetPath;
            Nodes = nodes;
            FirstOffsetTransform = firstOffsetTransform;
        }

        public string ModelName { get; }

        public string ModelAssetPath { get; }

        public IReadOnlyList<TransformNodeReport> Nodes { get; }

        public TransformNodeReport? FirstOffsetTransform { get; }
    }

    public sealed class TransformSnapshot
    {
        public TransformSnapshot(
            string label,
            string path,
            string parentName,
            Vector3 localPosition,
            Vector3 expectedLocalPosition,
            Vector3 worldPosition,
            Vector3 expectedWorldPosition,
            Vector3 localEulerAngles,
            Vector3 worldEulerAngles,
            Vector3 localScale,
            Vector3 worldDelta)
        {
            Label = label;
            Path = path;
            ParentName = parentName;
            LocalPosition = localPosition;
            ExpectedLocalPosition = expectedLocalPosition;
            WorldPosition = worldPosition;
            ExpectedWorldPosition = expectedWorldPosition;
            LocalEulerAngles = localEulerAngles;
            WorldEulerAngles = worldEulerAngles;
            LocalScale = localScale;
            WorldDelta = worldDelta;
        }

        public string Label { get; }

        public string Path { get; }

        public string ParentName { get; }

        public Vector3 LocalPosition { get; }

        public Vector3 ExpectedLocalPosition { get; }

        public Vector3 WorldPosition { get; }

        public Vector3 ExpectedWorldPosition { get; }

        public Vector3 LocalEulerAngles { get; }

        public Vector3 WorldEulerAngles { get; }

        public Vector3 LocalScale { get; }

        public Vector3 WorldDelta { get; }
    }

    public sealed class TransformNodeReport
    {
        public TransformNodeReport(string name, string path, Vector3 localPosition, Vector3 localEulerAngles, Vector3 localScale, Vector3? directRendererBoundsCenterLocal)
        {
            Name = name;
            Path = path;
            LocalPosition = localPosition;
            LocalEulerAngles = localEulerAngles;
            LocalScale = localScale;
            DirectRendererBoundsCenterLocal = directRendererBoundsCenterLocal;
        }

        public string Name { get; }

        public string Path { get; }

        public Vector3 LocalPosition { get; }

        public Vector3 LocalEulerAngles { get; }

        public Vector3 LocalScale { get; }

        public Vector3? DirectRendererBoundsCenterLocal { get; }
    }
}
