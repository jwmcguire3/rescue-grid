using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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

            List<ActionPlaybackStep> steps = new List<ActionPlaybackStep>(result.Events.Length + 1);
            foreach (ActionEvent actionEvent in result.Events)
            {
                if (TryMapStepType(actionEvent, out ActionPlaybackStepType stepType))
                {
                    steps.Add(new ActionPlaybackStep(stepType, actionEvent.GetType().Name, actionEvent));
                }
            }

            steps.Add(new ActionPlaybackStep(ActionPlaybackStepType.FinalSync, SourceEventName: null, SourceEvent: null));
            return new ActionPlaybackPlan(ImmutableArray.CreateRange(steps));
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
