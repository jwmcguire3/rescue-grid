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
                    signal = Create(
                        HapticEventId.InvalidTap,
                        new HapticPattern(HapticPatternStyle.Tick, 0.12f, 25),
                        10,
                        HapticCooldownKey.InvalidTap,
                        0.18f,
                        actionEvent);
                    return true;
                case GroupRemoved:
                    signal = default;
                    return false;
                case BlockerBroken:
                case IceRevealed:
                    signal = Create(HapticEventId.BlockerBreak, new HapticPattern(HapticPatternStyle.Pop, 0.30f, 40), 30, actionEvent);
                    return true;
                case DockWarningChanged warningChanged when warningChanged.After == DockWarningLevel.Caution:
                    signal = Create(HapticEventId.DockCaution, new HapticPattern(HapticPatternStyle.Tick, 0.22f, 35), 40, actionEvent);
                    return true;
                case DockWarningChanged warningChanged when warningChanged.After == DockWarningLevel.Acute:
                    signal = Create(HapticEventId.DockAcute, new HapticPattern(HapticPatternStyle.Pulse, 0.65f, 70), 70, actionEvent);
                    return true;
                case DockWarningChanged warningChanged when warningChanged.After == DockWarningLevel.Fail:
                case DockOverflowTriggered:
                    signal = Create(HapticEventId.DockOverflow, new HapticPattern(HapticPatternStyle.Failure, 0.90f, 125), 100, actionEvent);
                    return true;
                case DockJamTriggered:
                    signal = Create(HapticEventId.DockJam, new HapticPattern(HapticPatternStyle.Warning, 0.65f, 70, 0.40f, 85, 45), 80, actionEvent);
                    return true;
                case TargetProgressed:
                case TargetOneClearAway:
                    signal = Create(HapticEventId.TargetNearRescue, new HapticPattern(HapticPatternStyle.Lift, 0.22f, 40), 35, actionEvent);
                    return true;
                case TargetExtracted:
                    signal = Create(HapticEventId.TargetExtract, new HapticPattern(HapticPatternStyle.Pop, 0.38f, 50), 50, actionEvent);
                    return true;
                case WaterWarning:
                    signal = Create(HapticEventId.WaterWarning, new HapticPattern(HapticPatternStyle.Warning, 0.30f, 45), 45, actionEvent);
                    return true;
                case WaterRose:
                    signal = Create(HapticEventId.WaterRise, new HapticPattern(HapticPatternStyle.Pulse, 0.45f, 75), 60, actionEvent);
                    return true;
                case VinePreviewChanged previewChanged when previewChanged.PendingTile.HasValue:
                    signal = Create(HapticEventId.VinePreview, new HapticPattern(HapticPatternStyle.Tick, 0.16f, 30), 35, actionEvent);
                    return true;
                case VineGrown:
                    signal = Create(HapticEventId.VineGrow, new HapticPattern(HapticPatternStyle.Warning, 0.40f, 60), 55, actionEvent);
                    return true;
                case Lost lost when IsWaterLoss(lost.Outcome):
                    signal = Create(HapticEventId.WaterLoss, new HapticPattern(HapticPatternStyle.Failure, 0.90f, 125), 100, actionEvent);
                    return true;
                case Lost lost when lost.Outcome == ActionOutcome.LossDockOverflow:
                    signal = Create(HapticEventId.DockOverflow, new HapticPattern(HapticPatternStyle.Failure, 0.90f, 125), 100, actionEvent);
                    return true;
                case Won:
                    signal = Create(HapticEventId.Win, new HapticPattern(HapticPatternStyle.Success, 0.45f, 55, 0.30f, 95, 40), 75, actionEvent);
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
                    signal = Create(HapticEventId.Win, new HapticPattern(HapticPatternStyle.Success, 0.45f, 55, 0.30f, 95, 40), 75, nameof(ActionOutcome.Win));
                    return true;
                case ActionOutcome.LossDockOverflow:
                    signal = Create(HapticEventId.DockOverflow, new HapticPattern(HapticPatternStyle.Failure, 0.90f, 125), 100, nameof(ActionOutcome.LossDockOverflow));
                    return true;
                case ActionOutcome.LossWaterOnTarget:
                case ActionOutcome.LossRescuePathFlooded:
                case ActionOutcome.LossDistressedExpired:
                    signal = Create(HapticEventId.WaterLoss, new HapticPattern(HapticPatternStyle.Failure, 0.90f, 125), 100, outcome.ToString());
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
                HapticEventId.UndoUsed => Create(
                    id,
                    new HapticPattern(HapticPatternStyle.Tick, 0.20f, 35),
                    20,
                    HapticCooldownKey.ManualCommand,
                    0.20f,
                    nameof(HapticEventId.UndoUsed)),
                HapticEventId.RetryConfirmed => Create(
                    id,
                    new HapticPattern(HapticPatternStyle.Tick, 0.25f, 40),
                    25,
                    HapticCooldownKey.ManualCommand,
                    0.20f,
                    nameof(HapticEventId.RetryConfirmed)),
                _ => Create(id, new HapticPattern(HapticPatternStyle.Tick, 0f, 0), 0, id.ToString()),
            };
        }

        private static HapticFeedbackSignal Create(
            HapticEventId id,
            HapticPattern pattern,
            int priority,
            ActionEvent actionEvent)
        {
            return Create(id, pattern, priority, actionEvent.GetType().Name);
        }

        private static HapticFeedbackSignal Create(
            HapticEventId id,
            HapticPattern pattern,
            int priority,
            string debugLabel)
        {
            return Create(id, pattern, priority, HapticCooldownKey.None, 0f, debugLabel);
        }

        private static HapticFeedbackSignal Create(
            HapticEventId id,
            HapticPattern pattern,
            int priority,
            HapticCooldownKey cooldownKey,
            float cooldownSeconds,
            ActionEvent actionEvent)
        {
            return Create(id, pattern, priority, cooldownKey, cooldownSeconds, actionEvent.GetType().Name);
        }

        private static HapticFeedbackSignal Create(
            HapticEventId id,
            HapticPattern pattern,
            int priority,
            HapticCooldownKey cooldownKey,
            float cooldownSeconds,
            string debugLabel)
        {
            return new HapticFeedbackSignal(
                id,
                pattern.Clamp(1f),
                priority,
                cooldownKey,
                UnityEngine.Mathf.Max(0f, cooldownSeconds),
                debugLabel);
        }

        private static bool IsWaterLoss(ActionOutcome outcome)
        {
            return outcome == ActionOutcome.LossWaterOnTarget
                || outcome == ActionOutcome.LossRescuePathFlooded
                || outcome == ActionOutcome.LossDistressedExpired;
        }
    }
}
