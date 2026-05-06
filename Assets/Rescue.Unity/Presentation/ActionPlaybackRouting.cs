using System.Collections.Immutable;
using Rescue.Core.Pipeline;

namespace Rescue.Unity.Presentation
{
    internal static class ActionPlaybackRouting
    {
        public static string GetDebugLabel(ActionPlaybackStep step)
        {
            string? sourceEventName = step.SourceEventName;
            return string.IsNullOrWhiteSpace(sourceEventName)
                ? step.StepType.ToString()
                : sourceEventName;
        }

        public static ActionPlaybackStep CreateRoutedStep(ActionPlaybackStep sourceStep, ActionEvent sourceEvent)
        {
            return CreateRoutedStep(sourceStep.StepType, sourceEvent);
        }

        public static ActionPlaybackStep CreateRoutedStep(ActionPlaybackStepType stepType, ActionEvent sourceEvent)
        {
            return new ActionPlaybackStep(stepType, sourceEvent.GetType().Name, sourceEvent);
        }

        public static bool IsMultiBreakBlockerBatch(ActionPlaybackStep step, ImmutableArray<ActionEvent> sourceEvents)
        {
            return step.StepType == ActionPlaybackStepType.BreakBlockerOrReveal
                && CountBlockerBreaks(sourceEvents) > 1;
        }

        public static int CountBlockerBreaks(ImmutableArray<ActionEvent> sourceEvents)
        {
            if (sourceEvents.IsDefaultOrEmpty)
            {
                return 0;
            }

            int brokenCount = 0;
            for (int i = 0; i < sourceEvents.Length; i++)
            {
                if (sourceEvents[i] is BlockerBroken)
                {
                    brokenCount++;
                }
            }

            return brokenCount;
        }

        public static bool IsAudioPlaybackBeat(ActionEvent? actionEvent)
        {
            return actionEvent is GroupRemoved
                or BlockerDamaged
                or BlockerBroken
                or IceRevealed
                or DockInserted
                or DockCleared
                or DockWarningChanged
                or DockOverflowTriggered
                or DockJamTriggered
                or GravitySettled
                or Spawned
                or TargetExtracted
                or WaterRose;
        }
    }
}
