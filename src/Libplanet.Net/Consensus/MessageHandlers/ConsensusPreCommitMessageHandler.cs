using Libplanet.Net.MessageHandlers;
using Libplanet.Net.Messages;

namespace Libplanet.Net.Consensus.MessageHandlers;

internal sealed class ConsensusPreCommitMessageHandler(Consensus consensus)
    : MessageHandlerBase<ConsensusPreCommitMessage>
{
    protected override async ValueTask OnHandleAsync(ConsensusPreCommitMessage message, MessageEnvelope messageEnvelope, CancellationToken cancellationToken)
    {
        var preCommit = message.PreCommit;
        consensus.PostPreCommit(preCommit   );
        await ValueTask.CompletedTask;
    }
}
