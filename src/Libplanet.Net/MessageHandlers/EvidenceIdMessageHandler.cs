using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Messages;

namespace Libplanet.Net.MessageHandlers;

internal sealed class EvidenceIdMessageHandler(EvidenceFetcher fetcher)
    : MessageHandlerBase<EvidenceIdMessage>
{
    protected override async ValueTask OnHandleAsync(
        EvidenceIdMessage message, IReplyContext replyContext, CancellationToken cancellationToken)
    {
        fetcher.DemandMany(replyContext.Sender, [.. message.Ids]);
        await replyContext.PongAsync();
    }
}
