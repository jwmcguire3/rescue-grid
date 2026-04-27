using Rescue.Core.Pipeline;
using Rescue.Core.State;
using Rescue.Unity.BoardPresentation;
using UnityEngine;

namespace Rescue.Unity.Presentation
{
    public enum MaeReactionState
    {
        Neutral,
        Relief,
        Concern,
        Grief,
    }

    public interface IMaeTerminalReactionHook
    {
        void HandleTerminalOutcome(ActionOutcome outcome);
    }

    [DisallowMultipleComponent]
    public sealed class MaeReactionPresenter : MonoBehaviour, ITargetFeedbackHook, IMaeTerminalReactionHook
    {
        [SerializeField] private MaeReactionState currentReaction = MaeReactionState.Neutral;

        public MaeReactionState CurrentReaction => currentReaction;

        public TargetFeedbackKind? LastTargetFeedbackKind { get; private set; }

        public ActionOutcome? LastTerminalOutcome { get; private set; }

        public void HandleTargetFeedback(TargetFeedbackEvent feedbackEvent, GameState? previousState, GameState currentState)
        {
            _ = previousState;
            _ = currentState;

            LastTargetFeedbackKind = feedbackEvent.Kind;
            currentReaction = feedbackEvent.Kind switch
            {
                TargetFeedbackKind.Extraction => MaeReactionState.Relief,
                TargetFeedbackKind.Distressed => MaeReactionState.Concern,
                TargetFeedbackKind.NearRescue => MaeReactionState.Concern,
                TargetFeedbackKind.ExtractionReady => MaeReactionState.Relief,
                _ => currentReaction,
            };
        }

        public void HandleTerminalOutcome(ActionOutcome outcome)
        {
            LastTerminalOutcome = outcome;
            currentReaction = outcome switch
            {
                ActionOutcome.Win => MaeReactionState.Relief,
                ActionOutcome.LossDockOverflow => MaeReactionState.Concern,
                ActionOutcome.LossWaterOnTarget => MaeReactionState.Grief,
                ActionOutcome.LossDistressedExpired => MaeReactionState.Grief,
                _ => currentReaction,
            };
        }

        public void ResetReaction()
        {
            LastTargetFeedbackKind = null;
            LastTerminalOutcome = null;
            currentReaction = MaeReactionState.Neutral;
        }
    }
}
