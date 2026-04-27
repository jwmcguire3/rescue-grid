using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Rescue.Core.Pipeline.Steps;
using Rescue.Core.Rules;
using Rescue.Core.State;
using Rescue.Core.Undo;

namespace Rescue.Core.Pipeline
{
    public static class Pipeline
    {
        private static readonly ImmutableArray<string> StepOrder = ImmutableArray.Create(
            "Step01_AcceptInput",
            "Step02_RemoveGroup",
            "Step03_DamageBlockers",
            "Step04_ResolveBreaks",
            "Step05_UpdateTargets",
            "Step06_InsertDock",
            "Step07_ClearDock",
            "Step08_Extract",
            "Step09_CheckWin",
            "Step10_CheckLoss",
            "Step11_Gravity",
            "Step12_Spawn",
            "Step13_TickHazards",
            "Step14_ResolveHazards",
            "Step15_CheckWaterConsequence");

        public static ActionResult RunAction(
            GameState state,
            ActionInput input,
            RunOptions? options = null)
        {
            return RunAction(state, input, options, observer: null);
        }

        internal static ActionResult RunAction(
            GameState state,
            ActionInput input,
            RunOptions? options,
            Action<StepTrace>? observer)
        {
            if (state is null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            RunOptions effectiveOptions = options ?? new RunOptions(RecordSnapshot: true);
            Snapshot? snapshot = effectiveOptions.RecordSnapshot ? SnapshotHelpers.Take(state) : null;
            ImmutableArray<ActionEvent>.Builder events = ImmutableArray.CreateBuilder<ActionEvent>();
            StepContext context = StepContext.Create(state, input);

            StepResult result = Step01_AcceptInput.Run(state, context);
            EmitTrace(observer, StepOrder[0], result);
            Append(events, result.Events);

            if (!result.Context.IsValidInput)
            {
                return new ActionResult(result.State, events.ToImmutable(), ActionOutcome.Ok, Snapshot: null);
            }

            result = RunStep(StepOrder[1], Step02_RemoveGroup.Run, result, observer, events);
            result = RunStep(StepOrder[2], Step03_DamageBlockers.Run, result, observer, events);
            result = RunStep(StepOrder[3], Step04_ResolveBreaks.Run, result, observer, events);
            result = RunStep(StepOrder[4], Step05_UpdateTargets.Run, result, observer, events);
            result = RunStep(StepOrder[5], Step05_InsertDock.Run, result, observer, events);
            result = RunStep(StepOrder[6], Step06_ClearDock.Run, result, observer, events);
            result = RunStep(StepOrder[7], Step09_Extract.Run, result, observer, events);
            result = RunStep(StepOrder[8], Step10_CheckWin.Run, result, observer, events);

            if (result.Context.IsWin)
            {
                return new ActionResult(IncrementActionCount(result.State), events.ToImmutable(), ActionOutcome.Win, snapshot);
            }

            CheckLossResult lossResult = CheckLoss.Run(result.State, result.Context);
            EmitTrace(observer, StepOrder[9], lossResult.State, result.Context, lossResult.Events);
            Append(events, lossResult.Events);
            if (lossResult.Outcome != ActionOutcome.Ok || lossResult.State.Frozen)
            {
                return new ActionResult(IncrementActionCount(lossResult.State), events.ToImmutable(), lossResult.Outcome, snapshot);
            }

            result = new StepResult(lossResult.State, result.Context, ImmutableArray<ActionEvent>.Empty);
            result = RunStep(StepOrder[10], Step07_Gravity.Run, result, observer, events);
            result = RunStep(StepOrder[11], Step08_Spawn.Run, result, observer, events);
            result = RunStep(StepOrder[12], Step11_TickHazards.Run, result, observer, events);
            result = RunStep(StepOrder[13], Step12_ResolveHazards.Run, result, observer, events);

            CheckLossResult waterResult = WaterTargetConsequence.Run(result.State, result.Context);
            EmitTrace(observer, StepOrder[14], waterResult.State, result.Context, waterResult.Events);
            Append(events, waterResult.Events);
            if (waterResult.Outcome == ActionOutcome.Ok)
            {
                Append(events, DetectDeadboardDiagnostics(waterResult.State));
            }

            return new ActionResult(IncrementActionCount(waterResult.State), events.ToImmutable(), waterResult.Outcome, snapshot);
        }

        // Exposes the full nominal pipeline order for valid non-win actions.
        // Short-circuited paths such as invalid input and wins will stop earlier.
        internal static ImmutableArray<string> GetNonShortCircuitedStepOrder()
        {
            return StepOrder;
        }

        private static StepResult RunStep(
            string stepName,
            Func<GameState, StepContext, StepResult> step,
            StepResult input,
            Action<StepTrace>? observer,
            ImmutableArray<ActionEvent>.Builder events)
        {
            StepResult result = step(input.State, input.Context);
            EmitTrace(observer, stepName, result);
            Append(events, result.Events);
            return result;
        }

        private static void Append(
            ImmutableArray<ActionEvent>.Builder builder,
            ImmutableArray<ActionEvent> events)
        {
            for (int i = 0; i < events.Length; i++)
            {
                builder.Add(events[i]);
            }
        }

        private static void EmitTrace(Action<StepTrace>? observer, string stepName, StepResult result)
        {
            observer?.Invoke(new StepTrace(stepName, result.State, result.Context, result.Events));
        }

        private static void EmitTrace(
            Action<StepTrace>? observer,
            string stepName,
            GameState state,
            StepContext context,
            ImmutableArray<ActionEvent> events)
        {
            observer?.Invoke(new StepTrace(stepName, state, context, events));
        }

        private static GameState IncrementActionCount(GameState state)
        {
            return state with { ActionCount = state.ActionCount + 1 };
        }

        private static ImmutableArray<ActionEvent> DetectDeadboardDiagnostics(GameState state)
        {
            ImmutableArray<ActionEvent>.Builder events = ImmutableArray.CreateBuilder<ActionEvent>();
            bool hasValidGroup = HasValidGroup(state.Board);
            string? impossibleTargetId = FindRescueImpossibleTarget(state);
            if (impossibleTargetId is not null)
            {
                events.Add(new DeadboardDiagnosticDetected(
                    DeadboardDiagnosticReason.RescueImpossibleStatic,
                    impossibleTargetId));
            }

            if (!hasValidGroup)
            {
                events.Add(new DeadboardDiagnosticDetected(
                    DeadboardDiagnosticReason.HardNoValidGroups,
                    TargetId: null));
                return events.ToImmutable();
            }

            if (!HasImmediateRescueProgressMove(state))
            {
                events.Add(new DeadboardDiagnosticDetected(
                    DeadboardDiagnosticReason.SoftNoRescueProgressMove,
                    TargetId: null));
            }

            return events.ToImmutable();
        }

        private static bool HasValidGroup(Board board)
        {
            for (int row = 0; row < board.Height; row++)
            {
                for (int col = 0; col < board.Width; col++)
                {
                    if (GroupOps.FindGroup(board, new TileCoord(row, col)).HasValue)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static string? FindRescueImpossibleTarget(GameState state)
        {
            for (int i = 0; i < state.Targets.Length; i++)
            {
                TargetState target = state.Targets[i];
                if (target.Extracted)
                {
                    continue;
                }

                ImmutableArray<TileCoord> neighbors = BoardHelpers.OrthogonalNeighbors(state.Board, target.Coord);
                for (int j = 0; j < neighbors.Length; j++)
                {
                    if (BoardHelpers.GetTile(state.Board, neighbors[j]) is FloodedTile)
                    {
                        return target.TargetId;
                    }
                }
            }

            return null;
        }

        private static bool HasImmediateRescueProgressMove(GameState state)
        {
            for (int i = 0; i < state.Targets.Length; i++)
            {
                TargetState target = state.Targets[i];
                if (target.Extracted)
                {
                    continue;
                }

                ImmutableArray<TileCoord> neighbors = BoardHelpers.OrthogonalNeighbors(state.Board, target.Coord);
                for (int j = 0; j < neighbors.Length; j++)
                {
                    TileCoord neighbor = neighbors[j];
                    Tile tile = BoardHelpers.GetTile(state.Board, neighbor);
                    if (tile is EmptyTile)
                    {
                        continue;
                    }

                    if (tile is DebrisTile && GroupOps.FindGroup(state.Board, neighbor).HasValue)
                    {
                        return true;
                    }

                    if (tile is BlockerTile && HasValidGroupAdjacentTo(state.Board, neighbor))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool HasValidGroupAdjacentTo(Board board, TileCoord coord)
        {
            ImmutableArray<TileCoord> neighbors = BoardHelpers.OrthogonalNeighbors(board, coord);
            for (int i = 0; i < neighbors.Length; i++)
            {
                if (GroupOps.FindGroup(board, neighbors[i]).HasValue)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
