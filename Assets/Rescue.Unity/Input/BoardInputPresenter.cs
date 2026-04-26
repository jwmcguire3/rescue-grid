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
            ActionResult result = Pipeline.RunAction(previousState, input);
            ApplyResult(previousState, input, result);

            return true;
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

            return TryRunActionAt(coord);
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
