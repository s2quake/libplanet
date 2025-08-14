using Libplanet.Net.MessageHandlers;
using Libplanet.Net.Messages;

namespace Libplanet.Net.Consensus.MessageHandlers;

internal sealed class ConsensusPreVoteMessageHandler(Consensus consensus)
    : MessageHandlerBase<ConsensusPreVoteMessage>
{
    protected override async ValueTask OnHandleAsync(ConsensusPreVoteMessage message, MessageEnvelope messageEnvelope, CancellationToken cancellationToken)
    {
        var preVote = message.PreVote;
        consensus.PostPreVote(preVote);
        await ValueTask.CompletedTask;
    }
}
