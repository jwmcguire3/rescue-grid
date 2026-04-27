using System.Collections.Immutable;
using Rescue.Core.Pipeline;
using Rescue.Core.State;

namespace Rescue.Unity.FX
{
    public static class FxEventClassifier
    {
        public static ImmutableArray<FxEventHook> Classify(
            GameState previousState,
            ActionInput input,
            ActionResult result)
        {
            _ = previousState;
            _ = input;

            ImmutableArray<FxEventHook>.Builder hooks = ImmutableArray.CreateBuilder<FxEventHook>();
            bool sawWin = false;
            bool sawLossDockOverflow = false;
            bool sawLossWaterOnTarget = false;

            for (int i = 0; i < result.Events.Length; i++)
            {
                switch (result.Events[i])
                {
                    case InvalidInput:
                        hooks.Add(FxEventHook.InvalidTap);
                        break;
                    case GroupRemoved:
                        hooks.Add(FxEventHook.GroupClear);
                        break;
                    case BlockerBroken blockerBroken when blockerBroken.Type == BlockerType.Crate:
                        hooks.Add(FxEventHook.CrateBreak);
                        break;
                    case BlockerBroken blockerBroken when blockerBroken.Type == BlockerType.Vine:
                        hooks.Add(FxEventHook.VineClear);
                        break;
                    case IceRevealed:
                        hooks.Add(FxEventHook.IceReveal);
                        break;
                    case DockInserted:
                        hooks.Add(FxEventHook.DockInsert);
                        break;
                    case DockCleared:
                        hooks.Add(FxEventHook.DockTripleClear);
                        break;
                    case DockWarningChanged dockWarningChanged when dockWarningChanged.After != DockWarningLevel.Safe:
                        hooks.Add(FxEventHook.DockWarning);
                        break;
                    case WaterRose:
                        hooks.Add(FxEventHook.WaterRise);
                        break;
                    case TargetOneClearAway:
                        hooks.Add(FxEventHook.NearRescueRelief);
                        break;
                    case TargetExtracted:
                        hooks.Add(FxEventHook.TargetExtraction);
                        break;
                    case VinePreviewChanged vinePreviewChanged when vinePreviewChanged.PendingTile.HasValue:
                        hooks.Add(FxEventHook.VineGrowthPreview);
                        break;
                    case Won:
                        sawWin = true;
                        hooks.Add(FxEventHook.Win);
                        break;
                    case Lost lost when lost.Outcome == ActionOutcome.LossDockOverflow:
                        sawLossDockOverflow = true;
                        hooks.Add(FxEventHook.LossDockOverflow);
                        break;
                    case Lost lost when lost.Outcome == ActionOutcome.LossWaterOnTarget
                        || lost.Outcome == ActionOutcome.LossDistressedExpired:
                        sawLossWaterOnTarget = true;
                        hooks.Add(FxEventHook.LossWaterOnTarget);
                        break;
                }
            }

            if (!sawWin && result.Outcome == ActionOutcome.Win)
            {
                hooks.Add(FxEventHook.Win);
            }

            if (!sawLossDockOverflow && result.Outcome == ActionOutcome.LossDockOverflow)
            {
                hooks.Add(FxEventHook.LossDockOverflow);
            }

            if (!sawLossWaterOnTarget
                && (result.Outcome == ActionOutcome.LossWaterOnTarget
                    || result.Outcome == ActionOutcome.LossDistressedExpired))
            {
                hooks.Add(FxEventHook.LossWaterOnTarget);
            }

            return hooks.ToImmutable();
        }
    }
}
