using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Consensus;
using Libplanet.Net.MessageHandlers;
using Libplanet.Net.Messages;

namespace Libplanet.Net.Consensus.ConsensusMessageHandlers;

internal sealed class ConsensusMessageHandler(ConsensusReactor consensusReactor)
    : MessageHandlerBase<ConsensusMessage>
{
    protected override async ValueTask OnHandleAsync(
        ConsensusMessage message, IReplyContext replyContext, CancellationToken cancellationToken)
    {
        consensusReactor.HandleMessage(message);
        await ValueTask.CompletedTask;
    }
}
