using Rescue.Core.Pipeline;
using Rescue.Core.State;
using Rescue.Unity.BoardPresentation;
using Rescue.Unity.UI;
using UnityEngine;

namespace Rescue.Unity.Presentation
{
    public sealed class GameStateViewPresenter : MonoBehaviour
    {
        [SerializeField] private BoardGridViewPresenter? boardGrid;
        [SerializeField] private BoardContentViewPresenter? boardContent;
        [SerializeField] private WaterViewPresenter? waterView;
        [SerializeField] private DockViewPresenter? dockView;
        [SerializeField] private TargetFeedbackPresenter? targetFeedback;

        public GameState? CurrentState { get; private set; }

        public void Rebuild(GameState state)
        {
            if (state is null)
            {
                Debug.LogWarning($"{nameof(GameStateViewPresenter)} requires a valid {nameof(GameState)} to rebuild.", this);
                return;
            }

            GameState? previousState = CurrentState;
            CurrentState = state;

            if (boardGrid is null)
            {
                Debug.LogWarning($"{nameof(GameStateViewPresenter)} is missing {nameof(boardGrid)}.", this);
            }
            else
            {
                boardGrid.RebuildGrid(state);
            }

            if (boardContent is null)
            {
                Debug.LogWarning($"{nameof(GameStateViewPresenter)} is missing {nameof(boardContent)}.", this);
            }
            else
            {
                boardContent.RebuildContent(state);
            }

            if (waterView is null)
            {
                Debug.LogWarning($"{nameof(GameStateViewPresenter)} is missing {nameof(waterView)}.", this);
            }
            else
            {
                waterView.RebuildWater(state);
            }

            if (dockView is null)
            {
                Debug.LogWarning($"{nameof(GameStateViewPresenter)} is missing {nameof(dockView)}.", this);
            }
            else
            {
                dockView.Rebuild(state);
            }

            if (targetFeedback is null)
            {
                Debug.LogWarning($"{nameof(GameStateViewPresenter)} is missing {nameof(targetFeedback)}.", this);
            }
            else
            {
                targetFeedback.Apply(previousState, state);
            }
        }

        public void ApplyActionResult(ActionResult result)
        {
            if (result is null)
            {
                Debug.LogWarning($"{nameof(GameStateViewPresenter)} requires a valid {nameof(ActionResult)}.", this);
                return;
            }

            Rebuild(result.State);

            if (dockView is not null)
            {
                dockView.ApplyActionResult(result);
            }
        }

        public void ClearAll()
        {
            CurrentState = null;

            if (boardGrid is null)
            {
                Debug.LogWarning($"{nameof(GameStateViewPresenter)} is missing {nameof(boardGrid)}.", this);
            }
            else
            {
                boardGrid.ClearGrid();
            }

            if (boardContent is null)
            {
                Debug.LogWarning($"{nameof(GameStateViewPresenter)} is missing {nameof(boardContent)}.", this);
            }
            else
            {
                boardContent.ClearContent();
            }

            if (waterView is null)
            {
                Debug.LogWarning($"{nameof(GameStateViewPresenter)} is missing {nameof(waterView)}.", this);
            }
            else
            {
                waterView.ClearWater();
            }

            if (dockView is null)
            {
                Debug.LogWarning($"{nameof(GameStateViewPresenter)} is missing {nameof(dockView)}.", this);
            }
            else
            {
                dockView.ClearSlots();
            }

            if (targetFeedback is null)
            {
                Debug.LogWarning($"{nameof(GameStateViewPresenter)} is missing {nameof(targetFeedback)}.", this);
            }
            else
            {
                targetFeedback.ClearFeedback();
            }
        }
    }
}
