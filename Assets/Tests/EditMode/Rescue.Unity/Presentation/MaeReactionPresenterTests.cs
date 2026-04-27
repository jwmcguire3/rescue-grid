using NUnit.Framework;
using Rescue.Core.Pipeline;
using Rescue.Core.State;
using Rescue.Unity.BoardPresentation;
using Rescue.Unity.Presentation;
using UnityEngine;

namespace Rescue.Unity.Presentation.Tests
{
    public sealed class MaeReactionPresenterTests
    {
        private GameObject? root;

        [TearDown]
        public void TearDown()
        {
            if (root is not null)
            {
                Object.DestroyImmediate(root);
                root = null;
            }
        }

        [Test]
        public void MaeReactionPresenter_ReactsToExtractionAndDistress()
        {
            MaeReactionPresenter presenter = CreatePresenter();

            presenter.HandleTargetFeedback(
                new TargetFeedbackEvent("pup", new TileCoord(1, 1), TargetFeedbackKind.Extraction),
                previousState: null,
                currentState: CreateMinimalState());

            Assert.That(presenter.CurrentReaction, Is.EqualTo(MaeReactionState.Relief));
            Assert.That(presenter.LastTargetFeedbackKind, Is.EqualTo(TargetFeedbackKind.Extraction));

            presenter.HandleTargetFeedback(
                new TargetFeedbackEvent("pup", new TileCoord(1, 1), TargetFeedbackKind.Distressed),
                previousState: null,
                currentState: CreateMinimalState());

            Assert.That(presenter.CurrentReaction, Is.EqualTo(MaeReactionState.Concern));
            Assert.That(presenter.LastTargetFeedbackKind, Is.EqualTo(TargetFeedbackKind.Distressed));
        }

        [TestCase(ActionOutcome.LossDockOverflow, MaeReactionState.Concern)]
        [TestCase(ActionOutcome.LossWaterOnTarget, MaeReactionState.Grief)]
        [TestCase(ActionOutcome.LossDistressedExpired, MaeReactionState.Grief)]
        public void MaeReactionPresenter_ReactsToLoss(ActionOutcome outcome, MaeReactionState expected)
        {
            MaeReactionPresenter presenter = CreatePresenter();

            presenter.HandleTerminalOutcome(outcome);

            Assert.That(presenter.CurrentReaction, Is.EqualTo(expected));
            Assert.That(presenter.LastTerminalOutcome, Is.EqualTo(outcome));
        }

        private MaeReactionPresenter CreatePresenter()
        {
            root = new GameObject("MaeReactionPresenterTest");
            return root.AddComponent<MaeReactionPresenter>();
        }

        private static GameState CreateMinimalState()
        {
            return new GameState(
                Board: new Board(1, 1, System.Collections.Immutable.ImmutableArray.Create(
                    System.Collections.Immutable.ImmutableArray.Create<Tile>(new EmptyTile()))),
                Dock: new Dock(System.Collections.Immutable.ImmutableArray<DebrisType?>.Empty, Size: 7),
                Water: new WaterState(0, 3, 3),
                Vine: new VineState(0, 4, System.Collections.Immutable.ImmutableArray<TileCoord>.Empty, 0, null),
                Targets: System.Collections.Immutable.ImmutableArray<TargetState>.Empty,
                LevelConfig: new LevelConfig(
                    System.Collections.Immutable.ImmutableArray.Create(DebrisType.A, DebrisType.B, DebrisType.C),
                    BaseDistribution: null,
                    AssistanceChance: 0.0d,
                    ConsecutiveEmergencyCap: 2),
                RngState: new Rescue.Core.Rng.RngState(1u, 2u),
                ActionCount: 0,
                DockJamUsed: false,
                UndoAvailable: true,
                ExtractedTargetOrder: System.Collections.Immutable.ImmutableArray<string>.Empty,
                Frozen: false,
                ConsecutiveEmergencySpawns: 0,
                SpawnRecoveryCounter: 0);
        }
    }
}
