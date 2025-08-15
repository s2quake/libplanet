using Libplanet.Net.MessageHandlers;
using Libplanet.Net.Messages;

namespace Libplanet.Net.Consensus.MessageHandlers;

internal sealed class ConsensusPreCommitMessageHandler(Consensus consensus, MessageCollection pendingMessages)
    : MessageHandlerBase<ConsensusPreCommitMessage>
{
    protected override async ValueTask OnHandleAsync(
        ConsensusPreCommitMessage message, MessageEnvelope messageEnvelope, CancellationToken cancellationToken)
    {
        var preCommit = message.PreCommit;
        if (preCommit.Height < consensus.Height)
        {
            throw new InvalidMessageException("PreCommit height is lower than current consensus height");
        }
        else if (preCommit.Height == consensus.Height)
        {
            await consensus.PreCommitAsync(preCommit, cancellationToken);
        }
        else
        {
            pendingMessages.Add(message);
        }
    }
}
