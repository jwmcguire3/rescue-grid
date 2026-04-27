using System;
using System.Collections;
using Rescue.Core.Pipeline;
using Rescue.Core.State;
using Rescue.Unity.BoardPresentation;
using Rescue.Unity.FX;
using Rescue.Unity.UI;
using UnityEngine;

namespace Rescue.Unity.Presentation
{
    public sealed class ActionPlaybackController : MonoBehaviour
    {
        [SerializeField] private ActionPlaybackSettings settings = new ActionPlaybackSettings();
        [SerializeField] private BoardGridViewPresenter? boardGrid;
        [SerializeField] private BoardContentViewPresenter? boardContent;
        [SerializeField] private WaterViewPresenter? waterView;
        [SerializeField] private DockViewPresenter? dockView;
        [SerializeField] private FxEventRouter? fxEventRouter;

        private Coroutine? activePlayback;
        private PlaybackContext? activeContext;
        private int playbackSessionId;
        private string currentStepName = "Idle";

        public bool IsPlaying { get; private set; }

        public ActionPlaybackPlan CurrentPlan { get; private set; } = ActionPlaybackPlan.Empty;

        public string CurrentStepName => currentStepName;

        public ActionPlaybackSettings Settings => settings;

        public void ConfigureDebugPlayback(bool playbackEnabled, float playbackSpeedMultiplier)
        {
            settings.SetPlaybackEnabled(playbackEnabled);
            settings.SetPlaybackSpeedMultiplier(playbackSpeedMultiplier);
            ApplyPlaybackSettingsToPresenters();

            if (!playbackEnabled && IsPlaying)
            {
                CancelPlayback();
            }
        }

        public bool TryPlayAction(GameState previousState, ActionInput input, ActionResult result, Action<ActionResult> finalSync)
        {
            if (previousState is null)
            {
                throw new ArgumentNullException(nameof(previousState));
            }

            if (result is null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (finalSync is null)
            {
                throw new ArgumentNullException(nameof(finalSync));
            }

            ApplyPlaybackSettingsToPresenters();
            CurrentPlan = ActionPlaybackBuilder.Build(previousState, input, result);
            if (!CanPlay())
            {
                currentStepName = "Playback disabled";
                return false;
            }

            CancelPlayback();

            int sessionId = ++playbackSessionId;
            activeContext = new PlaybackContext(sessionId, result, finalSync);

            if (settings.YieldBetweenSteps)
            {
                activePlayback = StartCoroutine(RunPlayback(sessionId, previousState, input, result, finalSync));
            }
            else
            {
                RunPlaybackImmediately(sessionId, previousState, input, result, finalSync);
            }

            return true;
        }

        public void CancelPlayback()
        {
            PlaybackContext? context = activeContext;
            activeContext = null;
            playbackSessionId++;

            if (activePlayback is not null)
            {
                StopCoroutine(activePlayback);
                activePlayback = null;
            }

            IsPlaying = false;
            currentStepName = "Idle";

            if (context is not null)
            {
                context.FinalSync(context.Result);
            }
        }

        private bool CanPlay()
        {
            return isActiveAndEnabled && settings.PlaybackEnabled;
        }

        private IEnumerator RunPlayback(int sessionId, GameState previousState, ActionInput input, ActionResult result, Action<ActionResult> finalSync)
        {
            IsPlaying = true;
            currentStepName = "Starting";

            try
            {
                for (int i = 0; i < CurrentPlan.Count; i++)
                {
                    if (CurrentPlan[i].StepType == ActionPlaybackStepType.FinalSync)
                    {
                        continue;
                    }

                    ActionPlaybackStep step = CurrentPlan[i];
                    currentStepName = GetStepDebugLabel(step);
                    PlayStep(step, previousState, input, result.State);
                    yield return CreateStepYield(step);
                }
            }
            finally
            {
                CompletePlayback(sessionId, result, finalSync);
            }
        }

        private void RunPlaybackImmediately(int sessionId, GameState previousState, ActionInput input, ActionResult result, Action<ActionResult> finalSync)
        {
            IsPlaying = true;
            currentStepName = "Starting";

            try
            {
                for (int i = 0; i < CurrentPlan.Count; i++)
                {
                    if (CurrentPlan[i].StepType == ActionPlaybackStepType.FinalSync)
                    {
                        continue;
                    }

                    ActionPlaybackStep step = CurrentPlan[i];
                    currentStepName = GetStepDebugLabel(step);
                    PlayStep(step, previousState, input, result.State);
                }
            }
            finally
            {
                CompletePlayback(sessionId, result, finalSync);
            }
        }

        private void PlayStep(ActionPlaybackStep step, GameState previousState, ActionInput input, GameState resultState)
        {
            if (step.SourceEvent is null)
            {
                return;
            }

            switch (step.SourceEvent)
            {
                case GroupRemoved removed:
                    boardContent?.RemoveDebrisGroup(removed);
                    break;
                case BlockerDamaged damaged:
                    boardContent?.AnimateBlockerDamage(damaged);
                    break;
                case BlockerBroken broken:
                    boardContent?.AnimateBlockerBreak(broken);
                    break;
                case IceRevealed revealed:
                    boardContent?.AnimateIceReveal(revealed);
                    break;
                case DockInserted inserted:
                    ResolveDockView()?.PlayInsertFeedback(inserted);
                    break;
                case DockCleared cleared:
                    ResolveDockView()?.PlayClearFeedback(cleared);
                    break;
                case DockWarningChanged warningChanged:
                    ResolveDockView()?.PlayWarningFeedback(warningChanged);
                    break;
                case DockJamTriggered jamTriggered:
                    ResolveDockView()?.PlayJamFeedback(jamTriggered);
                    break;
                case GravitySettled gravity:
                    boardContent?.AnimateGravityMove(gravity);
                    break;
                case Spawned spawned:
                    boardContent?.AnimateSpawn(spawned);
                    break;
                case TargetExtracted extracted:
                    boardContent?.AnimateTargetExtract(extracted);
                    break;
                case WaterRose rose:
                    ResolveWaterView()?.AnimateWaterRise(
                        previousState,
                        resultState,
                        rose.FloodedRow);
                    break;
            }

            TryRoutePlaybackFx(step, previousState, input, resultState);
        }

        private object? CreateStepYield(ActionPlaybackStep step)
        {
            float duration = GetStepDurationSeconds(step);
            if (duration <= 0f)
            {
                return null;
            }

            if (Application.isPlaying)
            {
                return new WaitForSeconds(duration);
            }

            return null;
        }

        private float GetStepDurationSeconds(ActionPlaybackStep step)
        {
            if (step.SourceEvent is not null)
            {
                switch (step.SourceEvent)
                {
                    case Won:
                        return settings.WinFxDurationSeconds;
                    case DockInserted:
                        return settings.DockInsertFeedbackDurationSeconds;
                    case DockCleared:
                        return settings.DockClearFeedbackDurationSeconds;
                    case DockWarningChanged warningChanged:
                        return warningChanged.After switch
                        {
                            DockWarningLevel.Caution => settings.DockWarningCautionDurationSeconds,
                            DockWarningLevel.Acute => settings.DockWarningAcuteDurationSeconds,
                            DockWarningLevel.Fail => settings.DockJamFeedbackDurationSeconds,
                            _ => settings.DockFeedbackDurationSeconds,
                        };
                    case DockJamTriggered:
                        return settings.DockJamFeedbackDurationSeconds;
                }
            }

            switch (step.StepType)
            {
                case ActionPlaybackStepType.RemoveGroup:
                    return settings.RemoveDurationSeconds;
                case ActionPlaybackStepType.BreakBlockerOrReveal:
                    return settings.BreakBlockerOrRevealDurationSeconds;
                case ActionPlaybackStepType.DockFeedback:
                    return settings.DockFeedbackDurationSeconds;
                case ActionPlaybackStepType.Gravity:
                    return settings.GravityDurationSeconds;
                case ActionPlaybackStepType.Spawn:
                    return settings.SpawnDurationSeconds;
                case ActionPlaybackStepType.TargetExtract:
                    return settings.TargetExtractDurationSeconds;
                case ActionPlaybackStepType.WaterRise:
                    return Mathf.Max(settings.WaterRiseDurationSeconds, settings.WaterForecastTransitionDurationSeconds);
                default:
                    return 0f;
            }
        }

        private void CompletePlayback(int sessionId, ActionResult result, Action<ActionResult> finalSync)
        {
            if (activeContext is null || activeContext.SessionId != sessionId)
            {
                activePlayback = null;
                IsPlaying = false;
                return;
            }

            try
            {
                currentStepName = ActionPlaybackStepType.FinalSync.ToString();
                finalSync(result);
            }
            finally
            {
                activeContext = null;
                activePlayback = null;
                IsPlaying = false;
                currentStepName = "Idle";
            }
        }

        private static string GetStepDebugLabel(ActionPlaybackStep step)
        {
            string? sourceEventName = step.SourceEventName;
            return string.IsNullOrWhiteSpace(sourceEventName)
                ? step.StepType.ToString()
                : sourceEventName;
        }

        private WaterViewPresenter? ResolveWaterView()
        {
            if (waterView is not null)
            {
                return waterView;
            }

            waterView = GetComponent<WaterViewPresenter>();
            return waterView;
        }

        private DockViewPresenter? ResolveDockView()
        {
            if (dockView is not null)
            {
                return dockView;
            }

            dockView = GetComponent<DockViewPresenter>();
            return dockView;
        }

        private BoardGridViewPresenter? ResolveBoardGrid()
        {
            if (boardGrid is not null)
            {
                return boardGrid;
            }

            boardGrid = GetComponent<BoardGridViewPresenter>();
            return boardGrid;
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

        private void TryRoutePlaybackFx(ActionPlaybackStep step, GameState previousState, ActionInput input, GameState resultState)
        {
            FxEventRouter? router = ResolveFxEventRouter();
            if (router is null)
            {
                return;
            }

            router.BoardGrid ??= ResolveBoardGrid();

            try
            {
                router.RoutePlaybackBeat(previousState, input, resultState, step);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"{nameof(ActionPlaybackController)} skipped FX for playback step '{step.SourceEventName ?? step.StepType.ToString()}' after an exception: {exception.Message}",
                    this);
            }
        }

        private void ApplyPlaybackSettingsToPresenters()
        {
            boardContent?.ApplyPlaybackSettings(settings);
            ResolveWaterView()?.ApplyPlaybackSettings(settings);
            ResolveDockView()?.ApplyPlaybackSettings(settings);
        }

        private sealed class PlaybackContext
        {
            public PlaybackContext(int sessionId, ActionResult result, Action<ActionResult> finalSync)
            {
                SessionId = sessionId;
                Result = result;
                FinalSync = finalSync;
            }

            public int SessionId { get; }

            public ActionResult Result { get; }

            public Action<ActionResult> FinalSync { get; }
        }
    }
}
