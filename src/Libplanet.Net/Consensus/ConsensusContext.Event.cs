using Libplanet.Net.Messages;

namespace Libplanet.Net.Consensus;

public partial class ConsensusContext
{
    internal event EventHandler<(int Height, ConsensusMessage Message)>? MessagePublished;

    internal event EventHandler<(int Height, Exception)>? ExceptionOccurred;

    internal event EventHandler<(int Height, int Round, ConsensusStep Step)>? TimeoutProcessed;

    internal event EventHandler<ContextState>? StateChanged;

    internal event EventHandler<(int Height, ConsensusMessage Message)>? MessageConsumed;

    internal event EventHandler<(int Height, System.Action)>? MutationConsumed;

    private void AttachEventHandlers(Context context)
    {
        // NOTE: Events for testing and debugging.
        context.ExceptionOccurred += (sender, exception) =>
            ExceptionOccurred?.Invoke(this, (context.Height, exception));
        context.TimeoutProcessed += (sender, eventArgs) =>
            TimeoutProcessed?.Invoke(this, (context.Height, eventArgs.Round, eventArgs.Step));
        context.StateChanged += (sender, eventArgs) =>
            StateChanged?.Invoke(this, eventArgs);
        context.MessageConsumed += (sender, message) =>
            MessageConsumed?.Invoke(this, (context.Height, message));
        context.MutationConsumed += (sender, action) =>
            MutationConsumed?.Invoke(this, (context.Height, action));

        // NOTE: Events for consensus logic.
        context.HeightStarted += (sender, height) =>
            _consensusMessageCommunicator.StartHeight(height);
        context.RoundStarted += (sender, round) =>
            _consensusMessageCommunicator.StartRound(round);
        context.MessageToPublish += (sender, message) =>
        {
            _consensusMessageCommunicator.PublishMessage(message);
            MessagePublished?.Invoke(this, (context.Height, message));
        };
    }
}
