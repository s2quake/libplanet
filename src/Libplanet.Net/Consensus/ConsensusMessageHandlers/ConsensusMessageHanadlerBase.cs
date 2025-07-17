using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.MessageHandlers;
using Libplanet.Net.Messages;

namespace Libplanet.Net.Consensus.ConsensusMessageHandlers;

internal abstract class ConsensusMessageHanadlerBase<T>
    : MessageHandlerBase<T>
    where T : ConsensusMessage
{
    protected override async ValueTask OnHandleAsync(
        T message, IReplyContext replyContext, CancellationToken cancellationToken)
    {
        await ValueTask.CompletedTask;
    }
}
