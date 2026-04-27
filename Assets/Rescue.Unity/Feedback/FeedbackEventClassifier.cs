using System.Collections.Immutable;
using Rescue.Core.Pipeline;
using Rescue.Core.State;
using Rescue.Unity.Presentation;

namespace Rescue.Unity.Feedback
{
    public static class FeedbackEventClassifier
    {
        public static ImmutableArray<FeedbackEvent> Classify(ActionResult result)
        {
            ImmutableArray<FeedbackEvent>.Builder feedbackEvents = ImmutableArray.CreateBuilder<FeedbackEvent>();
            bool emittedWin = false;
            bool emittedLossDockOverflow = false;
            bool emittedLossWaterOnTarget = false;

            for (int i = 0; i < result.Events.Length; i++)
            {
                if (!TryClassify(result.Events[i], out FeedbackEvent feedbackEvent))
                {
                    continue;
                }

                if (!TryMarkTerminalFeedback(
                    feedbackEvent.Id,
                    ref emittedWin,
                    ref emittedLossDockOverflow,
                    ref emittedLossWaterOnTarget))
                {
                    continue;
                }

                feedbackEvents.Add(feedbackEvent);
            }

            AddOutcomeFallback(
                result.Outcome,
                feedbackEvents,
                ref emittedWin,
                ref emittedLossDockOverflow,
                ref emittedLossWaterOnTarget);

            return feedbackEvents.ToImmutable();
        }

        public static bool TryClassify(ActionPlaybackStep playbackStep, out FeedbackEvent feedbackEvent)
        {
            if (playbackStep.SourceEvent is null)
            {
                feedbackEvent = default;
                return false;
            }

            return TryClassify(playbackStep.SourceEvent, out feedbackEvent);
        }

        public static bool TryClassify(ActionEvent actionEvent, out FeedbackEvent feedbackEvent)
        {
            switch (actionEvent)
            {
                case InvalidInput invalidInput:
                    feedbackEvent = Create(FeedbackEventId.InvalidTap, actionEvent, invalidInput.TappedCoord);
                    return true;
                case GroupRemoved removed:
                    feedbackEvent = Create(FeedbackEventId.GroupClear, actionEvent, GetFirstOrNull(removed.Coords));
                    return true;
                case BlockerDamaged damaged:
                    feedbackEvent = Create(FeedbackEventId.BlockerDamage, actionEvent, damaged.Coord);
                    return true;
                case BlockerBroken broken when broken.Type == BlockerType.Crate:
                    feedbackEvent = Create(FeedbackEventId.CrateBreak, actionEvent, broken.Coord);
                    return true;
                case BlockerBroken broken when broken.Type == BlockerType.Vine:
                    feedbackEvent = Create(FeedbackEventId.VineClear, actionEvent, broken.Coord);
                    return true;
                case IceRevealed revealed:
                    feedbackEvent = Create(FeedbackEventId.IceReveal, actionEvent, revealed.Coord);
                    return true;
                case DockInserted:
                    feedbackEvent = Create(FeedbackEventId.DockInsert, actionEvent);
                    return true;
                case DockCleared:
                    feedbackEvent = Create(FeedbackEventId.DockTripleClear, actionEvent);
                    return true;
                case DockWarningChanged warningChanged when warningChanged.After == DockWarningLevel.Caution:
                    feedbackEvent = Create(FeedbackEventId.DockCaution, actionEvent);
                    return true;
                case DockWarningChanged warningChanged when warningChanged.After == DockWarningLevel.Acute:
                    feedbackEvent = Create(FeedbackEventId.DockAcute, actionEvent);
                    return true;
                case DockWarningChanged warningChanged when warningChanged.After == DockWarningLevel.Fail:
                    feedbackEvent = Create(FeedbackEventId.DockJamOrFail, actionEvent);
                    return true;
                case DockOverflowTriggered:
                case DockJamTriggered:
                    feedbackEvent = Create(FeedbackEventId.DockJamOrFail, actionEvent);
                    return true;
                case GravitySettled settled when !settled.Moves.IsDefaultOrEmpty:
                    feedbackEvent = Create(FeedbackEventId.GravitySettle, actionEvent, settled.Moves[0].To);
                    return true;
                case Spawned spawned when !spawned.Pieces.IsDefaultOrEmpty:
                    feedbackEvent = Create(FeedbackEventId.SpawnLand, actionEvent, spawned.Pieces[0].Coord);
                    return true;
                case TargetOneClearAway oneClearAway:
                    feedbackEvent = Create(FeedbackEventId.TargetOneClearAway, actionEvent, oneClearAway.Coord);
                    return true;
                case TargetExtracted extracted:
                    feedbackEvent = Create(FeedbackEventId.TargetExtract, actionEvent, extracted.Coord);
                    return true;
                case WaterWarning:
                    feedbackEvent = Create(FeedbackEventId.WaterWarning, actionEvent);
                    return true;
                case WaterRose rose:
                    feedbackEvent = Create(FeedbackEventId.WaterRise, actionEvent, new TileCoord(rose.FloodedRow, 0));
                    return true;
                case VinePreviewChanged previewChanged when previewChanged.PendingTile.HasValue:
                    feedbackEvent = Create(FeedbackEventId.VinePreview, actionEvent, previewChanged.PendingTile.Value);
                    return true;
                case VineGrown grown:
                    feedbackEvent = Create(FeedbackEventId.VineGrow, actionEvent, grown.Coord);
                    return true;
                case Won:
                    feedbackEvent = Create(FeedbackEventId.Win, actionEvent);
                    return true;
                case Lost lost when lost.Outcome == ActionOutcome.LossDockOverflow:
                    feedbackEvent = Create(FeedbackEventId.LossDockOverflow, actionEvent);
                    return true;
                case Lost lost when lost.Outcome == ActionOutcome.LossWaterOnTarget
                    || lost.Outcome == ActionOutcome.LossDistressedExpired:
                    feedbackEvent = Create(FeedbackEventId.LossWaterOnTarget, actionEvent);
                    return true;
                default:
                    feedbackEvent = default;
                    return false;
            }
        }

        private static void AddOutcomeFallback(
            ActionOutcome outcome,
            ImmutableArray<FeedbackEvent>.Builder feedbackEvents,
            ref bool emittedWin,
            ref bool emittedLossDockOverflow,
            ref bool emittedLossWaterOnTarget)
        {
            FeedbackEventId? fallbackId = outcome switch
            {
                ActionOutcome.Win => FeedbackEventId.Win,
                ActionOutcome.LossDockOverflow => FeedbackEventId.LossDockOverflow,
                ActionOutcome.LossWaterOnTarget => FeedbackEventId.LossWaterOnTarget,
                ActionOutcome.LossDistressedExpired => FeedbackEventId.LossWaterOnTarget,
                _ => null,
            };

            if (!fallbackId.HasValue ||
                !TryMarkTerminalFeedback(
                    fallbackId.Value,
                    ref emittedWin,
                    ref emittedLossDockOverflow,
                    ref emittedLossWaterOnTarget))
            {
                return;
            }

            feedbackEvents.Add(new FeedbackEvent(
                fallbackId.Value,
                SourceEvent: null,
                Location: null,
                DebugLabel: $"Outcome:{outcome.ToString()}"));
        }

        private static bool TryMarkTerminalFeedback(
            FeedbackEventId id,
            ref bool emittedWin,
            ref bool emittedLossDockOverflow,
            ref bool emittedLossWaterOnTarget)
        {
            switch (id)
            {
                case FeedbackEventId.Win:
                    if (emittedWin)
                    {
                        return false;
                    }

                    emittedWin = true;
                    return true;
                case FeedbackEventId.LossDockOverflow:
                    if (emittedLossDockOverflow)
                    {
                        return false;
                    }

                    emittedLossDockOverflow = true;
                    return true;
                case FeedbackEventId.LossWaterOnTarget:
                    if (emittedLossWaterOnTarget)
                    {
                        return false;
                    }

                    emittedLossWaterOnTarget = true;
                    return true;
                default:
                    return true;
            }
        }

        private static FeedbackEvent Create(
            FeedbackEventId id,
            ActionEvent sourceEvent,
            TileCoord? location = null)
        {
            return new FeedbackEvent(
                id,
                sourceEvent,
                location,
                sourceEvent.GetType().Name);
        }

        private static TileCoord? GetFirstOrNull(ImmutableArray<TileCoord> coords)
        {
            return coords.IsDefaultOrEmpty
                ? null
                : coords[0];
        }
    }
}
