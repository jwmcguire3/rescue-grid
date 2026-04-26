using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Rescue.Core.Pipeline;
using Rescue.Core.State;

namespace Rescue.Unity.Presentation
{
    public static class ActionPlaybackBuilder
    {
        public static ActionPlaybackPlan Build(GameState previousState, ActionInput input, ActionResult result)
        {
            if (previousState is null)
            {
                throw new ArgumentNullException(nameof(previousState));
            }

            if (result is null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            _ = previousState;
            _ = input;

            List<ActionPlaybackStep> mappedSteps = new List<ActionPlaybackStep>(result.Events.Length);
            foreach (ActionEvent actionEvent in result.Events)
            {
                if (TryMapStepType(actionEvent, out ActionPlaybackStepType stepType))
                {
                    mappedSteps.Add(new ActionPlaybackStep(stepType, actionEvent.GetType().Name, actionEvent));
                }
            }

            List<ActionPlaybackStep> steps = OrderSteps(mappedSteps);
            steps.Add(new ActionPlaybackStep(ActionPlaybackStepType.FinalSync, SourceEventName: null, SourceEvent: null));
            return new ActionPlaybackPlan(ImmutableArray.CreateRange(steps));
        }

        private static List<ActionPlaybackStep> OrderSteps(List<ActionPlaybackStep> mappedSteps)
        {
            if (mappedSteps.Count <= 1)
            {
                return mappedSteps;
            }

            return mappedSteps
                .Select((step, index) => (step, index))
                .OrderBy(static pair => GetStepSortOrder(pair.step.StepType))
                .ThenBy(static pair => pair.index)
                .Select(static pair => pair.step)
                .ToList();
        }

        private static int GetStepSortOrder(ActionPlaybackStepType stepType)
        {
            switch (stepType)
            {
                case ActionPlaybackStepType.RemoveGroup:
                    return 0;
                case ActionPlaybackStepType.BreakBlockerOrReveal:
                    return 1;
                case ActionPlaybackStepType.DockFeedback:
                    return 2;
                case ActionPlaybackStepType.Gravity:
                    return 3;
                case ActionPlaybackStepType.Spawn:
                    return 4;
                case ActionPlaybackStepType.TargetExtract:
                    return 5;
                case ActionPlaybackStepType.WaterRise:
                    return 6;
                default:
                    return int.MaxValue;
            }
        }

        private static bool TryMapStepType(ActionEvent actionEvent, out ActionPlaybackStepType stepType)
        {
            switch (actionEvent)
            {
                case GroupRemoved:
                    stepType = ActionPlaybackStepType.RemoveGroup;
                    return true;

                case BlockerDamaged:
                case BlockerBroken:
                case IceRevealed:
                    stepType = ActionPlaybackStepType.BreakBlockerOrReveal;
                    return true;

                case DockInserted:
                case DockCleared:
                case DockWarningChanged:
                case DockJamTriggered:
                    stepType = ActionPlaybackStepType.DockFeedback;
                    return true;

                case GravitySettled:
                    stepType = ActionPlaybackStepType.Gravity;
                    return true;

                case Spawned:
                    stepType = ActionPlaybackStepType.Spawn;
                    return true;

                case TargetExtracted:
                    stepType = ActionPlaybackStepType.TargetExtract;
                    return true;

                case WaterRose:
                    stepType = ActionPlaybackStepType.WaterRise;
                    return true;

                default:
                    stepType = default;
                    return false;
            }
        }
    }
}
