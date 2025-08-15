using Libplanet.Net.MessageHandlers;
using Libplanet.Net.Messages;

namespace Libplanet.Net.Consensus.MessageHandlers;

internal sealed class ConsensusPreVoteMessageHandler(Consensus consensus, MessageCollection pendingMessages)
    : MessageHandlerBase<ConsensusPreVoteMessage>
{
    protected override async ValueTask OnHandleAsync(
        ConsensusPreVoteMessage message, MessageEnvelope messageEnvelope, CancellationToken cancellationToken)
    {
        var preVote = message.PreVote;
        if (preVote.Height < consensus.Height)
        {
            throw new InvalidMessageException("PreVote height is lower than current consensus height");
        }
        else if (preVote.Height == consensus.Height)
        {
            await consensus.PreVoteAsync(preVote, cancellationToken);
        }
        else
        {
            pendingMessages.Add(message);
        }
    }
}
