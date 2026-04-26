using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Rescue.Core.State;

namespace Rescue.Unity.BoardPresentation
{
    public enum TargetFeedbackKind
    {
        NearRescue,
        Extraction,
    }

    public readonly record struct TargetFeedbackEvent(
        string TargetId,
        TileCoord Coord,
        TargetFeedbackKind Kind);

    public readonly record struct TargetFeedbackResolution(
        ImmutableArray<TargetFeedbackEvent> Events);

    public static class TargetFeedbackResolver
    {
        public static TargetFeedbackResolution Resolve(GameState? previous, GameState current)
        {
            if (current is null)
            {
                throw new ArgumentNullException(nameof(current));
            }

            if (previous is null)
            {
                return new TargetFeedbackResolution(ImmutableArray<TargetFeedbackEvent>.Empty);
            }

            Dictionary<string, TargetState> previousTargets = new Dictionary<string, TargetState>(StringComparer.Ordinal);
            for (int i = 0; i < previous.Targets.Length; i++)
            {
                TargetState target = previous.Targets[i];
                previousTargets[target.TargetId] = target;
            }

            ImmutableArray<TargetFeedbackEvent>.Builder events = ImmutableArray.CreateBuilder<TargetFeedbackEvent>();
            for (int i = 0; i < current.Targets.Length; i++)
            {
                TargetState currentTarget = current.Targets[i];
                if (!previousTargets.TryGetValue(currentTarget.TargetId, out TargetState previousTarget))
                {
                    continue;
                }

                if (!previousTarget.Extracted && currentTarget.Extracted)
                {
                    events.Add(new TargetFeedbackEvent(
                        currentTarget.TargetId,
                        previousTarget.Coord,
                        TargetFeedbackKind.Extraction));
                    continue;
                }

                if (!previousTarget.OneClearAway && currentTarget.OneClearAway && !currentTarget.Extracted)
                {
                    events.Add(new TargetFeedbackEvent(
                        currentTarget.TargetId,
                        currentTarget.Coord,
                        TargetFeedbackKind.NearRescue));
                }
            }

            return new TargetFeedbackResolution(events.ToImmutable());
        }
    }
}
