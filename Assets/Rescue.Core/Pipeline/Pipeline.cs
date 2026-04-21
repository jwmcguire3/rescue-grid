using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Rescue.Core.Pipeline.Steps;
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
            "Step05_InsertDock",
            "Step06_ClearDock",
            "Step07_Gravity",
            "Step08_Spawn",
            "Step09_Extract",
            "Step10_CheckWin",
            "Step11_TickHazards",
            "Step12_ResolveHazards");

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

            RunOptions effectiveOptions = options ?? new RunOptions();
            Snapshot? snapshot = null;
            ImmutableArray<ActionEvent>.Builder events = ImmutableArray.CreateBuilder<ActionEvent>();
            StepContext context = StepContext.Create(state, input);

            StepResult result = Step01_AcceptInput.Run(state, context);
            EmitTrace(observer, StepOrder[0], result);
            Append(events, result.Events);

            if (!result.Context.IsValidInput)
            {
                return new ActionResult(result.State, events.ToImmutable(), ActionOutcome.Ok, Snapshot: null);
            }

            if (effectiveOptions.RecordSnapshot)
            {
                snapshot = SnapshotHelpers.Take(state);
            }

            result = RunStep(StepOrder[1], Step02_RemoveGroup.Run, result, observer, events);
            result = RunStep(StepOrder[2], Step03_DamageBlockers.Run, result, observer, events);
            result = RunStep(StepOrder[3], Step04_ResolveBreaks.Run, result, observer, events);
            result = RunStep(StepOrder[4], Step05_InsertDock.Run, result, observer, events);
            result = RunStep(StepOrder[5], Step06_ClearDock.Run, result, observer, events);
            result = RunStep(StepOrder[6], Step07_Gravity.Run, result, observer, events);
            result = RunStep(StepOrder[7], Step08_Spawn.Run, result, observer, events);
            result = RunStep(StepOrder[8], Step09_Extract.Run, result, observer, events);
            result = RunStep(StepOrder[9], Step10_CheckWin.Run, result, observer, events);

            if (result.Context.IsWin)
            {
                return new ActionResult(result.State, events.ToImmutable(), ActionOutcome.Win, snapshot);
            }

            result = RunStep(StepOrder[10], Step11_TickHazards.Run, result, observer, events);
            result = RunStep(StepOrder[11], Step12_ResolveHazards.Run, result, observer, events);

            CheckLossResult lossResult = CheckLoss.Run(result.State, result.Context);
            Append(events, lossResult.Events);
            return new ActionResult(lossResult.State, events.ToImmutable(), lossResult.Outcome, snapshot);
        }

        internal static ImmutableArray<string> GetStepOrder()
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
    }
}
