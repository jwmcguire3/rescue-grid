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

        public GameState? CurrentState { get; private set; }

        public void Rebuild(GameState state)
        {
            if (state is null)
            {
                Debug.LogWarning($"{nameof(GameStateViewPresenter)} requires a valid {nameof(GameState)} to rebuild.", this);
                return;
            }

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
        }
    }
}
