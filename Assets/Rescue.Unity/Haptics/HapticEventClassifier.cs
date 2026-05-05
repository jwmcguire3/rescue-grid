using Rescue.Core.Pipeline;
using Rescue.Core.State;

namespace Rescue.Unity.Haptics
{
    public static class HapticEventClassifier
    {
        public static bool TryClassify(ActionResult result, out HapticFeedbackSignal signal)
        {
            if (result is null)
            {
                signal = default;
                return false;
            }

            bool hasSignal = false;
            HapticFeedbackSignal bestSignal = default;
            for (int i = 0; i < result.Events.Length; i++)
            {
                if (!TryClassify(result.Events[i], out HapticFeedbackSignal candidate))
                {
                    continue;
                }

                if (!hasSignal || candidate.Priority > bestSignal.Priority)
                {
                    bestSignal = candidate;
                    hasSignal = true;
                }
            }

            if (TryClassify(result.Outcome, out HapticFeedbackSignal outcomeSignal) &&
                (!hasSignal || outcomeSignal.Priority > bestSignal.Priority))
            {
                bestSignal = outcomeSignal;
                hasSignal = true;
            }

            signal = bestSignal;
            return hasSignal;
        }

        public static bool TryClassify(ActionEvent actionEvent, out HapticFeedbackSignal signal)
        {
            switch (actionEvent)
            {
                case InvalidInput:
                    signal = Create(HapticEventId.InvalidTap, 0.15f, 10, actionEvent);
                    return true;
                case GroupRemoved:
                    signal = Create(HapticEventId.GroupClear, 0.20f, 20, actionEvent);
                    return true;
                case BlockerBroken:
                case IceRevealed:
                    signal = Create(HapticEventId.BlockerBreak, 0.30f, 30, actionEvent);
                    return true;
                case DockWarningChanged warningChanged when warningChanged.After == DockWarningLevel.Caution:
                    signal = Create(HapticEventId.DockCaution, 0.25f, 40, actionEvent);
                    return true;
                case DockWarningChanged warningChanged when warningChanged.After == DockWarningLevel.Acute:
                    signal = Create(HapticEventId.DockAcute, 0.65f, 70, actionEvent);
                    return true;
                case DockWarningChanged warningChanged when warningChanged.After == DockWarningLevel.Fail:
                case DockOverflowTriggered:
                    signal = Create(HapticEventId.DockOverflow, 0.90f, 100, actionEvent);
                    return true;
                case DockJamTriggered:
                    signal = Create(HapticEventId.DockJam, 0.70f, 80, actionEvent);
                    return true;
                case TargetProgressed:
                case TargetOneClearAway:
                    signal = Create(HapticEventId.TargetNearRescue, 0.25f, 35, actionEvent);
                    return true;
                case TargetExtracted:
                    signal = Create(HapticEventId.TargetExtract, 0.40f, 50, actionEvent);
                    return true;
                case WaterWarning:
                    signal = Create(HapticEventId.WaterWarning, 0.35f, 45, actionEvent);
                    return true;
                case WaterRose:
                    signal = Create(HapticEventId.WaterRise, 0.50f, 60, actionEvent);
                    return true;
                case VinePreviewChanged previewChanged when previewChanged.PendingTile.HasValue:
                    signal = Create(HapticEventId.VinePreview, 0.25f, 35, actionEvent);
                    return true;
                case VineGrown:
                    signal = Create(HapticEventId.VineGrow, 0.45f, 55, actionEvent);
                    return true;
                case Lost lost when IsWaterLoss(lost.Outcome):
                    signal = Create(HapticEventId.WaterLoss, 0.90f, 100, actionEvent);
                    return true;
                case Lost lost when lost.Outcome == ActionOutcome.LossDockOverflow:
                    signal = Create(HapticEventId.DockOverflow, 0.90f, 100, actionEvent);
                    return true;
                case Won:
                    signal = Create(HapticEventId.Win, 0.55f, 65, actionEvent);
                    return true;
                default:
                    signal = default;
                    return false;
            }
        }

        public static bool TryClassify(ActionOutcome outcome, out HapticFeedbackSignal signal)
        {
            switch (outcome)
            {
                case ActionOutcome.Win:
                    signal = Create(HapticEventId.Win, 0.55f, 65, nameof(ActionOutcome.Win));
                    return true;
                case ActionOutcome.LossDockOverflow:
                    signal = Create(HapticEventId.DockOverflow, 0.90f, 100, nameof(ActionOutcome.LossDockOverflow));
                    return true;
                case ActionOutcome.LossWaterOnTarget:
                case ActionOutcome.LossRescuePathFlooded:
                case ActionOutcome.LossDistressedExpired:
                    signal = Create(HapticEventId.WaterLoss, 0.90f, 100, outcome.ToString());
                    return true;
                default:
                    signal = default;
                    return false;
            }
        }

        public static HapticFeedbackSignal CreateManual(HapticEventId id)
        {
            return id switch
            {
                HapticEventId.UndoUsed => Create(id, 0.20f, 20, nameof(HapticEventId.UndoUsed)),
                HapticEventId.RetryConfirmed => Create(id, 0.25f, 25, nameof(HapticEventId.RetryConfirmed)),
                _ => Create(id, 0f, 0, id.ToString()),
            };
        }

        private static HapticFeedbackSignal Create(
            HapticEventId id,
            float intensity,
            int priority,
            ActionEvent actionEvent)
        {
            return Create(id, intensity, priority, actionEvent.GetType().Name);
        }

        private static HapticFeedbackSignal Create(
            HapticEventId id,
            float intensity,
            int priority,
            string debugLabel)
        {
            return new HapticFeedbackSignal(id, UnityEngine.Mathf.Clamp01(intensity), priority, debugLabel);
        }

        private static bool IsWaterLoss(ActionOutcome outcome)
        {
            return outcome == ActionOutcome.LossWaterOnTarget
                || outcome == ActionOutcome.LossRescuePathFlooded
                || outcome == ActionOutcome.LossDistressedExpired;
        }
    }
}
