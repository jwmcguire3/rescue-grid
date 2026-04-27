using System.Collections.Generic;
using System.Collections.Immutable;
using NUnit.Framework;
using Rescue.Core.Pipeline;
using Rescue.Core.Pipeline.Steps;
using Rescue.Core.State;
using Rescue.Core.Tests.Pipeline;

namespace Rescue.Core.Tests.Rules
{
    public sealed class ExtractionTests
    {
        [Test]
        public void InteriorTargetWithFourOpenNeighborsExtracts()
        {
            GameState state = CreateStateForStep(
                PipelineTestFixtures.CreateBoard(
                    Row(new EmptyTile(), new EmptyTile(), new EmptyTile()),
                    Row(new EmptyTile(), new TargetTile("target", Extracted: false), new EmptyTile()),
                    Row(new EmptyTile(), new EmptyTile(), new EmptyTile())),
                new TargetState("target", new TileCoord(1, 1), Extracted: false, OneClearAway: false));

            StepResult result = RunUpdateAndExtract(state);

            Assert.That(result.State.Targets[0].Extracted, Is.True);
            Assert.That(result.State.Targets[0].OneClearAway, Is.False);
            Assert.That(result.State.Targets[0].ExtractableLatched, Is.False);
            Assert.That(result.State.ExtractedTargetOrder, Is.EqualTo(new[] { "target" }).AsCollection);
            Assert.That(BoardHelpers.GetTile(result.State.Board, new TileCoord(1, 1)), Is.TypeOf<EmptyTile>());
            Assert.That(result.Events, Is.EqualTo(new ActionEvent[]
            {
                new TargetExtracted("target", new TileCoord(1, 1)),
            }).AsCollection);
        }

        [Test]
        public void InteriorTargetWithThreeOpenNeighborsDoesNotExtract()
        {
            GameState state = CreateStateForStep(
                PipelineTestFixtures.CreateBoard(
                    Row(new EmptyTile(), new EmptyTile(), new EmptyTile()),
                    Row(new DebrisTile(DebrisType.A), new TargetTile("target", Extracted: false), new EmptyTile()),
                    Row(new EmptyTile(), new EmptyTile(), new EmptyTile())),
                new TargetState("target", new TileCoord(1, 1), Extracted: false, OneClearAway: false));

            StepResult result = Step05_UpdateTargets.Run(state, StepContext.Create(state, new ActionInput(new TileCoord(0, 0))));

            Assert.That(result.State.Targets[0].Extracted, Is.False);
            Assert.That(result.State.Targets[0].OneClearAway, Is.True);
            Assert.That(result.State.Targets[0].ExtractableLatched, Is.False);
            Assert.That(result.State.ExtractedTargetOrder, Is.Empty);
            Assert.That(result.Events, Is.EqualTo(new ActionEvent[]
            {
                new TargetOneClearAway("target", new TileCoord(1, 1)),
            }).AsCollection);
        }

        [Test]
        public void EdgeTargetWithAllExistingSidesOpenExtracts()
        {
            GameState state = CreateStateForStep(
                PipelineTestFixtures.CreateBoard(
                    Row(new EmptyTile(), new TargetTile("target", Extracted: false), new EmptyTile()),
                    Row(new EmptyTile(), new EmptyTile(), new EmptyTile()),
                    Row(new EmptyTile(), new EmptyTile(), new EmptyTile())),
                new TargetState("target", new TileCoord(0, 1), Extracted: false, OneClearAway: false));

            StepResult result = RunUpdateAndExtract(state);

            Assert.That(result.State.Targets[0].Extracted, Is.True);
            Assert.That(result.State.ExtractedTargetOrder, Is.EqualTo(new[] { "target" }).AsCollection);
        }

        [Test]
        public void CornerTargetExtractsWithTwoOpenSides()
        {
            GameState state = CreateStateForStep(
                PipelineTestFixtures.CreateBoard(
                    Row(new TargetTile("target", Extracted: false), new EmptyTile()),
                    Row(new EmptyTile(), new EmptyTile())),
                new TargetState("target", new TileCoord(0, 0), Extracted: false, OneClearAway: false));

            StepResult result = RunUpdateAndExtract(state);

            Assert.That(result.State.Targets[0].Extracted, Is.True);
            Assert.That(result.State.ExtractedTargetOrder, Is.EqualTo(new[] { "target" }).AsCollection);
        }

        [Test]
        public void TargetBecomesOneClearAwayWhenExactlyOneRequiredNeighborBlocks()
        {
            GameState state = CreateStateForStep(
                PipelineTestFixtures.CreateBoard(
                    Row(new EmptyTile(), new EmptyTile(), new EmptyTile()),
                    Row(new DebrisTile(DebrisType.B), new TargetTile("target", Extracted: false), new EmptyTile()),
                    Row(new EmptyTile(), new EmptyTile(), new EmptyTile())),
                new TargetState("target", new TileCoord(1, 1), Extracted: false, OneClearAway: false));

            StepResult result = Step05_UpdateTargets.Run(state, StepContext.Create(state, new ActionInput(new TileCoord(0, 0))));

            Assert.That(result.State.Targets[0].OneClearAway, Is.True);
            Assert.That(result.State.Targets[0].Extracted, Is.False);
            Assert.That(result.State.Targets[0].ExtractableLatched, Is.False);
            Assert.That(result.Events, Does.Contain(new TargetOneClearAway("target", new TileCoord(1, 1))));
        }

        [Test]
        public void TargetOneClearAwayFiresOnFalseToTrueTransitionOnly()
        {
            GameState alreadyOneClearAway = CreateStateForStep(
                PipelineTestFixtures.CreateBoard(
                    Row(new EmptyTile(), new EmptyTile(), new EmptyTile()),
                    Row(new DebrisTile(DebrisType.C), new TargetTile("target", Extracted: false), new EmptyTile()),
                    Row(new EmptyTile(), new EmptyTile(), new EmptyTile())),
                new TargetState("target", new TileCoord(1, 1), Extracted: false, OneClearAway: true));

            StepResult repeatedState = Step05_UpdateTargets.Run(
                alreadyOneClearAway,
                StepContext.Create(alreadyOneClearAway, new ActionInput(new TileCoord(0, 0))));

            Assert.That(repeatedState.Events, Is.Empty);

            GameState noLongerOneClearAway = CreateStateForStep(
                PipelineTestFixtures.CreateBoard(
                    Row(new EmptyTile(), new DebrisTile(DebrisType.A), new EmptyTile()),
                    Row(new DebrisTile(DebrisType.C), new TargetTile("target", Extracted: false), new EmptyTile()),
                    Row(new EmptyTile(), new EmptyTile(), new EmptyTile())),
                new TargetState("target", new TileCoord(1, 1), Extracted: false, OneClearAway: true));

            StepResult downgradedState = Step05_UpdateTargets.Run(
                noLongerOneClearAway,
                StepContext.Create(noLongerOneClearAway, new ActionInput(new TileCoord(0, 0))));

            Assert.That(downgradedState.State.Targets[0].OneClearAway, Is.False);
            Assert.That(downgradedState.Events, Is.Empty);
        }

        [Test]
        public void WinFiresImmediatelyOnFinalExtraction()
        {
            GameState state = CreateStateForStep(
                PipelineTestFixtures.CreateBoard(
                    Row(new TargetTile("target", Extracted: false), new EmptyTile(), new DebrisTile(DebrisType.B)),
                    Row(new EmptyTile(), new DebrisTile(DebrisType.C), new DebrisTile(DebrisType.D)),
                    Row(new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.E))),
                new TargetState("target", new TileCoord(0, 0), Extracted: false, OneClearAway: false));

            StepContext context = StepContext.Create(state, new ActionInput(new TileCoord(2, 0)));
            StepResult updateResult = Step05_UpdateTargets.Run(state, context);
            StepResult extractResult = Step09_Extract.Run(updateResult.State, updateResult.Context);
            StepResult winResult = Step10_CheckWin.Run(extractResult.State, extractResult.Context);

            Assert.That(extractResult.State.Targets[0].Extracted, Is.True);
            Assert.That(updateResult.State.Targets[0].ExtractableLatched, Is.True);
            Assert.That(extractResult.State.ExtractedTargetOrder, Is.EqualTo(new[] { "target" }).AsCollection);
            Assert.That(extractResult.Events, Is.EqualTo(new ActionEvent[]
            {
                new TargetExtracted("target", new TileCoord(0, 0)),
            }).AsCollection);
            Assert.That(winResult.Context.IsWin, Is.True);
            Assert.That(winResult.State.Frozen, Is.True);
            Assert.That(winResult.Events.Length, Is.EqualTo(1));
            Assert.That(winResult.Events[0], Is.TypeOf<Won>());
            Won won = (Won)winResult.Events[0];
            Assert.That(won.FinalExtractedTargetId, Is.EqualTo("target"));
            Assert.That(won.TotalActions, Is.EqualTo(state.ActionCount + 1));
            Assert.That(won.ExtractedTargetOrder, Is.EqualTo(new[] { "target" }).AsCollection);
        }

        [Test]
        public void WinSkipsLaterHazardSteps()
        {
            GameState state = PipelineTestFixtures.CreateState(
                PipelineTestFixtures.CreateBoard(
                    Row(new EmptyTile(), new EmptyTile(), new DebrisTile(DebrisType.B)),
                    Row(new EmptyTile(), new DebrisTile(DebrisType.C), new DebrisTile(DebrisType.D)),
                    Row(new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A), new DebrisTile(DebrisType.A))),
                targets: ImmutableArray.Create(new TargetState("target", new TileCoord(0, 0), Extracted: true, OneClearAway: false)))
                with
                {
                    ExtractedTargetOrder = ImmutableArray.Create("target"),
                };
            state = state with
            {
                Dock = new Dock(
                    ImmutableArray.Create<DebrisType?>(
                        DebrisType.B,
                        DebrisType.C,
                        DebrisType.D,
                        DebrisType.E,
                        DebrisType.B,
                        null,
                        null),
                    Size: 7),
            };

            List<string> trace = new List<string>();
            ActionResult result = Rescue.Core.Pipeline.Pipeline.RunAction(
                state,
                new ActionInput(new TileCoord(2, 0)),
                options: null,
                observer: step => trace.Add(step.StepName));

            Assert.That(result.Outcome, Is.EqualTo(ActionOutcome.Win));
            Assert.That(result.State.ExtractedTargetOrder, Is.EqualTo(new[] { "target" }).AsCollection);
            Assert.That(trace, Is.EqualTo(new[]
            {
                "Step01_AcceptInput",
                "Step02_RemoveGroup",
                "Step03_DamageBlockers",
                "Step04_ResolveBreaks",
                "Step05_UpdateTargets",
                "Step06_InsertDock",
                "Step07_ClearDock",
                "Step08_Extract",
                "Step09_CheckWin",
            }));
            Assert.That(result.Events, Has.Some.TypeOf<Won>());
            Assert.That(result.Events, Has.None.TypeOf<Lost>());
        }

        [Test]
        public void LatchedExtractionIgnoresLaterBoardRefill()
        {
            GameState state = CreateStateForStep(
                PipelineTestFixtures.CreateBoard(
                    Row(new EmptyTile(), new EmptyTile(), new EmptyTile()),
                    Row(new EmptyTile(), new TargetTile("target", Extracted: false), new EmptyTile()),
                    Row(new EmptyTile(), new EmptyTile(), new EmptyTile())),
                new TargetState("target", new TileCoord(1, 1), Extracted: false, OneClearAway: true));

            StepResult update = Step05_UpdateTargets.Run(state, StepContext.Create(state, new ActionInput(new TileCoord(0, 0))));
            Board refilledBoard = BoardHelpers.SetTile(update.State.Board, new TileCoord(0, 1), new DebrisTile(DebrisType.C));
            StepResult extract = Step09_Extract.Run(update.State with { Board = refilledBoard }, update.Context);

            Assert.That(update.State.Targets[0].ExtractableLatched, Is.True);
            Assert.That(extract.State.Targets[0].Extracted, Is.True);
            Assert.That(extract.Events, Has.Some.EqualTo(new TargetExtracted("target", new TileCoord(1, 1))));
        }

        private static GameState CreateStateForStep(Board board, TargetState target)
        {
            return PipelineTestFixtures.CreateState(board, ImmutableArray.Create(target));
        }

        private static StepResult RunUpdateAndExtract(GameState state)
        {
            StepContext context = StepContext.Create(state, new ActionInput(new TileCoord(0, 0)));
            StepResult update = Step05_UpdateTargets.Run(state, context);
            return Step09_Extract.Run(update.State, update.Context);
        }

        private static ImmutableArray<Tile> Row(params Tile[] tiles)
        {
            return tiles.ToImmutableArray();
        }
    }
}
