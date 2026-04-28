using Rescue.Core.Pipeline;
using Rescue.Core.State;
using Rescue.Unity.BoardPresentation;
using Rescue.Unity.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Rescue.Unity.Input
{
    public sealed class BoardInputPresenter : MonoBehaviour
    {
        [SerializeField] private Camera? inputCamera;
        [SerializeField] private BoardGridViewPresenter? gridView;
        [SerializeField] private GameStateViewPresenter? gameStateView;
        [SerializeField] private LayerMask boardCellLayer = ~0;
        [SerializeField] private bool enableMouseInput = true;
        [SerializeField] private bool enableTouchInput;
        [SerializeField] private bool logStateViewDiagnostics = true;

        private GameState? fallbackState;

        public GameState? CurrentState => gameStateView?.CurrentState ?? fallbackState;

        private void Update()
        {
            if (enableMouseInput && TryGetMouseScreenPosition(out Vector2 mouseScreenPosition))
            {
                TryHandleScreenPosition(mouseScreenPosition);
            }

            if (enableTouchInput && TryGetTouchScreenPosition(out Vector2 touchScreenPosition))
            {
                TryHandleScreenPosition(touchScreenPosition);
            }
        }

        public void SetCurrentState(GameState state, bool refreshView = true)
        {
            if (gameStateView is not null && refreshView)
            {
                fallbackState = null;
                gameStateView.Rebuild(state);
                return;
            }

            fallbackState = state;
        }

        public bool TryGetTileCoordFromObject(GameObject source, out TileCoord coord)
        {
            coord = default;
            if (source is null)
            {
                return false;
            }

            BoardCellView? cellView = source.GetComponentInParent<BoardCellView>();
            if (cellView is null)
            {
                return false;
            }

            coord = cellView.Coord;
            return true;
        }

        public bool TryRunActionAt(TileCoord coord)
        {
            return TryRunActionAt(coord, Vector2.zero, gameObject, "<direct-call>");
        }

        private bool TryHandleScreenPosition(Vector2 screenPosition)
        {
            Camera? cameraToUse = ResolveInputCamera();
            if (cameraToUse is null)
            {
                return false;
            }

            Ray ray = cameraToUse.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, boardCellLayer, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            if (!TryGetTileCoordFromObject(hit.collider.gameObject, out TileCoord coord))
            {
                return false;
            }

            return TryRunActionAt(coord, screenPosition, hit.collider.gameObject);
        }

        private bool TryRunActionAt(TileCoord coord, Vector2 screenPosition, GameObject hitObject)
        {
            return TryRunActionAt(coord, screenPosition, hitObject, GetObjectPath(hitObject.transform));
        }

        private bool TryRunActionAt(TileCoord coord, Vector2 screenPosition, GameObject hitObject, string hitObjectPath)
        {
            if (gameStateView is not null && gameStateView.IsPlaybackActive)
            {
                return false;
            }

            GameState? currentState = CurrentState;
            if (currentState is null)
            {
                Debug.LogWarning($"{nameof(BoardInputPresenter)} requires a current {nameof(GameState)} before handling input.", this);
                return false;
            }

            if (!BoardHelpers.InBounds(currentState.Board, coord))
            {
                return false;
            }

            GameState previousState = currentState;
            ActionInput input = new ActionInput(coord);
            LogPreActionDiagnostics(previousState, coord, screenPosition, hitObject, hitObjectPath);
            ActionResult result = Pipeline.RunAction(previousState, input);
            LogActionResultDiagnostics(previousState, coord, result);
            ApplyResult(previousState, input, result);
            LogPostApplyDiagnostics(result);

            return true;
        }

        private void ApplyResult(GameState previousState, ActionInput input, ActionResult result)
        {
            if (gameStateView is not null)
            {
                gameStateView.ApplyActionResult(previousState, input, result);
                fallbackState = null;
            }
            else
            {
                fallbackState = result.State;
            }

            if (TryGetInvalidInput(result.Events, out InvalidInput invalidInput))
            {
                Debug.Log(
                    $"Rejected board tap at ({invalidInput.TappedCoord.Row}, {invalidInput.TappedCoord.Col}) because {invalidInput.Reason}.",
                    this);
            }
        }

        private void LogPreActionDiagnostics(
            GameState previousState,
            TileCoord coord,
            Vector2 screenPosition,
            GameObject hitObject,
            string hitObjectPath)
        {
            if (!logStateViewDiagnostics)
            {
                return;
            }

            Tile tile = BoardHelpers.GetTile(previousState.Board, coord);
            string boardVisual = gameStateView?.DescribeBoardVisual(coord) ?? "board visual: <no GameStateViewPresenter>";
            Debug.Log(
                "[StateViewTrace] click "
                + $"screen=({screenPosition.x:0.0},{screenPosition.y:0.0}) "
                + $"hit='{hitObject.name}' path='{hitObjectPath}' "
                + $"coord=({coord.Row},{coord.Col}) "
                + $"coreBefore={FormatTile(tile)} "
                + $"boardVisualBefore={boardVisual}",
                this);
        }

        private void LogActionResultDiagnostics(GameState previousState, TileCoord coord, ActionResult result)
        {
            if (!logStateViewDiagnostics)
            {
                return;
            }

            string eventsSummary = FormatResultEvents(result);
            string dockBefore = FormatDock(previousState.Dock);
            string dockAfter = FormatDock(result.State.Dock);
            Debug.Log(
                "[StateViewTrace] action "
                + $"coord=({coord.Row},{coord.Col}) "
                + $"outcome={result.Outcome} "
                + $"events=[{eventsSummary}] "
                + $"dockBefore={dockBefore} "
                + $"dockAfter={dockAfter}",
                this);
        }

        private void LogPostApplyDiagnostics(ActionResult result)
        {
            if (!logStateViewDiagnostics)
            {
                return;
            }

            string dockVisual = gameStateView?.DescribeDockVisuals() ?? "dock visuals: <no GameStateViewPresenter>";
            Debug.Log(
                "[StateViewTrace] view-after-apply "
                + $"dockVisuals={dockVisual} "
                + "note=playback may update board/dock visuals over subsequent playback steps before final sync.",
                this);
        }

        private static string FormatResultEvents(ActionResult result)
        {
            if (result.Events.IsDefaultOrEmpty)
            {
                return "none";
            }

            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            for (int i = 0; i < result.Events.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append("; ");
                }

                builder.Append(FormatEvent(result.Events[i]));
            }

            return builder.ToString();
        }

        private static string FormatEvent(ActionEvent actionEvent)
        {
            return actionEvent switch
            {
                InvalidInput invalid => $"InvalidInput coord=({invalid.TappedCoord.Row},{invalid.TappedCoord.Col}) reason={invalid.Reason}",
                GroupRemoved removed => $"GroupRemoved type={removed.Type} coords={FormatCoords(removed.Coords)}",
                DockInserted inserted => $"DockInserted pieces={FormatDebris(inserted.Pieces)} occupancyAfterInsert={inserted.OccupancyAfterInsert} overflow={inserted.OverflowCount}",
                DockCleared cleared => $"DockCleared type={cleared.Type} sets={cleared.SetsCleared} occupancyAfterClear={cleared.OccupancyAfterClear}",
                GravitySettled gravity => $"GravitySettled moves={gravity.Moves.Length}",
                Spawned spawned => $"Spawned pieces={FormatSpawned(spawned)}",
                _ => actionEvent.GetType().Name,
            };
        }

        private static string FormatTile(Tile tile)
        {
            return tile switch
            {
                EmptyTile => "Empty",
                FloodedTile => "Flooded",
                RescuePathTile => "RescuePath",
                DebrisTile debris => $"Debris({debris.Type})",
                BlockerTile blocker when blocker.Hidden is not null => $"Blocker({blocker.Type}, hp={blocker.Hp}, hidden={blocker.Hidden.Type})",
                BlockerTile blocker => $"Blocker({blocker.Type}, hp={blocker.Hp})",
                TargetTile target => $"Target({target.TargetId}, extracted={target.Extracted})",
                _ => tile.GetType().Name,
            };
        }

        private static string FormatDock(Dock dock)
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            builder.Append('[');
            for (int i = 0; i < dock.Slots.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                builder.Append(dock.Slots[i]?.ToString() ?? "-");
            }

            builder.Append(']');
            return builder.ToString();
        }

        private static string FormatCoords(System.Collections.Immutable.ImmutableArray<TileCoord> coords)
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            for (int i = 0; i < coords.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append('|');
                }

                builder.Append('(').Append(coords[i].Row).Append(',').Append(coords[i].Col).Append(')');
            }

            return builder.ToString();
        }

        private static string FormatDebris(System.Collections.Immutable.ImmutableArray<DebrisType> debris)
        {
            if (debris.IsDefaultOrEmpty)
            {
                return "none";
            }

            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            for (int i = 0; i < debris.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append('|');
                }

                builder.Append(debris[i]);
            }

            return builder.ToString();
        }

        private static string FormatSpawned(Spawned spawned)
        {
            if (spawned.Pieces.IsDefaultOrEmpty)
            {
                return "none";
            }

            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            for (int i = 0; i < spawned.Pieces.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append('|');
                }

                SpawnedPiece piece = spawned.Pieces[i];
                builder.Append(piece.Type).Append("@(").Append(piece.Coord.Row).Append(',').Append(piece.Coord.Col).Append(')');
            }

            return builder.ToString();
        }

        private static string GetObjectPath(Transform transform)
        {
            System.Collections.Generic.Stack<string> segments = new System.Collections.Generic.Stack<string>();
            Transform? current = transform;
            while (current is not null)
            {
                segments.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", segments);
        }

        private Camera? ResolveInputCamera()
        {
            if (inputCamera is not null)
            {
                return inputCamera;
            }

            if (Camera.main is not null)
            {
                inputCamera = Camera.main;
                return inputCamera;
            }

            Debug.LogWarning($"{nameof(BoardInputPresenter)} is missing {nameof(inputCamera)}.", this);
            return null;
        }

        private static bool TryGetInvalidInput(
            System.Collections.Immutable.ImmutableArray<ActionEvent> events,
            out InvalidInput invalidInput)
        {
            for (int i = 0; i < events.Length; i++)
            {
                if (events[i] is InvalidInput)
                {
                    invalidInput = (InvalidInput)events[i];
                    return true;
                }
            }

            invalidInput = new InvalidInput(default, InvalidInputReason.Empty);
            return false;
        }

        private static bool TryGetMouseScreenPosition(out Vector2 screenPosition)
        {
            screenPosition = default;
            Mouse? mouse = Mouse.current;
            if (mouse is null || !mouse.leftButton.wasPressedThisFrame)
            {
                return false;
            }

            screenPosition = mouse.position.ReadValue();
            return true;
        }

        private static bool TryGetTouchScreenPosition(out Vector2 screenPosition)
        {
            screenPosition = default;
            Touchscreen? touchscreen = Touchscreen.current;
            if (touchscreen is null || !touchscreen.primaryTouch.press.wasPressedThisFrame)
            {
                return false;
            }

            screenPosition = touchscreen.primaryTouch.position.ReadValue();
            return true;
        }
    }
}
