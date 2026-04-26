using System;
using System.Collections;
using Rescue.Core.Pipeline;
using Rescue.Core.State;
using Rescue.Unity.BoardPresentation;
using Rescue.Unity.UI;
using UnityEngine;

namespace Rescue.Unity.Presentation
{
    public sealed class ActionPlaybackController : MonoBehaviour
    {
        [SerializeField] private ActionPlaybackSettings settings = new ActionPlaybackSettings();
        [SerializeField] private BoardContentViewPresenter? boardContent;
        [SerializeField] private WaterViewPresenter? waterView;
        [SerializeField] private DockViewPresenter? dockView;

        private Coroutine? activePlayback;

        public bool IsPlaying { get; private set; }

        public ActionPlaybackPlan CurrentPlan { get; private set; } = ActionPlaybackPlan.Empty;

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

            CurrentPlan = ActionPlaybackBuilder.Build(previousState, input, result);
            if (!CanPlay())
            {
                return false;
            }

            CancelPlayback();

            if (settings.YieldBetweenSteps)
            {
                activePlayback = StartCoroutine(RunPlayback(previousState, result, finalSync));
            }
            else
            {
                RunPlaybackImmediately(previousState, result, finalSync);
            }

            return true;
        }

        public void CancelPlayback()
        {
            if (activePlayback is not null)
            {
                StopCoroutine(activePlayback);
                activePlayback = null;
            }

            IsPlaying = false;
        }

        private bool CanPlay()
        {
            return isActiveAndEnabled && settings.PlaybackEnabled;
        }

        private IEnumerator RunPlayback(GameState previousState, ActionResult result, Action<ActionResult> finalSync)
        {
            IsPlaying = true;

            try
            {
                for (int i = 0; i < CurrentPlan.Count; i++)
                {
                    if (CurrentPlan[i].StepType == ActionPlaybackStepType.FinalSync)
                    {
                        continue;
                    }

                    ActionPlaybackStep step = CurrentPlan[i];
                    PlayStep(step, previousState, result.State);
                    yield return CreateStepYield(step.StepType);
                }
            }
            finally
            {
                CompletePlayback(result, finalSync);
            }
        }

        private void RunPlaybackImmediately(GameState previousState, ActionResult result, Action<ActionResult> finalSync)
        {
            IsPlaying = true;

            try
            {
                for (int i = 0; i < CurrentPlan.Count; i++)
                {
                    if (CurrentPlan[i].StepType == ActionPlaybackStepType.FinalSync)
                    {
                        continue;
                    }

                    PlayStep(CurrentPlan[i], previousState, result.State);
                }
            }
            finally
            {
                CompletePlayback(result, finalSync);
            }
        }

        private void PlayStep(ActionPlaybackStep step, GameState previousState, GameState resultState)
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
                    boardContent?.AnimateBlockerDamage(damaged, settings.BreakBlockerOrRevealDurationSeconds);
                    break;
                case BlockerBroken broken:
                    boardContent?.AnimateBlockerBreak(broken, settings.BreakBlockerOrRevealDurationSeconds);
                    break;
                case IceRevealed revealed:
                    boardContent?.AnimateIceReveal(revealed, settings.BreakBlockerOrRevealDurationSeconds);
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
                    boardContent?.AnimateTargetExtract(extracted, settings.TargetExtractDurationSeconds);
                    break;
                case WaterRose rose:
                    ResolveWaterView()?.AnimateWaterRise(previousState, resultState, rose.FloodedRow, settings.WaterRiseDurationSeconds);
                    break;
            }
        }

        private object? CreateStepYield(ActionPlaybackStepType stepType)
        {
            float duration = GetStepDurationSeconds(stepType);
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

        private float GetStepDurationSeconds(ActionPlaybackStepType stepType)
        {
            switch (stepType)
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
                    return settings.WaterRiseDurationSeconds;
                default:
                    return 0f;
            }
        }

        private void CompletePlayback(ActionResult result, Action<ActionResult> finalSync)
        {
            try
            {
                finalSync(result);
            }
            finally
            {
                activePlayback = null;
                IsPlaying = false;
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
    }
}
