using System.Collections.Immutable;
using System.Linq;
using NUnit.Framework;
using Rescue.Core.Pipeline;
using Rescue.Core.Rng;
using Rescue.Core.State;
using CoreBoard = Rescue.Core.State.Board;
using CoreDock = Rescue.Core.State.Dock;

namespace Rescue.Unity.Presentation.Tests
{
    public sealed class ActionPlaybackBuilderTests
    {
        [Test]
        public void Build_NormalActionEndsWithFinalSync()
        {
            ActionPlaybackPlan plan = ActionPlaybackBuilder.Build(
                CreateState(),
                new ActionInput(new TileCoord(0, 0)),
                CreateResult(
                    new GroupRemoved(DebrisType.A, ImmutableArray.Create(new TileCoord(0, 0), new TileCoord(0, 1))),
                    new DockInserted(ImmutableArray.Create(DebrisType.A, DebrisType.A), OccupancyAfterInsert: 2, OverflowCount: 0)));

            Assert.That(plan.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(plan[^1].StepType, Is.EqualTo(ActionPlaybackStepType.FinalSync));
        }

        [Test]
        public void Build_PreservesMappedEventOrderFromCanonicalActionEventStream()
        {
            ActionPlaybackPlan plan = ActionPlaybackBuilder.Build(
                CreateState(),
                new ActionInput(new TileCoord(0, 0)),
                CreateResult(
                    new WaterRose(FloodedRow: 3),
                    new Spawned(ImmutableArray.Create((new TileCoord(0, 1), DebrisType.C))),
                    new TargetExtracted("pup-1", new TileCoord(2, 1)),
                    new DockInserted(ImmutableArray.Create(DebrisType.A), OccupancyAfterInsert: 1, OverflowCount: 0),
                    new BlockerBroken(new TileCoord(0, 2), BlockerType.Crate),
                    new GroupRemoved(DebrisType.A, ImmutableArray.Create(new TileCoord(0, 0))),
                    new GravitySettled(ImmutableArray.Create((new TileCoord(0, 1), new TileCoord(1, 1))))));

            Assert.That(plan.Select(step => step.StepType), Is.EqualTo(new[]
            {
                ActionPlaybackStepType.WaterRise,
                ActionPlaybackStepType.Spawn,
                ActionPlaybackStepType.TargetExtract,
                ActionPlaybackStepType.DockFeedback,
                ActionPlaybackStepType.BreakBlockerOrReveal,
                ActionPlaybackStepType.RemoveGroup,
                ActionPlaybackStepType.Gravity,
                ActionPlaybackStepType.FinalSync,
            }));
        }

        [Test]
        public void Build_GravityAndSpawnAreNotArtificiallyReordered()
        {
            ActionPlaybackPlan plan = ActionPlaybackBuilder.Build(
                CreateState(),
                new ActionInput(new TileCoord(0, 0)),
                CreateResult(
                    new Spawned(ImmutableArray.Create((new TileCoord(0, 0), DebrisType.B))),
                    new GravitySettled(ImmutableArray.Create((new TileCoord(0, 0), new TileCoord(1, 0))))));

            Assert.That(plan.Select(step => step.StepType), Is.EqualTo(new[]
            {
                ActionPlaybackStepType.Spawn,
                ActionPlaybackStepType.Gravity,
                ActionPlaybackStepType.FinalSync,
            }));
        }

        [Test]
        public void Build_BlockerDamagedMapsToBreakBlockerOrReveal()
        {
            ActionPlaybackPlan plan = ActionPlaybackBuilder.Build(
                CreateState(),
                new ActionInput(new TileCoord(0, 0)),
                CreateResult(new BlockerDamaged(new TileCoord(0, 1), BlockerType.Crate, RemainingHp: 0)));

            Assert.That(plan[0].SourceEventName, Is.EqualTo(nameof(BlockerDamaged)));
            Assert.That(plan[0].StepType, Is.EqualTo(ActionPlaybackStepType.BreakBlockerOrReveal));
        }

        [Test]
        public void Build_DockInsertedMapsToDockFeedback()
        {
            ActionPlaybackPlan plan = ActionPlaybackBuilder.Build(
                CreateState(),
                new ActionInput(new TileCoord(0, 0)),
                CreateResult(new DockInserted(ImmutableArray.Create(DebrisType.A), OccupancyAfterInsert: 1, OverflowCount: 0)));

            Assert.That(plan[0].SourceEventName, Is.EqualTo(nameof(DockInserted)));
            Assert.That(plan[0].StepType, Is.EqualTo(ActionPlaybackStepType.DockFeedback));
        }

        [Test]
        public void Build_DockClearedMapsToDockFeedback()
        {
            ActionPlaybackPlan plan = ActionPlaybackBuilder.Build(
                CreateState(),
                new ActionInput(new TileCoord(0, 0)),
                CreateResult(new DockCleared(DebrisType.A, SetsCleared: 1, OccupancyAfterClear: 0)));

            Assert.That(plan[0].SourceEventName, Is.EqualTo(nameof(DockCleared)));
            Assert.That(plan[0].StepType, Is.EqualTo(ActionPlaybackStepType.DockFeedback));
        }

        [Test]
        public void Build_DockWarningChangedMapsToDockFeedback()
        {
            ActionPlaybackPlan plan = ActionPlaybackBuilder.Build(
                CreateState(),
                new ActionInput(new TileCoord(0, 0)),
                CreateResult(new DockWarningChanged(DockWarningLevel.Safe, DockWarningLevel.Caution)));

            Assert.That(plan[0].SourceEventName, Is.EqualTo(nameof(DockWarningChanged)));
            Assert.That(plan[0].StepType, Is.EqualTo(ActionPlaybackStepType.DockFeedback));
        }

        [Test]
        public void Build_DockJamTriggeredMapsToDockFeedback()
        {
            ActionPlaybackPlan plan = ActionPlaybackBuilder.Build(
                CreateState(),
                new ActionInput(new TileCoord(0, 0)),
                CreateResult(new DockJamTriggered(OverflowCount: 1)));

            Assert.That(plan[0].SourceEventName, Is.EqualTo(nameof(DockJamTriggered)));
            Assert.That(plan[0].StepType, Is.EqualTo(ActionPlaybackStepType.DockFeedback));
        }

        [Test]
        public void Build_PreservesCanonicalDockEventOrder()
        {
            ActionPlaybackPlan plan = ActionPlaybackBuilder.Build(
                CreateState(),
                new ActionInput(new TileCoord(0, 0)),
                CreateResult(
                    new DockInserted(ImmutableArray.Create(DebrisType.A, DebrisType.A), OccupancyAfterInsert: 2, OverflowCount: 0),
                    new DockCleared(DebrisType.A, SetsCleared: 1, OccupancyAfterClear: 0),
                    new DockWarningChanged(DockWarningLevel.Safe, DockWarningLevel.Caution),
                    new DockJamTriggered(OverflowCount: 1)));

            Assert.That(plan.Take(plan.Count - 1).Select(step => (step.SourceEventName, step.StepType)), Is.EqualTo(new[]
            {
                (nameof(DockInserted), ActionPlaybackStepType.DockFeedback),
                (nameof(DockCleared), ActionPlaybackStepType.DockFeedback),
                (nameof(DockWarningChanged), ActionPlaybackStepType.DockFeedback),
                (nameof(DockJamTriggered), ActionPlaybackStepType.DockFeedback),
            }));
            Assert.That(plan[^1].StepType, Is.EqualTo(ActionPlaybackStepType.FinalSync));
        }

        [Test]
        public void Build_BlockerBrokenMapsToBreakBlockerOrReveal()
        {
            ActionPlaybackPlan plan = ActionPlaybackBuilder.Build(
                CreateState(),
                new ActionInput(new TileCoord(0, 0)),
                CreateResult(new BlockerBroken(new TileCoord(0, 1), BlockerType.Crate)));

            Assert.That(plan[0].SourceEventName, Is.EqualTo(nameof(BlockerBroken)));
            Assert.That(plan[0].StepType, Is.EqualTo(ActionPlaybackStepType.BreakBlockerOrReveal));
        }

        [Test]
        public void Build_IceRevealedMapsToBreakBlockerOrReveal()
        {
            ActionPlaybackPlan plan = ActionPlaybackBuilder.Build(
                CreateState(),
                new ActionInput(new TileCoord(0, 0)),
                CreateResult(new IceRevealed(new TileCoord(0, 1), DebrisType.B)));

            Assert.That(plan[0].SourceEventName, Is.EqualTo(nameof(IceRevealed)));
            Assert.That(plan[0].StepType, Is.EqualTo(ActionPlaybackStepType.BreakBlockerOrReveal));
        }

        [Test]
        public void Build_RemoveGroupGravitySpawnAndFinalSyncAppearInEventOrder()
        {
            ActionPlaybackPlan plan = ActionPlaybackBuilder.Build(
                CreateState(),
                new ActionInput(new TileCoord(0, 0)),
                CreateResult(
                    new GroupRemoved(DebrisType.A, ImmutableArray.Create(new TileCoord(0, 0))),
                    new GravitySettled(ImmutableArray.Create((new TileCoord(0, 1), new TileCoord(1, 1)))),
                    new Spawned(ImmutableArray.Create((new TileCoord(0, 0), DebrisType.C)))));

            Assert.That(plan.Select(step => step.StepType), Is.EqualTo(new[]
            {
                ActionPlaybackStepType.RemoveGroup,
                ActionPlaybackStepType.Gravity,
                ActionPlaybackStepType.Spawn,
                ActionPlaybackStepType.FinalSync,
            }));
        }

        [Test]
        public void Build_WaterRiseOrderMatchesSourceEvents()
        {
            ActionPlaybackPlan plan = ActionPlaybackBuilder.Build(
                CreateState(),
                new ActionInput(new TileCoord(0, 0)),
                CreateResult(
                    new WaterRose(FloodedRow: 3),
                    new Spawned(ImmutableArray.Create((new TileCoord(0, 1), DebrisType.C))),
                    new GravitySettled(ImmutableArray.Create((new TileCoord(0, 1), new TileCoord(1, 1))))));

            Assert.That(plan.Select(step => step.StepType), Is.EqualTo(new[]
            {
                ActionPlaybackStepType.WaterRise,
                ActionPlaybackStepType.Spawn,
                ActionPlaybackStepType.Gravity,
                ActionPlaybackStepType.FinalSync,
            }));
        }

        [Test]
        public void Build_WaterRiseStepComesFromWaterRoseInCanonicalOrder()
        {
            ActionPlaybackPlan plan = ActionPlaybackBuilder.Build(
                CreateState(),
                new ActionInput(new TileCoord(0, 0)),
                CreateResult(
                    new TargetExtracted("pup-1", new TileCoord(2, 1)),
                    new WaterRose(FloodedRow: 3),
                    new GravitySettled(ImmutableArray.Create((new TileCoord(0, 1), new TileCoord(1, 1))))));

            Assert.That(plan.Take(plan.Count - 1).Select(step => (step.SourceEventName, step.StepType)), Is.EqualTo(new[]
            {
                (nameof(TargetExtracted), ActionPlaybackStepType.TargetExtract),
                (nameof(WaterRose), ActionPlaybackStepType.WaterRise),
                (nameof(GravitySettled), ActionPlaybackStepType.Gravity),
            }));
            Assert.That(plan[^1].StepType, Is.EqualTo(ActionPlaybackStepType.FinalSync));
        }

        [Test]
        public void Build_PreservesDockAndBreakOrderWithoutBucketSorting()
        {
            ActionPlaybackPlan plan = ActionPlaybackBuilder.Build(
                CreateState(),
                new ActionInput(new TileCoord(0, 0)),
                CreateResult(
                    new DockWarningChanged(DockWarningLevel.Safe, DockWarningLevel.Caution),
                    new IceRevealed(new TileCoord(0, 2), DebrisType.C),
                    new BlockerBroken(new TileCoord(0, 2), BlockerType.Ice),
                    new GroupRemoved(DebrisType.A, ImmutableArray.Create(new TileCoord(0, 0))),
                    new GravitySettled(ImmutableArray.Create((new TileCoord(0, 1), new TileCoord(1, 1))))));

            Assert.That(plan.Select(step => step.StepType), Is.EqualTo(new[]
            {
                ActionPlaybackStepType.DockFeedback,
                ActionPlaybackStepType.BreakBlockerOrReveal,
                ActionPlaybackStepType.BreakBlockerOrReveal,
                ActionPlaybackStepType.RemoveGroup,
                ActionPlaybackStepType.Gravity,
                ActionPlaybackStepType.FinalSync,
            }));
        }

        [Test]
        public void Build_PreservesCanonicalBlockerAndIceEventOrder()
        {
            ActionPlaybackPlan plan = ActionPlaybackBuilder.Build(
                CreateState(),
                new ActionInput(new TileCoord(0, 0)),
                CreateResult(
                    new BlockerDamaged(new TileCoord(0, 0), BlockerType.Ice, RemainingHp: 0),
                    new BlockerBroken(new TileCoord(0, 0), BlockerType.Ice),
                    new IceRevealed(new TileCoord(0, 0), DebrisType.B)));

            Assert.That(plan.Take(plan.Count - 1).Select(step => step.SourceEventName), Is.EqualTo(new[]
            {
                nameof(BlockerDamaged),
                nameof(BlockerBroken),
                nameof(IceRevealed),
            }));
            Assert.That(plan.Take(plan.Count - 1).Select(step => step.StepType), Is.EqualTo(new[]
            {
                ActionPlaybackStepType.BreakBlockerOrReveal,
                ActionPlaybackStepType.BreakBlockerOrReveal,
                ActionPlaybackStepType.BreakBlockerOrReveal,
            }));
        }

        [Test]
        public void Build_TargetExtractOrderMatchesSourceEventsAndStillEndsBeforeFinalSync()
        {
            ActionPlaybackPlan plan = ActionPlaybackBuilder.Build(
                CreateState(),
                new ActionInput(new TileCoord(0, 0)),
                CreateResult(
                    new WaterRose(FloodedRow: 3),
                    new TargetExtracted("pup-1", new TileCoord(2, 1)),
                    new Spawned(ImmutableArray.Create((new TileCoord(0, 1), DebrisType.C))),
                    new GravitySettled(ImmutableArray.Create((new TileCoord(0, 1), new TileCoord(1, 1))))));

            Assert.That(plan.Select(step => step.StepType), Is.EqualTo(new[]
            {
                ActionPlaybackStepType.WaterRise,
                ActionPlaybackStepType.TargetExtract,
                ActionPlaybackStepType.Spawn,
                ActionPlaybackStepType.Gravity,
                ActionPlaybackStepType.FinalSync,
            }));
        }

        [Test]
        public void Build_TargetExtractedMapsToTargetExtractWithoutReorderingCanonicalNeighbors()
        {
            ActionPlaybackPlan plan = ActionPlaybackBuilder.Build(
                CreateState(),
                new ActionInput(new TileCoord(0, 0)),
                CreateResult(
                    new Spawned(ImmutableArray.Create((new TileCoord(0, 0), DebrisType.C))),
                    new TargetExtracted("pup-1", new TileCoord(2, 1)),
                    new WaterRose(FloodedRow: 3)));

            Assert.That(plan.Take(plan.Count - 1).Select(step => (step.SourceEventName, step.StepType)), Is.EqualTo(new[]
            {
                (nameof(Spawned), ActionPlaybackStepType.Spawn),
                (nameof(TargetExtracted), ActionPlaybackStepType.TargetExtract),
                (nameof(WaterRose), ActionPlaybackStepType.WaterRise),
            }));
        }

        [Test]
        public void Build_EmptyEventsStillProduceSafeFinalSync()
        {
            ActionPlaybackPlan plan = ActionPlaybackBuilder.Build(
                CreateState(),
                new ActionInput(new TileCoord(1, 1)),
                CreateResult());

            Assert.That(plan.Count, Is.EqualTo(1));
            Assert.That(plan[0].StepType, Is.EqualTo(ActionPlaybackStepType.FinalSync));
            Assert.That(plan[0].SourceEvent, Is.Null);
        }

        [Test]
        public void Build_UnmappedEventsDoNotPreventFinalSync()
        {
            ActionPlaybackPlan plan = ActionPlaybackBuilder.Build(
                CreateState(),
                new ActionInput(new TileCoord(1, 1)),
                CreateResult(
                    new InvalidInput(new TileCoord(0, 0), InvalidInputReason.SingleTile),
                    new WaterWarning(ActionsUntilRise: 1, NextFloodRow: 2),
                    new GroupRemoved(DebrisType.A, ImmutableArray.Create(new TileCoord(0, 0))),
                    new Won("pup-1", TotalActions: 3, ExtractedTargetOrder: ImmutableArray.Create("pup-1"))));

            Assert.That(plan.Select(step => step.StepType), Is.EqualTo(new[]
            {
                ActionPlaybackStepType.WaterWarning,
                ActionPlaybackStepType.RemoveGroup,
                ActionPlaybackStepType.TerminalOutcome,
                ActionPlaybackStepType.FinalSync,
            }));
        }

        [Test]
        public void Build_WinStopsPlaybackBeforePostOutcomeHazardFeedback()
        {
            ActionPlaybackPlan plan = ActionPlaybackBuilder.Build(
                CreateState(),
                new ActionInput(new TileCoord(0, 0)),
                CreateResult(
                    new TargetExtracted("pup-1", new TileCoord(2, 1)),
                    new Won("pup-1", TotalActions: 3, ExtractedTargetOrder: ImmutableArray.Create("pup-1")),
                    new WaterRose(FloodedRow: 2),
                    new DockWarningChanged(DockWarningLevel.Safe, DockWarningLevel.Caution)));

            Assert.That(plan.Take(plan.Count - 1).Select(step => (step.SourceEventName, step.StepType)), Is.EqualTo(new[]
            {
                (nameof(TargetExtracted), ActionPlaybackStepType.TargetExtract),
                (nameof(Won), ActionPlaybackStepType.TerminalOutcome),
            }));
            Assert.That(plan[^1].StepType, Is.EqualTo(ActionPlaybackStepType.FinalSync));
        }

        [Test]
        public void Build_LossStopsPlaybackAfterReadablePreLossFeedback()
        {
            ActionPlaybackPlan plan = ActionPlaybackBuilder.Build(
                CreateState(),
                new ActionInput(new TileCoord(0, 0)),
                CreateResult(
                    new DockInserted(ImmutableArray.Create(DebrisType.A), OccupancyAfterInsert: 7, OverflowCount: 1),
                    new DockJamTriggered(OverflowCount: 1),
                    new Lost(ActionOutcome.LossDockOverflow),
                    new DockCleared(DebrisType.A, SetsCleared: 1, OccupancyAfterClear: 4)));

            Assert.That(plan.Take(plan.Count - 1).Select(step => (step.SourceEventName, step.StepType)), Is.EqualTo(new[]
            {
                (nameof(DockInserted), ActionPlaybackStepType.DockFeedback),
                (nameof(DockJamTriggered), ActionPlaybackStepType.DockFeedback),
                (nameof(Lost), ActionPlaybackStepType.TerminalOutcome),
            }));
            Assert.That(plan[^1].StepType, Is.EqualTo(ActionPlaybackStepType.FinalSync));
        }

        [Test]
        public void Build_NoWaterRoseDoesNotAddWaterRiseButKeepsFinalSync()
        {
            ActionPlaybackPlan plan = ActionPlaybackBuilder.Build(
                CreateState(),
                new ActionInput(new TileCoord(1, 1)),
                CreateResult(
                    new GroupRemoved(DebrisType.A, ImmutableArray.Create(new TileCoord(0, 0))),
                    new GravitySettled(ImmutableArray.Create((new TileCoord(0, 0), new TileCoord(1, 0))))));

            Assert.That(plan.Select(step => step.StepType), Is.EqualTo(new[]
            {
                ActionPlaybackStepType.RemoveGroup,
                ActionPlaybackStepType.Gravity,
                ActionPlaybackStepType.FinalSync,
            }));
        }

        [Test]
        public void Build_MapsExistingActionEventNamesWithoutInventingCoreEvents()
        {
            ActionPlaybackPlan plan = ActionPlaybackBuilder.Build(
                CreateState(),
                new ActionInput(new TileCoord(0, 0)),
                CreateResult(
                    new GroupRemoved(DebrisType.A, ImmutableArray.Create(new TileCoord(0, 0), new TileCoord(0, 1))),
                    new BlockerDamaged(new TileCoord(0, 2), BlockerType.Crate, RemainingHp: 0),
                    new BlockerBroken(new TileCoord(0, 2), BlockerType.Crate),
                    new IceRevealed(new TileCoord(1, 0), DebrisType.B),
                    new TargetProgressed("pup-1", new TileCoord(2, 1)),
                    new TargetOneClearAway("pup-1", new TileCoord(2, 1)),
                    new TargetExtractionLatched("pup-1", new TileCoord(2, 1)),
                    new DockInserted(ImmutableArray.Create(DebrisType.A), OccupancyAfterInsert: 1, OverflowCount: 0),
                    new DockCleared(DebrisType.A, SetsCleared: 1, OccupancyAfterClear: 0),
                    new DockOverflowTriggered(OverflowCount: 1),
                    new DockWarningChanged(DockWarningLevel.Safe, DockWarningLevel.Caution),
                    new DockJamTriggered(OverflowCount: 1),
                    new GravitySettled(ImmutableArray.Create((new TileCoord(0, 0), new TileCoord(1, 0)))),
                    new Spawned(ImmutableArray.Create((new TileCoord(0, 0), DebrisType.C))),
                    new WaterWarning(ActionsUntilRise: 1, NextFloodRow: 2),
                    new VinePreviewChanged(new TileCoord(1, 1)),
                    new VineGrown(new TileCoord(1, 1)),
                    new TargetExtracted("pup-1", new TileCoord(2, 1)),
                    new WaterRose(FloodedRow: 4)));

            Assert.That(plan.Take(plan.Count - 1).Select(step => (step.SourceEventName, step.StepType)), Is.EqualTo(new[]
            {
                ("GroupRemoved", ActionPlaybackStepType.RemoveGroup),
                ("BlockerDamaged", ActionPlaybackStepType.BreakBlockerOrReveal),
                ("BlockerBroken", ActionPlaybackStepType.BreakBlockerOrReveal),
                ("IceRevealed", ActionPlaybackStepType.BreakBlockerOrReveal),
                ("TargetProgressed", ActionPlaybackStepType.TargetReaction),
                ("TargetOneClearAway", ActionPlaybackStepType.TargetReaction),
                ("TargetExtractionLatched", ActionPlaybackStepType.TargetLatch),
                ("DockInserted", ActionPlaybackStepType.DockFeedback),
                ("DockCleared", ActionPlaybackStepType.DockFeedback),
                ("DockOverflowTriggered", ActionPlaybackStepType.DockOverflow),
                ("DockWarningChanged", ActionPlaybackStepType.DockFeedback),
                ("DockJamTriggered", ActionPlaybackStepType.DockFeedback),
                ("GravitySettled", ActionPlaybackStepType.Gravity),
                ("Spawned", ActionPlaybackStepType.Spawn),
                ("WaterWarning", ActionPlaybackStepType.WaterWarning),
                ("VinePreviewChanged", ActionPlaybackStepType.VinePreview),
                ("VineGrown", ActionPlaybackStepType.VineGrowth),
                ("TargetExtracted", ActionPlaybackStepType.TargetExtract),
                ("WaterRose", ActionPlaybackStepType.WaterRise),
            }));
        }

        [Test]
        public void Build_RescueActionPlacesTargetReactionBeforeDockAndExtraction()
        {
            ActionPlaybackPlan plan = ActionPlaybackBuilder.Build(
                CreateState(),
                new ActionInput(new TileCoord(0, 0)),
                CreateResult(
                    new GroupRemoved(DebrisType.A, ImmutableArray.Create(new TileCoord(0, 0), new TileCoord(0, 1))),
                    new TargetProgressed("pup-1", new TileCoord(2, 1)),
                    new TargetOneClearAway("pup-1", new TileCoord(2, 1)),
                    new TargetExtractionLatched("pup-1", new TileCoord(2, 1)),
                    new DockInserted(ImmutableArray.Create(DebrisType.A, DebrisType.A), OccupancyAfterInsert: 2, OverflowCount: 0),
                    new TargetExtracted("pup-1", new TileCoord(2, 1))));

            Assert.That(plan.Take(plan.Count - 1).Select(step => (step.SourceEventName, step.StepType)), Is.EqualTo(new[]
            {
                (nameof(GroupRemoved), ActionPlaybackStepType.RemoveGroup),
                (nameof(TargetProgressed), ActionPlaybackStepType.TargetReaction),
                (nameof(TargetOneClearAway), ActionPlaybackStepType.TargetReaction),
                (nameof(TargetExtractionLatched), ActionPlaybackStepType.TargetLatch),
                (nameof(DockInserted), ActionPlaybackStepType.DockFeedback),
                (nameof(TargetExtracted), ActionPlaybackStepType.TargetExtract),
            }));
        }

        [Test]
        public void Build_TargetExtractionBeforeGravityAndSpawn()
        {
            ActionPlaybackPlan plan = ActionPlaybackBuilder.Build(
                CreateState(),
                new ActionInput(new TileCoord(0, 0)),
                CreateResult(
                    new TargetExtracted("pup-1", new TileCoord(2, 1)),
                    new GravitySettled(ImmutableArray.Create((new TileCoord(0, 1), new TileCoord(1, 1)))),
                    new Spawned(ImmutableArray.Create((new TileCoord(0, 0), DebrisType.C)))));

            Assert.That(plan.Select(step => step.StepType), Is.EqualTo(new[]
            {
                ActionPlaybackStepType.TargetExtract,
                ActionPlaybackStepType.Gravity,
                ActionPlaybackStepType.Spawn,
                ActionPlaybackStepType.FinalSync,
            }));
        }

        [Test]
        public void Build_FinalRescueDoesNotIncludeLaterHazardBeats()
        {
            ActionPlaybackPlan plan = ActionPlaybackBuilder.Build(
                CreateState(),
                new ActionInput(new TileCoord(0, 0)),
                CreateResult(
                    new TargetExtracted("pup-1", new TileCoord(2, 1)),
                    new Won("pup-1", TotalActions: 3, ExtractedTargetOrder: ImmutableArray.Create("pup-1")),
                    new GravitySettled(ImmutableArray.Create((new TileCoord(0, 1), new TileCoord(1, 1)))),
                    new Spawned(ImmutableArray.Create((new TileCoord(0, 0), DebrisType.C))),
                    new WaterWarning(ActionsUntilRise: 1, NextFloodRow: 2),
                    new VinePreviewChanged(new TileCoord(1, 1)),
                    new VineGrown(new TileCoord(1, 1)),
                    new WaterRose(FloodedRow: 2)));

            Assert.That(plan.Take(plan.Count - 1).Select(step => step.SourceEventName), Is.EqualTo(new[]
            {
                nameof(TargetExtracted),
                nameof(Won),
            }));
        }

        [Test]
        public void Build_WaterWarningProducesPlaybackStep()
        {
            ActionPlaybackPlan plan = ActionPlaybackBuilder.Build(
                CreateState(),
                new ActionInput(new TileCoord(0, 0)),
                CreateResult(new WaterWarning(ActionsUntilRise: 1, NextFloodRow: 2)));

            Assert.That(plan[0].SourceEventName, Is.EqualTo(nameof(WaterWarning)));
            Assert.That(plan[0].StepType, Is.EqualTo(ActionPlaybackStepType.WaterWarning));
        }

        [Test]
        public void Build_VinePreviewAndGrowthProducePlaybackSteps()
        {
            ActionPlaybackPlan plan = ActionPlaybackBuilder.Build(
                CreateState(),
                new ActionInput(new TileCoord(0, 0)),
                CreateResult(
                    new VinePreviewChanged(new TileCoord(1, 1)),
                    new VineGrown(new TileCoord(1, 1))));

            Assert.That(plan.Take(plan.Count - 1).Select(step => (step.SourceEventName, step.StepType)), Is.EqualTo(new[]
            {
                (nameof(VinePreviewChanged), ActionPlaybackStepType.VinePreview),
                (nameof(VineGrown), ActionPlaybackStepType.VineGrowth),
            }));
        }

        [Test]
        public void Build_DockOverflowPlaybackShowsSpecificFailCause()
        {
            ActionPlaybackPlan plan = ActionPlaybackBuilder.Build(
                CreateState(),
                new ActionInput(new TileCoord(0, 0)),
                CreateResult(
                    new DockOverflowTriggered(OverflowCount: 2),
                    new Lost(ActionOutcome.LossDockOverflow)));

            Assert.That(plan.Take(plan.Count - 1).Select(step => (step.SourceEventName, step.StepType)), Is.EqualTo(new[]
            {
                (nameof(DockOverflowTriggered), ActionPlaybackStepType.DockOverflow),
                (nameof(Lost), ActionPlaybackStepType.TerminalOutcome),
            }));
        }

        private static ActionResult CreateResult(params ActionEvent[] events)
        {
            return new ActionResult(
                CreateState(),
                ImmutableArray.CreateRange(events),
                ActionOutcome.Ok,
                Snapshot: null);
        }

        private static GameState CreateState()
        {
            ImmutableArray<ImmutableArray<Tile>> rows = ImmutableArray.Create(
                ImmutableArray.Create<Tile>(
                    new DebrisTile(DebrisType.A),
                    new DebrisTile(DebrisType.B),
                    new EmptyTile()),
                ImmutableArray.Create<Tile>(
                    new EmptyTile(),
                    new EmptyTile(),
                    new EmptyTile()),
                ImmutableArray.Create<Tile>(
                    new EmptyTile(),
                    new TargetTile("pup-1", Extracted: false),
                    new EmptyTile()));

            CoreBoard board = new CoreBoard(3, 3, rows);

            return new GameState(
                Board: board,
                Dock: new CoreDock(
                    ImmutableArray.Create<DebrisType?>(
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null),
                    Size: 7),
                Water: new WaterState(FloodedRows: 0, ActionsUntilRise: 3, RiseInterval: 3),
                Vine: new VineState(0, 4, ImmutableArray<TileCoord>.Empty, 0, null),
                Targets: ImmutableArray.Create(new TargetState("pup-1", new TileCoord(2, 1), Extracted: false, OneClearAway: false)),
                LevelConfig: new LevelConfig(
                    ImmutableArray.Create(DebrisType.A, DebrisType.B, DebrisType.C),
                    null,
                    0.0d,
                    2),
                RngState: new RngState(1u, 2u),
                ActionCount: 0,
                DockJamUsed: false,
                UndoAvailable: true,
                ExtractedTargetOrder: ImmutableArray<string>.Empty,
                Frozen: false,
                ConsecutiveEmergencySpawns: 0,
                SpawnRecoveryCounter: 0);
        }
    }
}
