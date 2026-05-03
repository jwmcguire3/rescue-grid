using Rescue.Core.Pipeline;
using Rescue.Core.State;
using Rescue.Unity.BoardPresentation;
using Rescue.Unity.Feedback;
using Rescue.Unity.FX;
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
        [SerializeField] private ActionPlaybackController? playbackController;
        [SerializeField] private FxEventRouter? fxEventRouter;
        [SerializeField] private AudioEventRouter? audioEventRouter;
        [SerializeField] private VictoryScreenPresenter? victoryScreen;
        [SerializeField] private LossScreenPresenter? lossScreen;

        private bool dockFeedbackHandledByPlayback;

        public GameState? CurrentState { get; private set; }

        public ActionPlaybackPlan CurrentPlaybackPlan { get; private set; } = ActionPlaybackPlan.Empty;

        public bool IsPlaybackActive => ResolvePlaybackController()?.IsPlaying ?? false;

        public string DescribeBoardVisual(TileCoord coord)
        {
            return boardContent is not null
                ? boardContent.DescribeVisualAt(coord)
                : "board visual: <missing BoardContentViewPresenter>";
        }

        public bool TryFindNearestDebrisVisualCoord(
            Camera camera,
            Vector2 screenPosition,
            GameState state,
            float maxScreenDistancePixels,
            out TileCoord coord,
            out GameObject? visualObject)
        {
            coord = default;
            visualObject = null;
            return boardContent is not null &&
                boardContent.TryFindNearestDebrisVisualCoord(
                    camera,
                    screenPosition,
                    state,
                    maxScreenDistancePixels,
                    out coord,
                    out visualObject);
        }

        public string DescribeDockVisuals()
        {
            return dockView is not null
                ? dockView.DescribeTrackedSlots()
                : "dock visuals: <missing DockViewPresenter>";
        }

        public void Rebuild(GameState state)
        {
            if (state is null)
            {
                Debug.LogWarning($"{nameof(GameStateViewPresenter)} requires a valid {nameof(GameState)} to rebuild.", this);
                return;
            }

            victoryScreen?.Hide();
            lossScreen?.Hide();
            ResolveFxEventRouter()?.ClearSpawnedFx();
            ForceSyncToState(state, "rebuild", cancelActivePlayback: true, clearPlaybackPlan: true);
        }

        public void ApplyActionResult(GameState previousState, ActionInput input, ActionResult result)
        {
            if (result is null)
            {
                Debug.LogWarning($"{nameof(GameStateViewPresenter)} requires a valid {nameof(ActionResult)}.", this);
                return;
            }

            if (previousState is null)
            {
                Debug.LogWarning($"{nameof(GameStateViewPresenter)} requires a valid previous {nameof(GameState)}.", this);
                return;
            }

            ActionPlaybackController? resolvedPlaybackController = ResolvePlaybackController();
            TryRouteResultAudio(previousState, input, result);

            if (resolvedPlaybackController is not null &&
                resolvedPlaybackController.TryPlayAction(previousState, input, result, FinalSyncActionResult))
            {
                dockFeedbackHandledByPlayback = true;
                CurrentPlaybackPlan = resolvedPlaybackController.CurrentPlan;
                return;
            }

            dockFeedbackHandledByPlayback = false;
            CurrentPlaybackPlan = ActionPlaybackBuilder.Build(previousState, input, result);
            FinalSyncActionResult(result);
        }

        public void ClearAll()
        {
            ResolvePlaybackController()?.CancelPlayback();
            CurrentState = null;
            CurrentPlaybackPlan = ActionPlaybackPlan.Empty;
            victoryScreen?.Hide();
            lossScreen?.Hide();
            ResolveFxEventRouter()?.ClearSpawnedFx();

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

            TargetFeedbackPresenter? resolvedTargetFeedback = ResolveTargetFeedback();
            if (resolvedTargetFeedback is null)
            {
                Debug.LogWarning($"{nameof(GameStateViewPresenter)} is missing {nameof(targetFeedback)}.", this);
            }
            else
            {
                resolvedTargetFeedback.ClearFeedback();
            }
        }

        public void ForceSyncToState(
            GameState state,
            string context = "authoritative sync",
            bool cancelActivePlayback = true,
            bool clearPlaybackPlan = true)
        {
            if (state is null)
            {
                Debug.LogWarning($"{nameof(GameStateViewPresenter)} requires a valid {nameof(GameState)} to sync.", this);
                return;
            }

            if (cancelActivePlayback && IsPlaybackActive)
            {
                ResolvePlaybackController()?.CancelPlayback();
            }

            GameState? previousState = CurrentState;
            CurrentState = state;
            if (clearPlaybackPlan)
            {
                CurrentPlaybackPlan = ActionPlaybackPlan.Empty;
            }

            if (!IsWinStateForPresentation(state))
            {
                victoryScreen?.Hide();
            }

            lossScreen?.Hide();
            PortraitGameSceneLayout.ApplyBoardStageLayout(state.Board.Width);

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
                boardContent.ForceSyncToState(state);
                string mismatches = boardContent.DescribeStateMismatches(state);
                if (!string.Equals(mismatches, "none", System.StringComparison.Ordinal))
                {
                    Debug.LogWarning($"[StateViewTrace] board sync mismatches after {context}: {mismatches}", this);
                }
            }

            if (waterView is null)
            {
                Debug.LogWarning($"{nameof(GameStateViewPresenter)} is missing {nameof(waterView)}.", this);
            }
            else
            {
                waterView.ForceSyncToState(state);
            }

            if (dockView is null)
            {
                Debug.LogWarning($"{nameof(GameStateViewPresenter)} is missing {nameof(dockView)}.", this);
            }
            else
            {
                dockView.ForceSyncToState(state);
            }

            TargetFeedbackPresenter? resolvedTargetFeedback = ResolveTargetFeedback();
            if (resolvedTargetFeedback is null)
            {
                Debug.LogWarning($"{nameof(GameStateViewPresenter)} is missing {nameof(targetFeedback)}.", this);
            }
            else
            {
                resolvedTargetFeedback.ClearFeedback();
                resolvedTargetFeedback.Apply(previousState, state);
            }
        }

        private TargetFeedbackPresenter? ResolveTargetFeedback()
        {
            if (targetFeedback is not null)
            {
                return targetFeedback;
            }

            targetFeedback = GetComponent<TargetFeedbackPresenter>();
            return targetFeedback;
        }

        private ActionPlaybackController? ResolvePlaybackController()
        {
            if (playbackController is not null)
            {
                return playbackController;
            }

            playbackController = GetComponent<ActionPlaybackController>();
            return playbackController;
        }

        private AudioEventRouter? ResolveAudioEventRouter()
        {
            if (audioEventRouter is not null)
            {
                return audioEventRouter;
            }

            audioEventRouter = GetComponent<AudioEventRouter>();
            return audioEventRouter;
        }

        private FxEventRouter? ResolveFxEventRouter()
        {
            if (fxEventRouter is not null)
            {
                return fxEventRouter;
            }

            fxEventRouter = GetComponent<FxEventRouter>();
            return fxEventRouter;
        }

        private void TryRouteResultAudio(GameState previousState, ActionInput input, ActionResult result)
        {
            AudioEventRouter? router = ResolveAudioEventRouter();
            if (router is null)
            {
                return;
            }

            router.BoardGrid ??= boardGrid;

            try
            {
                router.RouteResultSignals(previousState, input, result);
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning(
                    $"{nameof(GameStateViewPresenter)} skipped result audio after an exception: {exception.Message}",
                    this);
            }
        }

        private VictoryScreenPresenter? ResolveVictoryScreen()
        {
            if (victoryScreen is not null)
            {
                return victoryScreen;
            }

            victoryScreen = GetComponent<VictoryScreenPresenter>();
            if (victoryScreen is not null)
            {
                return victoryScreen;
            }

            victoryScreen = VictoryScreenPresenter.EnsureInstance();
            return victoryScreen;
        }

        private LossScreenPresenter? ResolveLossScreen()
        {
            if (lossScreen is not null)
            {
                return lossScreen;
            }

            lossScreen = GetComponent<LossScreenPresenter>();
            if (lossScreen is not null)
            {
                return lossScreen;
            }

            lossScreen = LossScreenPresenter.EnsureInstance();
            return lossScreen;
        }

        private void FinalSyncActionResult(ActionResult result)
        {
            ForceSyncToState(
                result.State,
                "playback final sync",
                cancelActivePlayback: false,
                clearPlaybackPlan: false);

            if (result.Outcome == ActionOutcome.Win && IsWinStateForPresentation(result.State))
            {
                boardContent?.ClearContent();
                dockView?.ClearSlots();
                ResolveVictoryScreen()?.Show();
                dockFeedbackHandledByPlayback = false;
                return;
            }

            if (IsLossOutcome(result.Outcome))
            {
                ResolveVictoryScreen()?.Hide();
                ResolveLossScreen()?.Show(result.Outcome);
                dockFeedbackHandledByPlayback = false;
                return;
            }

            if (!dockFeedbackHandledByPlayback && dockView is not null)
            {
                dockView.ApplyActionResult(result);
            }

            dockFeedbackHandledByPlayback = false;
        }

        private static bool IsWinStateForPresentation(GameState state)
        {
            if (state.Targets.IsDefaultOrEmpty)
            {
                return false;
            }

            for (int i = 0; i < state.Targets.Length; i++)
            {
                if (!state.Targets[i].Extracted)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsLossOutcome(ActionOutcome outcome)
        {
            return outcome == ActionOutcome.LossDockOverflow
                || outcome == ActionOutcome.LossWaterOnTarget
                || outcome == ActionOutcome.LossRescuePathFlooded
                || outcome == ActionOutcome.LossDistressedExpired;
        }
    }
}
