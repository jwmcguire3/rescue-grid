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
            for (int i = 0; i < result.Events.Length; i++)
            {
                ActionEvent actionEvent = result.Events[i];
                if (actionEvent is Won or Lost)
                {
                    mappedSteps.Add(CreateStep(ActionPlaybackStepType.TerminalOutcome, actionEvent));
                    break;
                }

                if (IsBlockerPlaybackEvent(actionEvent))
                {
                    i = MapBlockerSteps(result.Events, i, mappedSteps);
                    continue;
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
                case InvalidInput:
                    mappedSteps.Add(CreateStep(ActionPlaybackStepType.RemoveGroup, actionEvent));
                    return;

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
                case TargetRescuePathLocked:
                case TargetDistressedEntered:
                case TargetDistressedRecovered:
                case TargetDistressedExpired:
                case TargetRescuePathFlooded:
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

        private static int MapBlockerSteps(
            ImmutableArray<ActionEvent> sourceEvents,
            int startIndex,
            List<ActionPlaybackStep> mappedSteps)
        {
            int endIndex = startIndex;
            while (endIndex < sourceEvents.Length && IsBlockerPlaybackEvent(sourceEvents[endIndex]))
            {
                endIndex++;
            }

            ImmutableArray<ActionEvent>.Builder damageEvents = ImmutableArray.CreateBuilder<ActionEvent>();
            ImmutableArray<ActionEvent>.Builder breakEvents = ImmutableArray.CreateBuilder<ActionEvent>();
            HashSet<TileCoord> brokenCoords = new HashSet<TileCoord>();

            for (int i = startIndex; i < endIndex; i++)
            {
                if (sourceEvents[i] is BlockerBroken broken)
                {
                    brokenCoords.Add(broken.Coord);
                }
            }

            for (int i = startIndex; i < endIndex; i++)
            {
                ActionEvent actionEvent = sourceEvents[i];
                switch (actionEvent)
                {
                    case BlockerDamaged damaged:
                        if (damaged.RemainingHp > 0 || !brokenCoords.Contains(damaged.Coord))
                        {
                            damageEvents.Add(damaged);
                        }

                        break;
                    case BlockerBroken:
                    case IceRevealed:
                        breakEvents.Add(actionEvent);
                        break;
                }
            }

            AddBlockerBatch(mappedSteps, "BlockerDamageBatch", damageEvents.ToImmutable());
            AddBlockerBatch(mappedSteps, "BlockerResolutionBatch", breakEvents.ToImmutable());
            return endIndex - 1;
        }

        private static void AddBlockerBatch(
            List<ActionPlaybackStep> mappedSteps,
            string sourceEventName,
            ImmutableArray<ActionEvent> events)
        {
            if (events.IsDefaultOrEmpty)
            {
                return;
            }

            if (events.Length == 1)
            {
                mappedSteps.Add(CreateStep(ActionPlaybackStepType.BreakBlockerOrReveal, events[0]));
                return;
            }

            mappedSteps.Add(new ActionPlaybackStep(
                ActionPlaybackStepType.BreakBlockerOrReveal,
                sourceEventName,
                events[0],
                events));
        }

        private static bool IsBlockerPlaybackEvent(ActionEvent actionEvent)
        {
            return actionEvent is BlockerDamaged or BlockerBroken or IceRevealed;
        }

        private static ActionPlaybackStep CreateStep(ActionPlaybackStepType stepType, ActionEvent actionEvent)
        {
            return new ActionPlaybackStep(stepType, actionEvent.GetType().Name, actionEvent);
        }
    }
}
