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

            List<ActionPlaybackStep> mappedSteps = new List<ActionPlaybackStep>(result.Events.Length);
            foreach (ActionEvent actionEvent in result.Events)
            {
                if (actionEvent is Won or Lost)
                {
                    mappedSteps.Add(CreateStep(ActionPlaybackStepType.TerminalOutcome, actionEvent));
                    break;
                }

                MapSteps(actionEvent, mappedSteps);
            }

            mappedSteps.Add(new ActionPlaybackStep(ActionPlaybackStepType.FinalSync, SourceEventName: null, SourceEvent: null));
            return new ActionPlaybackPlan(ImmutableArray.CreateRange(mappedSteps));
        }

        private static void MapSteps(ActionEvent actionEvent, List<ActionPlaybackStep> mappedSteps)
        {
            switch (actionEvent)
            {
                case GroupRemoved:
                    mappedSteps.Add(CreateStep(ActionPlaybackStepType.RemoveGroup, actionEvent));
                    return;

                case BlockerDamaged:
                case BlockerBroken:
                case IceRevealed:
                    mappedSteps.Add(CreateStep(ActionPlaybackStepType.BreakBlockerOrReveal, actionEvent));
                    return;

                case TargetProgressed:
                case TargetOneClearAway:
                    mappedSteps.Add(CreateStep(ActionPlaybackStepType.TargetReaction, actionEvent));
                    return;

                case TargetExtractionLatched:
                    mappedSteps.Add(CreateStep(ActionPlaybackStepType.TargetLatch, actionEvent));
                    return;

                case DockInserted:
                case DockCleared:
                case DockWarningChanged:
                case DockJamTriggered:
                    mappedSteps.Add(CreateStep(ActionPlaybackStepType.DockFeedback, actionEvent));
                    return;

                case DockOverflowTriggered:
                    mappedSteps.Add(CreateStep(ActionPlaybackStepType.DockOverflow, actionEvent));
                    return;

                case GravitySettled:
                    mappedSteps.Add(CreateStep(ActionPlaybackStepType.Gravity, actionEvent));
                    return;

                case Spawned:
                    mappedSteps.Add(CreateStep(ActionPlaybackStepType.Spawn, actionEvent));
                    return;

                case TargetExtracted:
                    mappedSteps.Add(CreateStep(ActionPlaybackStepType.TargetExtract, actionEvent));
                    return;

                case WaterWarning:
                    mappedSteps.Add(CreateStep(ActionPlaybackStepType.WaterWarning, actionEvent));
                    return;

                case WaterRose:
                    mappedSteps.Add(CreateStep(ActionPlaybackStepType.WaterRise, actionEvent));
                    return;

                case VinePreviewChanged:
                    mappedSteps.Add(CreateStep(ActionPlaybackStepType.VinePreview, actionEvent));
                    return;

                case VineGrown:
                    mappedSteps.Add(CreateStep(ActionPlaybackStepType.VineGrowth, actionEvent));
                    return;

                default:
                    // Playback V2 intentionally ignores unsupported core events for now.
                    // FinalSync still applies the authoritative result state at the end.
                    return;
            }
        }

        private static ActionPlaybackStep CreateStep(ActionPlaybackStepType stepType, ActionEvent actionEvent)
        {
            return new ActionPlaybackStep(stepType, actionEvent.GetType().Name, actionEvent);
        }
    }
}
