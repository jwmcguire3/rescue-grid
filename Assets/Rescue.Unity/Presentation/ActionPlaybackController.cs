using System;
using System.Collections;
using System.Collections.Immutable;
using Rescue.Core.Pipeline;
using Rescue.Core.State;
using Rescue.Unity.BoardPresentation;
using Rescue.Unity.Feedback;
using Rescue.Unity.FX;
using Rescue.Unity.Haptics;
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
        [SerializeField] private AudioEventRouter? audioEventRouter;
        [SerializeField] private HapticEventRouter? hapticEventRouter;

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
            ConfigureDebugPlayback(
                playbackEnabled,
                playbackSpeedMultiplier,
                settings.BoardActionSpeedMultiplier,
                settings.DockSpeedMultiplier,
                settings.TargetSpeedMultiplier,
                settings.HazardSpeedMultiplier,
                settings.TerminalSpeedMultiplier,
                settings.GravitySpawnSpeedMultiplier);
        }

        public void ConfigureDebugPlayback(
            bool playbackEnabled,
            float playbackSpeedMultiplier,
            float boardActionSpeedMultiplier,
            float dockSpeedMultiplier,
            float targetSpeedMultiplier,
            float hazardSpeedMultiplier,
            float terminalSpeedMultiplier,
            float gravitySpawnSpeedMultiplier)
        {
            settings.SetPlaybackEnabled(playbackEnabled);
            settings.SetPlaybackSpeedMultiplier(playbackSpeedMultiplier);
            settings.SetBoardActionSpeedMultiplier(boardActionSpeedMultiplier);
            settings.SetDockSpeedMultiplier(dockSpeedMultiplier);
            settings.SetTargetSpeedMultiplier(targetSpeedMultiplier);
            settings.SetHazardSpeedMultiplier(hazardSpeedMultiplier);
            settings.SetTerminalSpeedMultiplier(terminalSpeedMultiplier);
            settings.SetGravitySpawnSpeedMultiplier(gravitySpawnSpeedMultiplier);
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
            ResolveHapticEventRouter()?.BeginActionRoute(result);

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
                ResolveHapticEventRouter()?.EndActionRoute();
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
                    currentStepName = ActionPlaybackRouting.GetDebugLabel(step);
                    PlayStep(i, step, previousState, input, result.State);
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
                    currentStepName = ActionPlaybackRouting.GetDebugLabel(step);
                    PlayStep(i, step, previousState, input, result.State);
                }
            }
            finally
            {
                CompletePlayback(sessionId, result, finalSync);
            }
        }

        private void PlayStep(int stepIndex, ActionPlaybackStep step, GameState previousState, ActionInput input, GameState resultState)
        {
            ImmutableArray<ActionEvent> sourceEvents = step.Events;
            if (sourceEvents.IsDefaultOrEmpty)
            {
                return;
            }

            if (step.StepType == ActionPlaybackStepType.DockInsertionTravel)
            {
                PlayDockInsertionTravel(stepIndex, sourceEvents, input);
            }
            else if (step.StepType == ActionPlaybackStepType.BreakBlockerOrReveal && sourceEvents.Length > 1)
            {
                PlayBlockerBatch(sourceEvents);
            }
            else
            {
                for (int i = 0; i < sourceEvents.Length; i++)
                {
                    PlaySingleEvent(sourceEvents[i], previousState, resultState);
                }
            }

            for (int i = 0; i < sourceEvents.Length; i++)
            {
                ActionEvent sourceEvent = sourceEvents[i];
                ActionPlaybackStep routedStep = ActionPlaybackRouting.CreateRoutedStep(step, sourceEvent);
                TryRoutePlaybackFx(routedStep, previousState, input, resultState);
            }

            TryRoutePlaybackAudio(step, previousState, input, resultState);
            TryRoutePlaybackHaptics(step, previousState, input, resultState);
        }

        private void PlaySingleEvent(ActionEvent sourceEvent, GameState previousState, GameState resultState)
        {
            switch (sourceEvent)
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
                case DockOverflowTriggered overflowTriggered:
                    ResolveDockView()?.PlayOverflowFeedback(overflowTriggered);
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
                case TargetRescuePathLocked locked:
                    boardContent?.AnimateRescuePathLocked(locked);
                    break;
                case WaterWarning:
                    ResolveWaterView()?.AnimateForecastTransition(
                        previousState,
                        resultState);
                    break;
                case WaterRose rose:
                    ResolveWaterView()?.AnimateWaterRise(
                        previousState,
                        resultState,
                        rose.FloodedRow);
                    break;
                case VinePreviewChanged previewChanged:
                    boardContent?.AnimateVinePreview(previewChanged, settings.VinePreviewDurationSeconds);
                    break;
                case VineGrown grown:
                    boardContent?.AnimateVineGrowth(grown, settings.VineGrowthDurationSeconds);
                    break;
            }
        }

        private void PlayDockInsertionTravel(int stepIndex, ImmutableArray<ActionEvent> sourceEvents, ActionInput input)
        {
            if (!TryResolveDockInsertionSource(stepIndex, input, out Vector3 sourceWorldPosition))
            {
                for (int i = 0; i < sourceEvents.Length; i++)
                {
                    if (sourceEvents[i] is DockInserted inserted)
                    {
                        ResolveDockView()?.PlayInsertFeedback(inserted);
                    }
                }

                return;
            }

            ResolveDockView()?.PlayInsertionTravelFeedback(
                sourceEvents,
                sourceWorldPosition,
                settings.DockInsertionTravelDurationSeconds);
        }

        private void PlayBlockerBatch(ImmutableArray<ActionEvent> sourceEvents)
        {
            ImmutableArray<BlockerBroken>.Builder brokenBlockers = ImmutableArray.CreateBuilder<BlockerBroken>();
            ImmutableArray<IceRevealed>.Builder iceReveals = ImmutableArray.CreateBuilder<IceRevealed>();
            for (int i = 0; i < sourceEvents.Length; i++)
            {
                switch (sourceEvents[i])
                {
                    case BlockerDamaged damaged:
                        boardContent?.AnimateBlockerDamage(damaged);
                        break;
                    case BlockerBroken broken:
                        brokenBlockers.Add(broken);
                        break;
                    case IceRevealed revealed:
                        iceReveals.Add(revealed);
                        break;
                }
            }

            if (brokenBlockers.Count > 0)
            {
                boardContent?.AnimateBlockerBreakCascade(
                    brokenBlockers.ToImmutable(),
                    settings.BreakBlockerOrRevealDurationSeconds,
                    settings.BlockerBreakCascadeStaggerSeconds);
            }

            for (int i = 0; i < iceReveals.Count; i++)
            {
                boardContent?.AnimateIceReveal(iceReveals[i]);
            }
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
                    case Lost:
                        return settings.LossFxDurationSeconds;
                    case DockInserted:
                        return step.StepType == ActionPlaybackStepType.DockInsertionTravel
                            ? settings.DockInsertionTravelDurationSeconds
                            : settings.DockInsertFeedbackDurationSeconds;
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
                    return settings.BreakBlockerOrRevealDurationSeconds + GetBlockerCascadeExtraDurationSeconds(step);
                case ActionPlaybackStepType.TargetReaction:
                case ActionPlaybackStepType.TargetLatch:
                    return settings.TargetReactionDurationSeconds;
                case ActionPlaybackStepType.DockInsertionTravel:
                    return settings.DockInsertionTravelDurationSeconds;
                case ActionPlaybackStepType.DockFeedback:
                    return settings.DockFeedbackDurationSeconds;
                case ActionPlaybackStepType.DockOverflow:
                    return settings.DockJamFeedbackDurationSeconds;
                case ActionPlaybackStepType.Gravity:
                    return settings.GravityDurationSeconds;
                case ActionPlaybackStepType.Spawn:
                    return settings.SpawnDurationSeconds;
                case ActionPlaybackStepType.TargetExtract:
                    return settings.TargetExtractDurationSeconds;
                case ActionPlaybackStepType.WaterWarning:
                    return settings.WaterForecastPulseDurationSeconds;
                case ActionPlaybackStepType.WaterRise:
                    return Mathf.Max(settings.WaterRiseDurationSeconds, settings.WaterForecastTransitionDurationSeconds);
                case ActionPlaybackStepType.VinePreview:
                    return settings.VinePreviewDurationSeconds;
                case ActionPlaybackStepType.VineGrowth:
                    return settings.VineGrowthDurationSeconds;
                default:
                    return 0f;
            }
        }

        private float GetBlockerCascadeExtraDurationSeconds(ActionPlaybackStep step)
        {
            ImmutableArray<ActionEvent> events = step.Events;
            if (events.Length <= 1)
            {
                return 0f;
            }

            int brokenCount = ActionPlaybackRouting.CountBlockerBreaks(events);

            return brokenCount > 1
                ? (brokenCount - 1) * settings.BlockerBreakCascadeStaggerSeconds
                : 0f;
        }

        private bool TryResolveDockInsertionSource(int stepIndex, ActionInput input, out Vector3 sourceWorldPosition)
        {
            for (int i = stepIndex - 1; i >= 0; i--)
            {
                ImmutableArray<ActionEvent> events = CurrentPlan[i].Events;
                for (int eventIndex = events.Length - 1; eventIndex >= 0; eventIndex--)
                {
                    if (events[eventIndex] is GroupRemoved removed &&
                        TryResolveGroupWorldPosition(removed.Coords, out sourceWorldPosition))
                    {
                        return true;
                    }
                }
            }

            return TryResolveCellWorldPosition(input.TappedCoord, out sourceWorldPosition);
        }

        private bool TryResolveGroupWorldPosition(ImmutableArray<TileCoord> coords, out Vector3 worldPosition)
        {
            if (coords.IsDefaultOrEmpty)
            {
                worldPosition = Vector3.zero;
                return false;
            }

            Vector3 accumulated = Vector3.zero;
            int resolvedCount = 0;
            for (int i = 0; i < coords.Length; i++)
            {
                if (!TryResolveCellWorldPosition(coords[i], out Vector3 cellWorldPosition))
                {
                    continue;
                }

                accumulated += cellWorldPosition;
                resolvedCount++;
            }

            if (resolvedCount <= 0)
            {
                worldPosition = Vector3.zero;
                return false;
            }

            worldPosition = accumulated / resolvedCount;
            return true;
        }

        private bool TryResolveCellWorldPosition(TileCoord coord, out Vector3 worldPosition)
        {
            BoardGridViewPresenter? resolvedBoardGrid = ResolveBoardGrid();
            if (resolvedBoardGrid is not null &&
                resolvedBoardGrid.TryGetCellWorldPosition(coord, out worldPosition))
            {
                return true;
            }

            worldPosition = Vector3.zero;
            return false;
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
                ResolveHapticEventRouter()?.EndActionRoute();
                activeContext = null;
                activePlayback = null;
                IsPlaying = false;
                currentStepName = "Idle";
            }
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

        private AudioEventRouter? ResolveAudioEventRouter()
        {
            if (audioEventRouter is not null)
            {
                return audioEventRouter;
            }

            audioEventRouter = GetComponent<AudioEventRouter>();
            return audioEventRouter;
        }

        private HapticEventRouter? ResolveHapticEventRouter()
        {
            if (hapticEventRouter is not null)
            {
                return hapticEventRouter;
            }

            hapticEventRouter = GetComponent<HapticEventRouter>();
            if (hapticEventRouter is null)
            {
                hapticEventRouter = gameObject.AddComponent<HapticEventRouter>();
            }

            return hapticEventRouter;
        }

        private void TryRoutePlaybackFx(ActionPlaybackStep step, GameState previousState, ActionInput input, GameState resultState)
        {
            FxEventRouter? router = ResolveFxEventRouter();
            if (router is null)
            {
                return;
            }

            router.BoardGrid ??= ResolveBoardGrid();
            router.DockView ??= ResolveDockView();

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

        private void TryRoutePlaybackAudio(ActionPlaybackStep step, GameState previousState, ActionInput input, GameState resultState)
        {
            ImmutableArray<ActionEvent> sourceEvents = step.Events;
            if (sourceEvents.IsDefaultOrEmpty)
            {
                return;
            }

            AudioEventRouter? router = ResolveAudioEventRouter();
            if (router is null)
            {
                return;
            }

            router.BoardGrid ??= ResolveBoardGrid();

            if (ActionPlaybackRouting.IsMultiBreakBlockerBatch(step, sourceEvents))
            {
                RouteBlockerBatchAudio(router, previousState, input, resultState, sourceEvents);
                return;
            }

            for (int i = 0; i < sourceEvents.Length; i++)
            {
                ActionEvent sourceEvent = sourceEvents[i];
                if (!ActionPlaybackRouting.IsAudioPlaybackBeat(sourceEvent))
                {
                    continue;
                }

                ActionPlaybackStep routedStep = ActionPlaybackRouting.CreateRoutedStep(step, sourceEvent);
                try
                {
                    router.RoutePlaybackBeat(previousState, input, resultState, routedStep);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        $"{nameof(ActionPlaybackController)} skipped audio for playback step '{routedStep.SourceEventName ?? routedStep.StepType.ToString()}' after an exception: {exception.Message}",
                        this);
                }
            }
        }

        private void RouteBlockerBatchAudio(
            AudioEventRouter router,
            GameState previousState,
            ActionInput input,
            GameState resultState,
            ImmutableArray<ActionEvent> sourceEvents)
        {
            int breakIndex = 0;
            for (int i = 0; i < sourceEvents.Length; i++)
            {
                ActionEvent sourceEvent = sourceEvents[i];
                if (!ActionPlaybackRouting.IsAudioPlaybackBeat(sourceEvent))
                {
                    continue;
                }

                ActionPlaybackStep routedStep = ActionPlaybackRouting.CreateRoutedStep(
                    ActionPlaybackStepType.BreakBlockerOrReveal,
                    sourceEvent);

                if (sourceEvent is BlockerBroken)
                {
                    float delaySeconds = breakIndex * settings.BlockerBreakCascadeStaggerSeconds;
                    breakIndex++;
                    if (Application.isPlaying && isActiveAndEnabled && delaySeconds > 0f)
                    {
                        StartCoroutine(RoutePlaybackAudioAfterDelay(router, previousState, input, resultState, routedStep, delaySeconds));
                        continue;
                    }
                }

                TryRoutePlaybackAudioNow(router, previousState, input, resultState, routedStep);
            }
        }

        private IEnumerator RoutePlaybackAudioAfterDelay(
            AudioEventRouter router,
            GameState previousState,
            ActionInput input,
            GameState resultState,
            ActionPlaybackStep routedStep,
            float delaySeconds)
        {
            yield return new WaitForSeconds(delaySeconds);
            TryRoutePlaybackAudioNow(router, previousState, input, resultState, routedStep);
        }

        private void TryRoutePlaybackAudioNow(
            AudioEventRouter router,
            GameState previousState,
            ActionInput input,
            GameState resultState,
            ActionPlaybackStep routedStep)
        {
            try
            {
                router.RoutePlaybackBeat(previousState, input, resultState, routedStep);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"{nameof(ActionPlaybackController)} skipped audio for playback step '{routedStep.SourceEventName ?? routedStep.StepType.ToString()}' after an exception: {exception.Message}",
                    this);
            }
        }

        private void TryRoutePlaybackHaptics(ActionPlaybackStep step, GameState previousState, ActionInput input, GameState resultState)
        {
            HapticEventRouter? router = ResolveHapticEventRouter();
            if (router is null)
            {
                return;
            }

            try
            {
                router.RoutePlaybackBeat(previousState, input, resultState, step);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"{nameof(ActionPlaybackController)} skipped haptics for playback step '{step.SourceEventName ?? step.StepType.ToString()}' after an exception: {exception.Message}",
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
